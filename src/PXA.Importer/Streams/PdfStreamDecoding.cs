using System.IO.Compression;
using PXA.Importer.Objects;

namespace PXA.Importer.Streams;

public interface IPdfStreamDecoder
{
    string FilterName { get; }
    bool CanDecode(PdfName filterName);
    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary);
}

public enum PdfStreamDecoderSupportStatus
{
    Supported,
    Deferred,
    Unknown
}

public sealed record PdfStreamDecoderSupport(string FilterName, PdfStreamDecoderSupportStatus Status, string? Notes = null);

public sealed class PdfStreamDecoderRegistry
{
    private static readonly IReadOnlyDictionary<string, string> DeferredFilters = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["JBIG2Decode"] = "Deferred until an external JBIG2 decoder dependency is selected.",
        ["JPXDecode"] = "Deferred until a JPEG2000 decoder dependency is selected."
    };

    private readonly List<IPdfStreamDecoder> _decoders =
    [
        new FlateDecodeStreamDecoder(),
        new AsciiHexStreamDecoder(),
        new Ascii85StreamDecoder(),
        new LzwStreamDecoder(),
        new CcittFaxStreamDecoder()
    ];

    public void Add(IPdfStreamDecoder decoder) => _decoders.Add(decoder);

    public IReadOnlyList<PdfStreamDecoderSupport> Evaluate(PdfObject? filter)
    {
        return filter switch
        {
            PdfName name => [EvaluateOne(name)],
            PdfArray array => array.Items.OfType<PdfName>().Select(EvaluateOne).ToArray(),
            _ => []
        };
    }

    public ReadOnlyMemory<byte> Decode(PdfStreamObject stream)
    {
        var filter = stream.Dictionary["Filter"];
        if (filter is PdfName name)
        {
            return DecodeOne(name, stream.EncodedBytes, stream.Dictionary, filterIndex: 0);
        }

        if (filter is PdfArray array)
        {
            var current = stream.EncodedBytes;
            var filterIndex = 0;
            foreach (var filterName in array.Items.OfType<PdfName>())
            {
                current = DecodeOne(filterName, current, stream.Dictionary, filterIndex);
                filterIndex++;
            }

            return current;
        }

        return stream.EncodedBytes;
    }

    private ReadOnlyMemory<byte> DecodeOne(PdfName filterName, ReadOnlyMemory<byte> encodedBytes, PdfDictionary dictionary, int filterIndex)
    {
        var decoder = _decoders.FirstOrDefault(candidate => candidate.CanDecode(filterName));
        if (decoder is not null)
        {
            return decoder.Decode(encodedBytes, CreateFilterDictionary(dictionary, filterIndex));
        }

        var support = EvaluateOne(filterName);
        if (support.Status == PdfStreamDecoderSupportStatus.Deferred)
        {
            throw new NotSupportedException($"Stream filter '{support.FilterName}' is deferred. {support.Notes}");
        }

        return encodedBytes;
    }

    private PdfStreamDecoderSupport EvaluateOne(PdfName filterName)
    {
        if (_decoders.Any(candidate => candidate.CanDecode(filterName)))
        {
            return new PdfStreamDecoderSupport(filterName.Value, PdfStreamDecoderSupportStatus.Supported);
        }

        return DeferredFilters.TryGetValue(filterName.Value, out var notes)
            ? new PdfStreamDecoderSupport(filterName.Value, PdfStreamDecoderSupportStatus.Deferred, notes)
            : new PdfStreamDecoderSupport(filterName.Value, PdfStreamDecoderSupportStatus.Unknown);
    }

    private static PdfDictionary CreateFilterDictionary(PdfDictionary dictionary, int filterIndex)
    {
        var values = new Dictionary<string, PdfObject>(dictionary.Values, StringComparer.Ordinal);
        var decodeParms = dictionary["DecodeParms"];
        if (decodeParms is PdfArray array && filterIndex < array.Items.Count)
        {
            values["DecodeParms"] = array.Items[filterIndex];
        }

        return new PdfDictionary(values);
    }
}

