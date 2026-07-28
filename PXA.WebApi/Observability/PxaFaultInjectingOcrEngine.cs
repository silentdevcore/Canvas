using PXA.FileImporter.ImageOcr;

namespace PXA.WebApi.Observability;

internal sealed class PxaFaultInjectingOcrEngine(
    IOcrEngine inner,
    int failureCount) : IOcrEngine
{
    private int attempts;

    public string Name => inner.Name;
    public string Version => inner.Version;

    public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref attempts) <= failureCount)
        {
            throw new InvalidOperationException(
                "Bounded non-production OCR failure injection is active.");
        }

        return inner.RecognizeAsync(pages, options, cancellationToken);
    }
}
