using Canvas.Importer.Objects;
using Canvas.Importer.Streams;

namespace Canvas.Importer.Fonts;

public sealed class PdfSimpleFontParser : IPdfFontParser
{
    private readonly PdfStreamDecoderRegistry _streamDecoders;

    public PdfSimpleFontParser(PdfStreamDecoderRegistry? streamDecoders = null)
    {
        _streamDecoders = streamDecoders ?? new PdfStreamDecoderRegistry();
    }

    public PdfFontResource Parse(string resourceName, PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        var subtype = ResolveName(fontDictionary["Subtype"], resolver);
        var kind = subtype switch
        {
            "Type1" => PdfFontKind.Type1,
            "TrueType" => PdfFontKind.TrueType,
            "Type3" => PdfFontKind.Type3,
            "Type0" => PdfFontKind.Type0,
            "CIDFontType0" => PdfFontKind.CidType0,
            "CIDFontType2" => PdfFontKind.CidType2,
            _ => PdfFontKind.Unknown
        };

        var widthSource = GetWidthSourceDictionary(fontDictionary, subtype, resolver);
        var rawBaseFontName = ResolveName(fontDictionary["BaseFont"], resolver);
        if (rawBaseFontName.Length == 0)
        {
            rawBaseFontName = ResolveName(widthSource["BaseFont"], resolver);
        }

        if (rawBaseFontName.Length == 0)
        {
            rawBaseFontName = ResolveFontDescriptorName(widthSource, resolver) ??
                ResolveFontDescriptorName(fontDictionary, resolver) ??
                string.Empty;
        }

        var baseFontName = StripSubsetPrefix(rawBaseFontName.Length > 0 ? rawBaseFontName : null);
        var embeddedFont = ResolveEmbeddedFont(widthSource, resolver) ?? ResolveEmbeddedFont(fontDictionary, resolver);
        var descriptor = ResolveFontDescriptor(widthSource, resolver) ?? ResolveFontDescriptor(fontDictionary, resolver);
        var flags = ResolveInteger(descriptor?["Flags"], resolver);
        var fontWeight = ResolveNumber(descriptor?["FontWeight"], resolver);
        var italicAngle = ResolveNumber(descriptor?["ItalicAngle"], resolver);

        return new PdfFontResource
        {
            ResourceName = resourceName,
            Kind = kind,
            BaseFontName = baseFontName,
            Dictionary = fontDictionary,
            Widths = ReadWidths(widthSource, subtype, resolver),
            MissingWidth = ReadMissingWidth(widthSource, resolver),
            CodeByteLength = subtype == "Type0" ? 2 : 1,
            Bold = IsBold(baseFontName, fontWeight, flags),
            Italic = IsItalic(baseFontName, italicAngle, flags),
            FontDescriptorFlags = flags,
            FontWeight = fontWeight,
            ItalicAngle = italicAngle,
            EmbeddedFontBytes = embeddedFont?.Bytes ?? ReadOnlyMemory<byte>.Empty,
            EmbeddedFontFormat = embeddedFont?.Format,
            EmbeddedFontMimeType = embeddedFont?.MimeType
        };
    }

    private static PdfDictionary GetWidthSourceDictionary(PdfDictionary fontDictionary, string subtype, IPdfObjectResolver resolver)
    {
        if (subtype != "Type0")
        {
            return fontDictionary;
        }

        if (fontDictionary["DescendantFonts"] is not { } descendantFontsValue || resolver.Resolve(descendantFontsValue) is not PdfArray descendantFonts)
        {
            return fontDictionary;
        }

        return descendantFonts.Items.FirstOrDefault() is { } descendant && resolver.Resolve(descendant) is PdfDictionary descendantDictionary
            ? descendantDictionary
            : fontDictionary;
    }

    private static IReadOnlyDictionary<int, double> ReadWidths(PdfDictionary fontDictionary, string subtype, IPdfObjectResolver resolver)
    {
        return subtype == "Type0" ? ReadCidWidths(fontDictionary, resolver) : ReadSimpleWidths(fontDictionary, resolver);
    }

    private static IReadOnlyDictionary<int, double> ReadSimpleWidths(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        var firstChar = ResolveInteger(fontDictionary["FirstChar"], resolver) ?? 0;
        if (fontDictionary["Widths"] is not { } widthsValue || resolver.Resolve(widthsValue) is not PdfArray widthsArray)
        {
            return new Dictionary<int, double>();
        }

        var widths = new Dictionary<int, double>();
        for (var index = 0; index < widthsArray.Items.Count; index++)
        {
            if (ResolveNumber(widthsArray.Items[index], resolver) is not { } width)
            {
                continue;
            }

            widths[firstChar + index] = width;
        }

        return widths;
    }

