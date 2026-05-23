namespace Canvas.Pdf.Layout;

internal sealed record ImageElement(
    Canvas.Pdf.PdfImageData Image,
    string CacheKey,
    double X,
    double Y,
    double Width,
    double Height,
    double Opacity = 1,
    bool ClipToBounds = false,
    double? ClipX = null,
    double? ClipY = null,
    double? ClipWidth = null,
    double? ClipHeight = null) : PdfPageElement;
