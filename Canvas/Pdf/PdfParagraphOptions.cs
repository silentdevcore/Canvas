namespace Canvas.Pdf;

public sealed class PdfParagraphOptions
{
    public static PdfParagraphOptions Default { get; } = new();

    public double FontSize { get; init; } = 12;

    public double? LineHeight { get; init; }

    public PdfTextAlignment Alignment { get; init; } = PdfTextAlignment.Left;

    public PdfStandardFont? Font { get; init; }

    public PdfFontFamily? FontFamily { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public IPdfColor? FillColor { get; init; }

    public double RotationDegrees { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public double CharacterSpacing { get; init; }

    public double HorizontalScalingPercent { get; init; } = 100;
}
