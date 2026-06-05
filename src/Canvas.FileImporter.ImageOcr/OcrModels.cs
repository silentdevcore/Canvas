namespace Canvas.FileImporter.ImageOcr;

public sealed record OcrBoundingBox(int X, int Y, int Width, int Height);

public sealed class OcrWord
{
    public required string Text { get; init; }
    public required OcrBoundingBox Bounds { get; init; }
    public double Confidence { get; init; }
}

public sealed class OcrLine
{
    public required string Text { get; init; }
    public required OcrBoundingBox Bounds { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<OcrWord> Words { get; init; } = [];
}

public sealed class OcrBlock
{
    public required OcrBoundingBox Bounds { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<OcrLine> Lines { get; init; } = [];
}

public sealed class OcrPage
{
    public int PageIndex { get; init; }
    public int WidthPx { get; init; }
    public int HeightPx { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<OcrBlock> Blocks { get; init; } = [];
}
