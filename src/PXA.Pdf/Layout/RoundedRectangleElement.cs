namespace PXA.Pdf.Layout;

internal sealed record RoundedRectangleElement(
    double X,
    double Y,
    double Width,
    double Height,
    double CornerRadius,
    PdfStrokeStyle StrokeStyle,
    bool Stroke,
    bool Fill,
    IPdfColor StrokeColor,
    IPdfColor FillColor) : PdfPageElement;
