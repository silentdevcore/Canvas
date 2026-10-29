using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Redaction;
using PVAD.Importer.Saving;
using PVAD.Importer.Streams;
using PVAD.Importer.Xref;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

public sealed class PdfRedactionSanitizationAndVisibilityTests
{
    private const string Secret = "CONFIDENTIAL-CARRIER-55018";
    private const string PatternSecret = "CONFIDENTIAL-PATTERN-77142";

    // Item 1: masks, patterns, transparency groups, clipping.

    [Fact]
    public async Task Preflight_ShouldIgnoreAnUnusedSoftMaskedImageResource()
    {
        var pdf = WithPageResource("XObject", "Im0", new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("XObject"),
                ["Subtype"] = new PdfName("Image"),
                ["Width"] = new PdfInteger(1),
                ["Height"] = new PdfInteger(1),
                ["SMask"] = new PdfName("Placeholder")
            },
            new byte[] { 0 }));

        var report = await PreflightAsync(pdf);

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT209");
    }

    [Fact]
    public async Task Preflight_ShouldIgnoreUnusedPatternResources()
    {
        var pdf = WithPageResource("Pattern", "P0", new PdfDictionary
        {
            ["PatternType"] = new PdfInteger(1)
        });

        var report = await PreflightAsync(pdf);

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT210");
    }

    [Fact]
    public async Task Preflight_ShouldResolveAValidPatternPaintInsideTheRegion()
    {
        var report = await PreflightAsync(WithPatternPaint(overlapsRegion: true));

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT210");
        var region = Assert.Single(report.Regions);
        Assert.True(
            region.Capability == PdfRedactionCapability.Removable,
            string.Join(" | ", region.Findings.Select(static finding => $"{finding.Code}: {finding.Message}")) +
            " covered=" + string.Join(",", region.Covered.Select(static item => $"{item.Kind}:{item.Capability}")));
    }

    [Fact]
    public async Task Preflight_ShouldResolveAShadingPatternPaintInsideTheRegion()
    {
        var report = await PreflightAsync(WithPatternPaint(overlapsRegion: true, shadingPattern: true));

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT210");
        Assert.Equal(PdfRedactionCapability.Removable, Assert.Single(report.Regions).Capability);
    }

    [Fact]
    public async Task Preflight_ShouldIgnoreAValidPatternPaintOutsideTheRegion()
    {
        var report = await PreflightAsync(WithPatternPaint(overlapsRegion: false));

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT210");
    }

    [Fact]
    public async Task Preflight_ShouldReportAMalformedPatternPaintInsideTheRegion()
    {
        var report = await PreflightAsync(WithPatternPaint(overlapsRegion: true, malformed: true));

        Assert.Contains(Findings(report), f => f.Code == "PXAPDFREDACT210");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_ShouldPruneTheLastPatternUseAndItsStream(bool customColorSpace)
    {
        var result = await ApplyAsync(WithPatternPaint(overlapsRegion: true, customColorSpace: customColorSpace));

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(PatternSecret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_ShouldPreserveAPatternUsedOutsideTheRegion(bool customColorSpace)
    {
        var result = await ApplyAsync(WithPatternPaint(overlapsRegion: false, customColorSpace: customColorSpace));

        Assert.True(result.Verification.Passed);
        Assert.Contains(PatternSecret, Latin1(result.Bytes), StringComparison.Ordinal);
        Assert.Contains("/P0", Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldPruneTheLastShadingPatternUse()
    {
        var result = await ApplyAsync(WithPatternPaint(overlapsRegion: true, shadingPattern: true));

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(PatternSecret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_ShouldReportATransparencyGroupOnThePage()
    {
        var pdf = WithPageEntry("Group", new PdfDictionary
        {
            ["S"] = new PdfName("Transparency")
        });

        var report = await PreflightAsync(pdf);

        Assert.Contains(Findings(report), f => f.Code == "PXAPDFREDACT211");
    }

    [Fact]
    public async Task Preflight_ShouldReportASoftMaskInTheGraphicsState()
    {
        var pdf = WithPageResource("ExtGState", "GS0", new PdfDictionary
        {
            ["SMask"] = new PdfDictionary { ["S"] = new PdfName("Alpha") }
        });

        var report = await PreflightAsync(pdf);

        Assert.Contains(Findings(report), f => f.Code == "PXAPDFREDACT212");
    }

    [Fact]
    public async Task Preflight_ShouldNotReportMaskFindingsForAPlainDocument()
    {
        var report = await PreflightAsync(CreatePdf());

        Assert.DoesNotContain(Findings(report), f =>
            f.Code is "PXAPDFREDACT209" or "PXAPDFREDACT210" or "PXAPDFREDACT211" or "PXAPDFREDACT212");
    }

    [Fact]
    public async Task Preflight_ShouldNotTreatAnExtGStateSMaskOfNoneAsAMask()
    {
        var pdf = WithPageResource("ExtGState", "GS0", new PdfDictionary
        {
            ["SMask"] = new PdfName("None")
        });

        var report = await PreflightAsync(pdf);

        Assert.DoesNotContain(Findings(report), f => f.Code == "PXAPDFREDACT212");
    }

    [Fact]
    public async Task Apply_ShouldNotDeleteContentVisibleOnlyOutsideTheRedactionRegion()
    {
        var pdf = WrapPageContentInClip(CreatePdf(), "0 0 20 20 re W n");

        var result = await ApplyAsync(pdf);

        Assert.Equal(0, result.RemovedElementCount);
        Assert.True(result.Verification.Passed);
        Assert.Contains(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    // Item 3: document-wide actions, form values, alternates.

    [Fact]
    public async Task Apply_ShouldRemoveAnActionCarryingTheCoveredText()
    {
        var pdf = WithAnnotation(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Link"),
            ["Rect"] = Rect(500, 700, 560, 760),
            ["A"] = new PdfDictionary
            {
                ["S"] = new PdfName("URI"),
                ["URI"] = Text($"https://example.test/{Secret}")
            }
        });

        var result = await ApplyAsync(pdf);

        Assert.Contains("documentWideActionsAndValues", result.Audit.AppliedSanitizations);
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldClearFormFieldValuesAwayFromTheRegion()
    {
        var pdf = WithAnnotation(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Widget"),
            ["FT"] = new PdfName("Tx"),
            ["T"] = Text("Elsewhere"),
            ["V"] = Text(Secret),
            ["Rect"] = Rect(500, 700, 560, 760)
        });

        var result = await ApplyAsync(pdf);

        // The widget does not overlap the region, so only the document-wide pass reaches it.
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldRemoveAlternateDescriptions()
    {
        var pdf = WithAnnotation(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Square"),
            ["Rect"] = Rect(500, 700, 560, 760),
            ["Alt"] = Text(Secret),
            ["TU"] = Text(Secret)
        });

        var result = await ApplyAsync(pdf);

        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldSanitizeNestedDirectContainersAndAnnotationComments()
    {
        var pdf = WithAnnotation(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Square"),
            ["Rect"] = Rect(500, 700, 560, 760),
            ["Contents"] = Text(Secret),
            ["Custom"] = new PdfArray([
                new PdfDictionary
                {
                    ["Alt"] = Text(Secret),
                    ["A"] = new PdfDictionary { ["URI"] = Text(Secret) },
                    ["OC"] = new PdfDictionary { ["Name"] = Text(Secret) }
                }
            ])
        });

        var result = await ApplyAsync(pdf);

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldSanitizeStreamDictionaries()
    {
        var source = CreatePdf();
        var (table, _) = ParseGraph(source);
        var streamId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 5, 0);
        var pdf = WithAnnotation(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Square"),
                ["Rect"] = Rect(500, 700, 560, 760),
                ["CustomStream"] = new PdfReference(streamId)
            },
            extra: [new PdfIncrementalObjectUpdate(
                streamId,
                new PdfStreamObject(
                    new PdfDictionary { ["Alt"] = Text(Secret) },
                    ReadOnlyMemory<byte>.Empty))]);

        var result = await ApplyAsync(pdf);

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    // Item 4: hidden representations.

    [Fact]
    public async Task Apply_ShouldRemoveNonNormalAppearanceStreams()
    {
        var (table, _) = ParseGraph(CreatePdf());
        var downId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 5, 0);
        var pdf = WithAnnotation(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Annot"),
                ["Subtype"] = new PdfName("Widget"),
                ["Rect"] = Rect(500, 700, 560, 760),
                ["AP"] = new PdfDictionary
                {
                    ["N"] = new PdfDictionary(),
                    ["D"] = new PdfReference(downId)
                }
            },
            extra: [new PdfIncrementalObjectUpdate(downId, new PdfStreamObject(
                new PdfDictionary { ["Subtype"] = new PdfName("Form") },
                Encoding.ASCII.GetBytes($"BT ({Secret}) Tj ET")))]);

        var result = await ApplyAsync(pdf);

        Assert.Contains("hiddenRepresentations", result.Audit.AppliedSanitizations);
        // The down appearance is no longer referenced, so the full rewrite prunes its payload.
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldKeepWorkingWhenNothingNeedsSanitizing()
    {
        var result = await ApplyAsync(CreatePdf());

        Assert.True(result.Verification.Passed);
        Assert.DoesNotContain(Secret, Latin1(result.Bytes), StringComparison.Ordinal);
    }

    private static IEnumerable<PdfRedactionFinding> Findings(PdfRedactionPreflightReport report) =>
        report.DocumentFindings.Concat(report.Regions.SelectMany(static region => region.Findings));

    private static async Task<PdfRedactionPreflightReport> PreflightAsync(byte[] pdf)
    {
        await using var stream = new MemoryStream(pdf);
        return await new PdfRedactionPreflight().PreflightAsync(stream, [await RegionAsync(pdf)]);
    }

    private static async Task<PdfRedactionApplyResult> ApplyAsync(
        byte[] pdf,
        PdfRedactionApplyOptions? options = null)
    {
        await using var stream = new MemoryStream(pdf);
        return await new PdfRedactionApplier(new PdfRedactionPreflight())
            .ApplyAsync(stream, [await RegionAsync(pdf)], options);
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

    private static byte[] WithPageResource(string category, string name, PdfObject resource)
    {
        var source = CreatePdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var resourceId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updatedPage = Clone(page);
        updatedPage["Resources"] = new PdfDictionary
        {
            [category] = new PdfDictionary { [name] = new PdfReference(resourceId) }
        };
        return Append(source, [
            new PdfIncrementalObjectUpdate(resourceId, resource),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] WithPageEntry(string key, PdfObject value)
    {
        var source = CreatePdf();
        var (_, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var updatedPage = Clone(page);
        updatedPage[key] = value;
        return Append(source, [new PdfIncrementalObjectUpdate(pageId, updatedPage)]);
    }

    private static byte[] WithPatternPaint(
        bool overlapsRegion,
        bool malformed = false,
        bool customColorSpace = false,
        bool shadingPattern = false)
    {
        var source = CreatePdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var contentReference = Assert.IsType<PdfReference>(page["Contents"]);
        var content = Assert.IsType<PdfStreamObject>(graph.Resolve(contentReference.Id)!.Value);
        var decoded = content.IsDecoded ? content.DecodedBytes : new PdfStreamDecoderRegistry().Decode(content);
        var patternId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var patternDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Pattern"),
            ["PatternType"] = new PdfInteger(1),
            ["PaintType"] = new PdfInteger(1),
            ["TilingType"] = new PdfInteger(1),
            ["BBox"] = Rect(0, 0, 10, 10),
            ["XStep"] = new PdfInteger(10),
            ["YStep"] = new PdfInteger(10),
            ["Resources"] = new PdfDictionary()
        };
        if (malformed)
        {
            patternDictionary["XStep"] = null;
        }

        PdfObject pattern = new PdfStreamObject(
            patternDictionary,
            Encoding.ASCII.GetBytes($"BT ({PatternSecret}) Tj ET"));
        if (shadingPattern)
        {
            pattern = new PdfDictionary
            {
                ["Type"] = new PdfName("Pattern"),
                ["PatternType"] = new PdfInteger(2),
                ["Shading"] = new PdfDictionary
                {
                    ["ShadingType"] = new PdfInteger(2),
                    ["ColorSpace"] = new PdfName("DeviceRGB"),
                    ["Coords"] = new PdfArray([
                        new PdfInteger(0), new PdfInteger(0), new PdfInteger(10), new PdfInteger(10)])
                },
                ["PrivateData"] = Text(PatternSecret)
            };
        }

        var resources = Resolve(graph, page["Resources"]) is PdfDictionary currentResources
            ? Clone(currentResources)
            : new PdfDictionary();
        resources["Pattern"] = new PdfDictionary { ["P0"] = new PdfReference(patternId) };
        var colorSelection = "/Pattern cs /P0 scn";
        if (customColorSpace)
        {
            resources["ColorSpace"] = new PdfDictionary
            {
                ["CsPattern"] = new PdfArray([new PdfName("Pattern"), new PdfName("DeviceRGB")])
            };
            colorSelection = "/CsPattern cs 1 0 0 /P0 scn";
        }

        var updatedPage = Clone(page);
        updatedPage["Resources"] = resources;
        var x = overlapsRegion ? 35 : 400;
        var y = overlapsRegion ? 390 : 600;
        var paint = Encoding.ASCII.GetBytes($"\nq {colorSelection} {x} {y} 220 30 re f Q\n");
        var updatedContent = new byte[decoded.Length + paint.Length];
        decoded.Span.CopyTo(updatedContent);
        paint.CopyTo(updatedContent, decoded.Length);

        return Append(source, [
            new PdfIncrementalObjectUpdate(patternId, pattern),
            new PdfIncrementalObjectUpdate(contentReference.Id, new PdfStreamObject(new PdfDictionary(), updatedContent)),
            new PdfIncrementalObjectUpdate(pageId, updatedPage)
        ]);
    }

    private static byte[] WrapPageContentInClip(byte[] source, string clippingOperators)
    {
        var (_, graph) = ParseGraph(source);
        var (_, page) = FindPage(graph);
        var contentReference = Assert.IsType<PdfReference>(page["Contents"]);
        var stream = Assert.IsType<PdfStreamObject>(graph.Resolve(contentReference.Id)!.Value);
        var decoded = stream.IsDecoded
            ? stream.DecodedBytes
            : new PdfStreamDecoderRegistry().Decode(stream);
        var prefix = Encoding.ASCII.GetBytes($"q {clippingOperators}\n");
        var suffix = "\nQ"u8.ToArray();
        var wrapped = new byte[prefix.Length + decoded.Length + suffix.Length];
        prefix.CopyTo(wrapped, 0);
        decoded.Span.CopyTo(wrapped.AsSpan(prefix.Length));
        suffix.CopyTo(wrapped, prefix.Length + decoded.Length);
        return Append(source, [new PdfIncrementalObjectUpdate(
            contentReference.Id,
            new PdfStreamObject(new PdfDictionary(), wrapped))]);
    }

    private static byte[] WithAnnotation(
        PdfDictionary annotation,
        IReadOnlyList<PdfIncrementalObjectUpdate>? extra = null)
    {
        var source = CreatePdf();
        var (table, graph) = ParseGraph(source);
        var (pageId, page) = FindPage(graph);
        var annotationId = new PdfObjectId(table.Entries.Keys.Max(static id => id.Number) + 1, 0);
        var updatedPage = Clone(page);
        updatedPage["Annots"] = new PdfArray([new PdfReference(annotationId)]);
        var updates = new List<PdfIncrementalObjectUpdate>
        {
            new(annotationId, annotation),
            new(pageId, updatedPage)
        };
        if (extra is not null)
        {
            updates.AddRange(extra);
        }

        return Append(source, updates);
    }

    private static byte[] Append(byte[] source, IReadOnlyList<PdfIncrementalObjectUpdate> updates) =>
        new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;

    private static PdfString Text(string value) =>
        new(Encoding.Latin1.GetBytes(value), IsHex: false);

    private static PdfArray Rect(double left, double bottom, double right, double top) =>
        new([new PdfNumber(left), new PdfNumber(bottom), new PdfNumber(right), new PdfNumber(top)]);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static (PdfObjectId Id, PdfDictionary Page) FindPage(PdfObjectGraph graph)
    {
        var entry = graph.Objects.Values.First(static item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Page" });
        return (entry.Id, (PdfDictionary)entry.Value);
    }

    private static PdfDictionary Clone(PdfDictionary source) =>
        new(new Dictionary<string, PdfObject>(source.Values, StringComparer.Ordinal));

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value) => value switch
    {
        PdfReference reference => graph.Resolve(reference.Id)?.Value,
        _ => value
    };

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }
}
