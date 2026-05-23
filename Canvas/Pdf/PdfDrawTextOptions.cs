namespace Canvas.Pdf;

public sealed class PdfDrawTextOptions
{
    public static PdfDrawTextOptions Default { get; } = new();

    public double FontSize { get; init; } = 12;

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
