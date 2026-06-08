namespace Canvas.FileImporter.ImageOcr;

public sealed class OcrWorkerRequest
{
    public string Languages { get; init; } = "deu+eng";
    public string? TessDataPath { get; init; }
    public string? NativeLibraryPath { get; init; }
    public int MaxOcrRuntimeSeconds { get; init; } = 45;
    public IReadOnlyList<OcrWorkerImagePage> Pages { get; init; } = [];
}

public sealed class OcrWorkerImagePage
{
    public int PageIndex { get; init; }
    public int WidthPx { get; init; }
    public int HeightPx { get; init; }
    public required string EncodedImagePath { get; init; }
}

public sealed class OcrWorkerResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<OcrPage> Pages { get; init; } = [];
}
