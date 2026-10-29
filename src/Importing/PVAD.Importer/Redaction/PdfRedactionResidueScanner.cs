using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;

namespace PVAD.Importer.Redaction;

/// <summary>
/// Checks a redacted result for the ways covered content can survive other than as page
/// text: image pixels, thumbnails, layers, and hidden appearance streams.
///
/// Each check is separate and named, so a failure says which recovery route is still open
/// rather than only that verification failed.
/// </summary>
internal static class PdfRedactionResidueScanner
{
    public static void Scan(
        PdfObjectGraph graph,
        PdfDocumentModel document,
        IReadOnlyDictionary<int, PdfRectangle[]> regionsByPage,
        ICollection<PdfRedactionFinding> findings)
    {
        var coveredImages = CountCoveredImages(graph, document, regionsByPage);
        if (coveredImages > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT304",
                PdfRedactionCapability.Unsupported,
                $"{coveredImages} image(s) still intersect a redaction region, so covered pixels remain extractable."));
        }

        var thumbnails = CountEntries(graph, "Thumb");
        if (thumbnails > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT305",
                PdfRedactionCapability.Unsupported,
                $"{thumbnails} page thumbnail(s) remain; a thumbnail can render content the page no longer contains."));
        }

        var layers = CountEntries(graph, "OCProperties") + CountEntries(graph, "OC");
        if (layers > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT306",
                PdfRedactionCapability.Unsupported,
                $"{layers} optional content entr(y/ies) remain; a viewer could reveal content the rendered page does not show."));
        }

        var hiddenAppearances = CountHiddenAppearances(graph);
        if (hiddenAppearances > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT307",
                PdfRedactionCapability.Unsupported,
                $"{hiddenAppearances} non-normal appearance stream(s) remain and can carry covered content."));
        }

    }

    private static int CountCoveredImages(
        PdfObjectGraph graph,
        PdfDocumentModel document,
        IReadOnlyDictionary<int, PdfRectangle[]> regionsByPage)
    {
        var count = 0;
        foreach (var (pageNumber, regions) in regionsByPage)
        {
            var page = document.Pages.ElementAtOrDefault(pageNumber - 1);
            if (page is null)
            {
                continue;
            }

            foreach (var element in page.GraphicsObjects)
            {
                count += CountCoveredImages(graph, page.Resources, element, regions, depth: 0, []);
            }
        }

        return count;
    }

    private static int CountCoveredImages(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfGraphicsElement element,
        IReadOnlyList<PdfRectangle> regions,
        int depth,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (depth > 64 || element.IsDeleted)
        {
            return 0;
        }

        if (element is PdfGroupElement group)
        {
            var nested = ancestors.Append(group).ToArray();
            return group.Children.Sum(child => CountCoveredImages(graph, resources, child, regions, depth + 1, nested));
        }

        if (element is not PdfImageElement image)
        {
            return 0;
        }

        if (PdfImageRedactionEngine.IsSecurelyRedacted(graph, resources, image, regions, out _))
        {
            return 0;
        }

        var boundsCalculator = new Analysis.BoundingBoxCalculator();
        var bounds = PdfRedactionVisibility.EffectiveBounds(element, boundsCalculator, ancestors, out _);
        return regions.Any(region => PdfRedactionGeometry.Classify(bounds, region) != PdfRedactionCoverage.None)
            ? 1
            : 0;
    }

    private static int CountEntries(PdfObjectGraph graph, string key) =>
        graph.Objects.Values.Sum(item => CountEntries(item.Value, key, depth: 0));

    private static int CountEntries(PdfObject value, string key, int depth)
    {
        if (depth > 64)
        {
            return 0;
        }

        return value switch
        {
            PdfDictionary dictionary =>
                (dictionary[key] is null ? 0 : 1) +
                dictionary.Values.Values
                    .Where(static child => child is not PdfReference)
                    .Sum(child => CountEntries(child, key, depth + 1)),
            PdfArray array => array.Items
                .Where(static child => child is not PdfReference)
                .Sum(child => CountEntries(child, key, depth + 1)),
            PdfStreamObject stream => CountEntries(stream.Dictionary, key, depth + 1),
            _ => 0
        };
    }

    private static int CountHiddenAppearances(PdfObjectGraph graph)
    {
        var count = 0;
        foreach (var indirectObject in graph.Objects.Values)
        {
            count += CountHiddenAppearances(graph, indirectObject.Value, depth: 0);
        }

        return count;
    }

    private static int CountHiddenAppearances(PdfObjectGraph graph, PdfObject value, int depth)
    {
        if (depth > 64)
        {
            return 0;
        }

        switch (value)
        {
            case PdfDictionary dictionary:
            {
                var count = Resolve(graph, dictionary["AP"]) is PdfDictionary appearances &&
                    (appearances["D"] is not null || appearances["R"] is not null)
                        ? 1
                        : 0;
                return count + dictionary.Values.Values
                    .Where(static child => child is not PdfReference)
                    .Sum(child => CountHiddenAppearances(graph, child, depth + 1));
            }
            case PdfArray array:
                return array.Items
                    .Where(static child => child is not PdfReference)
                    .Sum(child => CountHiddenAppearances(graph, child, depth + 1));
            case PdfStreamObject stream:
                return CountHiddenAppearances(graph, stream.Dictionary, depth + 1);
            default:
                return 0;
        }
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
}