    private static IReadOnlyDictionary<int, double> ReadCidWidths(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        if (fontDictionary["W"] is not { } widthsValue || resolver.Resolve(widthsValue) is not PdfArray widthsArray)
        {
            return new Dictionary<int, double>();
        }

        var widths = new Dictionary<int, double>();
        var index = 0;
        while (index < widthsArray.Items.Count)
        {
            var startCode = ResolveInteger(widthsArray.Items[index++], resolver);
            if (startCode is null || index >= widthsArray.Items.Count)
            {
                break;
            }

            if (resolver.Resolve(widthsArray.Items[index]) is PdfArray explicitWidths)
            {
                index++;
                for (var offset = 0; offset < explicitWidths.Items.Count; offset++)
                {
                    if (ResolveNumber(explicitWidths.Items[offset], resolver) is { } width)
                    {
                        widths[startCode.Value + offset] = width;
                    }
                }

                continue;
            }

            var endCode = ResolveInteger(widthsArray.Items[index++], resolver);
            if (endCode is null || index >= widthsArray.Items.Count)
            {
                break;
            }

            var rangeWidth = ResolveNumber(widthsArray.Items[index++], resolver);
            if (rangeWidth is null)
            {
                continue;
            }

            for (var code = startCode.Value; code <= endCode.Value; code++)
            {
                widths[code] = rangeWidth.Value;
            }
        }

        return widths;
    }

    private static double ReadMissingWidth(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        if (ResolveNumber(fontDictionary["DW"], resolver) is { } cidDefaultWidth)
        {
            return cidDefaultWidth;
        }

        if (fontDictionary["FontDescriptor"] is not { } descriptorValue || resolver.Resolve(descriptorValue) is not PdfDictionary descriptor)
        {
            return 0;
        }

        return ResolveNumber(descriptor["MissingWidth"], resolver) ?? 0;
    }

    private static string? ResolveFontDescriptorName(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        if (ResolveFontDescriptor(fontDictionary, resolver) is not { } descriptor)
        {
            return null;
        }

        var fontName = ResolveName(descriptor["FontName"], resolver);
        return fontName.Length > 0 ? fontName : null;
    }

    private static PdfDictionary? ResolveFontDescriptor(PdfDictionary? fontDictionary, IPdfObjectResolver resolver)
    {
        if (fontDictionary is null ||
            fontDictionary["FontDescriptor"] is not { } descriptorValue ||
            resolver.Resolve(descriptorValue) is not PdfDictionary descriptor)
        {
            return null;
        }

        return descriptor;
    }

    private EmbeddedFontAsset? ResolveEmbeddedFont(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
        if (ResolveFontDescriptor(fontDictionary, resolver) is not { } descriptor)
        {
            return null;
        }

        return ResolveEmbeddedFontStream(descriptor["FontFile2"], resolver, "truetype", "font/ttf") ??
            ResolveFontFile3(descriptor["FontFile3"], resolver);
    }

    private EmbeddedFontAsset? ResolveFontFile3(PdfObject? value, IPdfObjectResolver resolver)
    {
        if (value is null || resolver.Resolve(value) is not PdfStreamObject stream)
        {
            return null;
        }

        var subtype = ResolveName(stream.Dictionary["Subtype"], resolver);
        return subtype switch
        {
            "OpenType" => CreateEmbeddedFontAsset(stream, "opentype", "font/otf"),
            _ => null
        };
    }

    private EmbeddedFontAsset? ResolveEmbeddedFontStream(PdfObject? value, IPdfObjectResolver resolver, string format, string mimeType)
    {
        if (value is null || resolver.Resolve(value) is not PdfStreamObject stream)
        {
            return null;
        }

        return CreateEmbeddedFontAsset(stream, format, mimeType);
    }

    private EmbeddedFontAsset CreateEmbeddedFontAsset(PdfStreamObject stream, string format, string mimeType)
    {
        DecodeIfPossible(stream);
        var bytes = stream.IsDecoded ? stream.DecodedBytes : stream.EncodedBytes;
        return new EmbeddedFontAsset(bytes, format, mimeType);
    }

    private void DecodeIfPossible(PdfStreamObject stream)
    {
        if (stream.IsDecoded)
        {
            return;
        }

        try
        {
            stream.SetDecodedBytes(_streamDecoders.Decode(stream));
        }
        catch (NotSupportedException)
        {
        }
        catch (InvalidDataException)
        {
        }
    }

    private static string ResolveName(PdfObject? value, IPdfObjectResolver resolver)
    {
        return value is not null && resolver.Resolve(value) is PdfName name ? name.Value : string.Empty;
    }

    private static int? ResolveInteger(PdfObject? value, IPdfObjectResolver resolver)
    {
        return ResolveNumber(value, resolver) is { } number ? (int)number : null;
    }

    private static double? ResolveNumber(PdfObject? value, IPdfObjectResolver resolver)
    {
        return value is null ? null : resolver.Resolve(value) switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => null
        };
    }

    private static bool IsBold(string? baseFontName, double? fontWeight, int? flags)
    {
        return (fontWeight is >= 600d) ||
            ((flags ?? 0) & 0x00040000) != 0 ||
            (baseFontName is not null &&
                (baseFontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("-Bd", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("Semibold", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("Demi", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("Black", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsItalic(string? baseFontName, double? italicAngle, int? flags)
    {
        return (italicAngle is < 0d or > 0d) ||
            ((flags ?? 0) & 0x00000040) != 0 ||
            (baseFontName is not null &&
                (baseFontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("-It", StringComparison.OrdinalIgnoreCase) ||
                 baseFontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase)));
    }

    // PDF embedded fonts use a 6-uppercase-letter subset tag prefix: "ABCDEF+FontName"
    private static string? StripSubsetPrefix(string? name)
    {
        if (name is null) return null;
        var plus = name.IndexOf('+');
        return plus == 6 ? name[(plus + 1)..] : name;
    }

    private sealed record EmbeddedFontAsset(ReadOnlyMemory<byte> Bytes, string Format, string MimeType);
}
