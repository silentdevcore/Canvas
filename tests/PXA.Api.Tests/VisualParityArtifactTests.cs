using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PXA.CodeWorker;
using PXA.Generator;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Infrastructure;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PXA.Api.Tests;

public sealed class VisualParityArtifactTests
{
    [Fact]
    public async Task CanonicalFixture_ProducesNormalAndGeneratedCSharpPdfArtifacts()
    {
        var root = FindRepositoryRoot();
        var configuredFixture = Environment.GetEnvironmentVariable("PXA_VISUAL_PARITY_FIXTURE");
        var fixturePath = string.IsNullOrWhiteSpace(configuredFixture)
            ? Path.Combine(root, "test-assets", "visual-parity", "all-elements.json")
            : Path.IsPathRooted(configuredFixture)
                ? configuredFixture
                : Path.Combine(root, configuredFixture);
        var outputDirectory = Environment.GetEnvironmentVariable("PXA_VISUAL_PARITY_ARTIFACTS")
            ?? Path.Combine(root, "artifacts", "visual-parity", "raw");
        Directory.CreateDirectory(outputDirectory);

        var source = await File.ReadAllTextAsync(fixturePath);
        var design = JsonSerializer.Deserialize<DesignExportDto>(source, JsonOptions);
        Assert.NotNull(design);
        var fixtureTypes = design.Pages.SelectMany(page => page.Elements).Select(element => element.Type)
            .ToHashSet(StringComparer.Ordinal);
        var isCatalogFixture = Path.GetFileName(fixturePath).Equals("all-elements.json", StringComparison.OrdinalIgnoreCase);
        if (isCatalogFixture)
            Assert.Equal(38, fixtureTypes.Count);
        var mapperSource = await File.ReadAllTextAsync(
            Path.Combine(root, "PXA.WebApi", "Infrastructure", "DesignJsonMapper.cs"));
        var mapperTypes = Regex.Matches(mapperSource, "case \\\"([a-z]+)\\\":")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(fixtureTypes.Except(mapperTypes));
        var fontLoader = new PXA.Pdf.PdfFontLoader(Path.Combine(root, "assets", "fonts", "liberation"));

        var qrCode = design.Pages.SelectMany(page => page.Elements).SingleOrDefault(element => element.Type == "qrcode");
        if (qrCode is not null)
            await File.WriteAllBytesAsync(
                Path.Combine(outputDirectory, "qrcode.png"),
                PxaSymbolRenderer.RenderQrCode(qrCode.QrValue!));
        var barcode = design.Pages.SelectMany(page => page.Elements).SingleOrDefault(element => element.Type == "barcode");
        if (barcode is not null)
            await File.WriteAllBytesAsync(
                Path.Combine(outputDirectory, "barcode.png"),
                PxaSymbolRenderer.RenderBarcode(
                    barcode.BarcodeValue!,
                    barcode.BarcodeType,
                    (int)Math.Max(barcode.Width, 1),
                    (int)Math.Max(barcode.Height * 0.7, 1)));

        var backendPdf = DesignJsonMapper.MapToPdfDocument(design, fontLoader)
            .ToBytes(DesignJsonMapper.BuildSaveOptions(design));
        AssertPdf(backendPdf);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "backend.pdf"), backendPdf);

        var service = CreatePackagedWorkerService(fontLoader);
        var conversion = await service.ConvertAsync(
            PxaCodeLanguages.Json, PxaCodeLanguages.CSharpPdf, source, default);
        Assert.DoesNotContain(conversion.Diagnostics, diagnostic => diagnostic.Severity == "error");
        Assert.Equal(PxaCodeFidelity.Exact, conversion.DocumentFidelity);

        var execution = await service.ExecuteAsync(
            PxaCodeLanguages.CSharpPdf, conversion.GeneratedSource, default);
        Assert.True(execution.Success,
            string.Join(" | ", execution.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.NotNull(execution.CanonicalDesign);
        Assert.Equal(conversion.CanonicalChecksum,
            PxaCodeConversionService.Hash(JsonSerializer.Serialize(execution.CanonicalDesign, JsonOptions)));
        AssertPdf(execution.PdfBytes);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "csharp.pdf"), execution.PdfBytes!);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "generated.csharp"), conversion.GeneratedSource);

        await WriteHybridInvoicePdfAsync(fixturePath, outputDirectory, fontLoader);
    }

    /// <summary>
    /// Renders the dedicated hybrid invoice beside the DesignExport PDF, when the fixture has a
    /// canonical invoice next to it.
    /// </summary>
    /// <remarks>
    /// The backend PDF above is a DesignExport rendering of the Designer's <em>projection</em>.
    /// Comparing the Canvas to it proves the Designer agrees with itself; it says nothing about the
    /// hybrid invoice PXA actually issues, which <see cref="EInvoice.CreatePdf"/> renders from the
    /// canonical invoice through its own layout. This writes that document so the parity run can
    /// compare it as a surface of its own.
    /// </remarks>
    private static async Task WriteHybridInvoicePdfAsync(
        string fixturePath,
        string outputDirectory,
        PXA.Pdf.PdfFontLoader fontLoader)
    {
        var sidecarPath = Path.ChangeExtension(fixturePath, null) + ".invoice.json";
        if (!File.Exists(sidecarPath)) return;

        var sidecar = JsonDocument.Parse(await File.ReadAllTextAsync(sidecarPath)).RootElement;
        Assert.Equal("pxa.e-invoice.parity-invoice/1", sidecar.GetProperty("schema").GetString());
        var invoice = sidecar.GetProperty("invoice");

        var created = EInvoice.CreatePdf(
            new PxaEInvoiceDocument
            {
                InvoiceNumber = invoice.GetProperty("invoiceNumber").GetString()!,
                IssueDate = DateOnly.Parse(invoice.GetProperty("issueDate").GetString()!, CultureInfo.InvariantCulture),
                DueDate = Optional(invoice, "dueDate") is { } due
                    ? DateOnly.Parse(due, CultureInfo.InvariantCulture)
                    : null,
                Currency = invoice.GetProperty("currency").GetString()!,
                BuyerReference = Optional(invoice, "buyerReference"),
                PaymentReference = Optional(invoice, "paymentReference"),
                CurrencyDecimalPlaces = invoice.GetProperty("currencyDecimalPlaces").GetInt32(),
                Seller = Party(invoice.GetProperty("seller")),
                Buyer = Party(invoice.GetProperty("buyer")),
                Lines = [.. invoice.GetProperty("lines").EnumerateArray().Select(static line =>
                    new PxaEInvoiceDocumentLine(
                        line.GetProperty("identifier").GetString()!,
                        line.GetProperty("description").GetString()!,
                        line.GetProperty("quantity").GetDecimal(),
                        line.GetProperty("unitCode").GetString()!,
                        line.GetProperty("unitPrice").GetDecimal(),
                        line.GetProperty("taxRate").GetDecimal(),
                        line.GetProperty("taxCategoryCode").GetString()!))]
            },
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.FacturX,
                Version = EInvoice.CurrentFacturXVersion,
                Profile = sidecar.GetProperty("profile").GetString()!
            },
            fontLoader);

        Assert.True(created.IsGenerated, string.Join(
            " | ", created.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        AssertPdf([.. created.Pdf.Span]);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "hybrid.pdf"), created.Pdf.ToArray());
    }

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static PxaEInvoiceBusinessParty Party(JsonElement element)
    {
        var address = element.GetProperty("address");
        return new PxaEInvoiceBusinessParty(
            element.GetProperty("name").GetString()!,
            Optional(element, "identifier"),
            Optional(element, "taxIdentifier"),
            Optional(element, "endpointIdentifier"),
            new PxaEInvoiceAddress(
                address.GetProperty("street").GetString()!,
                address.GetProperty("city").GetString()!,
                address.GetProperty("postalCode").GetString()!,
                address.GetProperty("countryCode").GetString()!,
                Optional(address, "additionalLine")));
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static void AssertPdf(byte[]? bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    private static PxaCodeConversionService CreatePackagedWorkerService(PXA.Pdf.PdfFontLoader fontLoader)
    {
        var workerDirectory = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location)!;
        return new PxaCodeConversionService(new PxaCodeWorkerClient(
            new TestEnvironment(),
            Options.Create(new PxaCodeWorkerOptions { Enabled = true, WorkerPath = workerDirectory }),
            NullLogger<PxaCodeWorkerClient>.Instance), fontLoader);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PXA.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PXA.Api.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
