using System.Security.Cryptography;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Streams;
using PXA.Importer.Tokenizer;

namespace PXA.Importer.Xref;

public enum PdfCrossReferenceEntryKind
{
    Free,
    InUse,
    Compressed
}

public enum PdfCrossReferenceSectionKind
{
    ClassicTable,
    CrossReferenceStream
}

public sealed record PdfCrossReferenceRevision(
    int SequenceNumber,
    int DistanceFromLatest,
    long CrossReferenceOffset,
    long? PreviousCrossReferenceOffset,
    PdfCrossReferenceSectionKind Kind,
    long EndOffset,
    string Sha256,
    int EntryCount,
    PdfDictionary Trailer);

public sealed record PdfCrossReferenceDiagnostic(string Code, string Message, long? Offset = null);

public sealed record PdfCrossReferenceEntry(
    PdfObjectId Id,
    long Offset,
    int Generation,
    bool IsFree,
    int Revision,
    PdfCrossReferenceEntryKind Kind = PdfCrossReferenceEntryKind.InUse,
    int ObjectStreamNumber = 0,
    int ObjectStreamIndex = 0);

public sealed class PdfCrossReferenceTable
{
    private readonly Dictionary<PdfObjectId, PdfCrossReferenceEntry> _entries = [];
    private readonly List<PdfCrossReferenceRevision> _revisions = [];
    private readonly List<PdfCrossReferenceDiagnostic> _diagnostics = [];

    public IReadOnlyDictionary<PdfObjectId, PdfCrossReferenceEntry> Entries => _entries;
    public IReadOnlyList<PdfCrossReferenceRevision> Revisions => _revisions;
    public IReadOnlyList<PdfCrossReferenceDiagnostic> Diagnostics => _diagnostics;
    public PdfDictionary? Trailer { get; set; }

    public void Add(PdfCrossReferenceEntry entry, bool overwrite = true)
    {
        if (overwrite || !_entries.ContainsKey(entry.Id))
        {
            _entries[entry.Id] = entry;
        }
    }

    internal void SetRevisions(IEnumerable<PdfCrossReferenceRevision> revisions)
    {
        _revisions.Clear();
        _revisions.AddRange(revisions);
    }

    internal void AddDiagnostic(PdfCrossReferenceDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
}

public sealed class PdfCrossReferenceParser
{
    public PdfCrossReferenceTable Parse(PdfParseContext context)
    {
        var table = new PdfCrossReferenceTable();
        if (!ParseStartXrefChain(context, table))
        {
            ParseClassicTables(context, table);
        }

        ParseIndirectObjectFallback(context.Source.Span, table);
        return table;
    }

