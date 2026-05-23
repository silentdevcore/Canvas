using Canvas.Importer.Objects;

namespace Canvas.Importer.Fonts;

public enum PdfFontKind
{
    Type1,
    TrueType,
    Type3,
    Type0,
    CidType0,
    CidType2,
    Unknown
}

public sealed class PdfFontResource
{
    public required string ResourceName { get; init; }
    public required PdfFontKind Kind { get; init; }
    public PdfDictionary Dictionary { get; init; } = new();
    public PdfEncodingMap Encoding { get; init; } = PdfEncodingMap.Identity;
    public PdfToUnicodeMap? ToUnicode { get; init; }

    public string Decode(ReadOnlySpan<byte> glyphBytes)
    {
        return ToUnicode?.Decode(glyphBytes) ?? Encoding.Decode(glyphBytes);
    }
}

public class PdfEncodingMap
{
    public static PdfEncodingMap Identity { get; } = new();

    public virtual string Decode(ReadOnlySpan<byte> glyphBytes)
    {
        return System.Text.Encoding.Latin1.GetString(glyphBytes);
    }
}

public sealed class PdfToUnicodeMap : PdfEncodingMap
{
    private readonly Dictionary<int, string> _glyphs = [];

    public void Add(int code, string unicodeText) => _glyphs[code] = unicodeText;

    public override string Decode(ReadOnlySpan<byte> glyphBytes)
    {
        var builder = new System.Text.StringBuilder(glyphBytes.Length);
        foreach (var value in glyphBytes)
        {
            builder.Append(_glyphs.GetValueOrDefault(value, ((char)value).ToString()));
        }

        return builder.ToString();
    }
}

public interface IPdfFontParser
{
    PdfFontResource Parse(string resourceName, PdfDictionary fontDictionary, IPdfObjectResolver resolver);
}

public interface IPdfObjectResolver
{
    PdfObject? Resolve(PdfObject value);
}
