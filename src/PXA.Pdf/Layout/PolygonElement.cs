namespace PXA.Pdf.Layout;

internal sealed record PolygonElement(
    IReadOnlyList<PdfPoint> Points,
    PdfStrokeStyle StrokeStyle,
    bool Stroke,
    bool Fill,
    IPdfColor StrokeColor,
    IPdfColor FillColor) : PdfPageElement;