    private static bool ParseStartXrefChain(PdfParseContext context, PdfCrossReferenceTable table)
    {
        var source = context.Source.Span;
        var startXrefOffset = FindLastMarker(source, "startxref"u8);
        if (startXrefOffset < 0)
        {
            return false;
        }

        var tokenizer = new PdfTokenizer(source[(startXrefOffset + "startxref"u8.Length)..]);
        var offsetToken = tokenizer.ReadToken();
        if (!long.TryParse(offsetToken.Text, out var xrefOffset) || xrefOffset < 0 || xrefOffset >= source.Length)
        {
            return false;
        }

        var visited = new HashSet<long>();
        var parsedRevisions = new List<ParsedRevision>();
        var revision = 0;
        while (true)
        {
            if (!visited.Add(xrefOffset))
            {
                table.AddDiagnostic(new PdfCrossReferenceDiagnostic(
                    "PXAPDFXREF003",
                    "The incremental revision chain contains a cyclic /Prev reference.",
                    xrefOffset));
                break;
            }

            var kind = PdfCrossReferenceSectionKind.ClassicTable;
            var trailer = ParseClassicSection(
                context,
                table,
                checked((int)xrefOffset),
                revision,
                preserveExistingEntries: true,
                out _,
                out var sectionEntryCount);
            if (trailer is null)
            {
                kind = PdfCrossReferenceSectionKind.CrossReferenceStream;
                trailer = ParseXrefStreamSection(
                    context,
                    table,
                    checked((int)xrefOffset),
                    revision,
                    preserveExistingEntries: true,
                    out sectionEntryCount);
            }

            if (trailer is null)
            {
                table.AddDiagnostic(new PdfCrossReferenceDiagnostic(
                    "PXAPDFXREF001",
                    "A cross-reference revision could not be parsed at the referenced offset.",
                    xrefOffset));
                break;
            }

            table.Trailer ??= trailer;

            var previousValue = trailer["Prev"];
            var previousOffset = TryGetInteger(previousValue, out var parsedPreviousOffset)
                ? parsedPreviousOffset
                : (long?)null;
            if (previousValue is not null && previousOffset is null)
            {
                table.AddDiagnostic(new PdfCrossReferenceDiagnostic(
                    "PXAPDFXREF004",
                    "The incremental revision trailer contains a non-integer /Prev reference.",
                    xrefOffset));
            }
            parsedRevisions.Add(new ParsedRevision(
                revision,
                xrefOffset,
                previousOffset,
                kind,
                FindRevisionEnd(source, checked((int)xrefOffset)),
                sectionEntryCount,
                trailer));

            if (previousOffset is null)
            {
                break;
            }

            if (previousOffset < 0 || previousOffset >= source.Length)
            {
                table.AddDiagnostic(new PdfCrossReferenceDiagnostic(
                    "PXAPDFXREF002",
                    "The incremental revision chain contains an out-of-range /Prev reference.",
                    previousOffset));
                break;
            }

            xrefOffset = previousOffset.Value;
            revision++;
        }

        var revisions = new List<PdfCrossReferenceRevision>(parsedRevisions.Count);
        parsedRevisions.Reverse();
        for (var index = 0; index < parsedRevisions.Count; index++)
        {
            var item = parsedRevisions[index];
            revisions.Add(new PdfCrossReferenceRevision(
                index,
                item.DistanceFromLatest,
                item.CrossReferenceOffset,
                item.PreviousCrossReferenceOffset,
                item.Kind,
                item.EndOffset,
                Convert.ToHexStringLower(SHA256.HashData(source[..checked((int)item.EndOffset)])),
                item.EntryCount,
                item.Trailer));
        }

        table.SetRevisions(revisions);
        if (revisions.Count > 0 &&
            revisions[^1].EndOffset < source.Length &&
            ContainsNonWhitespace(source[checked((int)revisions[^1].EndOffset)..]))
        {
            table.AddDiagnostic(new PdfCrossReferenceDiagnostic(
                "PXAPDFXREF005",
                "The PDF contains non-whitespace bytes after the final complete revision.",
                revisions[^1].EndOffset));
        }

        return table.Entries.Count > 0;
    }

    private static void ParseClassicTables(PdfParseContext context, PdfCrossReferenceTable table)
    {
        var source = context.Source.Span;
        var marker = "xref"u8;
        var offset = 0;
        while (offset < source.Length)
        {
            var found = source[offset..].IndexOf(marker);
            if (found < 0)
            {
                return;
            }

            offset += found;
            var trailer = ParseClassicSection(
                context,
                table,
                offset,
                revision: 0,
                preserveExistingEntries: false,
                out var nextOffset,
                out _);
            table.Trailer = trailer ?? table.Trailer;
            offset = nextOffset;
        }
    }

    private static PdfDictionary? ParseClassicSection(
        PdfParseContext context,
        PdfCrossReferenceTable table,
        int xrefOffset,
        int revision,
        bool preserveExistingEntries)
    {
        return ParseClassicSection(context, table, xrefOffset, revision, preserveExistingEntries, out _, out _);
    }