public sealed class FlateDecodeStreamDecoder : IPdfStreamDecoder
{
    public string FilterName => "FlateDecode";

    public bool CanDecode(PdfName filterName) => filterName.Value is "FlateDecode" or "Fl";

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary)
    {
        using var input = new MemoryStream(encodedBytes.ToArray());
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return PdfPredictor.Decode(output.ToArray(), streamDictionary);
    }
}

public sealed class AsciiHexStreamDecoder : IPdfStreamDecoder
{
    public string FilterName => "ASCIIHexDecode";

    public bool CanDecode(PdfName filterName) => filterName.Value is "ASCIIHexDecode" or "AHx";

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary)
    {
        var output = new List<byte>(encodedBytes.Length / 2);
        var highNibble = -1;
        foreach (var b in encodedBytes.Span)
        {
            if (b == (byte)'>')
            {
                break;
            }

            if (char.IsWhiteSpace((char)b))
            {
                continue;
            }

            var value = HexValue(b);
            if (value < 0)
            {
                continue;
            }

            if (highNibble < 0)
            {
                highNibble = value;
            }
            else
            {
                output.Add((byte)((highNibble << 4) | value));
                highNibble = -1;
            }
        }

        if (highNibble >= 0)
        {
            output.Add((byte)(highNibble << 4));
        }

        return PdfPredictor.Decode(output.ToArray(), streamDictionary);
    }

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
        >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
        _ => -1
    };
}

public sealed class Ascii85StreamDecoder : IPdfStreamDecoder
{
    public string FilterName => "ASCII85Decode";

    public bool CanDecode(PdfName filterName) => filterName.Value is "ASCII85Decode" or "A85";

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary)
    {
        var output = new List<byte>(encodedBytes.Length);
        Span<byte> tuple = stackalloc byte[5];
        var count = 0;

        foreach (var b in encodedBytes.Span)
        {
            if (char.IsWhiteSpace((char)b))
            {
                continue;
            }

            if (b == (byte)'~')
            {
                break;
            }

            if (b == (byte)'z' && count == 0)
            {
                output.AddRange([0, 0, 0, 0]);
                continue;
            }

            if (b < (byte)'!' || b > (byte)'u')
            {
                continue;
            }

            tuple[count++] = (byte)(b - 33);
            if (count == 5)
            {
                WriteTuple(tuple, 4, output);
                count = 0;
            }
        }

        if (count > 0)
        {
            for (var i = count; i < 5; i++)
            {
                tuple[i] = 84;
            }

            WriteTuple(tuple, count - 1, output);
        }

        return output.ToArray();
    }

    private static void WriteTuple(ReadOnlySpan<byte> tuple, int bytesToWrite, List<byte> output)
    {
        uint value = 0;
        for (var i = 0; i < 5; i++)
        {
            value = value * 85 + tuple[i];
        }

        Span<byte> bytes = stackalloc byte[4];
        bytes[0] = (byte)(value >> 24);
        bytes[1] = (byte)(value >> 16);
        bytes[2] = (byte)(value >> 8);
        bytes[3] = (byte)value;

        for (var i = 0; i < bytesToWrite; i++)
        {
            output.Add(bytes[i]);
        }
    }
}

