using PXA.Importer.Document;
using PXA.Importer.Objects;

namespace PXA.Importer.Redaction;

/// <summary>
/// Inspects the page resources that decide what is actually painted but that the interpreted
/// scene graph does not carry: tiling and shading patterns and transparency groups.
///
/// None of these can be resolved to a per-pixel answer here, so each is reported rather than
/// silently treated as absent.
/// </summary>
internal static class PdfRedactionResourceInspector
{
    public static void Inspect(
        PdfObjectGraph graph,
        PdfPageModel page,
        int pageNumber,
        int regionIndex,
        ICollection<PdfRedactionFinding> findings)
    {
        var resources = Resolve(graph, page.PageDictionary["Resources"]) as PdfDictionary;

        if (Resolve(graph, resources?["Pattern"]) is PdfDictionary { Values.Count: > 0 } patterns)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT210",
                PdfRedactionCapability.ReviewRequired,
                $"The page paints with {patterns.Values.Count} pattern resource(s); pattern content is drawn from a separate stream that PXA does not intersect with the region.",
                pageNumber,
                regionIndex));
        }

        var transparencyGroups = CountTransparencyGroups(graph, page, resources);
        if (transparencyGroups > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT211",
                PdfRedactionCapability.ReviewRequired,
                $"{transparencyGroups} transparency group(s) apply to this page; group compositing can make covered content visible through content that is not itself covered.",
                pageNumber,
                regionIndex));
        }

        var softMaskStates = CountSoftMaskStates(graph, resources);
        if (softMaskStates > 0)
        {
            findings.Add(new PdfRedactionFinding(
                "PXAPDFREDACT212",
                PdfRedactionCapability.ReviewRequired,
                $"{softMaskStates} graphics state(s) on this page set a soft mask, which changes what the page actually paints.",
                pageNumber,
                regionIndex));
        }
    }

    private static int CountTransparencyGroups(
        PdfObjectGraph graph,
        PdfPageModel page,
        PdfDictionary? resources)
    {
        var count = IsTransparencyGroup(graph, page.PageDictionary["Group"]) ? 1 : 0;
        if (Resolve(graph, resources?["XObject"]) is not PdfDictionary xObjects)
        {
            return count;
        }

        foreach (var entry in xObjects.Values)
        {
            if (Resolve(graph, entry.Value) is PdfStreamObject stream &&
                ReadName(graph, stream.Dictionary["Subtype"]) == "Form" &&
                IsTransparencyGroup(graph, stream.Dictionary["Group"]))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountSoftMaskStates(PdfObjectGraph graph, PdfDictionary? resources)
    {
        if (Resolve(graph, resources?["ExtGState"]) is not PdfDictionary states)
        {
            return 0;
        }

        var count = 0;
        foreach (var entry in states.Values)
        {
            if (Resolve(graph, entry.Value) is PdfDictionary state &&
                state["SMask"] is not null &&
                ReadName(graph, state["SMask"]) != "None")
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsTransparencyGroup(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfDictionary group &&
        ReadName(graph, group["S"]) == "Transparency";

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;

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
