using System.Text;
using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Converts a legacy Word 97-2003 .doc binary file into a <see cref="DesignExportDto"/>.
/// Reads text from the Word Binary File Format without external libraries:
/// locates the WordDocument stream inside the CFBF container, extracts the
/// text array using the File Information Block offsets, and stacks paragraphs
/// as Text elements on a single PXA page.
/// </summary>
public sealed class DocFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["doc"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name));

    private const double PageWidth  = 595;
    private const double PageHeight = 842;
    private const double MarginX    = 48;
    private const double MarginY    = 48;

    public static DesignExportDto Import(Stream stream, string? name = null)
    {
        byte[] data = ReadAll(stream);

        // Verify CFBF magic: D0 CF 11 E0 A1 B1 1A E1
        if (data.Length < 8 || data[0] != 0xD0 || data[1] != 0xCF || data[2] != 0x11 || data[3] != 0xE0)
            throw new InvalidDataException("Not a valid Compound File Binary (.doc) file.");

        int sectorSize  = 1 << ReadU16(data, 30);  // 2^ssz (typically 512)
        int miniFatCut  = ReadI32(data, 56);        // mini stream cutoff size

        // ── FAT chain ──────────────────────────────────────────────────────────
        int fatSectorCount = ReadI32(data, 44);
        var fatSectors = new int[fatSectorCount];
        for (int i = 0; i < fatSectorCount && i < 109; i++)
            fatSectors[i] = ReadI32(data, 76 + i * 4);

        // Build FAT from the listed FAT sectors
        var fat = BuildFat(data, fatSectors, fatSectorCount, sectorSize);

        // ── Directory ─────────────────────────────────────────────────────────
        int dirStart = ReadI32(data, 48);
        var dir = ReadDirectory(data, fat, dirStart, sectorSize);

        // ── Find WordDocument stream ───────────────────────────────────────────
        var wdEntry = dir.FirstOrDefault(e => e.Name == "WordDocument");
        if (wdEntry is null)
            throw new InvalidDataException("WordDocument stream not found — may not be a Word .doc file.");

        byte[] wdStream = ReadStream(data, fat, wdEntry.StartSector, wdEntry.Size, sectorSize);

        // ── File Information Block (FIB) ───────────────────────────────────────
        // wIdent (0x00) = 0xA5EC for Word doc
        // fcMin  (0x18) = start of text in character positions
        // ccpText(0x4C) = count of characters in main document text
        if (wdStream.Length < 0x60)
            throw new InvalidDataException("WordDocument stream too short.");

        int fcMin   = ReadI32(wdStream, 0x18);
        int ccpText = ReadI32(wdStream, 0x4C);
        bool unicode = (ReadU16(wdStream, 0x1A) & 0x0200) != 0; // fComplex bit 9 in flags

        if (ccpText <= 0 || fcMin < 0 || fcMin >= wdStream.Length)
        {
            // Fallback: try to extract printable text by scanning the stream
            return BuildDesign(name, ExtractPrintableText(wdStream));
        }

        int charSize  = unicode ? 2 : 1;
        int maxChars  = Math.Min(ccpText, (wdStream.Length - fcMin) / charSize);
        if (maxChars <= 0)
            return BuildDesign(name, ExtractPrintableText(wdStream));

        string rawText = unicode
            ? Encoding.Unicode.GetString(wdStream, fcMin, maxChars * 2)
            : Encoding.GetEncoding(1252).GetString(wdStream, fcMin, maxChars);

        return BuildDesign(name, rawText);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private static DesignExportDto BuildDesign(string? name, string rawText)
    {
        var elements = new List<ElementDto>();
        double y = MarginY;
        int seq = 0;

        foreach (string line in rawText.Split(['\r', '\n', '\x0D', '\x07'], StringSplitOptions.RemoveEmptyEntries))
        {
            string text = line.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            double fontSize = 12;
            double lineH    = fontSize * 1.5;

            elements.Add(new ElementDto
            {
                Id      = $"p-{seq++}",
                Type    = "text",
                X       = MarginX,
                Y       = Math.Round(y, 1),
                Width   = PageWidth - MarginX * 2,
                Height  = Math.Round(lineH, 1),
                Content = text,
                Style   = new Dictionary<string, object>
                {
                    ["fontSize"]   = fontSize,
                    ["color"]      = "#000000",
                    ["fontWeight"] = "normal",
                    ["textAlign"]  = "left",
                },
            });

            y += lineH;
            if (y + lineH > PageHeight - MarginY)
                y = MarginY; // wrap to next logical section (single page for simplicity)
        }

        return new DesignExportDto
        {
            Id    = Guid.NewGuid().ToString("N")[..12],
            Name  = name ?? "Imported DOC",
            Pages = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = [],
            PageSettings  = new PageSettingsDto
            {
                Width       = PageWidth,
                Height      = PageHeight,
                Orientation = "portrait",
                Margins     = new MarginsDto { Top = MarginY, Right = MarginX, Bottom = MarginY, Left = MarginX },
            },
        };
    }

    // ── CFBF parsing ──────────────────────────────────────────────────────────

    private static int[] BuildFat(byte[] data, int[] fatSectors, int count, int sectorSize)
    {
        var fat = new List<int>();
        for (int s = 0; s < count; s++)
        {
            int sec = fatSectors[s];
            if (sec < 0) continue;
            int off = (sec + 1) * sectorSize;
            int entries = sectorSize / 4;
            for (int e = 0; e < entries && off + e * 4 + 3 < data.Length; e++)
                fat.Add(ReadI32(data, off + e * 4));
        }
        return [.. fat];
    }

    private sealed record DirEntry(string Name, int StartSector, int Size);

    private static List<DirEntry> ReadDirectory(byte[] data, int[] fat, int startSector, int sectorSize)
    {
        var entries = new List<DirEntry>();
        int sector = startSector;
        while (sector >= 0 && sector < fat.Length)
        {
            int off = (sector + 1) * sectorSize;
            for (int i = 0; i + 128 <= sectorSize && off + i + 127 < data.Length; i += 128)
            {
                int nameLen  = ReadU16(data, off + i + 64);
                if (nameLen < 2 || nameLen > 64) continue;
                string entName = Encoding.Unicode.GetString(data, off + i, nameLen - 2);
                int startSec   = ReadI32(data, off + i + 116);
                int size       = ReadI32(data, off + i + 120);
                entries.Add(new DirEntry(entName, startSec, size));
            }
            sector = sector < fat.Length ? fat[sector] : -1;
        }
        return entries;
    }

    private static byte[] ReadStream(byte[] data, int[] fat, int startSector, int size, int sectorSize)
    {
        var buf = new byte[Math.Max(0, size)];
        int written = 0;
        int sector  = startSector;
        while (sector >= 0 && sector < fat.Length && written < size)
        {
            int off   = (sector + 1) * sectorSize;
            int chunk = Math.Min(sectorSize, size - written);
            if (off + chunk > data.Length) chunk = data.Length - off;
            if (chunk <= 0) break;
            Array.Copy(data, off, buf, written, chunk);
            written += chunk;
            sector = fat[sector];
        }
        return buf;
    }

    private static string ExtractPrintableText(byte[] data)
    {
        // Fallback: collect runs of printable ASCII characters (≥4 in a row)
        var sb = new StringBuilder();
        int run = 0;
        var runBuf = new StringBuilder();
        foreach (byte b in data)
        {
            if (b >= 32 && b < 127)
            {
                runBuf.Append((char)b);
                run++;
            }
            else
            {
                if (run >= 4) { sb.Append(runBuf); sb.Append('\n'); }
                runBuf.Clear();
                run = 0;
            }
        }
        if (run >= 4) sb.Append(runBuf);
        return sb.ToString();
    }

    // ── Binary helpers ────────────────────────────────────────────────────────

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static int ReadI32(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

    private static int ReadU16(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8);
}
