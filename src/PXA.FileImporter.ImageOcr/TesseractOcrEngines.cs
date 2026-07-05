namespace PXA.FileImporter.ImageOcr;

/// <summary>
/// Power Dox Automation facade for the embedded Tesseract OCR engine.
/// </summary>
public sealed class EmbeddedTesseractOcrEngine : IOcrEngine
{
    private readonly PxaOcrEngineAdapter adapter;

    public EmbeddedTesseractOcrEngine(string? tessDataPath = null, string? nativeLibraryPath = null)
    {
        adapter = new PxaOcrEngineAdapter(
            new Canvas.FileImporter.ImageOcr.EmbeddedTesseractOcrEngine(tessDataPath, nativeLibraryPath));
    }

    public string Name => adapter.Name;

    public string Version => adapter.Version;

    public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default) =>
        adapter.RecognizeAsync(pages, options, cancellationToken);
}

/// <summary>
/// Power Dox Automation facade for the process-isolated Tesseract OCR engine.
/// </summary>
public sealed class ProcessIsolatedTesseractOcrEngine : IOcrEngine
{
    private readonly PxaOcrEngineAdapter adapter;

    public ProcessIsolatedTesseractOcrEngine(
        string? workerPath = null,
        string? tessDataPath = null,
        string? nativeLibraryPath = null,
        string? tempRoot = null)
    {
        adapter = new PxaOcrEngineAdapter(
            new Canvas.FileImporter.ImageOcr.ProcessIsolatedTesseractOcrEngine(
                workerPath,
                tessDataPath,
                nativeLibraryPath,
                tempRoot));
    }

    public string Name => adapter.Name;

    public string Version => adapter.Version;

    public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default) =>
        adapter.RecognizeAsync(pages, options, cancellationToken);
}
