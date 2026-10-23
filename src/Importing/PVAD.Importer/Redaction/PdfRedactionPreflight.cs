using System.Security.Cryptography;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Redaction;

/// <summary>
/// Analyses what an applied redaction would cover, without changing the document.
///
/// The analysis intersects regions against the interpreted scene graph, which is also what
/// the applied redaction operates on. That shared basis is the point: anything the importer
/// did not model is invisible to redaction, so this class reports those cases explicitly
/// instead of letting them pass as removable.
/// </summary>
public sealed class PdfRedactionPreflight : IPdfRedactionPreflight
{
    private readonly BoundingBoxCalculator _bounds = new();

    public async Task<PdfRedactionPreflightReport> PreflightAsync(
        Stream pdfStream,
        IReadOnlyList<PdfRedactionRegion> regions,
        PdfRedactionPreflightOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        ArgumentNullException.ThrowIfNull(regions);
        options ??= new PdfRedactionPreflightOptions();
        if (options.MaximumFileSizeBytes <= 0 || options.MaximumFileSizeBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The maximum preflight size must be between 1 byte and 2 GiB.");
        }

        if (regions.Count == 0)
        {
            throw new ArgumentException("At least one redaction region is required.", nameof(regions));
        }

        if (regions.Count > options.MaximumRegionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(regions),
                $"A preflight accepts at most {options.MaximumRegionCount} regions.");
        }

