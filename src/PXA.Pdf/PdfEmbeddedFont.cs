using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace PXA.Pdf;

/// <summary>
/// Holds a TrueType/OpenType font loaded from disk and parsed minimally
/// (cmap format-4 and hmtx) to support glyph-ID lookup and width measurement.
/// </summary>
public sealed class PdfEmbeddedFont
{
    private readonly Dictionary<int, ushort> _charToGlyph;
    private readonly ushort[] _advanceWidths; // indexed by glyph ID
    private readonly int _unitsPerEm;

    public string BaseFontName { get; }
    public bool IsRtlCapable { get; }
    public ReadOnlyMemory<byte> FontBytes { get; }

    private PdfEmbeddedFont(
        string baseFontName,
        bool isRtlCapable,
        ReadOnlyMemory<byte> fontBytes,
        Dictionary<int, ushort> charToGlyph,
        ushort[] advanceWidths,
        int unitsPerEm)
    {
        BaseFontName = baseFontName;
        IsRtlCapable = isRtlCapable;
        FontBytes = fontBytes;
        _charToGlyph = charToGlyph;
        _advanceWidths = advanceWidths;
        _unitsPerEm = unitsPerEm;
    }

    public ushort GetGlyphId(char ch)
    {
        _charToGlyph.TryGetValue(ch, out var gid);
        return gid;
    }

    public ushort GetGlyphId(int codePoint)
    {
        _charToGlyph.TryGetValue(codePoint, out var gid);
        return gid;
    }

    public double GetGlyphAdvance(ushort glyphId)
    {
        if (glyphId < _advanceWidths.Length)
            return _advanceWidths[glyphId];
        return _advanceWidths.Length > 0 ? _advanceWidths[^1] : 500;
    }

    /// <summary>Returns advance width in PDF glyph space units (1/1000 of text space).</summary>
    public int GetPdfAdvanceWidth(int codePoint)
    {
        if (_unitsPerEm == 0) return 500;
        var gid = GetGlyphId(codePoint);
        var advance = GetGlyphAdvance(gid);
        return (int)Math.Round(advance / _unitsPerEm * 1000);
    }

