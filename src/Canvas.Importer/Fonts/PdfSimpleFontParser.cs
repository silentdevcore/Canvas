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

        return new PdfFontResource
        {
            ResourceName = resourceName,
            Kind = kind,
            Dictionary = fontDictionary,
            Widths = ReadWidths(fontDictionary, resolver),
            MissingWidth = ReadMissingWidth(fontDictionary, resolver)
        };
    }

    private static IReadOnlyDictionary<int, double> ReadWidths(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
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

    private static double ReadMissingWidth(PdfDictionary fontDictionary, IPdfObjectResolver resolver)
    {
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