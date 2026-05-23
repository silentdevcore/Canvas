namespace Canvas.Pdf;

public static class PdfPageSizes
{
    // A4 in PDF points (1/72 inch)
    public const double A4Width = 595;
    public const double A4Height = 842;

    public const double A3Width = 842;
    public const double A3Height = 1191;

    public const double LetterWidth = 612;
    public const double LetterHeight = 792;

    public static (double Width, double Height) Landscape(double width, double height)
    {
        return (Math.Max(width, height), Math.Min(width, height));
    }
}
