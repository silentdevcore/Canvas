namespace PXA.FileImporter.ImageOcr;

// Stage 1 output: immutable OCR text document with raw OCR output mapped back to
// original source pixels, produced by OcrTextExtractor and consumed by the
// fusion stage and the converter orchestrator.
internal sealed record OcrTextDocument(
    int SourceWidthPx,
    int SourceHeightPx,
    IReadOnlyList<OcrPage> Pages,
    OcrTextExtractionMetadata Metadata)
{
    public IReadOnlyList<OcrLine> Lines { get; } = Pages
        .SelectMany(p => p.Blocks)
        .SelectMany(b => b.Lines)
        .ToArray();

    public IReadOnlyList<OcrWord> Words { get; } = Pages
        .SelectMany(p => p.Blocks)
        .SelectMany(b => b.Lines)
        .SelectMany(l => l.Words)
        .ToArray();
}

internal sealed record OcrTextExtractionMetadata(
    string Language,
    string EngineName,
    string EngineVersion,
    double CoordinateScaleX,
    double CoordinateScaleY,
    IReadOnlyList<string> PreprocessingSteps);
