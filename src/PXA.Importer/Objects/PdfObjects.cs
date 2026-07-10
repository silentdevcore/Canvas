using System.Collections.ObjectModel;

namespace PXA.Importer.Objects;

public abstract record PdfObject
{
    public PdfSourceSpan SourceSpan { get; set; }
    public PdfObjectId? OriginalId { get; init; }
}

public readonly record struct PdfObjectId(int Number, int Generation);

public readonly record struct PdfSourceSpan(long Offset, int Length);

public sealed record PdfName(string Value) : PdfObject;

public sealed record PdfString(ReadOnlyMemory<byte> Bytes, bool IsHex) : PdfObject
{
    public ReadOnlyMemory<byte> GetDecodedBytes()
    {
        if (!IsHex)
        {
            return Bytes;
        }

        var source = Bytes.Span;
        var hexLength = source.Length + source.Length % 2;
        var decoded = new byte[hexLength / 2];
        for (var index = 0; index < decoded.Length; index++)
        {
            var high = HexValue(source, index * 2);
            var low = HexValue(source, index * 2 + 1);
            decoded[index] = (byte)((high << 4) | low);
        }

        return decoded;
    }

    public string ToLatin1String() => System.Text.Encoding.Latin1.GetString(GetDecodedBytes().Span);

    private static int HexValue(ReadOnlySpan<byte> source, int index)
    {
        if (index >= source.Length)
        {
            return 0;
        }

        return source[index] switch
        {
            >= (byte)'0' and <= (byte)'9' => source[index] - (byte)'0',
            >= (byte)'A' and <= (byte)'F' => source[index] - (byte)'A' + 10,
            >= (byte)'a' and <= (byte)'f' => source[index] - (byte)'a' + 10,
            _ => 0
        };
    }
}

public sealed record PdfNumber(double Value) : PdfObject;

public sealed record PdfInteger(long Value) : PdfObject;

public sealed record PdfBoolean(bool Value) : PdfObject;

public sealed record PdfNull : PdfObject
{
    public static PdfNull Value { get; } = new();
}

public sealed record PdfReference(PdfObjectId Id) : PdfObject;

public sealed record PdfArray : PdfObject
{
    private readonly List<PdfObject> _items;

    public PdfArray(IEnumerable<PdfObject>? items = null)
    {
        _items = items is null ? [] : [.. items];
    }

    public IReadOnlyList<PdfObject> Items => _items;

    public void Add(PdfObject value) => _items.Add(value);
}

public sealed record PdfDictionary : PdfObject
{
    private readonly Dictionary<string, PdfObject> _values;

    public PdfDictionary(IDictionary<string, PdfObject>? values = null)
    {
        _values = values is null ? [] : new Dictionary<string, PdfObject>(values, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, PdfObject> Values => new ReadOnlyDictionary<string, PdfObject>(_values);

    public PdfObject? this[string key]
    {
        get => _values.GetValueOrDefault(key);
        set
        {
            if (value is null)
            {
                _values.Remove(key);
            }
            else
            {
                _values[key] = value;
            }
        }
    }
}

public sealed record PdfStreamObject : PdfObject
{
    public PdfStreamObject(PdfDictionary dictionary, ReadOnlyMemory<byte> encodedBytes)
    {
        Dictionary = dictionary;
        EncodedBytes = encodedBytes;
    }

    public PdfDictionary Dictionary { get; }
    public ReadOnlyMemory<byte> EncodedBytes { get; }
    public bool IsDecoded { get; private set; }
    public ReadOnlyMemory<byte> DecodedBytes { get; private set; }

    public void SetDecodedBytes(ReadOnlyMemory<byte> decodedBytes)
    {
        DecodedBytes = decodedBytes;
        IsDecoded = true;
    }
}

public sealed class PdfIndirectObject
{
    public PdfIndirectObject(PdfObjectId id, PdfObject value, PdfSourceSpan sourceSpan)
    {
        Id = id;
        Value = value with { OriginalId = id, SourceSpan = sourceSpan };
        SourceSpan = sourceSpan;
    }

    public PdfObjectId Id { get; }
    public PdfObject Value { get; set; }
    public PdfSourceSpan SourceSpan { get; }
}

public sealed class PdfObjectGraph
{
    private readonly Dictionary<PdfObjectId, PdfIndirectObject> _objects = [];

    public IReadOnlyDictionary<PdfObjectId, PdfIndirectObject> Objects => _objects;
    public PdfDictionary? Trailer { get; set; }

    public void Add(PdfIndirectObject indirectObject) => _objects[indirectObject.Id] = indirectObject;

    public PdfIndirectObject? Resolve(PdfObjectId id) => _objects.GetValueOrDefault(id);
}
