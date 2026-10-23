namespace PVAD.FileImporter.ImageOcr;

/// <summary>
/// OCR engine contract used by the PVADE image OCR converter.
/// </summary>
public interface IOcrEngine
{
    string Name { get; }
    string Version { get; }

    Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default);
}
