using System.Globalization;
using System.Text;
using PVAD.Importer.Parsing;
using PVAD.Licensing;

namespace PVAD.Importer.Fonts;

public sealed class PdfToUnicodeCMapParser
{
    /// <summary>Character codes in a ToUnicode CMap span one to four bytes.</summary>
    private const int MaximumSourceCodeBytes = 4;

    /// <summary>
    /// Maximum code mappings expanded from one ToUnicode CMap. A complete two-byte code
    /// space has 65,536 codes; real fonts stay far below this bound, while a single hostile
    /// <c>bfrange</c> could otherwise expand billions of entries before any decoding begins.
    /// </summary>
    internal const int MaximumMappingCount = 262_144;

    public PdfToUnicodeMap Parse(ReadOnlyMemory<byte> cmapBytes)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfImport);
        return ParseAdmitted(cmapBytes);
    }

    internal PdfToUnicodeMap ParseAdmitted(ReadOnlyMemory<byte> cmapBytes)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        var map = new PdfToUnicodeMap();
        var budget = new MappingBudget();
        var lines = Encoding.ASCII.GetString(cmapBytes.Span)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.EndsWith("beginbfchar", StringComparison.Ordinal))
            {
                var count = ParseSectionCount(line, "beginbfchar");
                ParseBfChar(lines, ref index, count, map, budget);
                continue;
            }

            if (line.EndsWith("beginbfrange", StringComparison.Ordinal))
            {
                var count = ParseSectionCount(line, "beginbfrange");
                ParseBfRange(lines, ref index, count, map, budget);
            }
        }

        return map;
    }

    private static int ParseSectionCount(string line, string marker)
    {
        var prefix = line[..line.IndexOf(marker, StringComparison.Ordinal)].Trim();
        return int.TryParse(prefix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
    }

    private static void ParseBfChar(string[] lines, ref int index, int count, PdfToUnicodeMap map, MappingBudget budget)
    {
        for (var item = 0; item < count && index + 1 < lines.Length; item++)
        {
            var tokens = Tokenize(lines[++index]);
            if (tokens.Length < 2)
            {
                continue;
            }

            var sourceBytes = ParseSourceCode(tokens[0]);
            budget.Reserve(1);
            map.Add((int)ParseCode(sourceBytes), sourceBytes.Length, DecodeUnicode(ParseHexBytes(tokens[1])));
        }
    }

    private static void ParseBfRange(string[] lines, ref int index, int count, PdfToUnicodeMap map, MappingBudget budget)
    {
        for (var item = 0; item < count && index + 1 < lines.Length; item++)
        {
            var line = lines[++index];
            var tokens = Tokenize(line);
            if (tokens.Length < 3)
            {
                continue;
            }

            var startBytes = ParseSourceCode(tokens[0]);
            var startCode = ParseCode(startBytes);
            var endCode = ParseCode(ParseSourceCode(tokens[1]));
            var byteLength = startBytes.Length;

            if (tokens[2].StartsWith("<", StringComparison.Ordinal))
            {
                var targetBytes = ParseHexBytes(tokens[2]);
                if (endCode >= startCode)
                {
                    budget.Reserve(endCode - startCode + 1);
                }

                for (var sourceCode = startCode; sourceCode <= endCode; sourceCode++)
                {
                    map.Add((int)sourceCode, byteLength, DecodeUnicode(targetBytes));
                    IncrementHexBytes(targetBytes);
                }

                continue;
            }

            if (!line.Contains('[', StringComparison.Ordinal) || !line.Contains(']', StringComparison.Ordinal))
            {
                continue;
            }

            var values = ExtractHexTokens(line[(line.IndexOf('[', StringComparison.Ordinal) + 1)..line.IndexOf(']', StringComparison.Ordinal)]);
            for (var offset = 0; offset < values.Count && startCode + offset <= endCode; offset++)
            {
                budget.Reserve(1);
                map.Add((int)(startCode + offset), byteLength, DecodeUnicode(ParseHexBytes(values[offset])));
            }
        }
    }

    private static string[] Tokenize(string line)
    {
        return line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static List<string> ExtractHexTokens(string content)
    {
        var values = new List<string>();
        var index = 0;

        while (index < content.Length)
        {
            var start = content.IndexOf('<', index);
            if (start < 0)
            {
                break;
            }

            var end = content.IndexOf('>', start + 1);
            if (end < 0)
            {
                break;
            }

            values.Add(content[start..(end + 1)]);
            index = end + 1;
        }

        return values;
    }

    private static byte[] ParseHexBytes(string token)
    {
        var hex = token.Trim();
        if (hex.StartsWith("<", StringComparison.Ordinal) && hex.EndsWith(">", StringComparison.Ordinal))
        {
            hex = hex[1..^1];
        }

        if (hex.Length % 2 != 0)
        {
            hex += "0";
        }

        var bytes = new byte[hex.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(hex.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                throw new FormatException("A ToUnicode CMap contains an invalid hexadecimal code.");
            }
        }

        return bytes;
    }

    private static byte[] ParseSourceCode(string token)
    {
        var bytes = ParseHexBytes(token);
        if (bytes.Length is < 1 or > MaximumSourceCodeBytes)
        {
            throw new FormatException("A ToUnicode CMap source code must span one to four bytes.");
        }

        return bytes;
    }

    private static long ParseCode(ReadOnlySpan<byte> bytes)
    {
        long code = 0;
        foreach (var value in bytes)
        {
            code = (code << 8) | value;
        }

        return code;
    }

    private static string DecodeUnicode(byte[] bytes)
    {
        return bytes.Length == 0 ? string.Empty : Encoding.BigEndianUnicode.GetString(bytes);
    }

    private sealed class MappingBudget
    {
        private long _used;

        public void Reserve(long count)
        {
            _used += count;
            if (_used > MaximumMappingCount)
            {
                throw new PdfParseLimitExceededException(
                    "PVADPDFPARSE012",
                    $"A ToUnicode CMap exceeds the maximum of {MaximumMappingCount} code mappings.");
            }
        }
    }

    private static void IncrementHexBytes(byte[] bytes)
    {
        for (var index = bytes.Length - 1; index >= 0; index--)
        {
            if (bytes[index] == byte.MaxValue)
            {
                bytes[index] = 0;
                continue;
            }

            bytes[index]++;
            return;
        }
    }
}
