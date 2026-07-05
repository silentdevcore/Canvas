namespace PXA.FileImporter.ImageOcr;

internal static class OcrModelMapper
{
    public static Canvas.FileImporter.ImageOcr.OcrImagePage ToCanvas(OcrImagePage page) =>
        new(page.PageIndex, page.WidthPx, page.HeightPx, page.EncodedImageBytes);

    public static OcrPage FromCanvas(Canvas.FileImporter.ImageOcr.OcrPage page) => new()
    {
        PageIndex = page.PageIndex,
        WidthPx = page.WidthPx,
        HeightPx = page.HeightPx,
        Confidence = page.Confidence,
        Blocks = page.Blocks.Select(FromCanvas).ToArray(),
    };

    public static Canvas.FileImporter.ImageOcr.OcrPage ToCanvas(OcrPage page) => new()
    {
        PageIndex = page.PageIndex,
        WidthPx = page.WidthPx,
        HeightPx = page.HeightPx,
        Confidence = page.Confidence,
        Blocks = page.Blocks.Select(ToCanvas).ToArray(),
    };

    private static OcrBlock FromCanvas(Canvas.FileImporter.ImageOcr.OcrBlock block) => new()
    {
        Bounds = FromCanvas(block.Bounds),
        Confidence = block.Confidence,
        Lines = block.Lines.Select(FromCanvas).ToArray(),
    };

    private static Canvas.FileImporter.ImageOcr.OcrBlock ToCanvas(OcrBlock block) => new()
    {
        Bounds = ToCanvas(block.Bounds),
        Confidence = block.Confidence,
        Lines = block.Lines.Select(ToCanvas).ToArray(),
    };

    private static OcrLine FromCanvas(Canvas.FileImporter.ImageOcr.OcrLine line) => new()
    {
        Text = line.Text,
        Bounds = FromCanvas(line.Bounds),
        Confidence = line.Confidence,
        Words = line.Words.Select(FromCanvas).ToArray(),
    };

    private static Canvas.FileImporter.ImageOcr.OcrLine ToCanvas(OcrLine line) => new()
    {
        Text = line.Text,
        Bounds = ToCanvas(line.Bounds),
        Confidence = line.Confidence,
        Words = line.Words.Select(ToCanvas).ToArray(),
    };

    private static OcrWord FromCanvas(Canvas.FileImporter.ImageOcr.OcrWord word) => new()
    {
        Text = word.Text,
        Bounds = FromCanvas(word.Bounds),
        Confidence = word.Confidence,
    };

    private static Canvas.FileImporter.ImageOcr.OcrWord ToCanvas(OcrWord word) => new()
    {
        Text = word.Text,
        Bounds = ToCanvas(word.Bounds),
        Confidence = word.Confidence,
    };

    private static OcrBoundingBox FromCanvas(Canvas.FileImporter.ImageOcr.OcrBoundingBox bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static Canvas.FileImporter.ImageOcr.OcrBoundingBox ToCanvas(OcrBoundingBox bounds) =>
        new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
}
