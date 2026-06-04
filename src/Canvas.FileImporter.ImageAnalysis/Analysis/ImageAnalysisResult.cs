using Canvas.Core.Contracts;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

public sealed class ImageAnalysisImportResult
{
    public required DesignExportDto Design { get; init; }
    public required ImageAnalysisDiagnostics Diagnostics { get; init; }
    public byte[]? DebugOverlayPng { get; init; }
}

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
}
