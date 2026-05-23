namespace Canvas.Pdf.Layout;

internal sealed record BezierCurveElement(
    PdfPoint Start,
    PdfPoint Control1,
    PdfPoint Control2,
    PdfPoint End,
    PdfStrokeStyle StrokeStyle,
    IPdfColor StrokeColor) : PdfPageElement;
