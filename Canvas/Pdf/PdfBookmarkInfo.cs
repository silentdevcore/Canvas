namespace Canvas.Pdf;

public sealed class PdfBookmarkInfo
{
    public required string Title { get; init; }

    public required int PageNumber { get; init; }

    public required int Level { get; init; }
}
