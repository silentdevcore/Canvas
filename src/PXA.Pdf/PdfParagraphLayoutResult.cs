namespace PXA.Pdf;

public sealed class PdfParagraphLayoutResult
{
    public required double X { get; init; }

    public required double TopY { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required double BottomY { get; init; }

    public required int LineCount { get; init; }
}
