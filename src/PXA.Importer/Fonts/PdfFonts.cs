using PXA.Importer.Objects;
using System.Text;

namespace PXA.Importer.Fonts;

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
    public string? BaseFontName { get; init; }
    public PdfDictionary Dictionary { get; init; } = new();
    public PdfEncodingMap Encoding { get; init; } = PdfEncodingMap.Identity;
    public PdfToUnicodeMap? ToUnicode { get; init; }
    public IReadOnlyDictionary<int, double> Widths { get; init; } = new Dictionary<int, double>();
    public double MissingWidth { get; init; }
    public int CodeByteLength { get; init; } = 1;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public int? FontDescriptorFlags { get; init; }
    public double? FontWeight { get; init; }
    public double? ItalicAngle { get; init; }
    public bool IsSubset { get; init; }
    public ReadOnlyMemory<byte> EmbeddedFontBytes { get; init; }
    public string? EmbeddedFontFormat { get; init; }
    public string? EmbeddedFontMimeType { get; init; }

    public string Decode(ReadOnlySpan<byte> glyphBytes)
    {
        return ToUnicode?.Decode(glyphBytes) ?? Encoding.Decode(glyphBytes);
    }

    public double GetGlyphWidth(int code)
    {
        return Widths.TryGetValue(code, out var width) ? width : MissingWidth;
    }

    public IReadOnlyList<int> GetGlyphCodes(ReadOnlySpan<byte> glyphBytes)
    {
        var byteLength = Math.Max(1, CodeByteLength);
        var codes = new List<int>((glyphBytes.Length + byteLength - 1) / byteLength);
        for (var index = 0; index < glyphBytes.Length; index += byteLength)
        {
            var remaining = Math.Min(byteLength, glyphBytes.Length - index);
            var code = 0;
            for (var offset = 0; offset < remaining; offset++)
            {
                code = (code << 8) | glyphBytes[index + offset];
            }

            codes.Add(code);
        }

        return codes;
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
    private readonly Dictionary<(int Code, int ByteLength), string> _glyphs = [];

    public int MaxCodeLength { get; private set; } = 1;

    public void Add(int code, string unicodeText) => Add(code, 1, unicodeText);

    public void Add(int code, int byteLength, string unicodeText)
    {
        _glyphs[(code, byteLength)] = unicodeText;
        MaxCodeLength = Math.Max(MaxCodeLength, byteLength);
    }

    public override string Decode(ReadOnlySpan<byte> glyphBytes)
    {
        var builder = new StringBuilder(glyphBytes.Length);
        var index = 0;

        while (index < glyphBytes.Length)
        {
            var mapped = false;
            var maxLength = Math.Min(4, glyphBytes.Length - index);

            for (var byteLength = maxLength; byteLength >= 1; byteLength--)
            {
                var code = 0;
                for (var offset = 0; offset < byteLength; offset++)
                {
                    code = (code << 8) | glyphBytes[index + offset];
                }

                if (!_glyphs.TryGetValue((code, byteLength), out var unicodeText))
                {
                    continue;
                }

                builder.Append(unicodeText);
                index += byteLength;
                mapped = true;
                break;
            }

            if (mapped)
            {
                continue;
            }

            builder.Append((char)glyphBytes[index]);
            index++;
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
