using Canvas.Importer.Objects;

namespace Canvas.Importer.Fonts;

public sealed class PdfSimpleFontParser : IPdfFontParser
{
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

        return new PdfFontResource
        {
            ResourceName = resourceName,
            Kind = kind,
            Dictionary = fontDictionary,
            Widths = ReadWidths(widthSource, subtype, resolver),
            MissingWidth = ReadMissingWidth(widthSource, resolver),
            CodeByteLength = subtype == "Type0" ? 2 : 1
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
}