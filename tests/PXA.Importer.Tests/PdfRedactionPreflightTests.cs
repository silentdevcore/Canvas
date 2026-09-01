using System.Text;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Redaction;
using PXA.Importer.Saving;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfRedactionPreflightTests
{
    [Fact]
    public async Task Preflight_ShouldReportFullyCoveredTextAsRemovable()
    {
        var pdf = CreatePdf();

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        var region = Assert.Single(report.Regions);
        var text = Assert.Single(region.Covered, static item => item.Kind == "text");
        Assert.Equal(PdfRedactionCoverage.Contained, text.Coverage);
        Assert.Equal(PdfRedactionCapability.Removable, text.Capability);
        Assert.True(text.CharacterCount > 0);
        Assert.DoesNotContain(region.Findings, static finding => finding.Code == "PXAPDFREDACT201");
    }

    [Fact]
    public async Task Preflight_ShouldReportPartiallyCoveredTextAsReviewRequired()
    {
        var pdf = CreatePdf();
        var bounds = await FindFirstTextBoundsAsync(pdf);
        // Clip the region so it covers only the left half of the run.
        var half = new PdfRectangle(bounds.Left - 5, bounds.Bottom - 2, bounds.Width / 2, bounds.Height + 4);

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, half));

        var region = Assert.Single(report.Regions);
        Assert.Equal(PdfRedactionCapability.ReviewRequired, region.Capability);
        Assert.Contains(region.Findings, static finding => finding.Code == "PXAPDFREDACT201");
        Assert.Contains(region.Covered, static item => item.Coverage == PdfRedactionCoverage.Partial);
    }

    [Fact]
    public async Task Preflight_ShouldReportAContainedImageAsRemovable()
    {
        var document = new PdfDocument();
        document.AddPage().DrawImage(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="),
            40,
            40,
            100,
            100);
        var pdf = document.ToBytes();

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        var region = Assert.Single(report.Regions);
        Assert.Equal(PdfRedactionCapability.Removable, region.Capability);
        Assert.DoesNotContain(region.Findings, static finding => finding.Code == "PXAPDFREDACT208");
        Assert.Contains(
            region.Covered,
            static item => item.Kind == "image" && item.Capability == PdfRedactionCapability.Removable);
    }

    [Fact]
    public async Task Preflight_ShouldReportNoCoveredContentForAnEmptyArea()
    {
        var pdf = CreatePdf();

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, new PdfRectangle(500, 700, 20, 20)));

        var region = Assert.Single(report.Regions);
        Assert.Empty(region.Covered);
        Assert.Equal(PdfRedactionCapability.Removable, region.Capability);
    }

    [Fact]
    public async Task Preflight_ShouldNeverEchoCoveredText()
    {
        var pdf = CreatePdf("CONFIDENTIAL-PXA-SECRET");

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        var serialized = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain("CONFIDENTIAL-PXA-SECRET", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_ShouldNotMutateTheInput()
    {
        var pdf = CreatePdf();
        var original = pdf.ToArray();

        await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        Assert.Equal(original, pdf);
    }

    [Fact]
    public async Task Preflight_ShouldReportEarlierRevisionsAsReviewRequired()
    {
        var pdf = CreatePdf();
        var appended = new PdfIncrementalUpdateWriter().Append(pdf, [
            new PdfIncrementalObjectUpdate(
                new PdfObjectId(NextObjectNumber(pdf), 0),
                new PdfDictionary { ["Type"] = new PdfName("PxaMarker") })
        ]).Bytes;

        var report = await PreflightAsync(appended, new PdfRedactionRegion(1, WholePage));

        Assert.Equal(2, report.RevisionCount);
        Assert.Contains(report.DocumentFindings, static finding => finding.Code == "PXAPDFREDACT102");
        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
    }

    [Fact]
    public async Task Preflight_ShouldReportOverlappingFormFieldsAndAnnotations()
    {
        var pdf = CreatePdf();
        var withAnnotations = AddPageAnnotations(pdf);

        var report = await PreflightAsync(withAnnotations, new PdfRedactionRegion(1, WholePage));

        var region = Assert.Single(report.Regions);
        Assert.Contains(region.Findings, static finding => finding.Code == "PXAPDFREDACT207");
        Assert.Contains(region.Findings, static finding => finding.Code == "PXAPDFREDACT202");
        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
    }

    [Fact]
    public async Task Preflight_ShouldReportDocumentLevelCarriers()
    {
        var pdf = AddCatalogEntries(CreatePdf());

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        Assert.Contains(report.DocumentFindings, static finding => finding.Code == "PXAPDFREDACT107");
        Assert.Contains(report.DocumentFindings, static finding => finding.Code == "PXAPDFREDACT108");
        Assert.Equal(PdfRedactionCapability.ReviewRequired, report.Capability);
    }

    [Fact]
    public async Task Preflight_ShouldRejectAnUnknownPage()
    {
        var report = await PreflightAsync(CreatePdf(), new PdfRedactionRegion(7, WholePage));

        var region = Assert.Single(report.Regions);
        Assert.Equal(PdfRedactionCapability.Unsupported, region.Capability);
        Assert.Contains(region.Findings, static finding => finding.Code == "PXAPDFREDACT110");
        Assert.Equal(PdfRedactionCapability.Unsupported, report.Capability);
    }

    [Fact]
    public async Task Preflight_ShouldRejectAnEmptyRegion()
    {
        var report = await PreflightAsync(CreatePdf(), new PdfRedactionRegion(1, new PdfRectangle(10, 10, 0, 0)));

        Assert.Contains(
            Assert.Single(report.Regions).Findings,
            static finding => finding.Code == "PXAPDFREDACT111");
    }

    [Fact]
    public async Task Preflight_ShouldRejectEncryptedDocuments()
    {
        var pdf = Encoding.Latin1.GetBytes(
            Encoding.Latin1.GetString(CreatePdf()).Replace("/Root", "/Encrypt 1 0 R /Root", StringComparison.Ordinal));

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        Assert.Equal(PdfRedactionCapability.Unsupported, report.Capability);
        Assert.Contains(report.DocumentFindings, static finding => finding.Code == "PXAPDFREDACT101");
        Assert.Empty(report.Regions);
    }

    [Fact]
    public async Task Preflight_ShouldRequireAtLeastOneRegion() =>
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await using var stream = new MemoryStream(CreatePdf());
            await new PdfRedactionPreflight().PreflightAsync(stream, []);
        });

    [Fact]
    public async Task Preflight_ShouldBoundTheRegionCount() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await using var stream = new MemoryStream(CreatePdf());
            var regions = Enumerable.Repeat(new PdfRedactionRegion(1, WholePage), 5).ToArray();
            await new PdfRedactionPreflight().PreflightAsync(
                stream,
                regions,
                new PdfRedactionPreflightOptions { MaximumRegionCount = 4 });
        });

    [Fact]
    public async Task Preflight_ShouldIncludeTheDocumentHashForCorrelation()
    {
        var pdf = CreatePdf();

        var report = await PreflightAsync(pdf, new PdfRedactionRegion(1, WholePage));

        Assert.Equal(64, report.Sha256.Length);
        Assert.Equal(pdf.Length, report.FileSizeBytes);
        Assert.Equal(1, report.PageCount);
    }

    private static PdfRectangle WholePage => new(0, 0, 1000, 1000);

    private static byte[] CreatePdf(string text = "PXA redaction preflight fixture")
    {
        var document = new PdfDocument();
        document.AddPage().DrawText(text, 40, 40);
        return document.ToBytes();
    }

    private static async Task<PdfRedactionPreflightReport> PreflightAsync(byte[] pdf, params PdfRedactionRegion[] regions)
    {
        await using var stream = new MemoryStream(pdf);
        return await new PdfRedactionPreflight().PreflightAsync(stream, regions);
    }

    private static async Task<PdfRectangle> FindFirstTextBoundsAsync(byte[] pdf)
    {
        await using var stream = new MemoryStream(pdf);
        var document = await new PdfImporter().LoadAsync(stream);
        var text = document.Pages[0].GraphicsObjects.OfType<PdfTextElement>().First();
        return text.Bounds ?? new Analysis.BoundingBoxCalculator().ComputeElementBounds(text);
    }

    private static int NextObjectNumber(byte[] source)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        return new PdfCrossReferenceParser().Parse(context).Entries.Keys.Max(static id => id.Number) + 1;
    }

    private static byte[] AddPageAnnotations(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var pageId = graph.Objects.Values
            .First(static value => value.Value is PdfDictionary dictionary &&
                dictionary["Type"] is PdfName { Value: "Page" }).Id;
        var page = (PdfDictionary)graph.Resolve(pageId)!.Value;
        var next = table.Entries.Keys.Max(static id => id.Number) + 1;
        var widgetId = new PdfObjectId(next, 0);
        var noteId = new PdfObjectId(next + 1, 0);
        var updatedPage = new PdfDictionary(page.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["Annots"] = new PdfArray([new PdfReference(widgetId), new PdfReference(noteId)])
        };

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(widgetId, new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Widget"),
                ["FT"] = new PdfName("Tx"),
                ["T"] = new PdfString("Amount"u8.ToArray(), IsHex: false),
                ["V"] = new PdfString("secret"u8.ToArray(), IsHex: false),
                ["Rect"] = Rectangle(20, 20, 200, 60)
            }),
            new PdfIncrementalObjectUpdate(noteId, new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Text"),
                ["Contents"] = new PdfString("note"u8.ToArray(), IsHex: false),
                ["Rect"] = Rectangle(60, 60, 90, 90)
            }),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static byte[] AddCatalogEntries(byte[] source)
    {
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updated = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["OpenAction"] = new PdfDictionary { ["S"] = new PdfName("JavaScript") },
            ["OCProperties"] = new PdfDictionary { ["OCGs"] = new PdfArray() }
        };

        return new PdfIncrementalUpdateWriter()
            .Append(source, [new PdfIncrementalObjectUpdate(rootId, updated)]).Bytes;
    }

    private static PdfArray Rectangle(double left, double bottom, double right, double top) =>
        new([new PdfNumber(left), new PdfNumber(bottom), new PdfNumber(right), new PdfNumber(top)]);

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }
}