internal static class PdfPredictor
{
    public static ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> decodedBytes, PdfDictionary streamDictionary)
    {
        var parameters = streamDictionary["DecodeParms"] as PdfDictionary;
        if (parameters is null || !TryGetInteger(parameters["Predictor"], out var predictor) || predictor <= 1)
        {
            return decodedBytes;
        }

        var colors = GetInteger(parameters["Colors"], 1);
        var bitsPerComponent = GetInteger(parameters["BitsPerComponent"], 8);
        var columns = GetInteger(parameters["Columns"], 1);
        var rowLength = Math.Max(1, (colors * bitsPerComponent * columns + 7) / 8);
        var bytesPerPixel = Math.Max(1, (colors * bitsPerComponent + 7) / 8);

        return predictor switch
        {
            2 => DecodeTiff(decodedBytes.Span, rowLength, bytesPerPixel),
            >= 10 and <= 15 => DecodePng(decodedBytes.Span, rowLength, bytesPerPixel),
            _ => decodedBytes
        };
    }

    private static ReadOnlyMemory<byte> DecodeTiff(ReadOnlySpan<byte> source, int rowLength, int bytesPerPixel)
    {
        var output = source.ToArray();
        for (var rowStart = 0; rowStart < output.Length; rowStart += rowLength)
        {
            var rowEnd = Math.Min(rowStart + rowLength, output.Length);
            for (var index = rowStart + bytesPerPixel; index < rowEnd; index++)
            {
                output[index] = unchecked((byte)(output[index] + output[index - bytesPerPixel]));
            }
        }

        return output;
    }

    private static ReadOnlyMemory<byte> DecodePng(ReadOnlySpan<byte> source, int rowLength, int bytesPerPixel)
    {
        var output = new List<byte>(source.Length);
        var previousRow = new byte[rowLength];
        var offset = 0;

        while (offset < source.Length)
        {
            var filter = source[offset++];
            var available = Math.Min(rowLength, source.Length - offset);
            var row = source.Slice(offset, available).ToArray();
            offset += available;

            switch (filter)
            {
                case 0:
                    break;
                case 1:
                    DecodeSub(row, bytesPerPixel);
                    break;
                case 2:
                    DecodeUp(row, previousRow);
                    break;
                case 3:
                    DecodeAverage(row, previousRow, bytesPerPixel);
                    break;
                case 4:
                    DecodePaeth(row, previousRow, bytesPerPixel);
                    break;
            }

            output.AddRange(row);
            Array.Clear(previousRow);
            row.CopyTo(previousRow, 0);
        }

        return output.ToArray();
    }

    private static void DecodeSub(Span<byte> row, int bytesPerPixel)
    {
        for (var i = bytesPerPixel; i < row.Length; i++)
        {
            row[i] = unchecked((byte)(row[i] + row[i - bytesPerPixel]));
        }
    }

    private static void DecodeUp(Span<byte> row, ReadOnlySpan<byte> previousRow)
    {
        for (var i = 0; i < row.Length; i++)
        {
            row[i] = unchecked((byte)(row[i] + previousRow[i]));
        }
    }

    private static void DecodeAverage(Span<byte> row, ReadOnlySpan<byte> previousRow, int bytesPerPixel)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            var up = previousRow[i];
            row[i] = unchecked((byte)(row[i] + ((left + up) / 2)));
        }
    }

    private static void DecodePaeth(Span<byte> row, ReadOnlySpan<byte> previousRow, int bytesPerPixel)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            var up = previousRow[i];
            var upperLeft = i >= bytesPerPixel ? previousRow[i - bytesPerPixel] : 0;
            row[i] = unchecked((byte)(row[i] + PaethPredictor(left, up, upperLeft)));
        }
    }

    private static int PaethPredictor(int left, int up, int upperLeft)
    {
        var estimate = left + up - upperLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceUp = Math.Abs(estimate - up);
        var distanceUpperLeft = Math.Abs(estimate - upperLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpperLeft)
        {
            return left;
        }

        return distanceUp <= distanceUpperLeft ? up : upperLeft;
    }

    private static int GetInteger(PdfObject? value, int fallback)
    {
        return TryGetInteger(value, out var integer) ? checked((int)integer) : fallback;
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
}

public sealed class CcittFaxStreamDecoder : IPdfStreamDecoder
{
    private readonly record struct HuffmanCode(uint Value, int Length, int Run);

