using Canvas.Core.Contracts;

namespace PXA.FileImporter.ImageAnalysis;

/// <summary>
/// Result of a Power Dox Automation image analysis import.
/// </summary>
public sealed class ImageAnalysisImportResult
{
    public required DesignExportDto Design { get; init; }
    public required ImageAnalysisDiagnostics Diagnostics { get; init; }
    public byte[]? DebugOverlayPng { get; init; }

    internal static ImageAnalysisImportResult FromCanvas(
        Canvas.FileImporter.ImageAnalysis.Analysis.ImageAnalysisImportResult result) => new()
    {
        Design = result.Design,
        Diagnostics = ImageAnalysisDiagnostics.FromCanvas(result.Diagnostics),
        DebugOverlayPng = result.DebugOverlayPng,
    };
}

/// <summary>
/// Diagnostics emitted by the Power Dox Automation image analysis importer.
/// </summary>
public sealed class ImageAnalysisDiagnostics
{
    public required int SourceWidthPx { get; init; }
    public required int SourceHeightPx { get; init; }
    public required int WorkingWidthPx { get; init; }
    public required int WorkingHeightPx { get; init; }
    public required double ScaleFactor { get; init; }
    public required int ColorRegionCount { get; init; }
    public required int ShapeCount { get; init; }
    public required int TextLineCount { get; init; }
    public required int WordCount { get; init; }
    public required int GlyphCount { get; init; }
    public required int LowConfidenceGlyphCount { get; init; }
    public required double LowConfidenceGlyphRate { get; init; }
    public required int ElementCount { get; init; }
    public required double RuntimeMs { get; init; }
    public required long MemoryDeltaBytes { get; init; }
    public required string GlyphTemplateProfile { get; init; }
    public required string RecognitionReadiness { get; init; }
    public required string RecognitionFidelityScope { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }

    internal static ImageAnalysisDiagnostics FromCanvas(
        Canvas.FileImporter.ImageAnalysis.Analysis.ImageAnalysisDiagnostics diagnostics) => new()
    {
        SourceWidthPx = diagnostics.SourceWidthPx,
        SourceHeightPx = diagnostics.SourceHeightPx,
        WorkingWidthPx = diagnostics.WorkingWidthPx,
        WorkingHeightPx = diagnostics.WorkingHeightPx,
        ScaleFactor = diagnostics.ScaleFactor,
        ColorRegionCount = diagnostics.ColorRegionCount,
        ShapeCount = diagnostics.ShapeCount,
        TextLineCount = diagnostics.TextLineCount,
        WordCount = diagnostics.WordCount,
        GlyphCount = diagnostics.GlyphCount,
        LowConfidenceGlyphCount = diagnostics.LowConfidenceGlyphCount,
        LowConfidenceGlyphRate = diagnostics.LowConfidenceGlyphRate,
        ElementCount = diagnostics.ElementCount,
        RuntimeMs = diagnostics.RuntimeMs,
        MemoryDeltaBytes = diagnostics.MemoryDeltaBytes,
        GlyphTemplateProfile = diagnostics.GlyphTemplateProfile,
        RecognitionReadiness = diagnostics.RecognitionReadiness,
        RecognitionFidelityScope = diagnostics.RecognitionFidelityScope,
        Warnings = diagnostics.Warnings,
    };
}
