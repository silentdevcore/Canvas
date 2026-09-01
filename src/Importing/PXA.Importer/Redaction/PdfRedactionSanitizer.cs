using PXA.Importer.Objects;
using PXA.Importer.Saving;

namespace PXA.Importer.Redaction;

/// <summary>
/// Removes sensitive carriers outside page content. Traversal includes direct dictionaries,
/// arrays, and stream dictionaries; limiting the pass to indirect top-level dictionaries
/// would leave valid PDF nesting paths untouched.
/// </summary>
internal static class PdfRedactionSanitizer
{
    private static readonly string[] ActionKeys = ["A", "AA", "OpenAction"];
    private static readonly string[] AlternateKeys = ["Alt", "ActualText", "Alternates", "AlternatePresentations", "TU"];
    private static readonly string[] AnnotationCommentKeys = ["Contents", "RC", "Subj"];
    private static readonly string[] FieldValueKeys = ["V", "DV", "RV"];
    private static readonly string[] HiddenRepresentationKeys = ["OC", "OCProperties"];
    private static readonly HashSet<string> AnnotationSubtypes = new(StringComparer.Ordinal)
    {
        "Text", "Link", "FreeText", "Line", "Square", "Circle", "Polygon", "PolyLine",
        "Highlight", "Underline", "Squiggly", "StrikeOut", "Stamp", "Caret", "Ink",
        "Popup", "FileAttachment", "Sound", "Movie", "Widget", "Screen", "PrinterMark",
        "TrapNet", "Watermark", "3D", "Redact", "Projection", "RichMedia"
    };

    public static IReadOnlyList<string> Sanitize(
        PdfObjectGraph graph,
        PdfRedactionSanitizationPolicy policy,
        List<PdfIncrementalObjectUpdate> updates)
    {
        var stats = new SanitizationStats();
        foreach (var indirectObject in graph.Objects.Values.OrderBy(static value => value.Id.Number))
        {
            var source = updates.LastOrDefault(update => update.Id == indirectObject.Id)?.Value
                ?? indirectObject.Value;
            var sanitized = SanitizeValue(graph, source, policy, stats, depth: 0);
            if (!sanitized.Changed)
            {
                continue;
            }

            updates.RemoveAll(update => update.Id == indirectObject.Id);
            updates.Add(new PdfIncrementalObjectUpdate(indirectObject.Id, sanitized.Value));
        }

        var applied = new List<string>();
        if (stats.ActionsValuesAndAlternates > 0)
        {
            applied.Add("documentWideActionsAndValues");
        }

        if (stats.HiddenRepresentations > 0)
        {
            applied.Add("hiddenRepresentations");
        }

        return applied;
    }

    private static SanitizedObject SanitizeValue(
        PdfObjectGraph graph,
        PdfObject value,
        PdfRedactionSanitizationPolicy policy,
        SanitizationStats stats,
        int depth)
    {
        if (depth > 64)
        {
            throw new InvalidDataException("A redaction sanitization object exceeded the maximum direct-object nesting depth.");
        }

        return value switch
        {
            PdfDictionary dictionary => SanitizeDictionary(graph, dictionary, policy, stats, depth + 1),
            PdfArray array => SanitizeArray(graph, array, policy, stats, depth + 1),
            PdfStreamObject stream => SanitizeStream(graph, stream, policy, stats, depth + 1),
            _ => new SanitizedObject(value, false)
        };
    }

