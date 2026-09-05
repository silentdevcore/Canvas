using System.Text;
using PXA.Importer.Content;
using PXA.Importer.Document;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Redaction;
using PXA.Importer.Saving;
using PXA.Importer.Streams;
using PXA.Importer.Xref;
using PXA.Pdf;
using SkiaSharp;

namespace PXA.Importer.Tests;

public sealed class PdfImageRedactionTests
{
    private static readonly byte[] Samples =
    [
        101, 102, 103, 111, 112, 113, 121, 122, 123, 131, 132, 133,
        141, 142, 143, 151, 152, 153, 161, 162, 163, 171, 172, 173
    ];

    [Fact]
    public async Task Apply_ShouldReplaceCoveredPixelsAndKeepUncoveredPixels()
    {
        var pdf = CreateImagePdf(Samples);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, Assert.IsType<PdfDictionary>(
            Resolve(graph, page["Resources"]))["XObject"]));
        var redactedEntry = Assert.Single(xObjects.Values, entry => entry.Key.Contains("_PXARedacted", StringComparison.Ordinal));
        var stream = Assert.IsType<PdfStreamObject>(Resolve(graph, redactedEntry.Value));
        var decoded = new PdfStreamDecoderRegistry().Decode(stream).ToArray();

        Assert.Equal(24, decoded.Length);
        Assert.All(decoded.Take(6), value => Assert.Equal(0, value));
        Assert.Equal(Samples.Skip(6).Take(6), decoded.Skip(6).Take(6));
        Assert.All(decoded.Skip(12).Take(6), value => Assert.Equal(0, value));
        Assert.Equal(Samples.Skip(18).Take(6), decoded.Skip(18).Take(6));
    }

    [Fact]
    public async Task Apply_ShouldCloneASharedImageForOnlyTheCoveredOccurrence()
    {
        var pdf = CreateImagePdf(Samples, drawTwice: true);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        Assert.NotNull(xObjects["Im0"]);
        Assert.Contains(xObjects.Values.Keys, name => name.StartsWith("Im0_PXARedacted", StringComparison.Ordinal));

        var original = Assert.IsType<PdfStreamObject>(Resolve(graph, xObjects["Im0"]));
        Assert.Equal(Samples, original.EncodedBytes.ToArray());
        var content = ReadPageContent(graph, page);
        Assert.Contains("/Im0 Do", content, StringComparison.Ordinal);
        Assert.Contains("/Im0_PXARedacted1 Do", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldRedactRawInlineImagePixels()
    {
        var pdf = CreateInlineImagePdf(Samples);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        var (_, verificationGraph) = ParseGraph(result.Bytes);
        var verificationDocument = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter())
            .Build(verificationGraph);
        var verificationPage = verificationDocument.Pages[0];
        var verificationImage = Assert.Single(verificationPage.GraphicsObjects.OfType<PdfImageElement>());
        var secure = PdfImageRedactionEngine.IsSecurelyRedacted(
            verificationGraph,
            verificationPage.Resources,
            verificationImage,
            [new PdfRectangle(50, 50, 100, 200)],
            out var reason);
        Assert.True(secure, reason + " Keys: " + string.Join(",", Assert.IsType<PdfStreamObject>(
            verificationImage.SourceCommand.Operands.First()).Dictionary.Values.Keys));
        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        await using var resultStream = new MemoryStream(result.Bytes);
        var document = await new PdfImporter().LoadAsync(resultStream);
        var image = Assert.Single(document.Pages[0].GraphicsObjects.OfType<PdfImageElement>());
        var stream = Assert.IsType<PdfStreamObject>(image.SourceCommand.Operands.First());
        var decoded = new PdfStreamDecoderRegistry().Decode(stream).ToArray();
        Assert.All(decoded.Take(6), value => Assert.Equal(0, value));
        Assert.Equal(Samples.Skip(6).Take(6), decoded.Skip(6).Take(6));
    }

    [Fact]
    public async Task Apply_ShouldRedactFlateInlineImageUsingAbbreviatedKeys()
    {
        var pdf = CreateInlineImagePdf(Compress(Samples), "Fl");

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        await using var resultStream = new MemoryStream(result.Bytes);
        var document = await new PdfImporter().LoadAsync(resultStream);
        var image = Assert.Single(document.Pages[0].GraphicsObjects.OfType<PdfImageElement>());
        var stream = Assert.IsType<PdfStreamObject>(image.SourceCommand.Operands.First());
        var decoded = new PdfStreamDecoderRegistry().Decode(stream).ToArray();
        Assert.All(decoded.Take(6), value => Assert.Equal(0, value));
        Assert.Equal(Samples.Skip(6).Take(6), decoded.Skip(6).Take(6));
    }

    [Fact]
    public async Task Apply_ShouldDecodeJpegAndEmitLosslessRedactedPixels()
    {
        var jpeg = CreateJpeg();
        var pdf = CreateImagePdf(jpeg, filter: "DCTDecode");

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        var entry = Assert.Single(xObjects.Values, item => item.Key.Contains("_PXARedacted", StringComparison.Ordinal));
        var stream = Assert.IsType<PdfStreamObject>(Resolve(graph, entry.Value));
        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(stream.Dictionary["Filter"]).Value);
        var decoded = new PdfStreamDecoderRegistry().Decode(stream).ToArray();
        Assert.All(decoded.Take(6), value => Assert.Equal(0, value));
        Assert.Contains(decoded.Skip(6), value => value != 0);
    }

    [Fact]
    public async Task Apply_ShouldDecodeJpxAndEmitLosslessRedactedPixels()
    {
        var jpx = Convert.FromBase64String(
            "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAAAtanAyaAAAABZpaGRyAAAABAAAAAQAAw8HAAAAAAAPY29scgEAAAAAABAAAADSanAyY/9P/1EALwAAAAAABAAAAAQAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAw8BAQ8BAQ8BAf9SAAwAAAABAQAEBAAB/1wABECA/2QAJQABQ3JlYXRlZCBieSBPcGVuSlBFRyB2ZXJzaW9uIDIuNS40/5AACgAAAAAAWgAB/5PP/DAoEVBJmehQAAAAX9/4kNgI4XWhtfLJvF9OgM5LjKXKM3FFCrfOfWvNwg3f+JDYE4xBXaUm50urMuBda3Zc5UpRW8Eo3d6TvOcJ/9k=");
        var pdf = CreateImagePdf(jpx, filter: "JPXDecode", width: 4, height: 4);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        var decoded = ReadRedactedImageSamples(result.Bytes);
        Assert.Equal(4 * 4 * 3, decoded.Length);
        Assert.Contains(decoded, value => value == 0);
        Assert.Contains(decoded, value => value != 0);
    }

    [Fact]
    public async Task Apply_ShouldDecodeEmbeddedJbig2AndEmitLosslessRedactedPixels()
    {
        // BSD/MIT fixture derived from JBig2Decoder.NETStandard's examplepicture_nl.jb2;
        // the 13-byte standalone header is omitted as required for a PDF JBIG2 stream.
        var jbig2 = Convert.FromBase64String(
            "AAAAADAAAQAAABMAAABgAAAAYAAAAGAAAABgAQAAAAAAASYAAQAAAPsAAABgAAAAYAAAAAAAAAAAAAAD//3/Av7+/qU0eJn1XJxNSolaxuKNOzYqBM4UGoeUcEys/EoevXF9sKz7lnXleKpfpS/cOA9hJWalEvifoO0fv6nozzYcu0Z3KrqRIWToh/BBYPvzjGgLBbtel4AATnDUqXh0W/F/5/9oyLjtCUtxkDascTvCEH6O4/XLhFhIQi/7kchSLf3cFXHX+TdMgUHFB3vvgyBnSxgetHtFMuSjWuMVASEH0meSBm0JoRUyEw8XnMbIjVq5PkLR+92r/FGiMPE1bf1mHT24XCnVjkPLR/gqGRW9dpBsOaWVnRBj/M/+Dp19vGf/rAAAAAIxAAEAAAAAAAAAAzMAAQAAAAA=");
        var pdf = CreateImagePdf(
            jbig2,
            filter: "JBIG2Decode",
            width: 96,
            height: 96,
            bitsPerComponent: 1,
            colorSpace: new PdfName("DeviceGray"));

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        var decoded = ReadRedactedImageSamples(result.Bytes);
        Assert.Equal(96 * 96 * 3, decoded.Length);
        Assert.Contains(decoded, value => value == 0);
        Assert.Contains(decoded, value => value != 0);
    }

    [Fact]
    public async Task Preflight_ShouldRejectJpxWhenEncodedDimensionsDoNotMatchDictionary()
    {
        var jpx = Convert.FromBase64String(
            "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAAAtanAyaAAAABZpaGRyAAAABAAAAAQAAw8HAAAAAAAPY29scgEAAAAAABAAAADSanAyY/9P/1EALwAAAAAABAAAAAQAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAw8BAQ8BAQ8BAf9SAAwAAAABAQAEBAAB/1wABECA/2QAJQABQ3JlYXRlZCBieSBPcGVuSlBFRyB2ZXJzaW9uIDIuNS40/5AACgAAAAAAWgAB/5PP/DAoEVBJmehQAAAAX9/4kNgI4XWhtfLJvF9OgM5LjKXKM3FFCrfOfWvNwg3f+JDYE4xBXaUm50urMuBda3Zc5UpRW8Eo3d6TvOcJ/9k=");
        var pdf = CreateImagePdf(jpx, filter: "JPXDecode", width: 5, height: 4);

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("do not match", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ShouldRejectJpxWithUnsupportedComponentCountBeforeDecode()
    {
        var jpx = Convert.FromBase64String(
            "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAAAtanAyaAAAABZpaGRyAAAABAAAAAQAAw8HAAAAAAAPY29scgEAAAAAABAAAADSanAyY/9P/1EALwAAAAAABAAAAAQAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAw8BAQ8BAQ8BAf9SAAwAAAABAQAEBAAB/1wABECA/2QAJQABQ3JlYXRlZCBieSBPcGVuSlBFRyB2ZXJzaW9uIDIuNS40/5AACgAAAAAAWgAB/5PP/DAoEVBJmehQAAAAX9/4kNgI4XWhtfLJvF9OgM5LjKXKM3FFCrfOfWvNwg3f+JDYE4xBXaUm50urMuBda3Zc5UpRW8Eo3d6TvOcJ/9k=");
        var sizeMarker = FindBytes(jpx, [0xFF, 0x51]);
        Assert.True(sizeMarker >= 0);
        jpx[sizeMarker + 38] = 0;
        jpx[sizeMarker + 39] = 4;
        var pdf = CreateImagePdf(jpx, filter: "JPXDecode", width: 4, height: 4);

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("components", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ShouldRejectStandaloneJbig2FileOrganization()
    {
        var standalone = Convert.FromBase64String(
            "l0pCMg0KGgoBAAAAAQAAAAAwAAEAAAATAAAAYAAAAGAAAABgAAAAYAEAAAAAAAEmAAEAAAD7AAAAYAAAAGAAAAAAAAAAAAAAA//9/wL+/v6lNHiZ9VycTUqJWsbijTs2KgTOFBqHlHBMrPxKHr1xfbCs+5Z15XiqX6Uv3DgPYSVmpRL4n6DtH7+p6M82HLtGdyq6kSFk6IfwQWD784xoCwW7XpeAAE5w1Kl4dFvxf+f/aMi47QlLcZA2rHE7whB+juP1y4RYSEIv+5HIUi393BVx1/k3TIFBxQd774MgZ0sYHrR7RTLko1rjFQEhB9JnkgZtCaEVMhMPF5zGyI1auT5C0fvdq/xRojDxNW39Zh09uFwp1Y5Dy0f4KhkVvXaQbDmllZ0QY/zP/g6dfbxn/6wAAAACMQABAAAAAAAAAAMzAAEAAAAA");
        var pdf = CreateImagePdf(
            standalone,
            filter: "JBIG2Decode",
            width: 96,
            height: 96,
            bitsPerComponent: 1,
            colorSpace: new PdfName("DeviceGray"));

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("standalone file header", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ShouldRejectMultipleJbig2PageInformationSegments()
    {
        var jbig2 = CreateEmbeddedJbig2Fixture();
        var duplicatePageInformation = jbig2[..30].Concat(jbig2).ToArray();
        var pdf = CreateImagePdf(
            duplicatePageInformation,
            filter: "JBIG2Decode",
            width: 96,
            height: 96,
            bitsPerComponent: 1,
            colorSpace: new PdfName("DeviceGray"));

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("multiple page-information", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ShouldRejectOversizedJbig2SymbolDictionaryBeforeDecode()
    {
        var jbig2 = CreateEmbeddedJbig2Fixture();
        byte[] oversizedDictionary =
        [
            0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 10,
            0, 1, 0, 1, 0x86, 0xA1, 0, 0, 0, 0
        ];
        var guardedStream = jbig2[..30].Concat(oversizedDictionary).Concat(jbig2[30..]).ToArray();
        var pdf = CreateImagePdf(
            guardedStream,
            filter: "JBIG2Decode",
            width: 96,
            height: 96,
            bitsPerComponent: 1,
            colorSpace: new PdfName("DeviceGray"));

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("symbol dictionary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preflight_ShouldRejectOversizedJbig2PatternDictionaryBeforeDecode()
    {
        var jbig2 = CreateEmbeddedJbig2Fixture();
        byte[] oversizedDictionary =
        [
            0, 0, 0, 4, 16, 0, 0, 0, 0, 0, 7,
            0, 255, 255, 0, 1, 0x86, 0xA0
        ];
        var guardedStream = jbig2[..30].Concat(oversizedDictionary).Concat(jbig2[30..]).ToArray();
        var pdf = CreateImagePdf(
            guardedStream,
            filter: "JBIG2Decode",
            width: 96,
            height: 96,
            bitsPerComponent: 1,
            colorSpace: new PdfName("DeviceGray"));

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding =>
            finding.Code == "PXAPDFREDACT204" && finding.Message.Contains("pattern dictionary", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1, new byte[] { 0xF0, 0xA0 }, new byte[] { 0x30, 0x20 })]
    [InlineData(2, new byte[] { 0x1B, 0xE4 }, new byte[] { 0x0B, 0x04 })]
    public async Task Apply_ShouldRedactPackedGraySamples(
        int bitsPerComponent,
        byte[] samples,
        byte[] expected)
    {
        var pdf = CreateImagePdf(
            samples,
            bitsPerComponent: bitsPerComponent,
            colorSpace: new PdfName("DeviceGray"));

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        Assert.Equal(expected, ReadRedactedImageSamples(result.Bytes));
    }

    [Fact]
    public async Task Apply_ShouldRedactFourBitRgbSamplesWithoutChangingUncoveredNibbles()
    {
        var samples = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC };
        var pdf = CreateImagePdf(
            samples,
            width: 2,
            bitsPerComponent: 4,
            colorSpace: new PdfName("DeviceRGB"));

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        Assert.Equal(new byte[] { 0x00, 0x04, 0x56, 0x00, 0x0A, 0xBC }, ReadRedactedImageSamples(result.Bytes));
    }

    [Fact]
    public async Task Apply_ShouldRedactSixteenBitRgbSamples()
    {
        var samples = Enumerable.Range(1, 24).Select(value => (byte)value).ToArray();
        var pdf = CreateImagePdf(
            samples,
            width: 2,
            bitsPerComponent: 16,
            colorSpace: new PdfName("DeviceRGB"));

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        var decoded = ReadRedactedImageSamples(result.Bytes);
        Assert.All(decoded.Take(6), value => Assert.Equal(0, value));
        Assert.Equal(samples.Skip(6).Take(6), decoded.Skip(6).Take(6));
        Assert.All(decoded.Skip(12).Take(6), value => Assert.Equal(0, value));
        Assert.Equal(samples.Skip(18).Take(6), decoded.Skip(18).Take(6));
    }

    [Fact]
    public async Task Apply_ShouldResolveNamedIndexedColorSpaceWithoutConvertingUncoveredSamples()
    {
        var lookup = new PdfString(new byte[]
        {
            0, 0, 0,
            255, 0, 0,
            0, 255, 0,
            0, 0, 255
        }, IsHex: true);
        var colorSpaces = new PdfDictionary
        {
            ["Palette"] = new PdfArray([
                new PdfName("Indexed"), new PdfName("DeviceRGB"), new PdfInteger(3), lookup])
        };
        var pdf = CreateImagePdf(
            [0x1B, 0xE4],
            bitsPerComponent: 2,
            colorSpace: new PdfName("Palette"),
            colorSpaces: colorSpaces);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        Assert.Equal(new byte[] { 0x0B, 0x04 }, ReadRedactedImageSamples(result.Bytes));
    }

    [Fact]
    public async Task Apply_ShouldRedactAStencilImageMask()
    {
        var pdf = CreateImagePdf(
            [0xF0, 0xA0],
            bitsPerComponent: 1,
            colorSpace: null,
            imageMask: true);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        Assert.Equal(new byte[] { 0x30, 0x20 }, ReadRedactedImageSamples(result.Bytes));
    }

    [Fact]
    public async Task Apply_ShouldPreserveAColorKeyMaskWhileRemovingCoveredSamples()
    {
        var colorKeyMask = new PdfArray([
            new PdfInteger(0), new PdfInteger(0),
            new PdfInteger(0), new PdfInteger(0),
            new PdfInteger(0), new PdfInteger(0)]);
        var pdf = CreateImagePdf(Samples, mask: colorKeyMask);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        var entry = Assert.Single(xObjects.Values, item => item.Key.Contains("_PXARedacted", StringComparison.Ordinal));
        var stream = Assert.IsType<PdfStreamObject>(Resolve(graph, entry.Value));
        Assert.IsType<PdfArray>(stream.Dictionary["Mask"]);
    }

    [Theory]
    [InlineData("SMask")]
    [InlineData("Mask")]
    public async Task Apply_ShouldRedactMainAndAttachedMaskSamplesTogether(string maskKey)
    {
        var maskSamples = new byte[] { 201, 202, 203, 204, 211, 212, 213, 214 };
        var pdf = CreateSoftMaskedImagePdf(Samples, maskSamples, maskKey);

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        var entry = Assert.Single(xObjects.Values, item => item.Key.Contains("_PXARedacted", StringComparison.Ordinal));
        var image = Assert.IsType<PdfStreamObject>(Resolve(graph, entry.Value));
        var mask = Assert.IsType<PdfStreamObject>(Resolve(graph, image.Dictionary[maskKey]));
        var decodedMask = new PdfStreamDecoderRegistry().Decode(mask).ToArray();

        Assert.All(decodedMask.Take(2), value => Assert.Equal(0, value));
        Assert.Equal(maskSamples.Skip(2).Take(2), decodedMask.Skip(2).Take(2));
        Assert.All(decodedMask.Skip(4).Take(2), value => Assert.Equal(0, value));
        Assert.Equal(maskSamples.Skip(6).Take(2), decodedMask.Skip(6).Take(2));
        Assert.True(Assert.IsType<PdfBoolean>(mask.Dictionary["PXARedacted"]).Value);
    }

    [Fact]
    public async Task Apply_ShouldPreserveResourceAndAnnotationUpdatesOnTheSamePage()
    {
        var pdf = AddOverlappingAnnotation(CreateImagePdf(Samples));

        var result = await ApplyAsync(pdf, new PdfRectangle(50, 50, 100, 200));

        Assert.True(result.Verification.Passed);
        Assert.Equal(1, result.RemovedAnnotationCount);
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        Assert.Empty(Assert.IsType<PdfArray>(page["Annots"]).Items);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        Assert.Contains(xObjects.Values.Keys, name => name.Contains("_PXARedacted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Apply_ShouldRemoveAFullyContainedImageAndItsPayload()
    {
        var pdf = CreateImagePdf(Samples);

        var result = await ApplyAsync(pdf, new PdfRectangle(40, 40, 220, 220));

        Assert.True(result.Verification.Passed);
        Assert.Equal(1, result.RemovedElementCount);
        var (_, graph) = ParseGraph(result.Bytes);
        var (_, page) = FindPage(graph);
        Assert.DoesNotContain(" Do", ReadPageContent(graph, page), StringComparison.Ordinal);
        Assert.DoesNotContain(graph.Objects.Values, item =>
            item.Value is PdfStreamObject stream && stream.Dictionary["Subtype"] is PdfName { Value: "Image" });
    }

    [Theory]
    [InlineData("UnknownFilter")]
    public async Task Preflight_ShouldBlockPartialImagesWithUnsupportedFilters(string filter)
    {
        var pdf = CreateImagePdf(Samples, filter: filter);

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding => finding.Code == "PXAPDFREDACT204");
    }

    [Fact]
    public async Task Preflight_ShouldBlockAPartialSoftMaskedImage()
    {
        var pdf = CreateImagePdf(Samples, softMask: true);

        await using var source = new MemoryStream(pdf);
        var report = await new PdfRedactionPreflight().PreflightAsync(
            source,
            [new PdfRedactionRegion(1, new PdfRectangle(50, 50, 100, 200))]);

        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Regions.SelectMany(region => region.Findings), finding => finding.Code == "PXAPDFREDACT209");
    }

    [Fact]
    public async Task Verification_ShouldRejectAForgedRedactionMarkerWithLivePixels()
    {
        var pdf = CreateImagePdf(Samples, markedRedacted: true);
        var (_, graph) = ParseGraph(pdf);
        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);
        var findings = new List<PdfRedactionFinding>();

        typeof(PdfRedactionPreflight).Assembly
            .GetType("PXA.Importer.Redaction.PdfRedactionResidueScanner")!
            .GetMethod("Scan", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [
                graph,
                document,
                new Dictionary<int, PdfRectangle[]> { [1] = [new PdfRectangle(50, 50, 100, 200)] },
                findings
            ]);

        Assert.Contains(findings, finding => finding.Code == "PXAPDFREDACT304");
    }

    private static async Task<PdfRedactionApplyResult> ApplyAsync(byte[] pdf, PdfRectangle bounds)
    {
        await using var source = new MemoryStream(pdf);
        return await new PdfRedactionApplier(new PdfRedactionPreflight())
            .ApplyAsync(source, [new PdfRedactionRegion(1, bounds)]);
    }

    private static byte[] CreateEmbeddedJbig2Fixture() => Convert.FromBase64String(
        "AAAAADAAAQAAABMAAABgAAAAYAAAAGAAAABgAQAAAAAAASYAAQAAAPsAAABgAAAAYAAAAAAAAAAAAAAD//3/Av7+/qU0eJn1XJxNSolaxuKNOzYqBM4UGoeUcEys/EoevXF9sKz7lnXleKpfpS/cOA9hJWalEvifoO0fv6nozzYcu0Z3KrqRIWToh/BBYPvzjGgLBbtel4AATnDUqXh0W/F/5/9oyLjtCUtxkDascTvCEH6O4/XLhFhIQi/7kchSLf3cFXHX+TdMgUHFB3vvgyBnSxgetHtFMuSjWuMVASEH0meSBm0JoRUyEw8XnMbIjVq5PkLR+92r/FGiMPE1bf1mHT24XCnVjkPLR/gqGRW9dpBsOaWVnRBj/M/+Dp19vGf/rAAAAAIxAAEAAAAAAAAAAzMAAQAAAAA=");

    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (var index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }
        return -1;
    }

    private static byte[] CreateImagePdf(
        byte[] samples,
        bool drawTwice = false,
        string? filter = null,
        bool softMask = false,
        bool markedRedacted = false,
        int width = 4,
        int height = 2,
        int bitsPerComponent = 8,
        PdfObject? colorSpace = null,
        PdfDictionary? colorSpaces = null,
        bool imageMask = false,
        PdfObject? mask = null)
    {
        var document = new PdfDocument();
        document.AddPage();
        var source = document.ToBytes();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var imageId = new PdfObjectId(table.Entries.Keys.Max(id => id.Number) + 1, 0);
        var contentId = new PdfObjectId(imageId.Number + 1, 0);
        var dictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Image"),
            ["Width"] = new PdfInteger(width),
            ["Height"] = new PdfInteger(height),
            ["BitsPerComponent"] = new PdfInteger(bitsPerComponent)
        };
        if (!imageMask)
        {
            dictionary["ColorSpace"] = colorSpace ?? new PdfName("DeviceRGB");
        }
        else
        {
            dictionary["ImageMask"] = new PdfBoolean(true);
        }

        if (mask is not null)
        {
            dictionary["Mask"] = mask;
        }
        if (filter is not null)
        {
            dictionary["Filter"] = new PdfName(filter);
        }

        if (softMask)
        {
            dictionary["SMask"] = new PdfName("Placeholder");
        }

        if (markedRedacted)
        {
            dictionary["PXARedacted"] = new PdfBoolean(true);
        }

        var content = drawTwice
            ? "q 200 0 0 200 50 50 cm /Im0 Do Q q 200 0 0 200 300 50 cm /Im0 Do Q"
            : "q 200 0 0 200 50 50 cm /Im0 Do Q";
        var updatedPage = Clone(page);
        var resources = new PdfDictionary
        {
            ["XObject"] = new PdfDictionary { ["Im0"] = new PdfReference(imageId) }
        };
        if (colorSpaces is not null)
        {
            resources["ColorSpace"] = colorSpaces;
        }

        updatedPage["Resources"] = resources;
        updatedPage["Contents"] = new PdfReference(contentId);

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(imageId, new PdfStreamObject(dictionary, samples)),
            new PdfIncrementalObjectUpdate(contentId, new PdfStreamObject(
                new PdfDictionary(), Encoding.ASCII.GetBytes(content))),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static byte[] CreateInlineImagePdf(byte[] samples, string? abbreviatedFilter = null)
    {
        var document = new PdfDocument();
        document.AddPage();
        var source = document.ToBytes();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var contentId = new PdfObjectId(table.Entries.Keys.Max(id => id.Number) + 1, 0);
        using var content = new MemoryStream();
        content.Write(Encoding.ASCII.GetBytes(
            "q 200 0 0 200 50 50 cm BI /W 4 /H 2 /BPC 8 /CS /RGB" +
            (abbreviatedFilter is null ? string.Empty : $" /F /{abbreviatedFilter}") +
            " ID\n"));
        content.Write(samples);
        content.Write("\nEI Q"u8);
        var updatedPage = Clone(page);
        updatedPage["Contents"] = new PdfReference(contentId);
        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(contentId, new PdfStreamObject(new PdfDictionary(), content.ToArray())),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static byte[] CreateSoftMaskedImagePdf(
        byte[] samples,
        byte[] maskSamples,
        string maskKey = "SMask")
    {
        var document = new PdfDocument();
        document.AddPage();
        var source = document.ToBytes();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var imageId = new PdfObjectId(table.Entries.Keys.Max(id => id.Number) + 1, 0);
        var maskId = new PdfObjectId(imageId.Number + 1, 0);
        var contentId = new PdfObjectId(maskId.Number + 1, 0);
        var imageDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Image"),
            ["Width"] = new PdfInteger(4),
            ["Height"] = new PdfInteger(2),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["ColorSpace"] = new PdfName("DeviceRGB")
        };
        imageDictionary[maskKey] = new PdfReference(maskId);
        var maskDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"),
            ["Subtype"] = new PdfName("Image"),
            ["Width"] = new PdfInteger(4),
            ["Height"] = new PdfInteger(2),
            ["BitsPerComponent"] = new PdfInteger(8),
            ["ColorSpace"] = new PdfName("DeviceGray")
        };
        var updatedPage = Clone(page);
        updatedPage["Resources"] = new PdfDictionary
        {
            ["XObject"] = new PdfDictionary { ["Im0"] = new PdfReference(imageId) }
        };
        updatedPage["Contents"] = new PdfReference(contentId);

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(imageId, new PdfStreamObject(imageDictionary, samples)),
            new PdfIncrementalObjectUpdate(maskId, new PdfStreamObject(maskDictionary, maskSamples)),
            new PdfIncrementalObjectUpdate(contentId, new PdfStreamObject(
                new PdfDictionary(), "q 200 0 0 200 50 50 cm /Im0 Do Q"u8.ToArray())),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static byte[] Compress(byte[] samples)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(
                   output,
                   System.IO.Compression.CompressionLevel.Optimal,
                   leaveOpen: true))
        {
            zlib.Write(samples);
        }

        return output.ToArray();
    }

    private static byte[] ReadRedactedImageSamples(byte[] pdf)
    {
        var (_, graph) = ParseGraph(pdf);
        var (_, page) = FindPage(graph);
        var resources = Assert.IsType<PdfDictionary>(Resolve(graph, page["Resources"]));
        var xObjects = Assert.IsType<PdfDictionary>(Resolve(graph, resources["XObject"]));
        var entry = Assert.Single(xObjects.Values, item => item.Key.Contains("_PXARedacted", StringComparison.Ordinal));
        var stream = Assert.IsType<PdfStreamObject>(Resolve(graph, entry.Value));
        return new PdfStreamDecoderRegistry().Decode(stream).ToArray();
    }

    private static byte[] CreateJpeg()
    {
        using var bitmap = new SKBitmap(4, 2);
        for (var row = 0; row < bitmap.Height; row++)
        {
            for (var column = 0; column < bitmap.Width; column++)
            {
                bitmap.SetPixel(column, row, new SKColor(
                    (byte)(40 + column * 40),
                    (byte)(50 + row * 80),
                    (byte)(180 - column * 20)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    private static byte[] AddOverlappingAnnotation(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var annotationId = new PdfObjectId(table.Entries.Keys.Max(id => id.Number) + 1, 0);
        var updatedPage = Clone(page);
        updatedPage["Annots"] = new PdfArray([new PdfReference(annotationId)]);
        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(annotationId, new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Square"),
                ["Rect"] = new PdfArray([
                    new PdfInteger(60), new PdfInteger(60), new PdfInteger(120), new PdfInteger(120)])
            }),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static string ReadPageContent(PdfObjectGraph graph, PdfDictionary page)
    {
        var stream = Assert.IsType<PdfStreamObject>(Resolve(graph, page["Contents"]));
        return Encoding.ASCII.GetString(new PdfStreamDecoderRegistry().Decode(stream).Span);
    }

    private static (PdfObjectId Id, PdfDictionary Page) FindPage(PdfObjectGraph graph)
    {
        var entry = graph.Objects.Values.First(item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Page" });
        return (entry.Id, (PdfDictionary)entry.Value);
    }

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }
}
