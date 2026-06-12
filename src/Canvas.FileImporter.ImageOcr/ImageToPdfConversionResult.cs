using Canvas.Core.Contracts;

namespace Canvas.FileImporter.ImageOcr;

public sealed class ImageToPdfConversionResult
{
    public required DesignExportDto Design { get; init; }
    public IReadOnlyList<OcrPage> OcrPages { get; init; } = [];
    public required ImageToPdfDiagnostics Diagnostics { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public byte[]? DebugOverlayPng { get; init; }
}

public sealed class ImageToPdfDiagnostics
{
    public int SourceWidthPx { get; init; }
    public int SourceHeightPx { get; init; }
    public double EffectiveDpiX { get; init; }
    public double EffectiveDpiY { get; init; }
    public double PageWidthPt { get; init; }
    public double PageHeightPt { get; init; }
    public bool PreprocessingApplied { get; init; }
    public double PreprocessingScaleFactor { get; init; } = 1;
    public IReadOnlyList<string> PreprocessingSteps { get; init; } = [];
    public int PageCount { get; init; } = 1;
    public string OcrEngine { get; init; } = "";
    public string OcrEngineVersion { get; init; } = "";
    public string Languages { get; init; } = "";
    public int WordCount { get; init; }
    public int LineCount { get; init; }
    public double AverageConfidence { get; init; }
    public int LowConfidenceWordCount { get; init; }
    public double RuntimeMs { get; init; }
    public long MemoryDeltaBytes { get; init; }
    public ImageToPdfLayoutDiagnostics Layout { get; init; } = new();
}

public sealed class ImageToPdfLayoutDiagnostics
{
    public ImageToPdfRuleDiagnostics Rules { get; init; } = new();
    public IReadOnlyList<ImageToPdfTableCandidateDiagnostics> TableCandidates { get; init; } = [];
}

public sealed class ImageToPdfRuleDiagnostics
{
    public int SegmentCount { get; init; }
    public int HorizontalSegmentCount { get; init; }
    public int VerticalSegmentCount { get; init; }
    public double AverageContrast { get; init; }
    public double MaxContrast { get; init; }
    public IReadOnlyList<ImageToPdfRuleSegmentDiagnostics> SampleSegments { get; init; } = [];
}

public sealed class ImageToPdfRuleSegmentDiagnostics
{
    public required string Orientation { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Length { get; init; }
    public double Contrast { get; init; }
}

public sealed class ImageToPdfTableCandidateDiagnostics
{
    public required string Detector { get; init; }
    public required string Status { get; init; }
    public string? RejectionReason { get; init; }
    public required string SourceBoundsPx { get; init; }
    public string? RuleBoundsPx { get; init; }
    public string? BackgroundBoundsPx { get; init; }
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<double> ColumnAnchors { get; init; } = [];
    public IReadOnlyList<double> RowAnchors { get; init; } = [];
}
