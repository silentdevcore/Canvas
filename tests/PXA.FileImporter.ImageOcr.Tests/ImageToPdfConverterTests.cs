using PXA.FileImporter.ImageOcr;
using SkiaSharp;

namespace PXA.FileImporter.ImageOcr.Tests;

public sealed class ImageToPdfConverterTests
{
    [Fact]
    public async Task ConvertAsync_UsesPxaOcrEngineAndReturnsPxaResult()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "PXA OCR",
                Bounds = new OcrBoundingBox(20, 20, 120, 24),
                Confidence = 0.93,
                Words =
                [
                    new OcrWord { Text = "PXA", Bounds = new OcrBoundingBox(20, 20, 45, 24), Confidence = 0.94 },
                    new OcrWord { Text = "OCR", Bounds = new OcrBoundingBox(76, 20, 50, 24), Confidence = 0.92 },
                ],
            },
        ]));

        await using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            IncludeDebugOverlay = true,
        });

        Assert.Equal("FakePXA", result.Diagnostics.OcrEngine);
        Assert.Equal("1.0", result.Diagnostics.OcrEngineVersion);
        Assert.Equal(2, result.Diagnostics.WordCount);
        Assert.Single(result.OcrPages);
        Assert.NotNull(result.DebugOverlayPng);
        Assert.NotEmpty(result.DebugOverlayPng);
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text" && e.Content == "PXA OCR");
    }

    [Fact]
    public void TesseractFacadesExposePxaEngineContract()
    {
        IOcrEngine embedded = new EmbeddedTesseractOcrEngine("tessdata", "native");
        IOcrEngine isolated = new ProcessIsolatedTesseractOcrEngine("worker.dll", "tessdata", "native");

        Assert.Equal("Tesseract", embedded.Name);
        Assert.Equal("Tesseract", isolated.Name);
        Assert.Equal("5.2.0", embedded.Version);
        Assert.Equal("5.2.0-isolated", isolated.Version);
    }

    private static byte[] MakeImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(10, 10, 40, 20, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private sealed class FakeOcrEngine : IOcrEngine
    {
        private readonly IReadOnlyList<OcrLine> lines;

        public FakeOcrEngine(IReadOnlyList<OcrLine> lines)
        {
            this.lines = lines;
        }

        public string Name => "FakePXA";

        public string Version => "1.0";

        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
            IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            var page = pages[0];
            IReadOnlyList<OcrPage> result =
            [
                new OcrPage
                {
                    PageIndex = page.PageIndex,
                    WidthPx = page.WidthPx,
                    HeightPx = page.HeightPx,
                    Confidence = lines.Count == 0 ? 0 : lines.Average(l => l.Confidence),
                    Blocks =
                    [
                        new OcrBlock
                        {
                            Bounds = new OcrBoundingBox(0, 0, page.WidthPx, page.HeightPx),
                            Confidence = 0.9,
                            Lines = lines,
                        }
                    ],
                }
            ];

            return Task.FromResult(result);
        }
    }
}