    private static readonly HuffmanCode[] WhiteTermCodes =
    [
        new(0b00110101, 8, 0),   new(0b000111, 6, 1),    new(0b0111, 4, 2),      new(0b1000, 4, 3),
        new(0b1011, 4, 4),       new(0b1100, 4, 5),       new(0b1110, 4, 6),      new(0b1111, 4, 7),
        new(0b10011, 5, 8),      new(0b10100, 5, 9),      new(0b00111, 5, 10),    new(0b01000, 5, 11),
        new(0b001000, 6, 12),    new(0b000011, 6, 13),    new(0b110100, 6, 14),   new(0b110101, 6, 15),
        new(0b101010, 6, 16),    new(0b101011, 6, 17),
        new(0b0100111, 7, 18),   new(0b0001100, 7, 19),   new(0b0001000, 7, 20),  new(0b0010111, 7, 21),
        new(0b0000011, 7, 22),   new(0b0000100, 7, 23),   new(0b0101000, 7, 24),  new(0b0101011, 7, 25),
        new(0b0010011, 7, 26),   new(0b0100100, 7, 27),   new(0b0011000, 7, 28),
        new(0b00000010, 8, 29),  new(0b00000011, 8, 30),  new(0b00011010, 8, 31), new(0b00011011, 8, 32),
        new(0b00010010, 8, 33),  new(0b00010011, 8, 34),  new(0b00010100, 8, 35), new(0b00010101, 8, 36),
        new(0b00010110, 8, 37),  new(0b00010111, 8, 38),  new(0b00101000, 8, 39), new(0b00101001, 8, 40),
        new(0b00101010, 8, 41),  new(0b00101011, 8, 42),  new(0b00101100, 8, 43), new(0b00101101, 8, 44),
        new(0b00000100, 8, 45),  new(0b00000101, 8, 46),  new(0b00001010, 8, 47), new(0b00001011, 8, 48),
        new(0b01010010, 8, 49),  new(0b01010011, 8, 50),  new(0b01010100, 8, 51), new(0b01010101, 8, 52),
        new(0b00100100, 8, 53),  new(0b00100101, 8, 54),  new(0b01011000, 8, 55), new(0b01011001, 8, 56),
        new(0b01011010, 8, 57),  new(0b01011011, 8, 58),  new(0b01001010, 8, 59), new(0b01001011, 8, 60),
        new(0b00110010, 8, 61),  new(0b00110011, 8, 62),  new(0b00110100, 8, 63),
    ];

    private static readonly HuffmanCode[] WhiteMakeUpCodes =
    [
        new(0b11011, 5, 64),        new(0b10010, 5, 128),       new(0b010111, 6, 192),
        new(0b011000, 6, 1664),     new(0b0110111, 7, 256),
        new(0b00110110, 8, 320),    new(0b00110111, 8, 384),    new(0b01100100, 8, 448),
        new(0b01100101, 8, 512),    new(0b01101000, 8, 576),    new(0b01100111, 8, 640),
        new(0b011001100, 9, 704),   new(0b011001101, 9, 768),   new(0b011010010, 9, 832),
        new(0b011010011, 9, 896),   new(0b011010100, 9, 960),   new(0b011010101, 9, 1024),
        new(0b011010110, 9, 1088),  new(0b011010111, 9, 1152),  new(0b011011000, 9, 1216),
        new(0b011011001, 9, 1280),  new(0b011011010, 9, 1344),  new(0b011011011, 9, 1408),
        new(0b010011000, 9, 1472),  new(0b010011001, 9, 1536),  new(0b010011010, 9, 1600),
        new(0b010011011, 9, 1728),
    ];

