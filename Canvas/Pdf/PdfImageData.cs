namespace Canvas.Pdf;

internal sealed class PdfImageData
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int BitsPerComponent { get; init; }

    public required string ColorSpaceName { get; init; }

    public required string FilterName { get; init; }

    public string? DecodeParameters { get; init; }

    public required byte[] Data { get; init; }

    public PdfImageData? SoftMask { get; init; }
}
