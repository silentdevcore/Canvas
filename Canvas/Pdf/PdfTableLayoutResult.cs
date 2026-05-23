namespace Canvas.Pdf;

public sealed class PdfTableLayoutResult
{
    public required double X { get; init; }

    public required double TopY { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required double BottomY { get; init; }

    public required int RowCount { get; init; }

    public required int ColumnCount { get; init; }
}
