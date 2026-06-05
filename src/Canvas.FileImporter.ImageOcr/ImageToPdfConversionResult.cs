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
}
