namespace PXA.Pdf;

public sealed class PdfNamedDestinationInfo
{
    public required string Name { get; init; }

    public required int PageNumber { get; init; }

    public double? Y { get; init; }
}
