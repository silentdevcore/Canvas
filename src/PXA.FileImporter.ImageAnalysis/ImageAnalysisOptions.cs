namespace PXA.FileImporter.ImageAnalysis;

/// <summary>
/// Power Dox Automation options for image analysis import.
/// </summary>
public sealed class ImageAnalysisOptions
{
    public bool IncludeDebugOverlay { get; init; }
    public bool IncludeFallbackImageLayer { get; init; }
    public double LowConfidenceThreshold { get; init; } = 0.50;
    public double? SourceDpiX { get; init; }
    public double? SourceDpiY { get; init; }

    public static ImageAnalysisOptions Default { get; } = new();

    internal Canvas.FileImporter.ImageAnalysis.Analysis.ImageAnalysisOptions ToCanvasOptions() => new()
    {
        IncludeDebugOverlay = IncludeDebugOverlay,
        IncludeFallbackImageLayer = IncludeFallbackImageLayer,
        LowConfidenceThreshold = LowConfidenceThreshold,
        SourceDpiX = SourceDpiX,
        SourceDpiY = SourceDpiY,
    };
}
