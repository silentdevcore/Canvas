using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Redaction;
using PVAD.Importer.Saving;
using PVAD.Importer.Xref;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

/// <summary>
/// Each verification check must fire on a result that still carries the residue it looks
/// for. A check that can never fail proves nothing.
/// </summary>
public sealed class PdfRedactionVerificationTests
{
    private const string Secret = "CONFIDENTIAL-RESIDUE-90311";

    [Fact]
    public async Task Verify_ShouldReportARetainedThumbnail()
    {
        var findings = await ScanAsync(WithPageEntry("Thumb", new PdfStreamObject(
            new PdfDictionary { ["Subtype"] = new PdfName("Image") },
            new byte[] { 1, 2, 3 })));

        Assert.Contains(findings, f => f.Code == "PXAPDFREDACT305");
    }

    [Fact]
    public async Task Verify_ShouldReportRetainedOptionalContent()
    {
        var findings = await ScanAsync(WithCatalogEntry("OCProperties", new PdfDictionary
        {
            ["OCGs"] = new PdfArray()
        }));

        Assert.Contains(findings, f => f.Code == "PXAPDFREDACT306");
    }

    [Fact]
    public async Task Verify_ShouldReportARetainedHiddenAppearance()
    {
        var findings = await ScanAsync(WithAnnotation(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Widget"),
            ["Rect"] = Rect(500, 700, 560, 760),
            ["AP"] = new PdfDictionary
            {
                ["N"] = new PdfDictionary(),
                ["D"] = new PdfDictionary()
            }
        }));

        Assert.Contains(findings, f => f.Code == "PXAPDFREDACT307");
    }

    [Fact]
    public async Task Verify_ShouldReportACoveredImageThatSurvived()
    {
        var findings = await ScanAsync(WithCoveringImage(), coverWholePage: true);

        Assert.Contains(findings, f => f.Code == "PXAPDFREDACT304");
    }

    [Fact]
    public async Task Verify_ShouldReportNothingForACleanDocument()
    {
        // The clean document has no secret to locate, so the whole page stands in as the region.
        var findings = await ScanAsync(CreateCleanPdf(), coverWholePage: true);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Apply_ShouldPassEveryVerificationCheckOnASupportedDocument()
    {
        var pdf = CreatePdf();
        await using var stream = new MemoryStream(pdf);
        var preflight = new PdfRedactionPreflight();

        var result = await new PdfRedactionApplier(preflight)
            .ApplyAsync(stream, [await RegionAsync(pdf)]);

        Assert.True(result.Verification.Passed);
        Assert.Empty(result.Verification.Findings);
    }

    [Fact]
    public async Task Apply_ShouldNotRejectTheSameTextOutsideTheRedactedRegion()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawText(Secret, 40, 700);
        page.DrawText(Secret, 40, 400);
        var pdf = document.ToBytes();
        await using var source = new MemoryStream(pdf);

        var result = await new PdfRedactionApplier(new PdfRedactionPreflight())
            .ApplyAsync(source, [await RegionAsync(pdf)]);

        Assert.True(result.Verification.Passed);
        await using var resultStream = new MemoryStream(result.Bytes);
        var imported = await new PdfImporter().LoadAsync(resultStream);
        Assert.Single(imported.Pages[0].TextObjects, text => text.Text.Contains(Secret, StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<PdfRedactionFinding>> ScanAsync(
        byte[] pdf,
        bool coverWholePage = false)
    {
        var (_, graph) = ParseGraph(pdf);
        await using var stream = new MemoryStream(pdf);
        var document = await new PdfImporter().LoadAsync(stream);
        var region = coverWholePage
            ? new Graphics.PdfRectangle(0, 0, 1000, 1000)
            : (await RegionAsync(pdf)).Bounds;
        var findings = new List<PdfRedactionFinding>();

        typeof(PdfRedactionPreflight).Assembly
            .GetType("PVAD.Importer.Redaction.PdfRedactionResidueScanner")!
            .GetMethod("Scan", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [
                graph,
                document,
                new Dictionary<int, Graphics.PdfRectangle[]> { [1] = [region] },
                findings
            ]);

        return findings;
    }

    private static async Task<PdfRedactionRegion> RegionAsync(byte[] pdf)
    {
        await using var stream = new MemoryStream(pdf);
        var document = await new PdfImporter().LoadAsync(stream);
        var element = document.Pages[0].GraphicsObjects
            .OfType<Graphics.PdfTextElement>()
            .First(item => item.Text.Contains(Secret, StringComparison.Ordinal));
        var bounds = element.Bounds ?? new Analysis.BoundingBoxCalculator().ComputeElementBounds(element);
        return new PdfRedactionRegion(1, new Graphics.PdfRectangle(
            bounds.Left - 4, bounds.Bottom - 4, bounds.Width + 8, bounds.Height + 8));
    }

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawText("Public heading", 40, 700);
        page.DrawText(Secret, 40, 400);
        return document.ToBytes();
    }

    private static byte[] CreateCleanPdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Nothing to see", 40, 700);
        return document.ToBytes();
    }

    private static byte[] WithCoveringImage()
    {
        var source = CreateCleanPdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var imageId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var contentId = new PdfObjectId(imageId.Number + 1, 0);
        var updatedPage = Clone(page);
        updatedPage["Resources"] = new PdfDictionary
        {
            ["XObject"] = new PdfDictionary { ["Im0"] = new PdfReference(imageId) }
        };
        updatedPage["Contents"] = new PdfReference(contentId);
        return Append(source, [
            new PdfIncrementalObjectUpdate(imageId, new PdfStreamObject(
                new PdfDictionary
                {
                    ["Type"] = new PdfName("XObject"),
                    ["Subtype"] = new PdfName("Image"),
                    ["Width"] = new PdfInteger(1),
                    ["Height"] = new PdfInteger(1),
                    ["BitsPerComponent"] = new PdfInteger(8),
                    ["ColorSpace"] = new PdfName("DeviceGray")
                },
                new byte[] { 0 })),
            new PdfIncrementalObjectUpdate(contentId, new PdfStreamObject(
                new PdfDictionary(),
                "q 200 0 0 200 50 50 cm /Im0 Do Q"u8.ToArray())),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] WithPageEntry(string key, PdfObject value)
    {
        var source = CreatePdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var valueId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updatedPage = Clone(page);
        updatedPage[key] = new PdfReference(valueId);
        return Append(source, [
            new PdfIncrementalObjectUpdate(valueId, value),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] WithCatalogEntry(string key, PdfObject value)
    {
        var source = CreatePdf();
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updated = Clone(catalog);
        updated[key] = value;
        return Append(source, [new PdfIncrementalObjectUpdate(rootId, updated)]);
    }

    private static byte[] WithAnnotation(PdfDictionary annotation)
    {
        var source = CreatePdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var annotationId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updatedPage = Clone(page);
        updatedPage["Annots"] = new PdfArray([new PdfReference(annotationId)]);
        return Append(source, [
            new PdfIncrementalObjectUpdate(annotationId, annotation),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] Append(byte[] source, IReadOnlyList<PdfIncrementalObjectUpdate> updates) =>
        new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;

    private static PdfArray Rect(double left, double bottom, double right, double top) =>
        new([new PdfNumber(left), new PdfNumber(bottom), new PdfNumber(right), new PdfNumber(top)]);

    private static (PdfObjectId Id, PdfDictionary Page) FindPage(PdfObjectGraph graph)
    {
        var entry = graph.Objects.Values.First(static item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Page" });
        return (entry.Id, (PdfDictionary)entry.Value);
    }

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }
}
