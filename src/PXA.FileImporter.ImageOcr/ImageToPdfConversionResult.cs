using Canvas.Core.Contracts;

namespace PXA.FileImporter.ImageOcr;

/// <summary>
/// Result of a Power Dox Automation image OCR conversion.
/// </summary>
public sealed class ImageToPdfConversionResult
{
    public required DesignExportDto Design { get; init; }
    public IReadOnlyList<OcrPage> OcrPages { get; init; } = [];
    public required ImageToPdfDiagnostics Diagnostics { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public byte[]? DebugOverlayPng { get; init; }

    internal static ImageToPdfConversionResult FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfConversionResult result) => new()
    {
        Design = result.Design,
        OcrPages = result.OcrPages.Select(OcrModelMapper.FromCanvas).ToArray(),
        Diagnostics = ImageToPdfDiagnostics.FromCanvas(result.Diagnostics),
        Warnings = result.Warnings,
        DebugOverlayPng = result.DebugOverlayPng,
    };
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

    internal static ImageToPdfDiagnostics FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfDiagnostics diagnostics) => new()
    {
        SourceWidthPx = diagnostics.SourceWidthPx,
        SourceHeightPx = diagnostics.SourceHeightPx,
        EffectiveDpiX = diagnostics.EffectiveDpiX,
        EffectiveDpiY = diagnostics.EffectiveDpiY,
        PageWidthPt = diagnostics.PageWidthPt,
        PageHeightPt = diagnostics.PageHeightPt,
        PreprocessingApplied = diagnostics.PreprocessingApplied,
        PreprocessingScaleFactor = diagnostics.PreprocessingScaleFactor,
        PreprocessingSteps = diagnostics.PreprocessingSteps,
        PageCount = diagnostics.PageCount,
        OcrEngine = diagnostics.OcrEngine,
        OcrEngineVersion = diagnostics.OcrEngineVersion,
        Languages = diagnostics.Languages,
        WordCount = diagnostics.WordCount,
        LineCount = diagnostics.LineCount,
        AverageConfidence = diagnostics.AverageConfidence,
        LowConfidenceWordCount = diagnostics.LowConfidenceWordCount,
        RuntimeMs = diagnostics.RuntimeMs,
        MemoryDeltaBytes = diagnostics.MemoryDeltaBytes,
        Layout = ImageToPdfLayoutDiagnostics.FromCanvas(diagnostics.Layout),
    };
}

public sealed class ImageToPdfLayoutDiagnostics
{
    public ImageToPdfRuleDiagnostics Rules { get; init; } = new();
    public IReadOnlyList<ImageToPdfTableCandidateDiagnostics> TableCandidates { get; init; } = [];

    internal static ImageToPdfLayoutDiagnostics FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfLayoutDiagnostics diagnostics) => new()
    {
        Rules = ImageToPdfRuleDiagnostics.FromCanvas(diagnostics.Rules),
        TableCandidates = diagnostics.TableCandidates.Select(ImageToPdfTableCandidateDiagnostics.FromCanvas).ToArray(),
    };
}

public sealed class ImageToPdfRuleDiagnostics
{
    public int SegmentCount { get; init; }
    public int HorizontalSegmentCount { get; init; }
    public int VerticalSegmentCount { get; init; }
    public double AverageContrast { get; init; }
    public double MaxContrast { get; init; }
    public IReadOnlyList<ImageToPdfRuleSegmentDiagnostics> SampleSegments { get; init; } = [];

    internal static ImageToPdfRuleDiagnostics FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfRuleDiagnostics diagnostics) => new()
    {
        SegmentCount = diagnostics.SegmentCount,
        HorizontalSegmentCount = diagnostics.HorizontalSegmentCount,
        VerticalSegmentCount = diagnostics.VerticalSegmentCount,
        AverageContrast = diagnostics.AverageContrast,
        MaxContrast = diagnostics.MaxContrast,
        SampleSegments = diagnostics.SampleSegments.Select(ImageToPdfRuleSegmentDiagnostics.FromCanvas).ToArray(),
    };
}

public sealed class ImageToPdfRuleSegmentDiagnostics
{
    public required string Orientation { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Length { get; init; }
    public double Contrast { get; init; }

    internal static ImageToPdfRuleSegmentDiagnostics FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfRuleSegmentDiagnostics diagnostics) => new()
    {
        Orientation = diagnostics.Orientation,
        X = diagnostics.X,
        Y = diagnostics.Y,
        Length = diagnostics.Length,
        Contrast = diagnostics.Contrast,
    };
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

    internal static ImageToPdfTableCandidateDiagnostics FromCanvas(
        Canvas.FileImporter.ImageOcr.ImageToPdfTableCandidateDiagnostics diagnostics) => new()
    {
        Detector = diagnostics.Detector,
        Status = diagnostics.Status,
        RejectionReason = diagnostics.RejectionReason,
        SourceBoundsPx = diagnostics.SourceBoundsPx,
        RuleBoundsPx = diagnostics.RuleBoundsPx,
        BackgroundBoundsPx = diagnostics.BackgroundBoundsPx,
        RowCount = diagnostics.RowCount,
        ColumnCount = diagnostics.ColumnCount,
        Confidence = diagnostics.Confidence,
        ColumnAnchors = diagnostics.ColumnAnchors,
        RowAnchors = diagnostics.RowAnchors,
    };
}