    private static readonly HuffmanCode[] BlackTermCodes =
    [
        new(0b0000110111, 10, 0),   new(0b010, 3, 1),           new(0b11, 2, 2),            new(0b10, 2, 3),
        new(0b011, 3, 4),           new(0b0011, 4, 5),           new(0b0010, 4, 6),          new(0b00011, 5, 7),
        new(0b000101, 6, 8),        new(0b000100, 6, 9),         new(0b0000100, 7, 10),      new(0b0000101, 7, 11),
        new(0b0000111, 7, 12),      new(0b00000100, 8, 13),      new(0b00000111, 8, 14),
        new(0b000011000, 9, 15),
        new(0b0000010111, 10, 16),  new(0b0000011000, 10, 17),  new(0b0000001000, 10, 18),
        new(0b00001100111, 11, 19), new(0b00001101000, 11, 20), new(0b00001101100, 11, 21),
        new(0b00000110111, 11, 22), new(0b00000101000, 11, 23), new(0b00000010111, 11, 24),
        new(0b00000011000, 11, 25),
        new(0b000011001010, 12, 26), new(0b000011001011, 12, 27), new(0b000011001100, 12, 28),
        new(0b000011001101, 12, 29), new(0b000001101000, 12, 30), new(0b000001101001, 12, 31),
        new(0b000001101010, 12, 32), new(0b000001101011, 12, 33), new(0b000011010010, 12, 34),
        new(0b000011010011, 12, 35), new(0b000011010100, 12, 36), new(0b000011010101, 12, 37),
        new(0b000011010110, 12, 38), new(0b000011010111, 12, 39), new(0b000001101100, 12, 40),
        new(0b000001101101, 12, 41), new(0b000011011010, 12, 42), new(0b000011011011, 12, 43),
        new(0b000001010100, 12, 44), new(0b000001010101, 12, 45), new(0b000001010110, 12, 46),
        new(0b000001010111, 12, 47), new(0b000001100100, 12, 48), new(0b000001100101, 12, 49),
        new(0b000001010010, 12, 50), new(0b000001010011, 12, 51), new(0b000000100100, 12, 52),
        new(0b000000110111, 12, 53), new(0b000000111000, 12, 54), new(0b000000100111, 12, 55),
        new(0b000000101000, 12, 56), new(0b000001011000, 12, 57), new(0b000001011001, 12, 58),
        new(0b000000101011, 12, 59), new(0b000000101100, 12, 60), new(0b000001011010, 12, 61),
        new(0b000001100110, 12, 62), new(0b000001100111, 12, 63),
    ];

    private static readonly HuffmanCode[] BlackMakeUpCodes =
    [
        new(0b0000001111, 10, 64),
        new(0b000011001000, 12, 128),    new(0b000011001001, 12, 192),
        new(0b000001011011, 12, 256),    new(0b000000110011, 12, 320),
        new(0b000000110100, 12, 384),    new(0b000000110101, 12, 448),
        new(0b0000001101100, 13, 512),   new(0b0000001101101, 13, 576),
        new(0b0000001001010, 13, 640),   new(0b0000001001011, 13, 704),
        new(0b0000001001100, 13, 768),   new(0b0000001001101, 13, 832),
        new(0b0000001110010, 13, 896),   new(0b0000001110011, 13, 960),
        new(0b0000001110100, 13, 1024),  new(0b0000001110101, 13, 1088),
        new(0b0000001110110, 13, 1152),  new(0b0000001110111, 13, 1216),
        new(0b0000001010010, 13, 1280),  new(0b0000001010011, 13, 1344),
        new(0b0000001010100, 13, 1408),  new(0b0000001010101, 13, 1472),
        new(0b0000001011010, 13, 1536),  new(0b0000001011011, 13, 1600),
        new(0b0000001100100, 13, 1664),  new(0b0000001100101, 13, 1728),
    ];

    private static readonly HuffmanCode[] CommonMakeUpCodes =
    [
        new(0b00000001000, 11, 1792),    new(0b00000001100, 11, 1856),    new(0b00000001101, 11, 1920),
        new(0b000000010010, 12, 1984),   new(0b000000010011, 12, 2048),   new(0b000000010100, 12, 2112),
        new(0b000000010101, 12, 2176),   new(0b000000010110, 12, 2240),   new(0b000000010111, 12, 2304),
        new(0b000000011100, 12, 2368),   new(0b000000011101, 12, 2432),   new(0b000000011110, 12, 2496),
        new(0b000000011111, 12, 2560),
    ];

    // 2D mode codes for Group 4
    private static readonly (uint Code, int Length, int Offset, bool IsPass, bool IsHorizontal)[] TwoDModeCodes =
    [
        (0b1,       1, 0,  false, false), // V0
        (0b011,     3, 1,  false, false), // VR1
        (0b010,     3, -1, false, false), // VL1
        (0b001,     3, 0,  false, true),  // Horizontal
        (0b0001,    4, 0,  true,  false), // Pass
        (0b000011,  6, 2,  false, false), // VR2
        (0b000010,  6, -2, false, false), // VL2
        (0b0000011, 7, 3,  false, false), // VR3
        (0b0000010, 7, -3, false, false), // VL3
    ];

