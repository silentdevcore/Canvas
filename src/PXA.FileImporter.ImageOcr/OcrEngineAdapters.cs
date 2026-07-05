namespace PXA.FileImporter.ImageOcr;

internal sealed class CanvasOcrEngineAdapter : Canvas.FileImporter.ImageOcr.IOcrEngine
{
    private readonly IOcrEngine inner;

    public CanvasOcrEngineAdapter(IOcrEngine inner)
    {
        this.inner = inner;
    }

    public string Name => inner.Name;

    public string Version => inner.Version;

    public async Task<IReadOnlyList<Canvas.FileImporter.ImageOcr.OcrPage>> RecognizeAsync(
        IReadOnlyList<Canvas.FileImporter.ImageOcr.OcrImagePage> pages,
        Canvas.FileImporter.ImageOcr.ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var pxaPages = pages
            .Select(p => new OcrImagePage(p.PageIndex, p.WidthPx, p.HeightPx, p.EncodedImageBytes))
            .ToArray();
        var pxaOptions = ImageToPdfConversionOptionsMapper.FromCanvas(options);
        var result = await inner.RecognizeAsync(pxaPages, pxaOptions, cancellationToken);
        return result.Select(OcrModelMapper.ToCanvas).ToArray();
    }
}

internal sealed class PxaOcrEngineAdapter : IOcrEngine
{
    private readonly Canvas.FileImporter.ImageOcr.IOcrEngine inner;

    public PxaOcrEngineAdapter(Canvas.FileImporter.ImageOcr.IOcrEngine inner)
    {
        this.inner = inner;
    }

    public string Name => inner.Name;

    public string Version => inner.Version;

    public async Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var canvasPages = pages.Select(OcrModelMapper.ToCanvas).ToArray();
        var result = await inner.RecognizeAsync(canvasPages, options.ToCanvasOptions(), cancellationToken);
        return result.Select(OcrModelMapper.FromCanvas).ToArray();
    }
}

internal static class ImageToPdfConversionOptionsMapper
{
    public static ImageToPdfConversionOptions FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfConversionOptions options) => new()
    {
        Languages = options.Languages,
        NativeLibraryPath = options.NativeLibraryPath,
        SourceDpiX = options.SourceDpiX,
        SourceDpiY = options.SourceDpiY,
        PageWidthPt = options.PageWidthPt,
        PageHeightPt = options.PageHeightPt,
        PageSizingMode = options.PageSizingMode,
        IncludeBackgroundImage = options.IncludeBackgroundImage,
        IncludeDiagnostics = options.IncludeDiagnostics,
        IncludeDebugOverlay = options.IncludeDebugOverlay,
        EnablePreprocessing = options.EnablePreprocessing,
        PreprocessGrayscale = options.PreprocessGrayscale,
        PreprocessContrast = options.PreprocessContrast,
        PreprocessBinarize = options.PreprocessBinarize,
        LowConfidenceThreshold = options.LowConfidenceThreshold,
        LayoutMode = options.LayoutMode,
        RuleContrastThreshold = options.RuleContrastThreshold,
        LightFillMinLuma = options.LightFillMinLuma,
        LightFillMaxSaturation = options.LightFillMaxSaturation,
        LightFillMinDistance = options.LightFillMinDistance,
        LightFillMaxDistance = options.LightFillMaxDistance,
        BackgroundFillMinColorDistance = options.BackgroundFillMinColorDistance,
        BackgroundFillMinAreaFraction = options.BackgroundFillMinAreaFraction,
        BackgroundFillMinWidthFraction = options.BackgroundFillMinWidthFraction,
        BackgroundFillMinCoverage = options.BackgroundFillMinCoverage,
        DetectionMaxPixels = options.DetectionMaxPixels,
        MaxOcrRuntimeSeconds = options.MaxOcrRuntimeSeconds,
        MaxFileBytes = options.MaxFileBytes,
        MaxPixels = options.MaxPixels,
    };
}