    private static PdfDictionary? ParseClassicSection(
        PdfParseContext context,
        PdfCrossReferenceTable table,
        int xrefOffset,
        int revision,
        bool preserveExistingEntries,
        out int nextOffset,
        out int entryCount)
    {
        var source = context.Source.Span;
        nextOffset = Math.Min(xrefOffset + 1, source.Length);
        entryCount = 0;

        if (xrefOffset < 0 || xrefOffset >= source.Length || !source[xrefOffset..].StartsWith("xref"u8))
        {
            return null;
        }

        var xrefContentOffset = xrefOffset + "xref"u8.Length;
        var tokenizer = new PdfTokenizer(source[xrefContentOffset..]);
        PdfDictionary? trailer = null;

        while (true)
        {
            var first = tokenizer.ReadToken();
            if (first.Kind == PdfTokenKind.EndOfFile || first.Text == "trailer")
            {
                if (first.Text == "trailer")
                {
                    trailer = TryParseTrailer(context, xrefContentOffset + tokenizer.Position);
                }

                break;
            }

            var count = tokenizer.ReadToken();
            if (!int.TryParse(first.Text, out var startObject) || !int.TryParse(count.Text, out var objectCount))
            {
                break;
            }

            for (var i = 0; i < objectCount; i++)
            {
                var objectOffset = tokenizer.ReadToken();
                var generation = tokenizer.ReadToken();
                var status = tokenizer.ReadToken();
                if (!long.TryParse(objectOffset.Text, out var parsedOffset) || !int.TryParse(generation.Text, out var parsedGeneration))
                {
                    continue;
                }

                var id = new PdfObjectId(startObject + i, parsedGeneration);
                table.Add(
                    new PdfCrossReferenceEntry(
                        id,
                        parsedOffset,
                        parsedGeneration,
                        status.Text == "f",
                        revision,
                        status.Text == "f" ? PdfCrossReferenceEntryKind.Free : PdfCrossReferenceEntryKind.InUse),
                    overwrite: !preserveExistingEntries);
                entryCount++;
            }
        }

        nextOffset = xrefContentOffset + tokenizer.Position;
        return trailer;
    }

    private static PdfDictionary? ParseXrefStreamSection(
        PdfParseContext context,
        PdfCrossReferenceTable table,
        int objectOffset,
        int revision,
        bool preserveExistingEntries,
        out int entryCount)
    {
        entryCount = 0;
        var parser = new PdfObjectParser(context, table);
        var indirectObject = parser.TryParseIndirectObjectAt(objectOffset);
        if (indirectObject?.Value is not PdfStreamObject stream ||
            stream.Dictionary["Type"] is not PdfName { Value: "XRef" })
        {
            return null;
        }

        var widths = ReadIntegerArray(stream.Dictionary["W"]);
        if (widths.Count < 3)
        {
            return stream.Dictionary;
        }

        var indexPairs = ReadIntegerArray(stream.Dictionary["Index"]);
        if (indexPairs.Count == 0 && TryGetInteger(stream.Dictionary["Size"], out var size))
        {
            indexPairs = [0, size];
        }

        var decoded = new PdfStreamDecoderRegistry().Decode(stream).Span;
        var entryLength = checked((int)(widths[0] + widths[1] + widths[2]));
        if (entryLength <= 0)
        {
            return stream.Dictionary;
        }

        var dataOffset = 0;
        for (var pairIndex = 0; pairIndex + 1 < indexPairs.Count; pairIndex += 2)
        {
            var objectNumber = indexPairs[pairIndex];
            var count = indexPairs[pairIndex + 1];

            for (var i = 0; i < count && dataOffset + entryLength <= decoded.Length; i++)
            {
                var type = widths[0] == 0 ? 1 : ReadBigEndian(decoded.Slice(dataOffset, checked((int)widths[0])));
                dataOffset += checked((int)widths[0]);
                var field2 = ReadBigEndian(decoded.Slice(dataOffset, checked((int)widths[1])));
                dataOffset += checked((int)widths[1]);
                var field3 = ReadBigEndian(decoded.Slice(dataOffset, checked((int)widths[2])));
                dataOffset += checked((int)widths[2]);

                var id = new PdfObjectId(checked((int)objectNumber + i), type == 1 ? checked((int)field3) : 0);
                var entry = type switch
                {
                    0 => new PdfCrossReferenceEntry(id, field2, checked((int)field3), IsFree: true, revision, PdfCrossReferenceEntryKind.Free),
                    1 => new PdfCrossReferenceEntry(id, field2, checked((int)field3), IsFree: false, revision, PdfCrossReferenceEntryKind.InUse),
                    2 => new PdfCrossReferenceEntry(id, Offset: -1, Generation: 0, IsFree: false, revision, PdfCrossReferenceEntryKind.Compressed, checked((int)field2), checked((int)field3)),
                    _ => null
                };

                if (entry is not null)
                {
                    table.Add(entry, overwrite: !preserveExistingEntries);
                    entryCount++;
                }
            }
        }

        return stream.Dictionary;
    }

