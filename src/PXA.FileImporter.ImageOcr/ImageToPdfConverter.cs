namespace PXA.FileImporter.ImageOcr;

/// <summary>
/// Power Dox Automation facade for converting scanned images into editable design documents.
/// </summary>
public sealed class ImageToPdfConverter
{
    private readonly Canvas.FileImporter.ImageOcr.ImageToPdfConverter inner;

    public ImageToPdfConverter(IOcrEngine ocrEngine)
    {
        ArgumentNullException.ThrowIfNull(ocrEngine);
        inner = new Canvas.FileImporter.ImageOcr.ImageToPdfConverter(new CanvasOcrEngineAdapter(ocrEngine));
    }

    public async Task<ImageToPdfConversionResult> ConvertAsync(
        Stream stream,
        string? fileName,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await inner.ConvertAsync(stream, fileName, options.ToCanvasOptions(), cancellationToken);
        return ImageToPdfConversionResult.FromCanvas(result);
    }
}