    public double MeasureWidth(string text, double fontSize)
    {
        if (_unitsPerEm == 0) return text.Length * fontSize * 0.5;

        double total = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            var codePoint = char.ConvertToUtf32(element, 0);
            var gid = GetGlyphId(codePoint);
            total += GetGlyphAdvance(gid);
        }
        return total / _unitsPerEm * fontSize;
    }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static bool TryLoad(string filePath, bool isRtlCapable, out PdfEmbeddedFont? font)
    {
        font = null;
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var mem = bytes.AsMemory();
            var span = bytes.AsSpan();

            // Locate table directory entries
            var tables = ReadTableDirectory(span);
            if (tables is null) return false;

            // head — unitsPerEm
            int unitsPerEm = 1000;
            if (tables.TryGetValue("head", out var headEntry))
                unitsPerEm = ReadUnitsPerEm(span, headEntry.Offset);

            // hhea — numberOfHMetrics
            int numberOfHMetrics = 0;
            if (tables.TryGetValue("hhea", out var hheaEntry))
                numberOfHMetrics = ReadNumberOfHMetrics(span, hheaEntry.Offset);

            // hmtx — advance widths
            ushort[] advanceWidths = [];
            if (tables.TryGetValue("hmtx", out var hmtxEntry) && numberOfHMetrics > 0)
                advanceWidths = ReadAdvanceWidths(span, hmtxEntry.Offset, numberOfHMetrics);

            // cmap — char → glyph
            var charToGlyph = new Dictionary<int, ushort>();
            if (tables.TryGetValue("cmap", out var cmapEntry))
                ReadCmapFormat4(span, cmapEntry.Offset, charToGlyph);

            // name — PostScript font name (nameID=6)
            string fontName = Path.GetFileNameWithoutExtension(filePath);
            if (tables.TryGetValue("name", out var nameEntry))
            {
                var psName = ReadPostScriptName(span, nameEntry.Offset);
                if (!string.IsNullOrEmpty(psName)) fontName = psName;
            }

            // Sanitize font name for PDF (no spaces, no special chars)
            fontName = fontName.Replace(" ", string.Empty, StringComparison.Ordinal)
                               .Replace("-", string.Empty, StringComparison.Ordinal);

            font = new PdfEmbeddedFont(fontName, isRtlCapable, mem, charToGlyph, advanceWidths, unitsPerEm);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── TTF table parsing helpers ─────────────────────────────────────────────

    private sealed record TableEntry(uint Offset, uint Length);

    private static Dictionary<string, TableEntry>? ReadTableDirectory(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12) return null;

        // Check for TrueType / OpenType sfVersion
        uint sfVersion = BinaryPrimitives.ReadUInt32BigEndian(data);
        if (sfVersion != 0x00010000 && sfVersion != 0x4F54544F) // 0x00010000 = TrueType, 'OTTO' = CFF
        {
            // Try TrueType Collection (TTC)
            if (sfVersion != 0x74746366) return null; // 'ttcf'
            // For TTC, skip to the first font's offset table
            uint offset0 = BinaryPrimitives.ReadUInt32BigEndian(data[12..]);
            data = data[(int)offset0..];
            sfVersion = BinaryPrimitives.ReadUInt32BigEndian(data);
            if (sfVersion != 0x00010000 && sfVersion != 0x4F54544F) return null;
        }

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        if (numTables == 0 || numTables > 64) return null;

        var dict = new Dictionary<string, TableEntry>(numTables, StringComparer.Ordinal);
        for (int i = 0; i < numTables; i++)
        {
            int recordOffset = 12 + i * 16;
            if (recordOffset + 16 > data.Length) break;

            var tag = Encoding.ASCII.GetString(data.Slice(recordOffset, 4));
            uint tableOffset = BinaryPrimitives.ReadUInt32BigEndian(data[(recordOffset + 8)..]);
            uint tableLength = BinaryPrimitives.ReadUInt32BigEndian(data[(recordOffset + 12)..]);
            dict[tag] = new TableEntry(tableOffset, tableLength);
        }
        return dict;
    }

    private static int ReadUnitsPerEm(ReadOnlySpan<byte> data, uint tableOffset)
    {
        int off = (int)tableOffset;
        if (off + 18 > data.Length) return 1000;
        // unitsPerEm is at offset 18 in the head table
        return BinaryPrimitives.ReadUInt16BigEndian(data[(off + 18)..]);
    }

    private static int ReadNumberOfHMetrics(ReadOnlySpan<byte> data, uint tableOffset)
    {
        int off = (int)tableOffset;
        if (off + 36 > data.Length) return 0;
        // numberOfHMetrics is at offset 34 in the hhea table
        return BinaryPrimitives.ReadUInt16BigEndian(data[(off + 34)..]);
    }

    private static ushort[] ReadAdvanceWidths(ReadOnlySpan<byte> data, uint tableOffset, int count)
    {
        var widths = new ushort[count];
        int off = (int)tableOffset;
        for (int i = 0; i < count; i++)
        {
            int pos = off + i * 4;
            if (pos + 2 > data.Length) break;
            widths[i] = BinaryPrimitives.ReadUInt16BigEndian(data[pos..]);
        }
        return widths;
    }

    private static void ReadCmapFormat4(ReadOnlySpan<byte> data, uint tableOffset, Dictionary<int, ushort> result)
    {
        int baseOff = (int)tableOffset;
        if (baseOff + 4 > data.Length) return;

        // cmap header: version(2), numTables(2)
        int numSubtables = BinaryPrimitives.ReadUInt16BigEndian(data[(baseOff + 2)..]);

        // Find the best subtable: prefer platform 3 enc 1 (Windows Unicode BMP), fallback 0 enc 3
        int subtableOffset = -1;
        for (int i = 0; i < numSubtables; i++)
        {
            int recOff = baseOff + 4 + i * 8;
            if (recOff + 8 > data.Length) break;
            ushort platformId = BinaryPrimitives.ReadUInt16BigEndian(data[recOff..]);
            ushort encodingId = BinaryPrimitives.ReadUInt16BigEndian(data[(recOff + 2)..]);
            int subOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(data[(recOff + 4)..]);

            if ((platformId == 3 && encodingId == 1) || (platformId == 0 && encodingId == 3))
            {
                subtableOffset = baseOff + subOffset;
                // Prefer platform 3 enc 1 — keep looking in case we find it
                if (platformId == 3 && encodingId == 1) break;
            }
        }
        if (subtableOffset < 0) return;

        // Read subtable format
        if (subtableOffset + 2 > data.Length) return;
        ushort format = BinaryPrimitives.ReadUInt16BigEndian(data[subtableOffset..]);
        if (format != 4) return;

        // Format 4 layout:
        // format(2), length(2), language(2), segCount*2(2), ...
        int off = subtableOffset;
        if (off + 14 > data.Length) return;
        int segCount = BinaryPrimitives.ReadUInt16BigEndian(data[(off + 6)..]) / 2;
        if (segCount <= 0 || segCount > 8192) return;

        int endCodesOff = off + 14;
        int startCodesOff = endCodesOff + segCount * 2 + 2; // +2 for reservedPad
        int idDeltasOff = startCodesOff + segCount * 2;
        int idRangeOffsetsOff = idDeltasOff + segCount * 2;
        int glyphIdsOff = idRangeOffsetsOff + segCount * 2;

        for (int seg = 0; seg < segCount; seg++)
        {
            int endCode = BinaryPrimitives.ReadUInt16BigEndian(data[(endCodesOff + seg * 2)..]);
            int startCode = BinaryPrimitives.ReadUInt16BigEndian(data[(startCodesOff + seg * 2)..]);
            short idDelta = BinaryPrimitives.ReadInt16BigEndian(data[(idDeltasOff + seg * 2)..]);
            ushort idRangeOffset = BinaryPrimitives.ReadUInt16BigEndian(data[(idRangeOffsetsOff + seg * 2)..]);

            if (startCode == 0xFFFF) break;

            for (int c = startCode; c <= endCode; c++)
            {
                ushort glyphId;
                if (idRangeOffset == 0)
                {
                    glyphId = (ushort)((c + idDelta) & 0xFFFF);
                }
                else
                {
                    int glyphIdIndex = idRangeOffsetsOff + seg * 2 + idRangeOffset + (c - startCode) * 2;
                    if (glyphIdIndex + 2 > data.Length) continue;
                    ushort rawGid = BinaryPrimitives.ReadUInt16BigEndian(data[glyphIdIndex..]);
                    glyphId = rawGid == 0 ? (ushort)0 : (ushort)((rawGid + idDelta) & 0xFFFF);
                }
                if (glyphId != 0)
                    result[c] = glyphId;
            }
        }
    }

    private static string? ReadPostScriptName(ReadOnlySpan<byte> data, uint tableOffset)
    {
        // Find nameID=6 (PostScript name), platformID=3 (Windows), encodingID=1, languageID=0x409
        int baseOff = (int)tableOffset;
        if (baseOff + 6 > data.Length) return null;

        int count = BinaryPrimitives.ReadUInt16BigEndian(data[(baseOff + 2)..]);
        int storageOff = baseOff + BinaryPrimitives.ReadUInt16BigEndian(data[(baseOff + 4)..]);

        for (int i = 0; i < count; i++)
        {
            int recOff = baseOff + 6 + i * 12;
            if (recOff + 12 > data.Length) break;
            ushort platformId = BinaryPrimitives.ReadUInt16BigEndian(data[recOff..]);
            ushort nameId = BinaryPrimitives.ReadUInt16BigEndian(data[(recOff + 6)..]);
            int length = BinaryPrimitives.ReadUInt16BigEndian(data[(recOff + 8)..]);
            int strOff = storageOff + BinaryPrimitives.ReadUInt16BigEndian(data[(recOff + 10)..]);

            if (nameId == 6 && platformId == 3 && strOff + length <= data.Length)
                return Encoding.BigEndianUnicode.GetString(data.Slice(strOff, length));
        }
        return null;
    }
}
