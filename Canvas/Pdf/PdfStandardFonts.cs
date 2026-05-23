namespace Canvas.Pdf;

public static class PdfStandardFonts
{
    public static PdfStandardFont FromStyle(PdfFontFamily family, bool bold = false, bool italic = false)
    {
        return family switch
        {
            PdfFontFamily.Helvetica => (bold, italic) switch
            {
                (false, false) => PdfStandardFont.Helvetica,
                (true, false) => PdfStandardFont.HelveticaBold,
                (false, true) => PdfStandardFont.HelveticaOblique,
                (true, true) => PdfStandardFont.HelveticaBoldOblique
            },
            PdfFontFamily.Times => (bold, italic) switch
            {
                (false, false) => PdfStandardFont.TimesRoman,
                (true, false) => PdfStandardFont.TimesBold,
                (false, true) => PdfStandardFont.TimesItalic,
                (true, true) => PdfStandardFont.TimesBoldItalic
            },
            PdfFontFamily.Courier => (bold, italic) switch
            {
                (false, false) => PdfStandardFont.Courier,
                (true, false) => PdfStandardFont.CourierBold,
                (false, true) => PdfStandardFont.CourierOblique,
                (true, true) => PdfStandardFont.CourierBoldOblique
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported font family.")
        };
    }
}
