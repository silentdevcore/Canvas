namespace PVAD.Pdf;

/// <summary>Sparse table-cell overrides. Unset values inherit the table defaults.</summary>
public sealed class PdfTableCellStyle
{
    public int Row { get; init; }
    public int Column { get; init; }
    public IPdfColor? BackgroundColor { get; init; }
    public IPdfColor? TextColor { get; init; }
    public PdfTextAlignment? Alignment { get; init; }
    public double? Padding { get; init; }
    public PdfFontFamily? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public IPdfColor? BorderColor { get; init; }
    public double? BorderWidth { get; init; }
    public PdfTableCellBorder? BorderTop { get; init; }
    public PdfTableCellBorder? BorderRight { get; init; }
    public PdfTableCellBorder? BorderBottom { get; init; }
    public PdfTableCellBorder? BorderLeft { get; init; }

    internal PdfTableCellStyle AtRow(int row) => new()
    {
        Row = row, Column = Column, BackgroundColor = BackgroundColor, TextColor = TextColor,
        Alignment = Alignment, Padding = Padding, FontFamily = FontFamily, FontSize = FontSize,
        Bold = Bold, Italic = Italic, BorderColor = BorderColor, BorderWidth = BorderWidth,
        BorderTop = BorderTop, BorderRight = BorderRight, BorderBottom = BorderBottom, BorderLeft = BorderLeft
    };
}

/// <summary>A cell-side border; zero width explicitly suppresses the side.</summary>
public sealed class PdfTableCellBorder
{
    public IPdfColor? Color { get; init; }
    public double Width { get; init; } = 1;
}
