namespace PVAD.Pdf.Layout;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]
internal sealed record PolygonElement(
    IReadOnlyList<PdfPoint> Points,
    PdfStrokeStyle StrokeStyle,
    bool Stroke,
    bool Fill,
    IPdfColor StrokeColor,
    IPdfColor FillColor) : PdfPageElement;
