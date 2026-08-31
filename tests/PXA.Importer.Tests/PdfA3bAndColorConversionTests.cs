using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfA3bAndColorConversionTests
{
    [Fact]
    public async Task Preflight_ShouldConvertCmykContentInsteadOfBlockingIt()
    {
        var source = WithCmykContent(CreateBaseDocument());

        var report = await new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(Stream(source));

        Assert.NotEqual(PdfComplianceConversionCapability.Unsupported, report.Capability);
        var finding = Assert.Single(report.Findings, static item => item.Code == "PXAPDFACONVERT004");
        Assert.Equal(PdfTrustDiagnosticSeverity.Info, finding.Severity);
        Assert.Contains("converted to sRGB", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Convert_ShouldReplaceCmykOperatorsWithRgbInTheOutput()
    {
        var source = WithCmykContent(CreateBaseDocument());
        Assert.Contains(" k\n", DecodeContent(source), StringComparison.Ordinal);

        var result = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(Stream(source));

        var converted = DecodeContent(result.Bytes);
        Assert.DoesNotContain(" k\n", converted, StringComparison.Ordinal);
        Assert.Contains("rg", converted, StringComparison.Ordinal);
        // 0 0 0 1 k is black, which the specified conversion maps to 0 0 0 rg.
        Assert.Contains("0 0 0 rg", converted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_ShouldStillBlockAPatternColorSpace()
    {
        var source = WithPatternColorSpace(CreateBaseDocument());

        var report = await new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(Stream(source));

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFACONVERT012");
    }

    [Fact]
    public async Task Preflight_ShouldStillBlockACmykImage()
    {
        var source = WithCmykImage(CreateBaseDocument());

        var report = await new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(Stream(source));

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
        var finding = Assert.Single(report.Findings, static item => item.Code == "PXAPDFACONVERT010");
        Assert.Contains("DeviceCMYK", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_ShouldBlockEmbeddedFilesForPdfA2bButAllowThemForPdfA3b()
    {
        var source = WithEmbeddedFile(CreateBaseDocument());
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        var a2b = await converter.PreflightAsync(Stream(source));
        var a3b = await converter.PreflightAsync(
            Stream(source),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });

        Assert.Equal("PDF/A-2b", a2b.TargetProfile);
        Assert.Equal(PdfComplianceConversionCapability.Unsupported, a2b.Capability);
        Assert.Contains(a2b.Findings, static item => item.Code == "PXAPDFACONVERT006");

        Assert.Equal("PDF/A-3b", a3b.TargetProfile);
        Assert.NotEqual(PdfComplianceConversionCapability.Unsupported, a3b.Capability);
        Assert.Contains(a3b.Findings, static item => item.Code == "PXAPDFACONVERT013");
    }

    [Fact]
    public async Task Convert_ShouldWritePart3AndRetainTheEmbeddedFileForPdfA3b()
    {
        var source = WithEmbeddedFile(CreateBaseDocument());

        var result = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(
            Stream(source),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });

        if (Environment.GetEnvironmentVariable("PXA_PDFA_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(artifactDirectory, "pxa-pdfa3b-fixture.pdf"),
                result.Bytes);
        }

        var (_, graph) = ParseGraph(result.Bytes);
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", ReadXmp(graph), StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", ReadXmp(graph), StringComparison.Ordinal);
        Assert.Contains("PXA attachment payload", Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Convert_ShouldGiveEmbeddedFilesTheRelationshipAndAssociationPdfA3Requires()
    {
        var source = WithEmbeddedFile(CreateBaseDocument());

        var result = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(
            Stream(source),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });

        var (_, graph) = ParseGraph(result.Bytes);
        var specification = graph.Objects.Values
            .Select(static item => item.Value)
            .OfType<PdfDictionary>()
            .Single(static item => item["Type"] is PdfName { Value: "Filespec" });
        Assert.Equal("Unspecified", Assert.IsType<PdfName>(specification["AFRelationship"]).Value);
        Assert.NotNull(specification["UF"]);

        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        var associated = Assert.IsType<PdfArray>(catalog["AF"]);
        Assert.NotEmpty(associated.Items);
    }

    [Fact]
    public async Task Convert_ShouldStillWritePart2ForPdfA2b()
    {
        var source = CreateBaseDocument();

        var result = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(Stream(source));

        var (_, graph) = ParseGraph(result.Bytes);
        Assert.Contains("<pdfaid:part>2</pdfaid:part>", ReadXmp(graph), StringComparison.Ordinal);
        Assert.Equal("PDF/A-2b", result.Preflight.TargetProfile);
    }

    [Fact]
    public async Task Inspector_ShouldRejectPdfA3bWithAnInvalidEmbeddedFileMimeSubtype()
    {
        var converted = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(
            Stream(WithEmbeddedFile(CreateBaseDocument())),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });
        var (_, graph) = ParseGraph(converted.Bytes);
        var embedded = graph.Objects.Values.Single(item =>
            item.Value is PdfStreamObject stream && stream.Dictionary["Subtype"] is PdfName { Value: "text/plain" });
        var original = (PdfStreamObject)embedded.Value;
        var dictionary = Clone(original.Dictionary);
        dictionary["Subtype"] = new PdfName("invalid-mime");
        var malformed = Append(converted.Bytes, [
            new PdfIncrementalObjectUpdate(embedded.Id, new PdfStreamObject(dictionary, original.EncodedBytes))
        ]);

        var report = await new PdfTrustInspector().InspectAsync(Stream(malformed));

        Assert.Equal(PdfTrustStatus.Invalid, report.PdfA.Status);
        Assert.Contains(report.Diagnostics, static item => item.Code == "PXAPDFTRUST322");
    }

    [Fact]
    public async Task Preflight_ShouldBlockDirectEmbeddedFileSpecifications()
    {
        var report = await new PdfA2bConverter(new PdfTrustInspector()).PreflightAsync(
            Stream(WithDirectEmbeddedFile(CreateBaseDocument())),
            new PdfA2bConversionOptions { Profile = PdfArchiveProfile.PdfA3b });

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
        Assert.Contains(report.Findings, static item => item.Code == "PXAPDFTRUST323");
    }

    private static MemoryStream Stream(byte[] bytes) => new(bytes, writable: false);

    private static byte[] CreateBaseDocument()
    {
        var document = new PdfDocument
        {
            FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"))
        };
        document.Info.Title = "Colour conversion fixture";
        document.Info.Producer = "PXA";
        document.AddPage().DrawText("Archival text", 40, 40, new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B });
    }

    private static byte[] WithCmykContent(byte[] source)
    {
        var (_, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var contentId = ((PdfReference)page["Contents"]!).Id;
        var existing = (PdfStreamObject)graph.Resolve(contentId)!.Value;
        var content = Encoding.ASCII.GetBytes("0 0 0 1 k\n10 10 50 50 re f\n")
            .Concat(Decode(existing))
            .ToArray();
        return Append(source, [Replace(contentId, existing, content)]);
    }

    private static byte[] WithPatternColorSpace(byte[] source)
    {
        var (_, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var contentId = ((PdfReference)page["Contents"]!).Id;
        var existing = (PdfStreamObject)graph.Resolve(contentId)!.Value;
        var content = Encoding.ASCII.GetBytes("/CSPat cs /P0 scn\n10 10 50 50 re f\n")
            .Concat(Decode(existing))
            .ToArray();
        var updatedPage = Clone(page);
        updatedPage["Resources"] = new PdfDictionary
        {
            ["ColorSpace"] = new PdfDictionary { ["CSPat"] = new PdfName("Pattern") }
        };
        return Append(source, [
            Replace(contentId, existing, content),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] WithCmykImage(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var imageId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var image = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("XObject"),
                ["Subtype"] = new PdfName("Image"),
                ["Width"] = new PdfInteger(1),
                ["Height"] = new PdfInteger(1),
                ["BitsPerComponent"] = new PdfInteger(8),
                ["ColorSpace"] = new PdfName("DeviceCMYK")
            },
            new byte[] { 0, 0, 0, 255 });
        var (pageId, page) = FindPage(graph);
        var contentId = ((PdfReference)page["Contents"]!).Id;
        var content = (PdfStreamObject)graph.Resolve(contentId)!.Value;
        var resources = Resolve(graph, page["Resources"]) is PdfDictionary existingResources
            ? Clone(existingResources)
            : new PdfDictionary();
        var xObjects = Resolve(graph, resources["XObject"]) is PdfDictionary existingXObjects
            ? Clone(existingXObjects)
            : new PdfDictionary();
        xObjects["ImCmyk"] = new PdfReference(imageId);
        resources["XObject"] = xObjects;
        var updatedPage = Clone(page);
        updatedPage["Resources"] = resources;
        var updatedContent = Encoding.ASCII.GetBytes("q 1 0 0 1 0 0 cm /ImCmyk Do Q\n")
            .Concat(Decode(content))
            .ToArray();

        return Append(source, [
            new PdfIncrementalObjectUpdate(imageId, image),
            new PdfIncrementalObjectUpdate(pageId, updatedPage),
            Replace(contentId, content, updatedContent)
        ]);
    }

    private static byte[] WithEmbeddedFile(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var next = table.Entries.Keys.Max(static id => id.Number) + 1;
        var streamId = new PdfObjectId(next, 0);
        var specificationId = new PdfObjectId(next + 1, 0);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updatedCatalog = Clone(catalog);
        updatedCatalog["Names"] = new PdfDictionary
        {
            ["EmbeddedFiles"] = new PdfDictionary
            {
                ["Names"] = new PdfArray([
                    new PdfString("source.txt"u8.ToArray(), IsHex: false),
                    new PdfReference(specificationId)
                ])
            }
        };

        return Append(source, [
            new PdfIncrementalObjectUpdate(streamId, new PdfStreamObject(
                new PdfDictionary { ["Subtype"] = new PdfName("text/plain") },
                "PXA attachment payload"u8.ToArray())),
            new PdfIncrementalObjectUpdate(specificationId, new PdfDictionary
            {
                ["Type"] = new PdfName("Filespec"),
                ["F"] = new PdfString("source.txt"u8.ToArray(), IsHex: false),
                ["EF"] = new PdfDictionary { ["F"] = new PdfReference(streamId) }
            }),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]);
    }

    private static byte[] WithDirectEmbeddedFile(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var streamId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updatedCatalog = Clone(catalog);
        updatedCatalog["Names"] = new PdfDictionary
        {
            ["EmbeddedFiles"] = new PdfDictionary
            {
                ["Names"] = new PdfArray([
                    new PdfString("direct.txt"u8.ToArray(), IsHex: false),
                    new PdfDictionary
                    {
                        ["Type"] = new PdfName("Filespec"),
                        ["F"] = new PdfString("direct.txt"u8.ToArray(), IsHex: false),
                        ["EF"] = new PdfDictionary { ["F"] = new PdfReference(streamId) }
                    }
                ])
            }
        };

        return Append(source, [
            new PdfIncrementalObjectUpdate(streamId, new PdfStreamObject(
                new PdfDictionary { ["Subtype"] = new PdfName("text/plain") },
                "direct attachment"u8.ToArray())),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]);
    }

    private static PdfIncrementalObjectUpdate Replace(PdfObjectId id, PdfStreamObject existing, byte[] content)
    {
        var dictionary = Clone(existing.Dictionary);
        dictionary["Filter"] = null;
        dictionary["DecodeParms"] = null;
        dictionary["Length"] = new PdfInteger(content.Length);
        return new PdfIncrementalObjectUpdate(id, new PdfStreamObject(dictionary, content));
    }

    private static byte[] Append(byte[] source, IReadOnlyList<PdfIncrementalObjectUpdate> updates) =>
        new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;

    private static string DecodeContent(byte[] pdf)
    {
        var (_, graph) = ParseGraph(pdf);
        var (_, page) = FindPage(graph);
        var contentId = ((PdfReference)page["Contents"]!).Id;
        return Encoding.ASCII.GetString(Decode((PdfStreamObject)graph.Resolve(contentId)!.Value));
    }

    private static string ReadXmp(PdfObjectGraph graph)
    {
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        var metadata = (PdfStreamObject)graph.Resolve(((PdfReference)catalog["Metadata"]!).Id)!.Value;
        return Encoding.UTF8.GetString(Decode(metadata));
    }

    private static byte[] Decode(PdfStreamObject stream) =>
        stream.IsDecoded
            ? stream.DecodedBytes.ToArray()
            : new Streams.PdfStreamDecoderRegistry().Decode(stream).ToArray();

    private static (PdfObjectId Id, PdfDictionary Page) FindPage(PdfObjectGraph graph)
    {
        var entry = graph.Objects.Values.First(static item =>
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
