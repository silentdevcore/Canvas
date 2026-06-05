namespace Canvas.FileImporter.ImageOcr;

public interface IOcrEngine
{
    string Name { get; }
    string Version { get; }
    Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default);
}

public sealed record OcrImagePage(
    int PageIndex,
    int WidthPx,
    int HeightPx,
    byte[] EncodedImageBytes);
