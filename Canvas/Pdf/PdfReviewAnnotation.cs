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
    double Opacity,
    IReadOnlyList<PdfMarkupQuadPoint> QuadPoints);

public sealed record PdfMarkupQuadPoint(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double X3,
    double Y3,
    double X4,
    double Y4);
