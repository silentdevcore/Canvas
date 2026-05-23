namespace Canvas.Pdf.Layout;

internal sealed record TextElement(
    string Text,
    double X,
    double Y,
    double FontSize,
    PdfStandardFont Font,
    double WordSpacing = 0,
    IPdfColor? FillColor = null,
    double RotationDegrees = 0,
    bool Underline = false,
    bool Strikethrough = false,
    double CharacterSpacing = 0,
    double HorizontalScalingPercent = 100) : PdfPageElement;
