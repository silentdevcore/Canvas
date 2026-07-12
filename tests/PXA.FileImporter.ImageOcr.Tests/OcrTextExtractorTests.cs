namespace PXA.FileImporter.ImageOcr.Tests;

public sealed class OcrTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_MapsRawOcrOutputBackToSourcePixels()
    {
        var engine = new FakeOcrEngine([
            new OcrLine
            {
                Text = "Invoice total",
                Bounds = new OcrBoundingBox(10, 20, 80, 12),
                Confidence = 0.91,
                Words =
                [
                    new OcrWord
                    {
                        Text = "Invoice",
                        Bounds = new OcrBoundingBox(10, 20, 42, 12),
                        Confidence = 0.93,
                    },
                    new OcrWord
                    {
                        Text = "total",
                        Bounds = new OcrBoundingBox(56, 20, 34, 12),
                        Confidence = 0.89,
                    },
                ],
            },
        ]);
        var extractor = new OcrTextExtractor(engine);

        var document = await extractor.ExtractAsync(
            [1, 2, 3],
            ocrWidthPx: 100,
            ocrHeightPx: 50,
            sourceWidthPx: 200,
            sourceHeightPx: 100,
            new ImageToPdfConversionOptions
            {
                Languages = "deu+eng",
            },
            ["grayscale", "ocr-scale:0.5"]);

        Assert.Equal(200, document.SourceWidthPx);
        Assert.Equal(100, document.SourceHeightPx);
        Assert.Equal("deu+eng", document.Metadata.Language);
        Assert.Equal("FakeOCR", document.Metadata.EngineName);
        Assert.Equal("test", document.Metadata.EngineVersion);
        Assert.Equal(2, document.Metadata.CoordinateScaleX);
        Assert.Equal(2, document.Metadata.CoordinateScaleY);
        Assert.Equal(["grayscale", "ocr-scale:0.5"], document.Metadata.PreprocessingSteps);

        var line = Assert.Single(document.Lines);
        Assert.Equal("Invoice total", line.Text);
        Assert.Equal(0.91, line.Confidence);
        Assert.Equal(new OcrBoundingBox(20, 40, 160, 24), line.Bounds);

        Assert.Collection(
            document.Words,
            word =>
            {
                Assert.Equal("Invoice", word.Text);
                Assert.Equal(0.93, word.Confidence);
                Assert.Equal(new OcrBoundingBox(20, 40, 84, 24), word.Bounds);
            },
            word =>
            {
                Assert.Equal("total", word.Text);
                Assert.Equal(0.89, word.Confidence);
                Assert.Equal(new OcrBoundingBox(112, 40, 68, 24), word.Bounds);
            });
    }

    [Fact]
    public async Task ExtractAsync_CanSucceedWithoutVisualDetection()
    {
        var engine = new FakeOcrEngine([
            new OcrLine
            {
                Text = "Standalone paragraph",
                Bounds = new OcrBoundingBox(12, 18, 140, 16),
                Confidence = 0.87,
                Words =
                [
                    new OcrWord
                    {
                        Text = "Standalone",
                        Bounds = new OcrBoundingBox(12, 18, 82, 16),
                        Confidence = 0.88,
                    },
                    new OcrWord
                    {
                        Text = "paragraph",
                        Bounds = new OcrBoundingBox(98, 18, 54, 16),
                        Confidence = 0.86,
                    },
                ],
            },
        ]);
        var extractor = new OcrTextExtractor(engine);

        var document = await extractor.ExtractAsync(
            [4, 5, 6],
            ocrWidthPx: 200,
            ocrHeightPx: 100,
            sourceWidthPx: 200,
            sourceHeightPx: 100,
            new ImageToPdfConversionOptions(),
            []);

        // OCR text recognition succeeds on its own, with no visual detection stage involved.
        Assert.Single(document.Pages);
        Assert.Single(document.Lines);
        Assert.Equal(2, document.Words.Count);
        Assert.Equal("Standalone paragraph", document.Lines[0].Text);
        Assert.Equal(200, document.SourceWidthPx);
        Assert.Equal(100, document.SourceHeightPx);
    }

    private sealed class FakeOcrEngine : IOcrEngine
    {
        private readonly IReadOnlyList<OcrLine> _lines;

        public FakeOcrEngine(IReadOnlyList<OcrLine> lines)
        {
            _lines = lines;
        }

        public string Name => "FakeOCR";
        public string Version => "test";

        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
            IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            var page = pages[0];
            return Task.FromResult<IReadOnlyList<OcrPage>>([
                new OcrPage
                {
                    PageIndex = page.PageIndex,
                    WidthPx = page.WidthPx,
                    HeightPx = page.HeightPx,
                    Confidence = _lines.Count == 0 ? 0 : _lines.Average(l => l.Confidence),
                    Blocks =
                    [
                        new OcrBlock
                        {
                            Bounds = new OcrBoundingBox(0, 0, page.WidthPx, page.HeightPx),
                            Confidence = _lines.Count == 0 ? 0 : _lines.Average(l => l.Confidence),
                            Lines = _lines,
                        },
                    ],
                },
            ]);
        }
    }
}
