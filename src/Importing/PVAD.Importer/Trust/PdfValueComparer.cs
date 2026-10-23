using PVAD.Importer.Objects;

namespace PVAD.Importer.Trust;

/// <summary>
/// Compares PDF values structurally without resolving indirect references, so that two revisions of
/// the same object can be diffed without following cycles.
/// </summary>
internal static class PdfValueComparer
{
    private const int MaximumDepth = 64;

    public static bool AreEqual(PdfObject? left, PdfObject? right) => AreEqual(left, right, depth: 0);

    private static bool AreEqual(PdfObject? left, PdfObject? right, int depth)
    {
        if (depth > MaximumDepth)
        {
            return false;
        }

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return (left, right) switch
        {
            (null, null) => true,
            (null, PdfNull) or (PdfNull, null) => true,
            (null, _) or (_, null) => false,
            (PdfNull, PdfNull) => true,
            (PdfName a, PdfName b) => string.Equals(a.Value, b.Value, StringComparison.Ordinal),
            (PdfBoolean a, PdfBoolean b) => a.Value == b.Value,
            (PdfInteger a, PdfInteger b) => a.Value == b.Value,
            (PdfInteger a, PdfNumber b) => a.Value == b.Value,
            (PdfNumber a, PdfInteger b) => a.Value == b.Value,
            (PdfNumber a, PdfNumber b) => a.Value.Equals(b.Value),
            (PdfReference a, PdfReference b) => a.Id == b.Id,
            (PdfString a, PdfString b) => a.GetDecodedBytes().Span.SequenceEqual(b.GetDecodedBytes().Span),
            (PdfArray a, PdfArray b) => AreEqual(a, b, depth),
            (PdfDictionary a, PdfDictionary b) => AreEqual(a, b, depth),
            (PdfStreamObject a, PdfStreamObject b) =>
                AreEqual(a.Dictionary, b.Dictionary, depth) && a.EncodedBytes.Span.SequenceEqual(b.EncodedBytes.Span),
            _ => false
        };
    }

    private static bool AreEqual(PdfArray left, PdfArray right, int depth)
    {
        if (left.Items.Count != right.Items.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Items.Count; index++)
        {
            if (!AreEqual(left.Items[index], right.Items[index], depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEqual(PdfDictionary left, PdfDictionary right, int depth)
    {
        if (left.Values.Count != right.Values.Count)
        {
            return false;
        }

        foreach (var (key, value) in left.Values)
        {
            if (!right.Values.TryGetValue(key, out var other) || !AreEqual(value, other, depth + 1))
            {
                return false;
            }
        }

        return true;
    }
}
