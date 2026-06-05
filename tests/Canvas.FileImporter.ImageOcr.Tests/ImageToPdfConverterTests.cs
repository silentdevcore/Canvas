using Canvas.FileImporter.ImageOcr;
using Canvas.WebApi.Infrastructure;
using SkiaSharp;
using System.Text;

namespace Canvas.FileImporter.ImageOcr.Tests;

public sealed class ImageToPdfConverterTests
{
    [Fact]
    public async Task ConvertAsync_WithEmbeddedTesseractData_RecognizesEnglishText()
    {
        var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var nativePath = Path.Combine(AppContext.BaseDirectory, "native");
        if (!Directory.Exists(nativePath))
            return;

        var converter = new ImageToPdfConverter(new EmbeddedTesseractOcrEngine(tessDataPath, nativePath));

        using var stream = new MemoryStream(MakeTextImage("HELLO 123", 720, 220));
        ImageToPdfConversionResult result;
        try
        {
            result = await converter.ConvertAsync(stream, "hello.png", new ImageToPdfConversionOptions
            {
                Languages = "eng",
                SourceDpiX = 300,
                SourceDpiY = 300,
                IncludeBackgroundImage = true,
                IncludeDebugOverlay = true,
                NativeLibraryPath = nativePath,
            });
        }
        catch (OcrNativeDependencyMissingException)
        {
            return;
        }
        catch (DllNotFoundException)
        {
            return;
        }

        var text = string.Join(" ", result.Design.Pages[0].Elements
            .Where(e => e.Type == "text")
            .Select(e => e.Content));

        Assert.Contains("HELLO", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Diagnostics.WordCount >= 2);
        Assert.Equal("Tesseract", result.Diagnostics.OcrEngine);
        Assert.NotNull(result.DebugOverlayPng);
    }

    [Fact]
    public async Task ConvertAsync_BuildsEditableTextAndBackgroundImage()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "Hallo Welt",
                Bounds = new OcrBoundingBox(20, 30, 120, 24),
                Confidence = 0.91,
                Words =
                [
                    new OcrWord { Text = "Hallo", Bounds = new OcrBoundingBox(20, 30, 54, 24), Confidence = 0.94 },
                    new OcrWord { Text = "Welt", Bounds = new OcrBoundingBox(86, 30, 54, 24), Confidence = 0.88 },
                ],
            },
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            IncludeDebugOverlay = true,
        });

        Assert.Equal(144, result.Design.PageSettings!.Width, 3);
        Assert.Equal(72, result.Design.PageSettings!.Height, 3);
        Assert.Equal("landscape", result.Design.PageSettings.Orientation);

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.True(background.Locked);
        Assert.StartsWith("data:image/png;base64,", background.Content);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("Hallo Welt", text.Content);
        Assert.Equal(14.4, text.X, 1);
        Assert.Equal(21.6, text.Y, 1);
        Assert.NotNull(result.DebugOverlayPng);
        Assert.NotEmpty(result.DebugOverlayPng);
        Assert.Equal(2, result.Diagnostics.WordCount);
        Assert.Equal(1, result.Diagnostics.LineCount);
        Assert.Equal("FakeOCR", result.Diagnostics.OcrEngine);
    }

    [Fact]
    public async Task ConvertAsync_WithoutBackground_DoesNotAddImageElementAndWarns()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeImage(80, 120));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            IncludeBackgroundImage = false,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.Contains(result.Warnings, w => w.Contains("Background image layer is disabled", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Contains("No OCR words", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertAsync_RejectsFilesAboveConfiguredLimit()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeImage(50, 50));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
            {
                MaxFileBytes = 10,
            }));

        Assert.Contains("too large", ex.Message);
    }

    [Fact]
    public async Task ConvertAsync_CanRenderGeneratedDesignToPdfBytes()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "Invoice 123",
                Bounds = new OcrBoundingBox(10, 20, 140, 20),
                Confidence = 0.95,
                Words =
                [
                    new OcrWord { Text = "Invoice", Bounds = new OcrBoundingBox(10, 20, 80, 20), Confidence = 0.95 },
                    new OcrWord { Text = "123", Bounds = new OcrBoundingBox(100, 20, 50, 20), Confidence = 0.95 },
                ],
            },
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "invoice.png", new ImageToPdfConversionOptions());

        var document = DesignJsonMapper.MapToPdfDocument(result.Design);
        var pdf = document.ToBytes();

        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf[..4]));
        Assert.Contains(1, document.GetPagesWithText());
        Assert.Contains(1, document.GetPagesWithImages());
    }

    private static byte[] MakeImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.LightSteelBlue };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeTextImage(string text, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 72);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
            };
            canvas.DrawText(text, 48, 132, font, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
                        }
                    ],
                }
            ]);
        }
    }
}
