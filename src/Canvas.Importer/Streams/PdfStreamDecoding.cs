using System.IO.Compression;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Streams;

public interface IPdfStreamDecoder
{
    string FilterName { get; }
    bool CanDecode(PdfName filterName);
    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> encodedBytes, PdfDictionary streamDictionary);
}

public sealed class PdfStreamDecoderRegistry
{
    private readonly List<IPdfStreamDecoder> _decoders =
    [
        new FlateDecodeStreamDecoder(),
        new AsciiHexStreamDecoder(),
        new Ascii85StreamDecoder(),
        new LzwStreamDecoder()
    ];

    public void Add(IPdfStreamDecoder decoder) => _decoders.Add(decoder);

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
        return decoder is null ? encodedBytes : decoder.Decode(encodedBytes, CreateFilterDictionary(dictionary, filterIndex));
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