        var source = await ReadBoundedAsync(pdfStream, checked((int)options.MaximumFileSizeBytes), cancellationToken)
            .ConfigureAwait(false);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(source));
        var documentFindings = new List<PdfRedactionFinding>();

        var context = new PdfParseContext(source, new PdfImporterOptions(), cancellationToken);
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();

        if (table.Trailer?["Encrypt"] is not null)
        {
            documentFindings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT101",
                PdfRedactionCapability.Unsupported,
                "The document is encrypted; redaction requires encrypted serialization that is not enabled yet."));
            return new PdfRedactionPreflightReport(
                PdfRedactionCapability.Unsupported,
                sha256,
                source.Length,
                0,
                table.Revisions.Count,
                [],
                documentFindings);
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            documentFindings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT106",
                PdfRedactionCapability.Unsupported,
                $"The document could not be parsed in full ({diagnostic.Code}), so covered content cannot be assessed."));
        }

        var document = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(graph);
        InspectDocumentCarriers(graph, table, document, documentFindings);

        var regionReports = new List<PdfRedactionRegionReport>(regions.Count);
        for (var index = 0; index < regions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            regionReports.Add(AnalyzeRegion(index, regions[index], document, graph));
        }

        var capability = Worst(
            documentFindings.Select(static finding => finding.Capability)
                .Concat(regionReports.Select(static report => report.Capability)));
        return new PdfRedactionPreflightReport(
            capability,
            sha256,
            source.Length,
            document.Pages.Count,
            table.Revisions.Count,
            regionReports,
            documentFindings);
    }

    private PdfRedactionRegionReport AnalyzeRegion(
        int index,
        PdfRedactionRegion region,
        PdfDocumentModel document,
        PdfObjectGraph graph)
    {
        var findings = new List<PdfRedactionFinding>();
        var page = document.Pages.ElementAtOrDefault(region.PageNumber - 1);
        if (page is null)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT110",
                PdfRedactionCapability.Unsupported,
                $"The document does not contain page {region.PageNumber}.",
                region.PageNumber,
                index));
            return new PdfRedactionRegionReport(
                index, region.PageNumber, region.Bounds, PdfRedactionCapability.Unsupported, [], findings);
        }

        if (region.Bounds.Width <= 0 || region.Bounds.Height <= 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT111",
                PdfRedactionCapability.Unsupported,
                "The redaction region has no area.",
                region.PageNumber,
                index));
            return new PdfRedactionRegionReport(
                index, region.PageNumber, region.Bounds, PdfRedactionCapability.Unsupported, [], findings);
        }

        var pageBox = page.CropBox ?? page.MediaBox;
        if (pageBox is { } box && !box.Intersects(region.Bounds))
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT112",
                PdfRedactionCapability.ReviewRequired,
                "The redaction region lies outside the page geometry.",
                region.PageNumber,
                index));
        }

        // A page that draws something the importer did not model cannot be assessed: the
        // applied redaction would test intersection against nothing.
        if (page.GraphicsObjects.Count == 0 && page.ContentStreams.Count > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT105",
                PdfRedactionCapability.Unsupported,
                "The page has content streams but produced no interpreted content, so covered content cannot be assessed.",
                region.PageNumber,
                index));
        }

        var covered = new List<PdfRedactionCoveredContent>();
        var accumulator = new CoverageAccumulator();
        foreach (var element in page.GraphicsObjects)
        {
            Accumulate(graph, page.Resources, element, region.Bounds, accumulator, findings, region.PageNumber, index, depth: 0, []);
        }

        accumulator.Fill(covered);
        InspectPageAnnotations(graph, page, region, index, findings);
        PdfRedactionResourceInspector.Inspect(graph, page, region.Bounds, region.PageNumber, index, findings);

        var capability = Worst(
            findings.Select(static finding => finding.Capability)
                .Concat(covered.Select(static item => item.Capability)));
        return new PdfRedactionRegionReport(index, region.PageNumber, region.Bounds, capability, covered, findings);
    }

    private void Accumulate(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfGraphicsElement element,
        PdfRectangle region,
        CoverageAccumulator accumulator,
        ICollection<PdfRedactionFinding> findings,
        int pageNumber,
        int regionIndex,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT113",
                PdfRedactionCapability.Unsupported,
                "The page nests content too deeply to assess.",
                pageNumber,
                regionIndex));
            return;
        }

        if (element is PdfGroupElement group)
        {
            if (group.MarkedContentTag is "OC")
            {
                var bounds = group.Bounds ?? _bounds.ComputeElementBounds(group);
                if (bounds.Intersects(region))
                {
                    findings.Add(new PdfRedactionFinding(
                        "PXAPDFREDACT203",
                        PdfRedactionCapability.ReviewRequired,
                        "Optional content overlaps the region; visibility depends on viewer state.",
                        pageNumber,
                        regionIndex));
                }
            }

            var nested = ancestors.Append(group).ToArray();
            foreach (var child in group.Children)
            {
                Accumulate(graph, resources, child, region, accumulator, findings, pageNumber, regionIndex, depth + 1, nested);
            }

            return;
        }

        var elementBounds = PdfRedactionVisibility.EffectiveBounds(element, _bounds, ancestors, out var isClipped);
        var coverage = PdfRedactionGeometry.Classify(elementBounds, region);
        if (coverage == PdfRedactionCoverage.None)
        {
            return;
        }

        if (isClipped)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT213",
                PdfRedactionCapability.ReviewRequired,
                "Covered content is clipped; PVAD narrows coverage by the clipping path bounds, which is an approximation of the real clip shape.",
                pageNumber,
                regionIndex));
        }

        switch (element)
        {
            case PdfTextElement text:
                var textCapability = PdfRedactionCapability.Removable;
                if (coverage == PdfRedactionCoverage.Partial &&
                    !PdfTextRedactionEngine.CanRedactPartial(text, [region], out var textReason))
                {
                    textCapability = PdfRedactionCapability.ReviewRequired;
                    findings.Add(new PdfRedactionFinding(
                        "PXAPDFREDACT201",
                        PdfRedactionCapability.ReviewRequired,
                        $"A text run only partially overlaps the region and cannot be split safely. {textReason}",
                        pageNumber,
                        regionIndex));
                }

                accumulator.Add("text", coverage, 1, text.Text.Length, textCapability);

                break;
            case PdfImageElement image:
                var imageCapability = PdfRedactionCapability.Removable;
                if (coverage == PdfRedactionCoverage.Partial &&
                    !PdfImageRedactionEngine.CanRedactPartial(graph, resources, image, out var reason))
                {
                    imageCapability = PdfRedactionCapability.ReviewRequired;
                    var masked = PdfImageRedactionEngine.RequiresMaskReconstruction(graph, resources, image);
                    findings.Add(new PdfRedactionFinding(
                        masked ? "PXAPDFREDACT209" : "PXAPDFREDACT204",
                        PdfRedactionCapability.ReviewRequired,
                        masked
                            ? $"The image only partially overlaps the region and its mask cannot be reconstructed safely. {reason}"
                            : $"The image only partially overlaps the region and cannot be rewritten safely. {reason}",
                        pageNumber,
                        regionIndex));
                }

                accumulator.Add("image", coverage, 1, capability: imageCapability);

                break;
            case PdfShadingElement:
                accumulator.Add("shading", coverage, 1);
                findings.Add(new PdfRedactionFinding(
                    "PXAPDFREDACT205",
                    PdfRedactionCapability.ReviewRequired,
                    "A shading overlaps the region and is carried through a separate preservation path.",
                    pageNumber,
                    regionIndex));
                break;
            case PdfPathElement path:
                var usesPattern =
                    (PathUsesFill(path) && !string.IsNullOrEmpty(path.FillPatternResourceName)) ||
                    (PathUsesStroke(path) && !string.IsNullOrEmpty(path.StrokePatternResourceName));
                accumulator.Add(
                    "path",
                    coverage,
                    1,
                    capability: usesPattern ? PdfRedactionCapability.Removable : null);
                break;
            default:
                accumulator.Add("other", coverage, 1);
                findings.Add(new PdfRedactionFinding(
                    "PXAPDFREDACT206",
                    PdfRedactionCapability.ReviewRequired,
                    "Content of an unrecognised kind overlaps the region.",
                    pageNumber,
                    regionIndex));
                break;
        }
    }

    private static bool PathUsesFill(PdfPathElement path) =>
        path.SourceCommand.Operator.Name is "f" or "F" or "f*" or "B" or "B*" or "b" or "b*";

    private static bool PathUsesStroke(PdfPathElement path) =>
        path.SourceCommand.Operator.Name is "S" or "s" or "B" or "B*" or "b" or "b*";

    private static void InspectPageAnnotations(
        PdfObjectGraph graph,
        PdfPageModel page,
        PdfRedactionRegion region,
        int regionIndex,
        ICollection<PdfRedactionFinding> findings)
    {
        if (Resolve(graph, page.PageDictionary["Annots"]) is not PdfArray annotations)
        {
            return;
        }

        var overlapping = 0;
        var fields = 0;
        foreach (var item in annotations.Items)
        {
            if (Resolve(graph, item) is not PdfDictionary annotation ||
                ReadRectangle(graph, annotation["Rect"]) is not { } rectangle ||
                !rectangle.Intersects(region.Bounds))
            {
                continue;
            }

            overlapping++;
            if (annotation["FT"] is not null || annotation["Subtype"] is PdfName { Value: "Widget" })
            {
                fields++;
            }
        }

        if (fields > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT207",
                PdfRedactionCapability.ReviewRequired,
                $"{fields} form field widget(s) overlap the region; field values are not interpreted content.",
                region.PageNumber,
                regionIndex));
        }

        if (overlapping > fields)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT202",
                PdfRedactionCapability.ReviewRequired,
                $"{overlapping - fields} annotation(s) overlap the region; annotation content is not interpreted content.",
                region.PageNumber,
                regionIndex));
        }
    }

    private static void InspectDocumentCarriers(
        PdfObjectGraph graph,
        PdfCrossReferenceTable table,
        PdfDocumentModel document,
        ICollection<PdfRedactionFinding> findings)
    {
        if (table.Revisions.Count > 1)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT102",
                PdfRedactionCapability.ReviewRequired,
                $"The document has {table.Revisions.Count} revisions; earlier revisions retain covered content unless the apply stage performs a full rewrite."));
        }

        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        if (catalog is not null && HasEmbeddedFiles(graph, catalog))
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT103",
                PdfRedactionCapability.ReviewRequired,
                "The document contains embedded files; attachments may repeat covered content."));
        }

        if (document.Metadata.Values.Count > 0 || catalog?["Metadata"] is not null)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT104",
                PdfRedactionCapability.ReviewRequired,
                "The document carries information-dictionary or XMP metadata that may repeat covered content."));
        }

        if (catalog?["OpenAction"] is not null || catalog?["AA"] is not null ||
            Resolve(graph, catalog?["Names"]) is PdfDictionary names && names["JavaScript"] is not null)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT107",
                PdfRedactionCapability.ReviewRequired,
                "The document declares actions or JavaScript that may carry covered content."));
        }

        if (catalog?["OCProperties"] is not null)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT108",
                PdfRedactionCapability.ReviewRequired,
                "The document declares optional content groups; covered content may be on a layer that is not currently visible."));
        }
    }

    private static bool HasEmbeddedFiles(PdfObjectGraph graph, PdfDictionary catalog)
    {
        if (Resolve(graph, catalog["Names"]) is PdfDictionary names && names["EmbeddedFiles"] is not null)
        {
            return true;
        }

        return graph.Objects.Values.Any(static value =>
            value.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Filespec" });
    }

    private static PdfRedactionCapability Worst(IEnumerable<PdfRedactionCapability> capabilities)
    {
        var worst = PdfRedactionCapability.Removable;
        foreach (var capability in capabilities)
        {
            if (capability > worst)
            {
                worst = capability;
            }
        }

        return worst;
    }

    private static PdfRectangle? ReadRectangle(PdfObjectGraph graph, PdfObject? value)
    {
        if (Resolve(graph, value) is not PdfArray array || array.Items.Count < 4)
        {
            return null;
        }

        var numbers = new double[4];
        for (var index = 0; index < 4; index++)
        {
            if (Resolve(graph, array.Items[index]) is PdfInteger integer)
            {
                numbers[index] = integer.Value;
            }
            else if (Resolve(graph, array.Items[index]) is PdfNumber number)
            {
                numbers[index] = number.Value;
            }
            else
            {
                return null;
            }
        }

        return new PdfRectangle(
            Math.Min(numbers[0], numbers[2]),
            Math.Min(numbers[1], numbers[3]),
            Math.Abs(numbers[2] - numbers[0]),
            Math.Abs(numbers[3] - numbers[1]));
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            if (buffer.Length + read > maximumBytes)
            {
                throw new InvalidDataException($"The PDF exceeds the maximum preflight size of {maximumBytes} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private sealed class CoverageAccumulator
    {
        private readonly Dictionary<(string Kind, PdfRedactionCoverage Coverage), (int Elements, int Characters, PdfRedactionCapability Capability)> _items = [];

        public void Add(
            string kind,
            PdfRedactionCoverage coverage,
            int elements,
            int characters = 0,
            PdfRedactionCapability? capability = null)
        {
            var key = (kind, coverage);
            var current = _items.GetValueOrDefault(key);
            var effective = capability ?? DefaultCapability(kind, coverage);
            _items[key] = (
                current.Elements + elements,
                current.Characters + characters,
                current.Elements == 0 ? effective : (PdfRedactionCapability)Math.Max((int)current.Capability, (int)effective));
        }

        public void Fill(ICollection<PdfRedactionCoveredContent> target)
        {
            foreach (var entry in _items.OrderBy(static item => item.Key.Kind, StringComparer.Ordinal)
                         .ThenBy(static item => item.Key.Coverage))
            {
                target.Add(new PdfRedactionCoveredContent(
                    entry.Key.Kind,
                    entry.Key.Coverage,
                    entry.Value.Capability,
                    entry.Value.Elements,
                    entry.Value.Characters));
            }
        }

        private static PdfRedactionCapability DefaultCapability(string kind, PdfRedactionCoverage coverage) =>
            kind == "image" ||
            (coverage == PdfRedactionCoverage.Contained && kind is "text" or "path")
                ? PdfRedactionCapability.Removable
                : PdfRedactionCapability.ReviewRequired;
    }
}
