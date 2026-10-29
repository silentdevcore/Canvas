using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Trust;

/// <summary>
/// Detects object definitions that a conforming reader would never resolve.
///
/// A legitimate incremental update defines the same object number many times, once per
/// revision, and every one of those definitions is referenced by that revision's
/// cross-reference data. A definition sitting at an offset that no revision points to is
/// therefore not a normal update: it is a body of bytes that PVAD can see and a viewer
/// cannot, which is exactly how a document is made to validate as one thing and display
/// as another.
/// </summary>
internal static class PdfShadowObjectAnalyzer
{
    private const int MaximumReportedFindings = 32;

    public static void Analyze(
        byte[] source,
        PdfCrossReferenceTable? xref,
        PdfObjectGraph? graph,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        if (xref is null || graph is null || xref.Revisions.Count == 0)
        {
            return;
        }

        var referencedOffsets = new HashSet<long>();
        foreach (var entry in xref.RevisionEntries)
        {
            if (!entry.IsFree && entry.Kind == PdfCrossReferenceEntryKind.InUse && entry.Offset >= 0)
            {
                referencedOffsets.Add(entry.Offset);
            }
        }

        foreach (var entry in xref.Entries.Values)
        {
            if (!entry.IsFree && entry.Kind == PdfCrossReferenceEntryKind.InUse && entry.Offset >= 0)
            {
                referencedOffsets.Add(entry.Offset);
            }
        }

        var streamRanges = CollectStreamRanges(graph);
        var reported = 0;
        var duplicates = 0;
        foreach (var (id, offset) in FindObjectHeaders(source))
        {
            if (referencedOffsets.Contains(offset) || IsInsideStream(streamRanges, offset))
            {
                continue;
            }

            duplicates++;
            if (reported < MaximumReportedFindings)
            {
                reported++;
                diagnostics.Add(new PdfTrustDiagnostic(
                    "PXAPDFTRUST150",
                    PdfTrustDiagnosticSeverity.Error,
                    $"Object {id.Number} {id.Generation} R is defined at offset {offset}, which no cross-reference revision references.",
                    id.Number,
                    id.Generation));
            }
        }

        if (duplicates > reported)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST151",
                PdfTrustDiagnosticSeverity.Error,
                $"{duplicates} object definitions are not referenced by any cross-reference revision."));
        }
    }

    private static List<(long Start, long End)> CollectStreamRanges(PdfObjectGraph graph)
    {
        var ranges = new List<(long Start, long End)>();
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is PdfStreamObject && indirectObject.SourceSpan.Length > 0)
            {
                ranges.Add((
                    indirectObject.SourceSpan.Offset,
                    indirectObject.SourceSpan.Offset + indirectObject.SourceSpan.Length));
            }
        }

        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return ranges;
    }

    private static bool IsInsideStream(List<(long Start, long End)> ranges, long offset)
    {
        var low = 0;
        var high = ranges.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var (start, end) = ranges[middle];
            if (offset < start)
            {
                high = middle - 1;
            }
            else if (offset >= end)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds <c>N G obj</c> headers that start a line, which is where a real indirect object
    /// definition begins. Binary stream payloads that happen to contain the same bytes are
    /// excluded by the caller through the parsed stream ranges.
    /// </summary>
    private static List<(PdfObjectId Id, long Offset)> FindObjectHeaders(byte[] source)
    {
        var headers = new List<(PdfObjectId Id, long Offset)>();
        var searchOffset = 0;
        while (searchOffset < source.Length)
        {
            var found = source.AsSpan(searchOffset).IndexOf(" obj"u8);
            if (found < 0)
            {
                break;
            }

            var keywordOffset = searchOffset + found;
            var next = keywordOffset + " obj"u8.Length;
            if (next >= source.Length || IsDelimiter(source[next]))
            {
                var lineStart = FindLineStart(source, keywordOffset);
                if (TryReadHeader(source, lineStart, keywordOffset, out var id))
                {
                    headers.Add((id, lineStart));
                }
            }

            searchOffset = keywordOffset + " obj"u8.Length;
        }

        return headers;
    }

    private static bool TryReadHeader(byte[] source, int lineStart, int keywordOffset, out PdfObjectId id)
    {
        id = default;
        if (lineStart >= keywordOffset)
        {
            return false;
        }

        var header = Encoding.ASCII.GetString(source, lineStart, keywordOffset - lineStart);
        var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
            int.TryParse(parts[0], out var number) &&
            int.TryParse(parts[1], out var generation) &&
            number > 0 &&
            generation >= 0 &&
            TrySet(number, generation, out id);
    }

    private static bool TrySet(int number, int generation, out PdfObjectId id)
    {
        id = new PdfObjectId(number, generation);
        return true;
    }

    private static bool IsDelimiter(byte value) =>
        value is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t' or (byte)'\f' or 0 or
            (byte)'<' or (byte)'[' or (byte)'/' or (byte)'(';

    private static int FindLineStart(byte[] source, int offset)
    {
        var index = offset - 1;
        while (index > 0 && source[index] is not (byte)'\r' and not (byte)'\n')
        {
            index--;
        }

        return index == 0 ? 0 : index + 1;
    }
}
