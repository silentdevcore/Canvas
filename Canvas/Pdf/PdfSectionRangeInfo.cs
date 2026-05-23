namespace Canvas.Pdf;

public sealed class PdfSectionRangeInfo
{
    public required string Name { get; init; }

    public required int StartPageNumber { get; init; }

    public required int EndPageNumber { get; init; }

    public required int PageCount { get; init; }
}