    private static readonly Dictionary<uint, (int Run, bool IsTerminating)>[] _whiteLookup;
    private static readonly Dictionary<uint, (int Run, bool IsTerminating)>[] _blackLookup;

    static CcittFaxStreamDecoder()
    {
        _whiteLookup = BuildLookup(WhiteTermCodes, WhiteMakeUpCodes);
        _blackLookup = BuildLookup(BlackTermCodes, BlackMakeUpCodes);
    }

    private static Dictionary<uint, (int Run, bool IsTerminating)>[] BuildLookup(
        HuffmanCode[] terminating, HuffmanCode[] makeUp)
    {
        var tables = new Dictionary<uint, (int, bool)>[14];
        for (var i = 0; i < 14; i++)
            tables[i] = new Dictionary<uint, (int, bool)>();

        foreach (var code in terminating)
            tables[code.Length][code.Value] = (code.Run, true);

        foreach (var code in makeUp)
            tables[code.Length][code.Value] = (code.Run, false);

        foreach (var code in CommonMakeUpCodes)
        {
            tables[code.Length].TryAdd(code.Value, (code.Run, false));
        }

        return tables;
    }

    public string FilterName => "CCITTFaxDecode";
    public bool CanDecode(PdfName filterName) => filterName.Value is "CCITTFaxDecode" or "CCF";

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary)
    {
        var parms = streamDictionary["DecodeParms"] as PdfDictionary;
        var k = GetInt(parms, "K", 0);
        var columns = GetInt(parms, "Columns", 1728);
        var rows = GetInt(parms, "Rows", 0);
        var encodedByteAlign = GetBool(parms, "EncodedByteAlign", false);
        var endOfBlock = GetBool(parms, "EndOfBlock", true);
        var blackIs1 = GetBool(parms, "BlackIs1", false);

        var reader = new CcittBitReader(encodedBytes.Span);
        var rowBytes = (columns + 7) / 8;
        var output = new List<byte>(rowBytes * Math.Max(1, rows));
        var decodedRows = 0;

        if (k < 0)
        {
            var refRow = new bool[columns];
            while (!reader.IsEnd)
            {
                if (endOfBlock && IsEofb(ref reader)) break;
                var row = DecodeRow2D(ref reader, refRow, columns);
                AppendPackedRow(output, row, rowBytes, blackIs1);
                Array.Copy(row, refRow, columns);
                decodedRows++;
                if (rows > 0 && decodedRows >= rows) break;
            }
        }
        else
        {
            var endOfLine = GetBool(parms, "EndOfLine", false);
            while (!reader.IsEnd)
            {
                if (encodedByteAlign) reader.AlignToByte();
                if (endOfLine)
                {
                    SkipFillAndEol(ref reader, endOfBlock, out var isRtc);
                    if (isRtc) break;
                }
                if (reader.IsEnd) break;
                var row = DecodeRow1D(ref reader, columns);
                AppendPackedRow(output, row, rowBytes, blackIs1);
                decodedRows++;
                if (rows > 0 && decodedRows >= rows) break;
            }
        }

        return output.ToArray();
    }

    private static bool[] DecodeRow1D(ref CcittBitReader reader, int columns)
    {
        var row = new bool[columns];
        var pos = 0;
        var isBlack = false;

        while (pos < columns)
        {
            var run = DecodeRun(ref reader, isBlack ? _blackLookup : _whiteLookup);
            if (run < 0) break;
            var end = Math.Min(pos + run, columns);
            if (isBlack)
                Array.Fill(row, true, pos, end - pos);
            pos = end;
            isBlack = !isBlack;
        }

        return row;
    }

    private static bool[] DecodeRow2D(ref CcittBitReader reader, bool[] refRow, int columns)
    {
        var row = new bool[columns];
        var a0 = -1;
        var currentColor = false; // white

        while (a0 < columns)
        {
            if (!TryReadTwoDMode(ref reader, out var offset, out var isPass, out var isHorizontal))
                break;

            if (isPass)
            {
                var b1 = FindB1(refRow, a0, currentColor, columns);
                var b2 = FindB2(refRow, b1, columns);
                Fill(row, a0 + 1, b2, currentColor);
                a0 = b2;
            }
            else if (isHorizontal)
            {
                var run1 = DecodeRun(ref reader, currentColor ? _blackLookup : _whiteLookup);
                var run2 = DecodeRun(ref reader, currentColor ? _whiteLookup : _blackLookup);
                if (run1 < 0 || run2 < 0) break;
                Fill(row, a0 + 1, a0 + 1 + run1, currentColor);
                Fill(row, a0 + 1 + run1, a0 + 1 + run1 + run2, !currentColor);
                a0 += run1 + run2;
            }
            else
            {
                var b1 = FindB1(refRow, a0, currentColor, columns);
                var a1 = Math.Clamp(b1 + offset, 0, columns);
                Fill(row, a0 + 1, a1, currentColor);
                a0 = a1;
                currentColor = !currentColor;
            }
        }

        return row;
    }

    private static int DecodeRun(ref CcittBitReader reader, Dictionary<uint, (int Run, bool IsTerminating)>[] lookup)
    {
        var total = 0;
        while (true)
        {
            var found = false;
            for (var len = 2; len <= 13 && !found; len++)
            {
                if (!reader.TryPeek(len, out var val)) return total;
                if (!lookup[len].TryGetValue(val, out var entry)) continue;
                reader.Advance(len);
                total += entry.Run;
                if (entry.IsTerminating) return total;
                found = true;
            }
            if (!found) return total;
        }
    }

    private static bool TryReadTwoDMode(ref CcittBitReader reader, out int offset, out bool isPass, out bool isHorizontal)
    {
        offset = 0; isPass = false; isHorizontal = false;
        for (var len = 1; len <= 7; len++)
        {
            if (!reader.TryPeek(len, out var val)) return false;
            foreach (var mode in TwoDModeCodes)
            {
                if (mode.Length != len || mode.Code != val) continue;
                reader.Advance(len);
                offset = mode.Offset;
                isPass = mode.IsPass;
                isHorizontal = mode.IsHorizontal;
                return true;
            }
        }
        return false;
    }

    private static int FindB1(bool[] refRow, int a0, bool currentColor, int columns)
    {
        var targetColor = !currentColor;
        var start = Math.Max(0, a0 + 1);
        for (var i = start; i < columns; i++)
        {
            var prev = i > 0 ? refRow[i - 1] : false;
            if (refRow[i] == targetColor && refRow[i] != prev) return i;
        }
        return columns;
    }

    private static int FindB2(bool[] refRow, int b1, int columns)
    {
        if (b1 >= columns) return columns;
        var b1Color = refRow[b1];
        for (var i = b1 + 1; i < columns; i++)
        {
            if (refRow[i] != b1Color) return i;
        }
        return columns;
    }

    private static void Fill(bool[] row, int from, int to, bool color)
    {
        var start = Math.Max(0, from);
        var end = Math.Min(to, row.Length);
        if (end > start && color)
            Array.Fill(row, true, start, end - start);
    }

    private static bool IsEofb(ref CcittBitReader reader)
    {
        const uint EolCode = 1u;
        const int EolLength = 12;
        if (!reader.TryPeek(EolLength * 2, out var val)) return false;
        var eol1 = val >> EolLength;
        var eol2 = val & ((1u << EolLength) - 1);
        if (eol1 != EolCode || eol2 != EolCode) return false;
        reader.Advance(EolLength * 2);
        return true;
    }

    private static void SkipFillAndEol(ref CcittBitReader reader, bool endOfBlock, out bool isRtc)
    {
        isRtc = false;
        const uint EolCode = 1u;
        const int EolLength = 12;
        var eolCount = 0;

        // Skip fill bits (leading zeros up to an EOL) then the EOL itself
        while (!reader.IsEnd)
        {
            if (reader.TryPeek(EolLength, out var val) && val == EolCode)
            {
                reader.Advance(EolLength);
                eolCount++;
                if (!endOfBlock || eolCount < 6) return;
            }
            else if (reader.TryPeek(1, out var bit) && bit == 0)
            {
                reader.Advance(1);
            }
            else
            {
                break;
            }
        }

        if (endOfBlock && eolCount >= 6) isRtc = true;
    }

    private static void AppendPackedRow(List<byte> output, bool[] row, int rowBytes, bool blackIs1)
    {
        for (var b = 0; b < rowBytes; b++)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                var px = b * 8 + bit;
                if (px >= row.Length) break;
                var isBlack = row[px];
                var bitOn = blackIs1 ? isBlack : !isBlack;
                if (bitOn) value |= (byte)(0x80 >> bit);
            }
            output.Add(value);
        }
    }

    private static int GetInt(PdfDictionary? parms, string key, int fallback)
    {
        if (parms is null) return fallback;
        return parms[key] switch
        {
            PdfInteger i => (int)i.Value,
            PdfNumber n => (int)n.Value,
            _ => fallback
        };
    }

    private static bool GetBool(PdfDictionary? parms, string key, bool fallback)
    {
        if (parms is null) return fallback;
        return parms[key] switch
        {
            PdfBoolean b => b.Value,
            _ => fallback
        };
    }
}

