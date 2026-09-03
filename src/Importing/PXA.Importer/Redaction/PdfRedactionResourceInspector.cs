using PXA.Importer.Analysis;
using PXA.Importer.Document;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;

namespace PXA.Importer.Redaction;

/// <summary>
/// Inspects resources that affect what a page paints. Pattern findings are scoped to actual
/// paint operations intersecting the requested region; merely declaring an unused resource
/// is not a redaction blocker.
/// </summary>
internal static class PdfRedactionResourceInspector
{
    public static void Inspect(
        PdfObjectGraph graph,
        PdfPageModel page,
        PdfRectangle region,
        int pageNumber,
        int regionIndex,
        ICollection<PdfRedactionFinding> findings)
    {
        var resources = page.Resources;

        var patternUses = CollectPatternUses(page.GraphicsObjects, region);
        if (patternUses.Count > 0)
        {
            var patterns = Resolve(graph, resources?["Pattern"]) as PdfDictionary;
            foreach (var name in patternUses.Order(StringComparer.Ordinal))
            {
                var reason = patterns is null || !patterns.Values.TryGetValue(name, out var pattern)
                    ? "The selected pattern is not present in the page resources."
                    : IsSupportedPattern(graph, pattern, out var validationReason)
                        ? null
                        : validationReason;
                if (reason is not null)
                {
                    findings.Add(new PdfRedactionFinding(
                        "PXAPDFREDACT210",
                        PdfRedactionCapability.ReviewRequired,
                        $"Pattern '{name}' paints inside the region but cannot be removed safely. {reason}",
                        pageNumber,
                        regionIndex));
                }
            }
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

    private static HashSet<string> CollectPatternUses(
        IEnumerable<PdfGraphicsElement> elements,
        PdfRectangle region)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var bounds = new BoundingBoxCalculator();
        Visit(elements, [], depth: 0);
        return names;

        void Visit(
            IEnumerable<PdfGraphicsElement> candidates,
            IReadOnlyList<PdfGroupElement> ancestors,
            int depth)
        {
            if (depth > 64)
            {
                return;
            }

            foreach (var element in candidates)
            {
                if (element is PdfGroupElement group)
                {
                    Visit(group.Children, ancestors.Append(group).ToArray(), depth + 1);
                    continue;
                }

                var effectiveBounds = PdfRedactionVisibility.EffectiveBounds(element, bounds, ancestors, out _);
                if (!effectiveBounds.Intersects(region))
                {
                    continue;
                }

                if (UsesFill(element) && !string.IsNullOrEmpty(element.FillPatternResourceName))
                {
                    names.Add(element.FillPatternResourceName);
                }

                if (UsesStroke(element) && !string.IsNullOrEmpty(element.StrokePatternResourceName))
                {
                    names.Add(element.StrokePatternResourceName);
                }
            }
        }
    }

    private static bool UsesFill(PdfGraphicsElement element) => element switch
    {
        PdfTextElement => true,
        PdfPathElement path => path.SourceCommand.Operator.Name is "f" or "F" or "f*" or "B" or "B*" or "b" or "b*",
        _ => false
    };

    private static bool UsesStroke(PdfGraphicsElement element) => element switch
    {
        PdfTextElement => true,
        PdfPathElement path => path.SourceCommand.Operator.Name is "S" or "s" or "B" or "B*" or "b" or "b*",
        _ => false
    };

    private static bool IsSupportedPattern(PdfObjectGraph graph, PdfObject value, out string reason)
    {
        var resolved = Resolve(graph, value);
        var dictionary = resolved switch
        {
            PdfStreamObject stream => stream.Dictionary,
            PdfDictionary direct => direct,
            _ => null
        };
        if (dictionary is null)
        {
            reason = "The pattern resource is missing or is not a dictionary or stream.";
            return false;
        }

        switch (ReadInteger(graph, dictionary["PatternType"]))
        {
            case 1 when resolved is PdfStreamObject &&
                             ReadInteger(graph, dictionary["PaintType"]) is 1 or 2 &&
                             ReadRectangle(graph, dictionary["BBox"]) is not null &&
                             ReadNumber(graph, dictionary["XStep"]) is { } xStep && Math.Abs(xStep) > double.Epsilon &&
                             ReadNumber(graph, dictionary["YStep"]) is { } yStep && Math.Abs(yStep) > double.Epsilon:
                reason = string.Empty;
                return true;
            case 2 when dictionary["Shading"] is not null:
                reason = string.Empty;
                return true;
            case 1:
                reason = "The tiling pattern is malformed or lacks its required stream geometry.";
                return false;
            case 2:
                reason = "The shading pattern has no shading definition.";
                return false;
            default:
                reason = "The pattern type is missing or unsupported.";
                return false;
        }
    }

    private static PdfRectangle? ReadRectangle(PdfObjectGraph graph, PdfObject? value)
    {
        if (Resolve(graph, value) is not PdfArray { Items.Count: >= 4 } array)
        {
            return null;
        }

        var values = array.Items.Take(4).Select(item => ReadNumber(graph, item)).ToArray();
        return values.Any(static number => number is null)
            ? null
            : new PdfRectangle(
                values[0]!.Value,
                values[1]!.Value,
                values[2]!.Value - values[0]!.Value,
                values[3]!.Value - values[1]!.Value);
    }

    private static int? ReadInteger(PdfObjectGraph graph, PdfObject? value) =>
        ReadNumber(graph, value) is { } number ? (int)number : null;

    private static double? ReadNumber(PdfObjectGraph graph, PdfObject? value) => Resolve(graph, value) switch
    {
        PdfInteger integer => integer.Value,
        PdfNumber number => number.Value,
        _ => null
    };

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
