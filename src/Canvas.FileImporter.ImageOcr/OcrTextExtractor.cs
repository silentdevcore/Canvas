namespace Canvas.FileImporter.ImageOcr;

internal sealed class OcrTextExtractor
{
    private readonly IOcrEngine _ocrEngine;

    public OcrTextExtractor(IOcrEngine ocrEngine)
    {
        _ocrEngine = ocrEngine;
    }

    public async Task<OcrTextDocument> ExtractAsync(
        byte[] encodedImageBytes,
        int ocrWidthPx,
        int ocrHeightPx,
        int sourceWidthPx,
        int sourceHeightPx,
        ImageToPdfConversionOptions options,
        IReadOnlyList<string> preprocessingSteps,
        CancellationToken cancellationToken = default)
    {
        var pages = await _ocrEngine.RecognizeAsync(
            [new OcrImagePage(0, ocrWidthPx, ocrHeightPx, encodedImageBytes)],
            options,
            cancellationToken);

        var scaleX = sourceWidthPx / (double)Math.Max(1, ocrWidthPx);
        var scaleY = sourceHeightPx / (double)Math.Max(1, ocrHeightPx);
        var sourcePages = ScalePagesToSource(pages, sourceWidthPx, sourceHeightPx);

        return new OcrTextDocument(
            sourceWidthPx,
            sourceHeightPx,
            sourcePages,
            new OcrTextExtractionMetadata(
                options.Languages,
                _ocrEngine.Name,
                _ocrEngine.Version,
                scaleX,
                scaleY,
                preprocessingSteps));
    }

    private static IReadOnlyList<OcrPage> ScalePagesToSource(
        IReadOnlyList<OcrPage> pages,
        int sourceWidth,
        int sourceHeight)
    {
        if (pages.Count == 0)
            return pages;

        var first = pages[0];
        if (first.WidthPx == sourceWidth && first.HeightPx == sourceHeight)
            return pages;

        return pages
            .Select(page =>
            {
                var scaleX = sourceWidth / (double)Math.Max(1, page.WidthPx);
                var scaleY = sourceHeight / (double)Math.Max(1, page.HeightPx);
                return new OcrPage
                {
                    PageIndex = page.PageIndex,
                    WidthPx = sourceWidth,
                    HeightPx = sourceHeight,
                    Confidence = page.Confidence,
                    Blocks = page.Blocks.Select(block => new OcrBlock
                    {
                        Bounds = ScaleBounds(block.Bounds, scaleX, scaleY, sourceWidth, sourceHeight),
                        Confidence = block.Confidence,
                        Lines = block.Lines.Select(line => new OcrLine
                        {
                            Text = line.Text,
                            Bounds = ScaleBounds(line.Bounds, scaleX, scaleY, sourceWidth, sourceHeight),
                            Confidence = line.Confidence,
                            Words = line.Words.Select(word => new OcrWord
                            {
                                Text = word.Text,
                                Bounds = ScaleBounds(word.Bounds, scaleX, scaleY, sourceWidth, sourceHeight),
                                Confidence = word.Confidence,
                            }).ToArray(),
                        }).ToArray(),
                    }).ToArray(),
                };
            })
            .ToArray();
    }

    private static OcrBoundingBox ScaleBounds(
        OcrBoundingBox bounds,
        double scaleX,
        double scaleY,
        int maxWidth,
        int maxHeight)
    {
        var x = Math.Clamp((int)Math.Round(bounds.X * scaleX), 0, Math.Max(0, maxWidth - 1));
        var y = Math.Clamp((int)Math.Round(bounds.Y * scaleY), 0, Math.Max(0, maxHeight - 1));
        var right = Math.Clamp((int)Math.Round((bounds.X + bounds.Width) * scaleX), x + 1, maxWidth);
        var bottom = Math.Clamp((int)Math.Round((bounds.Y + bounds.Height) * scaleY), y + 1, maxHeight);
        return new OcrBoundingBox(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }
}