internal ref struct CcittBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _bitPos;

    public CcittBitReader(ReadOnlySpan<byte> data) { _data = data; _bitPos = 0; }

    public bool IsEnd => _bitPos >= _data.Length * 8;

    public bool TryPeek(int count, out uint value)
    {
        value = 0;
        if (_bitPos + count > _data.Length * 8) return false;
        for (var i = 0; i < count; i++)
        {
            var abs = _bitPos + i;
            value = (value << 1) | (uint)((_data[abs >> 3] >> (7 - (abs & 7))) & 1);
        }
        return true;
    }

    public void Advance(int count) => _bitPos += count;

    public void AlignToByte()
    {
        if (_bitPos % 8 != 0)
            _bitPos = (_bitPos / 8 + 1) * 8;
    }
}

public sealed class LzwStreamDecoder : IPdfStreamDecoder
{
    public string FilterName => "LZWDecode";

    public bool CanDecode(PdfName filterName) => filterName.Value is "LZWDecode" or "LZW";

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary)
    {
        var reader = new MostSignificantBitReader(encodedBytes.Span);
        var table = CreateInitialTable();
        var output = new List<byte>(encodedBytes.Length * 2);
        var codeSize = 9;
        var nextCode = 258;
        byte[]? previous = null;

        while (reader.TryRead(codeSize, out var code))
        {
            if (code == 257)
            {
                break;
            }

            if (code == 256)
            {
                table = CreateInitialTable();
                codeSize = 9;
                nextCode = 258;
                previous = null;
                continue;
            }

            byte[] entry;
            if (table.TryGetValue(code, out var existing))
            {
                entry = existing;
            }
            else if (code == nextCode && previous is not null)
            {
                entry = [.. previous, previous[0]];
            }
            else
            {
                break;
            }

            output.AddRange(entry);

            if (previous is not null && nextCode < 4096)
            {
                table[nextCode++] = [.. previous, entry[0]];
                if (nextCode == (1 << codeSize) - 1 && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previous = entry;
        }

        return output.ToArray();
    }

    private static Dictionary<int, byte[]> CreateInitialTable()
    {
        var table = new Dictionary<int, byte[]>(4096);
        for (var i = 0; i < 256; i++)
        {
            table[i] = [(byte)i];
        }

        return table;
    }

    private ref struct MostSignificantBitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bitOffset;

        public MostSignificantBitReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _bitOffset = 0;
        }

        public bool TryRead(int bitCount, out int value)
        {
            value = 0;
            if (_bitOffset + bitCount > _data.Length * 8)
            {
                return false;
            }

            for (var i = 0; i < bitCount; i++)
            {
                var absoluteBit = _bitOffset++;
                var byteIndex = absoluteBit / 8;
                var bitIndex = 7 - absoluteBit % 8;
                value = (value << 1) | ((_data[byteIndex] >> bitIndex) & 1);
            }

            return true;
        }
    }
}
