namespace Canvas.Pdf;

internal sealed record PdfLinkAnnotation(
    double X,
    double Y,
    double Width,
    double Height,
    string? Url,
    int? TargetPageNumber,
    string? NamedDestination)
{
    public static PdfLinkAnnotation ForUrl(double x, double y, double width, double height, string url)
    {
        return new PdfLinkAnnotation(x, y, width, height, url, null, null);
    }

    public static PdfLinkAnnotation ForPage(double x, double y, double width, double height, int targetPageNumber)
    {
        return new PdfLinkAnnotation(x, y, width, height, null, targetPageNumber, null);
    }

    public static PdfLinkAnnotation ForNamedDestination(double x, double y, double width, double height, string destinationName)
    {
        return new PdfLinkAnnotation(x, y, width, height, null, null, destinationName);
    }
}
