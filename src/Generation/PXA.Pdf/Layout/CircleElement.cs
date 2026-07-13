namespace PXA.Pdf.Layout;

internal sealed record CircleElement(
    double CenterX,
    double CenterY,
    double Radius,
    PdfStrokeStyle StrokeStyle,
    bool Stroke,
    bool Fill,
    IPdfColor StrokeColor,
    IPdfColor FillColor) : PdfPageElement;