    private static SanitizedObject SanitizeDictionary(
        PdfObjectGraph graph,
        PdfDictionary dictionary,
        PdfRedactionSanitizationPolicy policy,
        SanitizationStats stats,
        int depth)
    {
        var updated = Clone(dictionary);
        var changed = false;

        if (policy.RemoveDocumentWideActionsAndValues)
        {
            changed |= RemoveKeys(updated, ActionKeys, stats.MarkActionsValuesAndAlternates);
            changed |= RemoveKeys(updated, AlternateKeys, stats.MarkActionsValuesAndAlternates);
            if (IsAnnotation(dictionary))
            {
                changed |= RemoveKeys(updated, AnnotationCommentKeys, stats.MarkActionsValuesAndAlternates);
            }

            if (IsFormField(graph, dictionary))
            {
                changed |= RemoveKeys(updated, FieldValueKeys, stats.MarkActionsValuesAndAlternates);
            }
        }

        if (policy.RemoveHiddenRepresentations)
        {
            changed |= RemoveKeys(updated, HiddenRepresentationKeys, stats.MarkHiddenRepresentations);
            if (Resolve(graph, updated["AP"]) is PdfDictionary appearances)
            {
                var sanitizedAppearances = Clone(appearances);
                var appearanceChanged = RemoveKeys(
                    sanitizedAppearances,
                    ["D", "R"],
                    stats.MarkHiddenRepresentations);
                var nestedAppearance = SanitizeDictionary(
                    graph,
                    sanitizedAppearances,
                    policy,
                    stats,
                    depth + 1);
                if (appearanceChanged || nestedAppearance.Changed || updated["AP"] is PdfReference)
                {
                    updated["AP"] = nestedAppearance.Value;
                    changed = true;
                }
            }
        }

        foreach (var (key, child) in updated.Values.ToArray())
        {
            if (child is PdfReference)
            {
                continue;
            }

            var sanitized = SanitizeValue(graph, child, policy, stats, depth + 1);
            if (sanitized.Changed)
            {
                updated[key] = sanitized.Value;
                changed = true;
            }
        }

        return new SanitizedObject(updated, changed);
    }

    private static SanitizedObject SanitizeArray(
        PdfObjectGraph graph,
        PdfArray array,
        PdfRedactionSanitizationPolicy policy,
        SanitizationStats stats,
        int depth)
    {
        var changed = false;
        var items = new List<PdfObject>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (item is PdfReference)
            {
                items.Add(item);
                continue;
            }

            var sanitized = SanitizeValue(graph, item, policy, stats, depth + 1);
            items.Add(sanitized.Value);
            changed |= sanitized.Changed;
        }

        return new SanitizedObject(changed ? new PdfArray(items) : array, changed);
    }

    private static SanitizedObject SanitizeStream(
        PdfObjectGraph graph,
        PdfStreamObject stream,
        PdfRedactionSanitizationPolicy policy,
        SanitizationStats stats,
        int depth)
    {
        var dictionary = SanitizeDictionary(graph, stream.Dictionary, policy, stats, depth + 1);
        if (!dictionary.Changed)
        {
            return new SanitizedObject(stream, false);
        }

        var updated = new PdfStreamObject((PdfDictionary)dictionary.Value, stream.EncodedBytes);
        if (stream.IsDecoded)
        {
            updated.SetDecodedBytes(stream.DecodedBytes);
        }

        return new SanitizedObject(updated, true);
    }

    private static bool IsFormField(PdfObjectGraph graph, PdfDictionary dictionary)
    {
        var current = dictionary;
        var visited = new HashSet<PdfObjectId>();
        for (var depth = 0; depth <= 64; depth++)
        {
            if (current["FT"] is not null || current["Subtype"] is PdfName { Value: "Widget" })
            {
                return true;
            }

            if (current["Parent"] is not PdfReference parent ||
                !visited.Add(parent.Id) ||
                graph.Resolve(parent.Id)?.Value is not PdfDictionary parentDictionary)
            {
                return false;
            }

            current = parentDictionary;
        }

        return false;
    }

    private static bool IsAnnotation(PdfDictionary dictionary) =>
        dictionary["Type"] is PdfName { Value: "Annot" } ||
        dictionary["Subtype"] is PdfName subtype && AnnotationSubtypes.Contains(subtype.Value);

    private static bool RemoveKeys(PdfDictionary dictionary, IEnumerable<string> keys, Action onRemoved)
    {
        var changed = false;
        foreach (var key in keys)
        {
            if (dictionary[key] is null)
            {
                continue;
            }

            dictionary[key] = null;
            onRemoved();
            changed = true;
        }

        return changed;
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

    private sealed class SanitizationStats
    {
        public int ActionsValuesAndAlternates { get; private set; }
        public int HiddenRepresentations { get; private set; }

        public void MarkActionsValuesAndAlternates() => ActionsValuesAndAlternates++;
        public void MarkHiddenRepresentations() => HiddenRepresentations++;
    }

    private readonly record struct SanitizedObject(PdfObject Value, bool Changed);
}
