namespace Canvas.FileImporter.ImageAnalysis.Analysis;

public sealed class ImageAnalysisOptions
{
    public bool IncludeDebugOverlay { get; init; }
    public bool IncludeFallbackImageLayer { get; init; }
    public double LowConfidenceThreshold { get; init; } = 0.50;
    public double? SourceDpiX { get; init; }
    public double? SourceDpiY { get; init; }

    public static ImageAnalysisOptions Default { get; } = new();
}
