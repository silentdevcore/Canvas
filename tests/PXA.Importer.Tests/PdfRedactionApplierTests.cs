using System.Text;
using PXA.Importer.Content;
using PXA.Importer.Document;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Redaction;
using PXA.Importer.Saving;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfRedactionApplierTests
{
    private const string Secret = "CONFIDENTIAL-SALARY-84213";

    [Fact]
    public async Task Apply_ShouldRemoveCoveredTextSoItCannotBeExtracted()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.True(result.Verification.Passed);
        Assert.Equal(0, result.Verification.ResidualExtractableMatches);
        Assert.Equal(0, result.Verification.ResidualCoveredElementCount);
        Assert.Contains(Secret, ExtractText(pdf), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, ExtractText(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldNotLeaveTheSecretAnywhereInTheOutputBytes()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        // The generator writes text as plain operators for this fixture, so a raw byte scan is
        // a meaningful independent check on top of re-extraction.
        Assert.DoesNotContain(Secret, Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldKeepContentThatIsNotCovered()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.Contains("Public heading", ExtractText(result.Bytes), StringComparison.Ordinal);
        Assert.Equal(1, result.RemovedElementCount);
        Assert.Equal([1], result.RewrittenPages);
    }

    [Fact]
    public async Task Apply_ShouldProduceASingleRevisionSoNothingSurvivesInHistory()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        var context = new PdfParseContext(result.Bytes, new PdfImporterOptions());
        Assert.Single(new PdfCrossReferenceParser().Parse(context).Revisions);
    }

    [Fact]
    public async Task Apply_ShouldRemoveCoveredGlyphsAndRetainUncoveredGlyphs()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var full = await RegionCoveringAsync(pdf, Secret);
        var half = new PdfRedactionRegion(1, new PdfRectangle(
            full.Bounds.Left - 5, full.Bounds.Bottom - 2, full.Bounds.Width / 2, full.Bounds.Height + 4));

        var result = await ApplyAsync(pdf, half);
        var extracted = ExtractText(result.Bytes);
        var compact = string.Concat(extracted.Where(static character => !char.IsWhiteSpace(character)));

        Assert.True(result.Verification.Passed, string.Join("; ", result.Verification.Findings.Select(finding =>
            $"{finding.Code}: {finding.Message}")));
        Assert.DoesNotContain(Secret, extracted, StringComparison.Ordinal);
        Assert.Contains("Public heading", extracted, StringComparison.Ordinal);
        Assert.Contains(Secret[^3..], compact, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldRemoveOptionalContentRatherThanBlocking()
    {
        var pdf = AddCatalogEntry(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            "OCProperties",
            new PdfDictionary { ["OCGs"] = new PdfArray() });
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        // Layers decide visibility at view time, so covered content behind them must not
        // survive a redaction that assumed the page as rendered.
        Assert.True(result.Verification.Passed);
        Assert.Contains("hiddenRepresentations", result.Audit.AppliedSanitizations);
        var (_, graph) = ParseGraph(result.Bytes);
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        Assert.Null(catalog["OCProperties"]);
    }

    [Fact]
    public async Task Apply_ShouldBlockOnOptionalContentWhenHiddenRepresentationsAreKept()
    {
        var pdf = AddCatalogEntry(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            "OCProperties",
            new PdfDictionary { ["OCGs"] = new PdfArray() });
        var region = await RegionCoveringAsync(pdf, Secret);

        var exception = await Assert.ThrowsAsync<PdfRedactionBlockedException>(() => ApplyAsync(
            pdf,
            region,
            new PdfRedactionApplyOptions
            {
                Sanitization = new PdfRedactionSanitizationPolicy { RemoveHiddenRepresentations = false }
            }));

        Assert.Contains("PXAPDFREDACT108", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldProceedWhenSanitizationResolvesTheFinding()
    {
        var pdf = AddCatalogEntry(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            "OpenAction",
            new PdfDictionary { ["S"] = new PdfName("JavaScript") });
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.True(result.Verification.Passed);
        Assert.Contains("actionsAndJavaScript", result.Audit.AppliedSanitizations);
        var (_, graph) = ParseGraph(result.Bytes);
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        Assert.Null(catalog["OpenAction"]);
    }

    [Fact]
    public async Task Apply_ShouldBlockWhenSanitizationIsDisabledForThatFinding()
    {
        var pdf = AddCatalogEntry(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            "OpenAction",
            new PdfDictionary { ["S"] = new PdfName("JavaScript") });
        var region = await RegionCoveringAsync(pdf, Secret);

        await Assert.ThrowsAsync<PdfRedactionBlockedException>(() => ApplyAsync(
            pdf,
            region,
            new PdfRedactionApplyOptions
            {
                Sanitization = new PdfRedactionSanitizationPolicy { RemoveActionsAndJavaScript = false }
            }));
    }

    [Fact]
    public async Task Apply_ShouldRemoveDocumentMetadata()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.Contains("documentMetadata", result.Audit.AppliedSanitizations);
        var (_, graph) = ParseGraph(result.Bytes);
        Assert.Null(graph.Trailer?["Info"]);
    }

    [Fact]
    public async Task Apply_ShouldRemoveUnlinkedXmpPayloadFromTheOutputBytes()
    {
        const string metadataSecret = "XMP-PRIVATE-VALUE-7391";
        var pdf = AddXmpMetadata(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            metadataSecret);
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(metadataSecret, Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
        var (_, graph) = ParseGraph(result.Bytes);
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        Assert.Null(catalog["Metadata"]);
        Assert.DoesNotContain(
            graph.Objects.Values,
            value => value.Value is PdfStreamObject stream &&
                stream.Dictionary["Type"] is PdfName { Value: "Metadata" });
    }

    [Fact]
    public async Task Apply_ShouldRemoveEmbeddedFilesAndTheirPayloads()
    {
        const string attachmentSecret = "ATTACHMENT-PRIVATE-VALUE-5827";
        var pdf = AddEmbeddedFile(
            CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)),
            attachmentSecret);
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.True(result.Verification.Passed);
        Assert.Contains("embeddedFiles", result.Audit.AppliedSanitizations);
        Assert.DoesNotContain(attachmentSecret, Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
        var (_, graph) = ParseGraph(result.Bytes);
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        var names = catalog["Names"] switch
        {
            PdfReference reference => graph.Resolve(reference.Id)?.Value as PdfDictionary,
            PdfDictionary dictionary => dictionary,
            _ => null
        };
        Assert.True(names is null || names["EmbeddedFiles"] is null);
        Assert.DoesNotContain(
            graph.Objects.Values,
            value => value.Value is PdfDictionary dictionary &&
                dictionary["Type"] is PdfName { Value: "Filespec" });
    }

    [Fact]
    public async Task Apply_ShouldRecordAuditEvidenceWithoutTheRemovedContent()
    {
        var pdf = CreatePdf(("Public heading", 40, 700), (Secret, 40, 400));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region, new PdfRedactionApplyOptions
        {
            Actor = "auditor@example.com",
            Reason = "Personal data"
        });

        Assert.Equal(64, result.Audit.SourceSha256.Length);
        Assert.Equal(64, result.Audit.ResultSha256.Length);
        Assert.Equal(64, result.Audit.RecordSha256.Length);
        Assert.NotEqual(result.Audit.SourceSha256, result.Audit.ResultSha256);
        Assert.True(result.Audit.VerificationPassed);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Audit);
        Assert.DoesNotContain(Secret, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldRemoveOverlappingAnnotationObjects()
    {
        var pdf = AddOverlappingAnnotation(CreatePdf(("Public heading", 40, 700), (Secret, 40, 400)));
        var region = await RegionCoveringAsync(pdf, Secret);

        var result = await ApplyAsync(pdf, region);

        Assert.Equal(1, result.RemovedAnnotationCount);
        Assert.DoesNotContain("annotation-secret", Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
    }

    private static async Task<PdfRedactionApplyResult> ApplyAsync(
        byte[] pdf,
        PdfRedactionRegion region,
        PdfRedactionApplyOptions? options = null)
    {
        await using var stream = new MemoryStream(pdf);
        return await new PdfRedactionApplier(new PdfRedactionPreflight())
            .ApplyAsync(stream, [region], options);
    }

    private static byte[] CreatePdf(params (string Text, double X, double Y)[] runs)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        foreach (var (text, x, y) in runs)
        {
            page.DrawText(text, x, y);
        }

        return document.ToBytes();
    }

    private static async Task<PdfRedactionRegion> RegionCoveringAsync(byte[] pdf, string text)
    {
        await using var stream = new MemoryStream(pdf);
        var document = await new PdfImporter().LoadAsync(stream);
        var element = document.Pages[0].GraphicsObjects
            .OfType<PdfTextElement>()
            .First(item => item.Text.Contains(text, StringComparison.Ordinal));
        var bounds = element.Bounds ?? new Analysis.BoundingBoxCalculator().ComputeElementBounds(element);
        return new PdfRedactionRegion(1, new PdfRectangle(
            bounds.Left - 4, bounds.Bottom - 4, bounds.Width + 8, bounds.Height + 8));
    }

    private static string ExtractText(byte[] pdf)
    {
        var (_, graph) = ParseGraph(pdf);
        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);
        return string.Join('\n', document.Pages.SelectMany(static page => page.TextObjects).Select(static item => item.Text));
    }

    private static byte[] AddCatalogEntry(byte[] source, string key, PdfObject value)
    {
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updated = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value)) { [key] = value };
        return new PdfIncrementalUpdateWriter()
            .Append(source, [new PdfIncrementalObjectUpdate(rootId, updated)]).Bytes;
    }

    private static byte[] AddXmpMetadata(byte[] source, string value)
    {
        var (table, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var metadataId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updated = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["Metadata"] = new PdfReference(metadataId)
        };

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(metadataId, new PdfStreamObject(
                new PdfDictionary
                {
                    ["Type"] = new PdfName("Metadata"),
                    ["Subtype"] = new PdfName("XML")
                },
                Encoding.UTF8.GetBytes($"<x:xmpmeta>{value}</x:xmpmeta>"))),
            new PdfIncrementalObjectUpdate(rootId, updated)
        ]).Bytes;
    }

    private static byte[] AddEmbeddedFile(byte[] source, string value)
    {
        var (table, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var nextObjectNumber = table.Entries.Keys.Max(static id => id.Number) + 1;
        var streamId = new PdfObjectId(nextObjectNumber, 0);
        var fileSpecificationId = new PdfObjectId(nextObjectNumber + 1, 0);
        var updated = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["Names"] = new PdfDictionary
            {
                ["EmbeddedFiles"] = new PdfDictionary
                {
                    ["Names"] = new PdfArray([
                        new PdfString(Encoding.UTF8.GetBytes("private.txt"), IsHex: false),
                        new PdfReference(fileSpecificationId)
                    ])
                }
            }
        };

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(streamId, new PdfStreamObject(
                new PdfDictionary { ["Type"] = new PdfName("EmbeddedFile") },
                Encoding.UTF8.GetBytes(value))),
            new PdfIncrementalObjectUpdate(fileSpecificationId, new PdfDictionary
            {
                ["Type"] = new PdfName("Filespec"),
                ["F"] = new PdfString(Encoding.UTF8.GetBytes("private.txt"), IsHex: false),
                ["EF"] = new PdfDictionary { ["F"] = new PdfReference(streamId) }
            }),
            new PdfIncrementalObjectUpdate(rootId, updated)
        ]).Bytes;
    }

    private static byte[] AddOverlappingAnnotation(byte[] source)
    {
        var (table, graph) = ParseGraph(source);
        var pageId = graph.Objects.Values
            .First(static value => value.Value is PdfDictionary dictionary &&
                dictionary["Type"] is PdfName { Value: "Page" }).Id;
        var page = (PdfDictionary)graph.Resolve(pageId)!.Value;
        var annotationId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updatedPage = new PdfDictionary(page.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["Annots"] = new PdfArray([new PdfReference(annotationId)])
        };

        return new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(annotationId, new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Text"),
                ["Contents"] = new PdfString(Encoding.ASCII.GetBytes("annotation-secret"), IsHex: false),
                ["Rect"] = new PdfArray([
                    new PdfNumber(30), new PdfNumber(380), new PdfNumber(400), new PdfNumber(430)
                ])
            }),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]).Bytes;
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }
}
