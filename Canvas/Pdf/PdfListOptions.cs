namespace Canvas.Pdf;

public sealed class PdfListOptions
{
    public static PdfListOptions Default { get; } = new();

    public bool Ordered { get; init; }

    public int StartIndex { get; init; } = 1;

    public string NumberFormat { get; init; } = "{0}.";

    public string Bullet { get; init; } = "•";

    public double FontSize { get; init; } = 11;

    public double? LineHeight { get; init; }

    public double Indent { get; init; } = 16;

    public double MarkerWidth { get; init; }

    public double MarkerGap { get; init; } = 6;

    public bool AlignMarkersRight { get; init; } = true;

    public double ItemSpacing { get; init; } = 4;

    public PdfTextAlignment ItemAlignment { get; init; } = PdfTextAlignment.Left;

    public PdfStandardFont? Font { get; init; }

    public PdfFontFamily? FontFamily { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public PdfStandardFont? MarkerFont { get; init; }

    public PdfFontFamily? MarkerFontFamily { get; init; }

    public bool MarkerBold { get; init; }

    public bool MarkerItalic { get; init; }

    public double? MarkerFontSize { get; init; }

    public IPdfColor? FillColor { get; init; }

    public IPdfColor? MarkerColor { get; init; }
}
