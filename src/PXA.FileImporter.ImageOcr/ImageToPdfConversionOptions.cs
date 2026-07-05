namespace PXA.FileImporter.ImageOcr;

/// <summary>
/// Power Dox Automation options for image OCR to editable design conversion.
/// </summary>
public sealed class ImageToPdfConversionOptions
{
    public string Languages { get; init; } = "deu+eng";
    public string? NativeLibraryPath { get; init; }
    public double? SourceDpiX { get; init; }
    public double? SourceDpiY { get; init; }
    public double? PageWidthPt { get; init; }
    public double? PageHeightPt { get; init; }
    public string PageSizingMode { get; init; } = "source-dpi";
    public bool IncludeBackgroundImage { get; init; } = true;
    public bool IncludeDiagnostics { get; init; }
    public bool IncludeDebugOverlay { get; init; }
    public bool EnablePreprocessing { get; init; }
    public bool PreprocessGrayscale { get; init; } = true;
    public bool PreprocessContrast { get; init; } = true;
    public bool PreprocessBinarize { get; init; }
    public double LowConfidenceThreshold { get; init; } = 0.50;
    public string LayoutMode { get; init; } = "structured";
    public int RuleContrastThreshold { get; init; } = 12;
    public int LightFillMinLuma { get; init; } = 170;
    public double LightFillMaxSaturation { get; init; } = 0.30;
    public int LightFillMinDistance { get; init; } = 6;
    public int LightFillMaxDistance { get; init; } = 110;
    public int BackgroundFillMinColorDistance { get; init; } = 14;
    public double BackgroundFillMinAreaFraction { get; init; } = 0.015;
    public double BackgroundFillMinWidthFraction { get; init; } = 0.12;
    public double BackgroundFillMinCoverage { get; init; } = 0.45;
    public long DetectionMaxPixels { get; init; } = 2_000_000;
    public int MaxOcrRuntimeSeconds { get; init; } = 45;
    public long MaxFileBytes { get; init; } = 25 * 1024 * 1024;
    public long MaxPixels { get; init; } = 40_000_000;

    internal Canvas.FileImporter.ImageOcr.ImageToPdfConversionOptions ToCanvasOptions() => new()
    {
        Languages = Languages,
        NativeLibraryPath = NativeLibraryPath,
        SourceDpiX = SourceDpiX,
        SourceDpiY = SourceDpiY,
        PageWidthPt = PageWidthPt,
        PageHeightPt = PageHeightPt,
        PageSizingMode = PageSizingMode,
        IncludeBackgroundImage = IncludeBackgroundImage,
        IncludeDiagnostics = IncludeDiagnostics,
        IncludeDebugOverlay = IncludeDebugOverlay,
        EnablePreprocessing = EnablePreprocessing,
        PreprocessGrayscale = PreprocessGrayscale,
        PreprocessContrast = PreprocessContrast,
        PreprocessBinarize = PreprocessBinarize,
        LowConfidenceThreshold = LowConfidenceThreshold,
        LayoutMode = LayoutMode,
        RuleContrastThreshold = RuleContrastThreshold,
        LightFillMinLuma = LightFillMinLuma,
        LightFillMaxSaturation = LightFillMaxSaturation,
        LightFillMinDistance = LightFillMinDistance,
        LightFillMaxDistance = LightFillMaxDistance,
        BackgroundFillMinColorDistance = BackgroundFillMinColorDistance,
        BackgroundFillMinAreaFraction = BackgroundFillMinAreaFraction,
        BackgroundFillMinWidthFraction = BackgroundFillMinWidthFraction,
        BackgroundFillMinCoverage = BackgroundFillMinCoverage,
        DetectionMaxPixels = DetectionMaxPixels,
        MaxOcrRuntimeSeconds = MaxOcrRuntimeSeconds,
        MaxFileBytes = MaxFileBytes,
        MaxPixels = MaxPixels,
    };
}