    private static PdfDictionary? TryParseTrailer(PdfParseContext context, int offset)
    {
        if (offset < 0 || offset >= context.Source.Length)
        {
            return null;
        }

        var parser = new PdfObjectParser(context, new PdfCrossReferenceTable());
        var parsed = parser.ParseObjectAt(offset, baseOffset: 0);
        return parsed as PdfDictionary;
    }

    private static void ParseIndirectObjectFallback(ReadOnlySpan<byte> source, PdfCrossReferenceTable table)
    {
        var marker = " obj"u8;
        var searchOffset = 0;
        while (searchOffset < source.Length)
        {
            var found = source[searchOffset..].IndexOf(marker);
            if (found < 0)
            {
                return;
            }

            var objKeywordOffset = searchOffset + found;
            var lineStart = FindLineStart(source, objKeywordOffset);
            var header = source[lineStart..objKeywordOffset];
            var split = header.LastIndexOf((byte)' ');
            if (split > 0 &&
                int.TryParse(System.Text.Encoding.ASCII.GetString(header[..split]), out var objectNumber) &&
                int.TryParse(System.Text.Encoding.ASCII.GetString(header[(split + 1)..]), out var generation))
            {
                var id = new PdfObjectId(objectNumber, generation);
                if (!table.Entries.ContainsKey(id))
                {
                    table.Add(new PdfCrossReferenceEntry(id, lineStart, generation, IsFree: false, Revision: 0));
                }
            }

            searchOffset = objKeywordOffset + marker.Length;
        }
    }

    private static int FindLastMarker(ReadOnlySpan<byte> source, ReadOnlySpan<byte> marker)
    {
        for (var i = source.Length - marker.Length; i >= 0; i--)
        {
            if (source[i..].StartsWith(marker))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsNonWhitespace(ReadOnlySpan<byte> source)
    {
        foreach (var value in source)
        {
            if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0 or 12))
            {
                return true;
            }
        }

        return false;
    }

    private static long FindRevisionEnd(ReadOnlySpan<byte> source, int crossReferenceOffset)
    {
        var relativeEof = source[crossReferenceOffset..].IndexOf("%%EOF"u8);
        if (relativeEof < 0)
        {
            return source.Length;
        }

        var end = crossReferenceOffset + relativeEof + "%%EOF"u8.Length;
        while (end < source.Length && source[end] is (byte)'\r' or (byte)'\n')
        {
            end++;
        }

        return end;
    }

    private static bool TryGetInteger(PdfObject? value, out long integer)
    {
        switch (value)
        {
            case PdfInteger pdfInteger:
                integer = pdfInteger.Value;
                return true;
            case PdfNumber number:
                integer = (long)number.Value;
                return true;
            default:
                integer = 0;
                return false;
        }
    }

    private static List<long> ReadIntegerArray(PdfObject? value)
    {
        if (value is not PdfArray array)
        {
            return [];
        }

        var values = new List<long>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (TryGetInteger(item, out var integer))
            {
                values.Add(integer);
            }
        }

        return values;
    }

    private static long ReadBigEndian(ReadOnlySpan<byte> bytes)
    {
        long value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }

    private static int FindLineStart(ReadOnlySpan<byte> source, int offset)
    {
        var index = offset - 1;
        while (index > 0 && source[index] is not (byte)'\r' and not (byte)'\n')
        {
            index--;
        }

        return index == 0 ? 0 : index + 1;
    }

    private sealed record ParsedRevision(
        int DistanceFromLatest,
        long CrossReferenceOffset,
        long? PreviousCrossReferenceOffset,
        PdfCrossReferenceSectionKind Kind,
        long EndOffset,
        int EntryCount,
        PdfDictionary Trailer);
}
