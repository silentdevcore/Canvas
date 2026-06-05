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
    public long MaxFileBytes { get; init; } = 25 * 1024 * 1024;
    public long MaxPixels { get; init; } = 40_000_000;
}
