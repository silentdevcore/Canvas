namespace Canvas.Pdf.Layout;

internal sealed record LineElement(
    double X1,
    double Y1,
    double X2,
    double Y2,
    PdfStrokeStyle StrokeStyle,
    IPdfColor StrokeColor) : PdfPageElement;
