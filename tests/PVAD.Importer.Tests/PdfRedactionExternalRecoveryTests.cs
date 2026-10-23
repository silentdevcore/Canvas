using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Redaction;
using PVAD.Importer.Saving;
using PVAD.Importer.Xref;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

public sealed class PdfRedactionExternalRecoveryTests
{
    private const string TextSecret = "PXA-RECOVERY-TEXT-7C89E1";
    private const string ImageSecret = "PXA-RECOVERY-IMAGE-4D21B8";
    private const string MaskSecret = "PXA-RECOVERY-MASK-91AF30";

    [Fact]
    public async Task CommittedCorpus_ShouldMatchItsManifestAndSources()
    {
        var (root, manifest) = await ReadCorpusAsync();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(3, manifest.Fixtures.Count);
        Assert.Equal(manifest.Fixtures.Count, manifest.Fixtures.Select(item => item.Id).Distinct().Count());
        foreach (var fixture in manifest.Fixtures)
        {
            var pdfPath = Path.Combine(root, fixture.File);
            var sourcePath = Path.Combine(root, fixture.Source);
            Assert.True(File.Exists(pdfPath), $"Missing corpus PDF: {fixture.File}");
            Assert.True(File.Exists(sourcePath), $"Missing corpus source: {fixture.Source}");
            Assert.False(string.IsNullOrWhiteSpace(fixture.Producer));
            Assert.Equal(
                fixture.Sha256,
                Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(pdfPath))));
            Assert.Contains(fixture.Expectation, new[] { "redact", "block" });
        }
    }

    [Theory]
    [InlineData("chromium-encoded-glyphs")]
    [InlineData("libreoffice-reusable-resources")]
    [InlineData("malformed-compressed-stream")]
    [Trait("Category", "ExternalPdfTools")]
    public async Task CommittedCorpus_ShouldEitherResistRecoveryOrFailClosed(string fixtureId)
    {
        if (!ExternalRecoveryEnabled())
        {
            return;
        }

        var pdftotext = RequireTool("pdftotext");
        var pdfimages = RequireTool("pdfimages");
        var strings = RequireTool("strings");
        var (root, manifest) = await ReadCorpusAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"pxa-redaction-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var fixture = Assert.Single(manifest.Fixtures, item => item.Id == fixtureId);
            var source = await File.ReadAllBytesAsync(Path.Combine(root, fixture.File));
            if (fixture.Expectation == "block")
            {
                await AssertBlockedAsync(source, fixture.Region, fixture.Id);
                return;
            }

            var result = await RedactFixtureTextAsync(source, Assert.IsType<CorpusRegion>(fixture.Region));
            var outputPath = Path.Combine(directory, $"{fixture.Id}-redacted.pdf");
            await File.WriteAllBytesAsync(outputPath, result.Bytes);
            var extractedText = Run(pdftotext, [outputPath, "-"]).StandardOutput;
            Assert.DoesNotContain(fixture.Secret!, extractedText, StringComparison.Ordinal);
            Assert.Contains(fixture.ControlText!, extractedText, StringComparison.Ordinal);
            Assert.DoesNotContain(
                fixture.Secret!,
                Run(strings, [outputPath]).StandardOutput,
                StringComparison.Ordinal);
            Run(pdfimages, ["-list", outputPath]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "ExternalPdfTools")]
    public async Task AppliedRedaction_ShouldNotBeRecoverableWithPopplerOrStrings()
    {
        if (!ExternalRecoveryEnabled())
        {
            return;
        }

        var pdftotext = RequireTool("pdftotext");
        var pdfimages = RequireTool("pdfimages");
        var strings = RequireTool("strings");
        var directory = Path.Combine(Path.GetTempPath(), $"pxa-redaction-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var textResult = await RedactTextAsync();
            var textPdf = Path.Combine(directory, "text-redacted.pdf");
            await File.WriteAllBytesAsync(textPdf, textResult.Bytes);

            var extractedText = Run(pdftotext, [textPdf, "-"]).StandardOutput;
            var textStrings = Run(strings, [textPdf]).StandardOutput;
            Assert.DoesNotContain(TextSecret, extractedText, StringComparison.Ordinal);
            Assert.DoesNotContain(TextSecret, textStrings, StringComparison.Ordinal);

            var imageResult = await RedactMaskedImageAsync();
            var imagePdf = Path.Combine(directory, "image-redacted.pdf");
            var imagePrefix = Path.Combine(directory, "image");
            await File.WriteAllBytesAsync(imagePdf, imageResult.Bytes);

            Run(pdfimages, ["-all", imagePdf, imagePrefix]);
            var extractedImages = Directory.GetFiles(directory, "image-*");
            Assert.NotEmpty(extractedImages);
            foreach (var extractedImage in extractedImages)
            {
                var bytes = await File.ReadAllBytesAsync(extractedImage);
                Assert.False(bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(ImageSecret)) >= 0);
                Assert.False(bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(MaskSecret)) >= 0);
            }

            var imageStrings = Run(strings, [imagePdf]).StandardOutput;
            Assert.DoesNotContain(ImageSecret, imageStrings, StringComparison.Ordinal);
            Assert.DoesNotContain(MaskSecret, imageStrings, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool ExternalRecoveryEnabled() => string.Equals(
        PVAD.Testing.PvadTestEnvironment.Read("PVAD_RUN_PDF_RECOVERY_TESTS"),
        "1",
        StringComparison.Ordinal);

    private static async Task<(string Root, CorpusManifest Manifest)> ReadCorpusAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "RedactionRecovery");
        var json = await File.ReadAllTextAsync(Path.Combine(root, "corpus.json"));
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return (root, Assert.IsType<CorpusManifest>(manifest));
    }

    private static async Task<PdfRedactionApplyResult> RedactFixtureTextAsync(
        byte[] source,
        CorpusRegion region)
    {
        await using var stream = new MemoryStream(source, writable: false);
        return await new PdfRedactionApplier(new PdfRedactionPreflight()).ApplyAsync(
            stream,
            [new PdfRedactionRegion(region.PageNumber, new PdfRectangle(
                region.X,
                region.Y,
                region.Width,
                region.Height))]);
    }

    private static async Task AssertBlockedAsync(
        byte[] source,
        CorpusRegion? configuredRegion,
        string fixtureId)
    {
        await using var preflightStream = new MemoryStream(source, writable: false);
        var preflight = new PdfRedactionPreflight();
        var region = configuredRegion is null
            ? new PdfRedactionRegion(1, new PdfRectangle(0, 0, 1000, 1000))
            : new PdfRedactionRegion(configuredRegion.PageNumber, new PdfRectangle(
                configuredRegion.X,
                configuredRegion.Y,
                configuredRegion.Width,
                configuredRegion.Height));
        var report = await preflight.PreflightAsync(preflightStream, [region]);
        Assert.True(
            report.Capability is PdfRedactionCapability.ReviewRequired or PdfRedactionCapability.Unsupported,
            $"Corpus fixture '{fixtureId}' unexpectedly reported removable.");

        await using var applyStream = new MemoryStream(source, writable: false);
        var exception = await Record.ExceptionAsync(() =>
            new PdfRedactionApplier(preflight).ApplyAsync(applyStream, [region]));
        Assert.True(
            exception is PdfRedactionBlockedException,
            $"Corpus fixture '{fixtureId}' did not fail closed; actual exception: {exception?.GetType().Name ?? "none"}.");
    }

    private static async Task<PdfRedactionApplyResult> RedactTextAsync()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText(TextSecret, 40, 400);
        var source = document.ToBytes();
        await using var inspect = new MemoryStream(source);
        var imported = await new PdfImporter().LoadAsync(inspect);
        var text = Assert.Single(imported.Pages[0].GraphicsObjects.OfType<PdfTextElement>());
        var bounds = text.Bounds ?? new Analysis.BoundingBoxCalculator().ComputeElementBounds(text);
        var region = new PdfRectangle(bounds.Left - 2, bounds.Bottom - 2, bounds.Width / 2, bounds.Height + 4);
        await using var stream = new MemoryStream(source);
        return await new PdfRedactionApplier(new PdfRedactionPreflight())
            .ApplyAsync(stream, [new PdfRedactionRegion(1, region)]);
    }

    private static async Task<PdfRedactionApplyResult> RedactMaskedImageAsync()
    {
        const int width = 64;
        const int height = 4;
        var image = Enumerable.Repeat((byte)173, width * height * 3).ToArray();
        var mask = Enumerable.Repeat((byte)219, width * height).ToArray();
        var imageSecret = Encoding.ASCII.GetBytes(ImageSecret);
        var maskSecret = Encoding.ASCII.GetBytes(MaskSecret);
        for (var row = 0; row < height; row++)
        {
            imageSecret.CopyTo(image.AsSpan(row * width * 3));
            maskSecret.CopyTo(mask.AsSpan(row * width));
        }

        var source = CreateSoftMaskedImagePdf(image, mask, width, height);
        await using var stream = new MemoryStream(source);
        return await new PdfRedactionApplier(new PdfRedactionPreflight()).ApplyAsync(
            stream,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);
    }

    private static byte[] CreateSoftMaskedImagePdf(byte[] samples, byte[] maskSamples, int width, int height)
    {
        var document = new PdfDocument();
        document.AddPage();
        var source = document.ToBytes();
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var pageEntry = graph.Objects.Values.First(item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Page" });
        var page = (PdfDictionary)pageEntry.Value;
        var imageId = new PdfObjectId(table.Entries.Keys.Max(id => id.Number) + 1, 0);
        var maskId = new PdfObjectId(imageId.Number + 1, 0);
        var contentId = new PdfObjectId(maskId.Number + 1, 0);
        var imageDictionary = ImageDictionary(width, height, "DeviceRGB");
        imageDictionary["SMask"] = new PdfReference(maskId);
        var updatedPage = new PdfDictionary(new Dictionary<string, PdfObject>(page.Values, StringComparer.Ordinal))
        {
            ["Resources"] = new PdfDictionary
            {
                ["XObject"] = new PdfDictionary { ["Im0"] = new PdfReference(imageId) }
            },
            ["Contents"] = new PdfReference(contentId)
        };

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(imageId, new PdfStreamObject(imageDictionary, samples)),
            new PdfIncrementalObjectUpdate(maskId, new PdfStreamObject(
                ImageDictionary(width, height, "DeviceGray"), maskSamples)),
            new PdfIncrementalObjectUpdate(contentId, new PdfStreamObject(
                new PdfDictionary(), "q 200 0 0 200 50 50 cm /Im0 Do Q"u8.ToArray())),
            new PdfIncrementalObjectUpdate(pageEntry.Id, updatedPage)
        ]).Bytes;
    }

    private static PdfDictionary ImageDictionary(int width, int height, string colorSpace) => new()
    {
        ["Type"] = new PdfName("XObject"),
        ["Subtype"] = new PdfName("Image"),
        ["Width"] = new PdfInteger(width),
        ["Height"] = new PdfInteger(height),
        ["BitsPerComponent"] = new PdfInteger(8),
        ["ColorSpace"] = new PdfName(colorSpace)
    };

    private static string RequireTool(string name)
    {
        var path = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
        return path ?? throw new InvalidOperationException(
            $"The external PDF recovery suite requires '{name}' on PATH.");
    }

    private static ProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{executable} exceeded the 30 second recovery-test limit.");
        }

        Assert.Equal(0, process.ExitCode);
        return new ProcessResult(
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    private sealed record ProcessResult(string StandardOutput, string StandardError);

    private sealed record CorpusManifest(int SchemaVersion, IReadOnlyList<CorpusFixture> Fixtures);

    private sealed record CorpusFixture(
        string Id,
        string File,
        string Sha256,
        string Producer,
        string Source,
        string Expectation,
        string? Secret,
        string? ControlText,
        CorpusRegion? Region);

    private sealed record CorpusRegion(
        int PageNumber,
        double X,
        double Y,
        double Width,
        double Height);
}
