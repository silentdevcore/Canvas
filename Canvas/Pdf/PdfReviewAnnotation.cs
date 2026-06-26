namespace Canvas.Pdf;

public enum PdfReviewAnnotationType
{
    StickyNote,
    FreeText,
    Highlight,
    Underline,
    StrikeOut,
    Square,
    Circle,
    Redaction
}

internal sealed record PdfReviewAnnotation(
    PdfReviewAnnotationType Type,
    double X,
    double Y,
    double Width,
    double Height,
    string Contents,
    PdfColor Color,
    double Opacity);

