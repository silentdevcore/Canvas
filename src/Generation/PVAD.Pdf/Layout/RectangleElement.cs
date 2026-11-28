namespace PVAD.Pdf.Layout;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]
internal sealed record RectangleElement(
    double X,
    double Y,
    double Width,
    double Height,
    PdfStrokeStyle StrokeStyle,
    bool Stroke,
    bool Fill,
    IPdfColor StrokeColor,
    IPdfColor FillColor) : PdfPageElement;
