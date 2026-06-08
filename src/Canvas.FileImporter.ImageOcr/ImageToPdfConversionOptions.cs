namespace Canvas.FileImporter.ImageOcr;

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

    // Light-line / light-fill detection sensitivity (more sensitive than the original
    // hard-coded values so faint gray rules and subtle cell/region fills are picked up).
    public int RuleContrastThreshold { get; init; } = 12;     // rule-pixel min luma contrast
    public int LightFillMinLuma { get; init; } = 170;         // min luma to count as a light fill
    public double LightFillMaxSaturation { get; init; } = 0.30; // max saturation for a light fill
    public int LightFillMinDistance { get; init; } = 6;       // min color distance from page background
    public int LightFillMaxDistance { get; init; } = 110;     // max color distance from page background

    // Background-fill reconstruction ("text-background" layout mode): detects large colored
    // blocks (header bars, pills, cards, header-row shading) and reproduces them as colored
    // rectangles behind the text.
    public int BackgroundFillMinColorDistance { get; init; } = 14;   // min distance from page bg to count as a fill pixel
    public double BackgroundFillMinAreaFraction { get; init; } = 0.015; // min region area as a fraction of the image
    public double BackgroundFillMinWidthFraction { get; init; } = 0.12; // OR min region width as a fraction of image width
    public double BackgroundFillMinCoverage { get; init; } = 0.45;    // min fraction of the region's box that is fill-colored
    public long DetectionMaxPixels { get; init; } = 2_000_000;        // cap for downscaled visual detection

    public int MaxOcrRuntimeSeconds { get; init; } = 45;
    public long MaxFileBytes { get; init; } = 25 * 1024 * 1024;
    public long MaxPixels { get; init; } = 40_000_000;
}
