using Canvas.Core.Contracts;
using Canvas.FileImporter.ImageOcr;
using Canvas.WebApi.Infrastructure;
using SkiaSharp;
using System.Text;
using System.Text.Json;

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
    public async Task ConvertAsync_WithTargetPage_AlignsTextToPlacedBackgroundImage()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "Top left",
                Bounds = new OcrBoundingBox(20, 10, 100, 20),
                Confidence = 0.90,
                Words =
                [
                    new OcrWord { Text = "Top", Bounds = new OcrBoundingBox(20, 10, 45, 20), Confidence = 0.90 },
                    new OcrWord { Text = "left", Bounds = new OcrBoundingBox(75, 10, 45, 20), Confidence = 0.90 },
                ],
            },
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            PageWidthPt = 595,
            PageHeightPt = 842,
        });

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.Equal(0, background.X, 1);
        Assert.Equal(272.25, background.Y, 1);
        Assert.Equal(595, background.Width, 1);
        Assert.Equal(297.5, background.Height, 1);
        Assert.Equal("fill", background.FitMode);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal(59.5, text.X, 1);
        Assert.Equal(302, text.Y, 1);
        Assert.Equal(297.5, text.Width, 1);
        Assert.Equal(59.5, text.Height, 1);
    }

    [Fact]
    public async Task ConvertAsync_WithPortraitTargetPage_AlignsTextToPlacedBackgroundImage()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "Portrait",
                Bounds = new OcrBoundingBox(10, 20, 40, 30),
                Confidence = 0.90,
                Words =
                [
                    new OcrWord { Text = "Portrait", Bounds = new OcrBoundingBox(10, 20, 40, 30), Confidence = 0.90 },
                ],
            },
        ]));

        using var stream = new MemoryStream(MakeImage(100, 200));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            PageWidthPt = 595,
            PageHeightPt = 842,
        });

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.Equal(87, background.X, 1);
        Assert.Equal(0, background.Y, 1);
        Assert.Equal(421, background.Width, 1);
        Assert.Equal(842, background.Height, 1);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal(129.1, text.X, 1);
        Assert.Equal(84.2, text.Y, 1);
        Assert.Equal(168.4, text.Width, 1);
        Assert.Equal(126.3, text.Height, 1);
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
    public async Task ConvertAsync_TextOnlyMode_EmitsOnlyTextWithNoBackgroundOrShapes()
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

        // Draw a box that would normally be detected as a shape in structured mode.
        using var stream = new MemoryStream(MakeImageWithRectangle(200, 100));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-only",
            // Even with the background image requested, text-only mode omits it.
            IncludeBackgroundImage = true,
        });

        var elements = result.Design.Pages[0].Elements;
        Assert.NotEmpty(elements);
        Assert.All(elements, e => Assert.Equal("text", e.Type));
        Assert.DoesNotContain(elements, e => e.Type == "image");

        var text = Assert.Single(elements, e => e.Type == "text");
        Assert.Equal("Hallo Welt", text.Content);
    }

    [Fact]
    public async Task ConvertAsync_TextOnly_ClampsOutlierTallBoxFontSize()
    {
        // Many normal-height lines establish the baseline; one line has a 5x-tall box that
        // would otherwise blow the font size up to the 72pt clamp.
        var lines = new List<OcrLine>();
        for (var i = 0; i < 6; i++)
        {
            var y = 40 + i * 30;
            lines.Add(MakeOcrLine("normal line", 20, y,
            [
                new OcrWord { Text = "normal", Bounds = new OcrBoundingBox(20, y, 60, 20), Confidence = 0.9 },
                new OcrWord { Text = "line", Bounds = new OcrBoundingBox(86, y, 40, 20), Confidence = 0.9 },
            ]));
        }
        // Outlier: a single word with a 200px-tall box.
        lines.Add(MakeOcrLine("RATE", 20, 240,
        [
            new OcrWord { Text = "RATE", Bounds = new OcrBoundingBox(20, 240, 60, 200), Confidence = 0.9 },
        ]));

        var converter = new ImageToPdfConverter(new FakeOcrEngine(lines));
        using var stream = new MemoryStream(MakeImage(400, 600));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-only",
        });

        double FontSize(ElementDto e) => Convert.ToDouble(e.Style!["fontSize"]);
        var normal = result.Design.Pages[0].Elements.First(e => e.Type == "text" && e.Content == "normal line");
        var outlier = result.Design.Pages[0].Elements.First(e => e.Type == "text" && e.Content == "RATE");

        // Outlier is bounded to ~1.8x the normal text height, not pinned at the 72pt cap.
        Assert.True(FontSize(outlier) < 72);
        Assert.True(FontSize(outlier) <= FontSize(normal) * 1.8 + 0.5,
            $"outlier {FontSize(outlier)} should be <= 1.8x normal {FontSize(normal)}");
    }

    [Fact]
    public async Task ConvertAsync_TextOnly_FitsPageToScanAspectWithinSelectedSize()
    {
        // Off-aspect scan (400x600 = 0.667) exported onto A4 (595x842 = 0.707).
        var line = MakeOcrLine("Top left", 10, 10,
        [
            new OcrWord { Text = "Top", Bounds = new OcrBoundingBox(10, 10, 40, 18), Confidence = 0.9 },
            new OcrWord { Text = "left", Bounds = new OcrBoundingBox(54, 10, 30, 18), Confidence = 0.9 },
        ]);

        var converter = new ImageToPdfConverter(new FakeOcrEngine([line]));
        using var stream = new MemoryStream(MakeImage(400, 600));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-only",
            PageWidthPt = 595,
            PageHeightPt = 842,
        });

        var page = result.Design.PageSettings!;
        // Page matches the scan aspect (0.667) and is bounded by the selected A4 size.
        Assert.Equal(400.0 / 600.0, page.Width / page.Height, 3);
        Assert.True(page.Width <= 595 + 0.5 && page.Height <= 842 + 0.5);

        // No centering margin: top-left text maps from the page origin.
        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        var scale = Math.Min(page.Width / 400.0, page.Height / 600.0);
        Assert.Equal(10 * scale, text.X, 1);
        Assert.Equal(10 * scale, text.Y, 1);
    }

    [Fact]
    public async Task ConvertAsync_TextOnly_SplitsLineAtLargeHorizontalGaps()
    {
        // One OCR line that spans two columns: a left label and a far-right value. Normal word
        // spacing inside each cluster, a big gap between them.
        var line = MakeOcrLine("Subtotal 12,080.00", 20, 40,
        [
            new OcrWord { Text = "Subtotal", Bounds = new OcrBoundingBox(20, 40, 70, 18), Confidence = 0.9 },
            new OcrWord { Text = "12,080.00", Bounds = new OcrBoundingBox(360, 40, 80, 18), Confidence = 0.9 },
        ]);

        var converter = new ImageToPdfConverter(new FakeOcrEngine([line]));
        using var stream = new MemoryStream(MakeImage(500, 200));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-only",
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").OrderBy(e => e.X).ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal("Subtotal", texts[0].Content);
        Assert.Equal("12,080.00", texts[1].Content);
        // The right cluster keeps its true (far-right) X rather than being pulled to the left.
        Assert.True(texts[1].X > texts[0].X + 100);
    }

    [Fact]
    public async Task ConvertAsync_TextBackground_ReconstructsColoredBlockBehindText()
    {
        // 400x300 white page with a purple header bar across the top.
        byte[] image;
        using (var bitmap = new SKBitmap(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = new SKColor(124, 92, 255), IsAntialias = false };
                canvas.DrawRect(0, 0, 400, 60, paint);
            }
            using var img = SKImage.FromBitmap(bitmap);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            image = data.ToArray();
        }

        var line = MakeOcrLine("INVOICE", 280, 18,
        [
            new OcrWord { Text = "INVOICE", Bounds = new OcrBoundingBox(280, 18, 100, 24), Confidence = 0.9 },
        ]);

        var converter = new ImageToPdfConverter(new FakeOcrEngine([line]));
        using var stream = new MemoryStream(image);
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-background",
        });

        var elements = result.Design.Pages[0].Elements.ToList();

        // No original-image layer and none of the table/shape machinery.
        Assert.DoesNotContain(elements, e => e.Type == "image");
        Assert.DoesNotContain(elements, e => e.Type is "table" or "circle");

        // The purple header bar was reconstructed as a colored rect.
        var rect = Assert.Single(elements, e => e.Type == "rect");
        var color = (string)rect.Style!["backgroundColor"];
        Assert.True(IsPurpleish(color), $"expected purple-ish fill, got {color}");

        // Text sits on top (after the fill in z-order).
        var textIdx = elements.FindIndex(e => e.Type == "text" && e.Content == "INVOICE");
        Assert.True(textIdx >= 0);
        Assert.True(elements.IndexOf(rect) < textIdx);
    }

    private static bool IsPurpleish(string hex)
    {
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return b > 150 && b > g + 30 && r > g;
    }

    [Fact]
    public async Task ConvertAsync_TextOnly_KeepsLineIntactWithoutPerWordFragments()
    {
        // A multi-word line whose word heights vary enough to trip the run-splitter.
        var line = MakeOcrLine("Brand workshop interviews", 20, 40,
        [
            new OcrWord { Text = "Brand", Bounds = new OcrBoundingBox(20, 40, 50, 16), Confidence = 0.9 },
            new OcrWord { Text = "workshop", Bounds = new OcrBoundingBox(76, 38, 80, 22), Confidence = 0.9 },
            new OcrWord { Text = "interviews", Bounds = new OcrBoundingBox(162, 40, 90, 16), Confidence = 0.9 },
        ]);

        var converter = new ImageToPdfConverter(new FakeOcrEngine([line]));
        using var stream = new MemoryStream(MakeImage(400, 200));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
            LayoutMode = "text-only",
        });

        // One element for the whole line, not three per-word fragments.
        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("Brand workshop interviews", text.Content);
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

    [Fact]
    public async Task ConvertAsync_WithJpegInput_CanRenderGeneratedDesignToPdfBytes()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "JPEG scan",
                Bounds = new OcrBoundingBox(10, 20, 140, 20),
                Confidence = 0.95,
                Words =
                [
                    new OcrWord { Text = "JPEG", Bounds = new OcrBoundingBox(10, 20, 70, 20), Confidence = 0.95 },
                    new OcrWord { Text = "scan", Bounds = new OcrBoundingBox(90, 20, 60, 20), Confidence = 0.95 },
                ],
            },
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100, SKEncodedImageFormat.Jpeg));
        var result = await converter.ConvertAsync(stream, "scan.jpg", new ImageToPdfConversionOptions());

        var document = DesignJsonMapper.MapToPdfDocument(result.Design);
        var pdf = document.ToBytes();

        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf[..4]));
        Assert.Contains(1, document.GetPagesWithText());
        Assert.Contains(1, document.GetPagesWithImages());
        Assert.Equal(48, result.Design.PageSettings!.Width, 1);
        Assert.Equal(24, result.Design.PageSettings.Height, 1);
    }

    [Fact]
    public async Task ConvertAsync_ReadsJpegJfifDpiMetadata_WhenNoOverrideIsProvided()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));
        var jpeg = WithJfifDpi(MakeImage(600, 300, SKEncodedImageFormat.Jpeg), 150, 300);

        using var stream = new MemoryStream(jpeg);
        var result = await converter.ConvertAsync(stream, "scan.jpg", new ImageToPdfConversionOptions());

        Assert.Equal(150, result.Diagnostics.EffectiveDpiX, 1);
        Assert.Equal(300, result.Diagnostics.EffectiveDpiY, 1);
        Assert.Equal(288, result.Design.PageSettings!.Width, 1);
        Assert.Equal(72, result.Design.PageSettings.Height, 1);
    }

    [Fact]
    public async Task ConvertAsync_SourceDpiOptionsOverrideJpegMetadata()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));
        var jpeg = WithJfifDpi(MakeImage(600, 300, SKEncodedImageFormat.Jpeg), 72, 72);

        using var stream = new MemoryStream(jpeg);
        var result = await converter.ConvertAsync(stream, "scan.jpg", new ImageToPdfConversionOptions
        {
            SourceDpiX = 144,
            SourceDpiY = 288,
        });

        Assert.Equal(144, result.Diagnostics.EffectiveDpiX, 1);
        Assert.Equal(288, result.Diagnostics.EffectiveDpiY, 1);
        Assert.Equal(300, result.Design.PageSettings!.Width, 1);
        Assert.Equal(75, result.Design.PageSettings.Height, 1);
    }

    [Fact]
    public async Task ConvertAsync_HonoursExifOrientationAndMapsRotatedCoordinates()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            new OcrLine
            {
                Text = "Rotated",
                Bounds = new OcrBoundingBox(2, 4, 10, 8),
                Confidence = 0.90,
                Words =
                [
                    new OcrWord { Text = "Rotated", Bounds = new OcrBoundingBox(2, 4, 10, 8), Confidence = 0.90 },
                ],
            },
        ]));
        var jpeg = InjectExifOrientation(MakeImage(40, 20, SKEncodedImageFormat.Jpeg), orientation: 6);

        using var stream = new MemoryStream(jpeg);
        var result = await converter.ConvertAsync(stream, "rotated.jpg", new ImageToPdfConversionOptions());

        Assert.Equal(20, result.Diagnostics.SourceWidthPx);
        Assert.Equal(40, result.Diagnostics.SourceHeightPx);
        Assert.Equal("portrait", result.Design.PageSettings!.Orientation);
        Assert.Equal(4.8, result.Design.PageSettings.Width, 1);
        Assert.Equal(9.6, result.Design.PageSettings.Height, 1);

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.Equal(0, background.X, 1);
        Assert.Equal(0, background.Y, 1);
        Assert.Equal(4.8, background.Width, 1);
        Assert.Equal(9.6, background.Height, 1);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal(0.48, text.X, 2);
        Assert.Equal(0.96, text.Y, 2);
        Assert.Equal(2.4, text.Width, 2);
        Assert.Equal(1.92, text.Height, 2);
    }

    [Fact]
    public async Task ConvertAsync_DefaultPreprocessingPassesOriginalImageBytesToOcr()
    {
        var engine = new CapturingOcrEngine([]);
        var converter = new ImageToPdfConverter(engine);

        using var stream = new MemoryStream(MakeImage(40, 20));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions());

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.False(result.Diagnostics.PreprocessingApplied);
        Assert.Equal(1, result.Diagnostics.PreprocessingScaleFactor);
        Assert.Empty(result.Diagnostics.PreprocessingSteps);
        Assert.Equal(ReadDataUriBytes(background.Content), engine.CapturedEncodedImageBytes);
    }

    [Fact]
    public async Task ConvertAsync_PreprocessingSendsProcessedImageToOcrWithoutChangingLayout()
    {
        var engine = new CapturingOcrEngine([
            new OcrLine
            {
                Text = "Preprocessed",
                Bounds = new OcrBoundingBox(10, 5, 20, 8),
                Confidence = 0.90,
                Words =
                [
                    new OcrWord { Text = "Preprocessed", Bounds = new OcrBoundingBox(10, 5, 20, 8), Confidence = 0.90 },
                ],
            },
        ]);
        var converter = new ImageToPdfConverter(engine);

        using var stream = new MemoryStream(MakeImage(40, 20));
        var result = await converter.ConvertAsync(stream, "scan.png", new ImageToPdfConversionOptions
        {
            EnablePreprocessing = true,
            PreprocessBinarize = true,
        });

        var background = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image");
        Assert.True(result.Diagnostics.PreprocessingApplied);
        Assert.Equal(1, result.Diagnostics.PreprocessingScaleFactor);
        Assert.Equal(["grayscale", "contrast", "binarize"], result.Diagnostics.PreprocessingSteps);
        Assert.NotEqual(ReadDataUriBytes(background.Content), engine.CapturedEncodedImageBytes);

        using var processed = DecodeBitmap(engine.CapturedEncodedImageBytes);
        var pixel = processed.GetPixel(0, 0);
        Assert.Equal(pixel.Red, pixel.Green);
        Assert.Equal(pixel.Green, pixel.Blue);

        Assert.Equal(9.6, result.Design.PageSettings!.Width, 1);
        Assert.Equal(4.8, result.Design.PageSettings.Height, 1);
        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal(2.4, text.X, 1);
        Assert.Equal(1.2, text.Y, 1);
        Assert.Equal(4.8, text.Width, 1);
        Assert.Equal(1.92, text.Height, 2);
    }

    [Fact]
    public async Task ConvertAsync_LargeImageDownscalesOcrInputAndMapsBoundsBackToSource()
    {
        var engine = new CapturingOcrEngine([
            new OcrLine
            {
                Text = "Scaled",
                Bounds = new OcrBoundingBox(707, 354, 283, 71),
                Confidence = 0.90,
                Words =
                [
                    new OcrWord { Text = "Scaled", Bounds = new OcrBoundingBox(707, 354, 283, 71), Confidence = 0.90 },
                ],
            },
        ]);
        var converter = new ImageToPdfConverter(engine);

        using var stream = new MemoryStream(MakeImage(4000, 2000));
        var result = await converter.ConvertAsync(stream, "large-scan.png", new ImageToPdfConversionOptions());

        Assert.True(engine.CapturedWidthPx < 4000);
        Assert.True(engine.CapturedHeightPx < 2000);
        Assert.True(result.Diagnostics.PreprocessingApplied);
        Assert.True(result.Diagnostics.PreprocessingScaleFactor < 1);
        Assert.Contains(result.Diagnostics.PreprocessingSteps, step => step.StartsWith("ocr-scale:", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("downscaled for OCR", StringComparison.OrdinalIgnoreCase));

        var page = Assert.Single(result.OcrPages);
        Assert.Equal(4000, page.WidthPx);
        Assert.Equal(2000, page.HeightPx);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal(240, text.X, 0);
        Assert.Equal(120, text.Y, 0);
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutEmitsTextForAlignedOcrWordsWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(110, 10, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(10, 32, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(112, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "table.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // No visible table lines in the image, so aligned OCR text stays as text (not a table).
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");

        var document = DesignJsonMapper.MapToPdfDocument(result.Design);
        var pdf = document.ToBytes();
        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf[..4]));
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutEmitsTextForSplitAlignedOcrCellLinesWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 38, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Qty", 108, 10, [
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(108, 10, 30, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Price", 168, 10, [
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(168, 10, 44, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(10, 32, 54, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("2", 108, 32, [
                new OcrWord { Text = "2", Bounds = new OcrBoundingBox(108, 32, 10, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("3.50", 168, 32, [
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(168, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(240, 100));
        var result = await converter.ConvertAsync(stream, "split-cell-table.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // No visible table lines, so the split-cell text is emitted as text (not a table).
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutEmitsTextForEmptyCellSplitLinesWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 38, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Qty", 108, 10, [
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(108, 10, 30, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Price", 168, 10, [
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(168, 10, 44, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(10, 32, 54, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("3.50", 168, 32, [
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(168, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(240, 100));
        var result = await converter.ConvertAsync(stream, "split-cell-table-empty.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // No visible table lines, so the split-cell text is emitted as text (not a table).
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutDetectsWordGridCandidateButEmitsTextWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Qty Price", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 38, 12), Confidence = 0.95 },
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(108, 10, 30, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(168, 10, 44, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 2 3.50", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(22, 32, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "2", Bounds = new OcrBoundingBox(128, 32, 10, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(188, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(250, 100));
        var result = await converter.ConvertAsync(stream, "jittered-word-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // The word-grid is still found as a candidate, but with no visible lines it is
        // emitted as text rather than a table.
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");

        var candidate = Assert.Single(result.Diagnostics.Layout.TableCandidates);
        Assert.Equal("word-grid-table", candidate.Detector);
        Assert.Equal(3, candidate.ColumnCount);
        Assert.Equal("rejected-no-visible-table-lines", candidate.Status);
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutDetectsMultiRowWordGridCandidateButEmitsTextWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Qty Price", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 38, 12), Confidence = 0.95 },
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(108, 10, 30, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(168, 10, 44, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(22, 32, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(188, 32, 36, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Tea 1 2.25", 10, 54, [
                new OcrWord { Text = "Tea", Bounds = new OcrBoundingBox(8, 54, 28, 12), Confidence = 0.95 },
                new OcrWord { Text = "1", Bounds = new OcrBoundingBox(110, 54, 10, 12), Confidence = 0.95 },
                new OcrWord { Text = "2.25", Bounds = new OcrBoundingBox(170, 54, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(250, 120));
        var result = await converter.ConvertAsync(stream, "missing-cell-word-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // Multi-row word-grid is detected as a candidate, but without visible lines it is
        // emitted as text rather than a table.
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");

        var candidate = Assert.Single(result.Diagnostics.Layout.TableCandidates);
        Assert.Equal("word-grid-table", candidate.Detector);
        Assert.Equal("rejected-no-visible-table-lines", candidate.Status);
    }

    [Fact]
    public async Task ConvertAsync_TableUsesLightRowFillsAsBackgroundBounds()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Qty Price", 20, 30, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 30, 38, 12), Confidence = 0.95 },
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(112, 30, 30, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(180, 30, 44, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 2 3.50", 20, 58, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 58, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "2", Bounds = new OcrBoundingBox(116, 58, 10, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(181, 58, 36, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Tea 1 2.25", 20, 86, [
                new OcrWord { Text = "Tea", Bounds = new OcrBoundingBox(35, 86, 28, 12), Confidence = 0.95 },
                new OcrWord { Text = "1", Bounds = new OcrBoundingBox(116, 86, 10, 12), Confidence = 0.95 },
                new OcrWord { Text = "2.25", Bounds = new OcrBoundingBox(181, 86, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeLightFilledTableBackgroundImage());
        var result = await converter.ConvertAsync(stream, "light-filled-table.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(true, table.Style!["imageOcrBackgroundBounded"]);
        Assert.Equal(false, table.Style["imageOcrRuleBounded"]);
        Assert.True(table.Width > 140);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "rect");

        var candidate = Assert.Single(result.Diagnostics.Layout.TableCandidates);
        Assert.Equal("accepted", candidate.Status);
        Assert.Null(candidate.RuleBoundsPx);
        Assert.NotNull(candidate.BackgroundBoundsPx);
    }

    [Fact]
    public async Task ConvertAsync_RealFailingTableFixture_WhenPresent_ProducesTableAndDiagnostics()
    {
        var imagePath = ResolveTableSamplePath("failing-table-01.png");
        var ocrPath = ResolveTableSamplePath("failing-table-01.ocr.json");
        if (!File.Exists(imagePath) || !File.Exists(ocrPath))
            return;

        var lines = LoadOcrFixtureLines(ocrPath);
        var converter = new ImageToPdfConverter(new FakeOcrEngine(lines));
        await using var stream = File.OpenRead(imagePath);
        var result = await converter.ConvertAsync(stream, "failing-table-01.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.True(result.Diagnostics.Layout.Rules.SegmentCount > 0 ||
                    result.Diagnostics.Layout.TableCandidates.Any(c => c.BackgroundBoundsPx is not null));
        Assert.Contains(result.Diagnostics.Layout.TableCandidates, c =>
            c.Status == "accepted" &&
            c.ColumnCount >= 2 &&
            c.RowCount >= 2 &&
            c.ColumnAnchors.Count >= 2 &&
            c.RowAnchors.Count >= 2);
    }

    [Fact]
    public async Task ConvertAsync_EditableLayoutKeepsAlignedOcrWordsAsTextLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(110, 10, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(10, 32, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(112, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "table.png", new ImageToPdfConversionOptions
        {
            LayoutMode = "editable",
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(2, result.Design.Pages[0].Elements.Count(e => e.Type == "text"));
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutGroupsNearbyLinesIntoParagraph()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("First line", 10, 10, [
                new OcrWord { Text = "First line", Bounds = new OcrBoundingBox(10, 10, 70, 12), Confidence = 0.92 },
            ]),
            MakeOcrLine("Second line", 11, 25, [
                new OcrWord { Text = "Second line", Bounds = new OcrBoundingBox(11, 25, 82, 12), Confidence = 0.93 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "paragraph.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("First line\nSecond line", text.Content);
        Assert.Equal("paragraph", text.Style!["imageOcrRole"]);
        Assert.Equal(2, text.Style["sourceLineCount"]);
        Assert.Equal("10,10,83,27", text.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutKeepsDistantLinesSeparate()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Top line", 10, 10, [
                new OcrWord { Text = "Top line", Bounds = new OcrBoundingBox(10, 10, 62, 12), Confidence = 0.92 },
            ]),
            MakeOcrLine("Bottom line", 10, 70, [
                new OcrWord { Text = "Bottom line", Bounds = new OcrBoundingBox(10, 70, 84, 12), Confidence = 0.93 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "paragraph.png", new ImageToPdfConversionOptions());

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal(["Top line", "Bottom line"], texts.Select(t => t.Content!).ToArray());
        Assert.All(texts, text => Assert.Equal("text", text.Style!["imageOcrRole"]));
    }

    [Fact]
    public async Task ConvertAsync_EditableLayoutKeepsNearbyLinesSeparate()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("First line", 10, 10, [
                new OcrWord { Text = "First line", Bounds = new OcrBoundingBox(10, 10, 70, 12), Confidence = 0.92 },
            ]),
            MakeOcrLine("Second line", 10, 25, [
                new OcrWord { Text = "Second line", Bounds = new OcrBoundingBox(10, 25, 82, 12), Confidence = 0.93 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(200, 100));
        var result = await converter.ConvertAsync(stream, "paragraph.png", new ImageToPdfConversionOptions
        {
            LayoutMode = "editable",
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal(["First line", "Second line"], texts.Select(t => t.Content!).ToArray());
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutReadsStableColumnsBeforeRows()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Left one", 10, 10, [
                new OcrWord { Text = "Left one", Bounds = new OcrBoundingBox(10, 10, 62, 12), Confidence = 0.92 },
            ]),
            MakeOcrLine("Right one", 130, 10, [
                new OcrWord { Text = "Right one", Bounds = new OcrBoundingBox(130, 10, 68, 12), Confidence = 0.93 },
            ]),
            MakeOcrLine("Left two", 10, 25, [
                new OcrWord { Text = "Left two", Bounds = new OcrBoundingBox(10, 25, 62, 12), Confidence = 0.91 },
            ]),
            MakeOcrLine("Right two", 130, 25, [
                new OcrWord { Text = "Right two", Bounds = new OcrBoundingBox(130, 25, 68, 12), Confidence = 0.94 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(240, 100));
        var result = await converter.ConvertAsync(stream, "columns.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(2, texts.Count);
        Assert.Equal(["Left one\nLeft two", "Right one\nRight two"], texts.Select(t => t.Content!).ToArray());
        Assert.All(texts, text => Assert.Equal("paragraph", text.Style!["imageOcrRole"]));
        Assert.All(texts, text => Assert.Equal(2, text.Style!["sourceColumnCount"]));
        Assert.Equal(0, texts[0].Style!["sourceColumnIndex"]);
        Assert.Equal(1, texts[1].Style!["sourceColumnIndex"]);
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutKeepsHeadingSeparateFromBodyText()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Invoice Summary", 10, 8, [
                new OcrWord { Text = "Invoice Summary", Bounds = new OcrBoundingBox(10, 8, 150, 22), Confidence = 0.94 },
            ]),
            MakeOcrLine("First body line", 10, 40, [
                new OcrWord { Text = "First body line", Bounds = new OcrBoundingBox(10, 40, 92, 12), Confidence = 0.92 },
            ]),
            MakeOcrLine("Second body line", 10, 55, [
                new OcrWord { Text = "Second body line", Bounds = new OcrBoundingBox(10, 55, 104, 12), Confidence = 0.93 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(240, 120));
        var result = await converter.ConvertAsync(stream, "rich-text.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(2, texts.Count);

        Assert.Equal("Invoice Summary", texts[0].Content);
        var headingStyle = texts[0].Style!;
        Assert.Equal("heading", headingStyle["imageOcrTextRole"]);
        Assert.Equal("700", headingStyle["fontWeight"]);

        Assert.Equal("First body line\nSecond body line", texts[1].Content);
        var bodyStyle = texts[1].Style!;
        Assert.Equal("body", bodyStyle["imageOcrTextRole"]);
        Assert.Equal("normal", bodyStyle["fontWeight"]);
    }

    [Fact]
    public async Task ConvertAsync_TableRulesRefineTableBounds()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeTableGridImage());
        var result = await converter.ConvertAsync(stream, "grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(14.4, table.X, 1);
        Assert.Equal(14.4, table.Y, 1);
        Assert.Equal(115.2, table.Width, 1);
        Assert.Equal(57.6, table.Height, 1);
        Assert.Equal(true, table.Style!["imageOcrRuleBounded"]);
        Assert.Equal("20,20,160,80", table.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_TableRulesDetectLightGrayLinesOnLightBackground()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeTableGridImage(
            background: new SKColor(0xF8, 0xFA, 0xFC),
            rule: new SKColor(0xB8, 0xC0, 0xCC)));
        var result = await converter.ConvertAsync(stream, "light-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(true, table.Style!["imageOcrRuleBounded"]);
        Assert.Equal("20,20,160,80", table.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_TableDiagnosticsReportRulesAndCandidates()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeTableGridImage(
            background: new SKColor(0xF8, 0xFA, 0xFC),
            rule: new SKColor(0xB8, 0xC0, 0xCC)));
        var result = await converter.ConvertAsync(stream, "light-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.True(result.Diagnostics.Layout.Rules.HorizontalSegmentCount >= 3);
        Assert.True(result.Diagnostics.Layout.Rules.VerticalSegmentCount >= 3);
        Assert.True(result.Diagnostics.Layout.Rules.AverageContrast > 0);
        Assert.NotEmpty(result.Diagnostics.Layout.Rules.SampleSegments);

        var candidate = Assert.Single(result.Diagnostics.Layout.TableCandidates);
        Assert.Equal("accepted", candidate.Status);
        Assert.Equal("rule-bounded-table", candidate.Detector);
        Assert.Null(candidate.RejectionReason);
        Assert.Equal("20,20,160,80", candidate.RuleBoundsPx);
        Assert.Equal(2, candidate.RowCount);
        Assert.Equal(2, candidate.ColumnCount);
        Assert.Equal(2, candidate.ColumnAnchors.Count);
        Assert.Equal(2, candidate.RowAnchors.Count);
    }

    [Fact]
    public async Task ConvertAsync_TableRulesDetectFragmentedAntialiasedLightLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeGappedAntialiasedLightTableGridImage());
        var result = await converter.ConvertAsync(stream, "fragmented-light-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(true, table.Style!["imageOcrRuleBounded"]);
        Assert.Equal("20,20,160,80", table.Style["sourceBoundsPx"]);
        Assert.True(result.Diagnostics.Layout.Rules.AverageContrast > 0);
    }

    [Fact]
    public async Task ConvertAsync_VeryLargeImageSkipsGlobalShapeDetectionAndEmitsText()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 260, 260, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(260, 260, 160, 48), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(760, 260, 180, 48), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 260, 460, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(260, 460, 220, 48), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(760, 460, 150, 48), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeLargeTableGridImage());
        var result = await converter.ConvertAsync(stream, "large-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // Very large image: global shape detection is skipped to keep conversion responsive.
        Assert.Contains(result.Warnings, w => w.Contains("shape detection was skipped", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type is "line" or "rect" or "checkbox" or "field" or "signature");
        // OCR text is still produced; aligned text without validated table lines stays text.
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        // The aligned text is still surfaced as a (rejected) table candidate in diagnostics.
        Assert.NotEmpty(result.Diagnostics.Layout.TableCandidates);
    }

    [Fact]
    public async Task ConvertAsync_TableRulesDetectLightLinesOnDarkTableBackground()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeDarkTableGridImage());
        var result = await converter.ConvertAsync(stream, "dark-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(true, table.Style!["imageOcrRuleBounded"]);
        Assert.Equal("19,19,162,82", table.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_StructuredLayoutEmitsTextForEmptyCellGridWithoutVisibleLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Qty Price", 10, 10, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(10, 10, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Qty", Bounds = new OcrBoundingBox(108, 10, 32, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(168, 10, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 10, 32, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(10, 32, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(170, 32, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeImage(240, 100));
        var result = await converter.ConvertAsync(stream, "empty-cell-table.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        // No visible table lines, so the aligned cells are emitted as text (not a table).
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Contains(result.Design.Pages[0].Elements, e => e.Type == "text");

        var document = DesignJsonMapper.MapToPdfDocument(result.Design);
        var pdf = document.ToBytes();
        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf[..4]));
    }

    [Fact]
    public async Task ConvertAsync_TableRulesTolerateIncompleteLines()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeIncompleteTableGridImage());
        var result = await converter.ConvertAsync(stream, "incomplete-grid.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(true, table.Style!["imageOcrRuleBounded"]);
        Assert.Equal("20,20,160,80", table.Style["sourceBoundsPx"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type is "line" or "rect");
    }

    [Fact]
    public async Task ConvertAsync_IsolatedRulesDoNotCreateTableWithoutAlignedOcrRows()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Invoice", 35, 35, [
                new OcrWord { Text = "Invoice", Bounds = new OcrBoundingBox(35, 35, 60, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Total", 35, 65, [
                new OcrWord { Text = "Total", Bounds = new OcrBoundingBox(35, 65, 42, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeTableGridImage());
        var result = await converter.ConvertAsync(stream, "grid.png", new ImageToPdfConversionOptions());

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(2, result.Design.Pages[0].Elements.Count(e => e.Type == "text"));
    }

    [Fact]
    public async Task ConvertAsync_DetectsIsolatedHorizontalRuleAsLineShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 30, 120, 1, paint);
        }));
        var result = await converter.ConvertAsync(stream, "line.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var line = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
        Assert.Equal(14.4, line.X, 1);
        Assert.Equal(21.6, line.Y, 1);
        Assert.Equal(86.4, line.Width, 1);
        Assert.Equal("horizontal-line", line.Style!["imageOcrShapeKind"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastHorizontalRuleAsLineShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(
            canvas =>
            {
                using var paint = new SKPaint { Color = new SKColor(0xB8, 0xC0, 0xCC) };
                canvas.DrawRect(20, 30, 120, 1, paint);
            },
            background: new SKColor(0xF8, 0xFA, 0xFC)));
        var result = await converter.ConvertAsync(stream, "light-line.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var line = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
        Assert.Equal("horizontal-line", line.Style!["imageOcrShapeKind"]);
        Assert.Equal("rule-line", line.Style["imageOcrDetector"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsIsolatedVerticalRuleAsLineShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(40, 20, 1, 80, paint);
        }));
        var result = await converter.ConvertAsync(stream, "line.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var line = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
        Assert.Equal(28.8, line.X, 1);
        Assert.Equal(14.4, line.Y, 1);
        Assert.Equal(57.6, line.Height, 1);
        Assert.Equal("vertical-line", line.Style!["imageOcrShapeKind"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastVerticalRuleAsLineShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(
            canvas =>
            {
                using var paint = new SKPaint { Color = new SKColor(0xB8, 0xC0, 0xCC) };
                canvas.DrawRect(40, 20, 1, 80, paint);
            },
            background: new SKColor(0xF8, 0xFA, 0xFC)));
        var result = await converter.ConvertAsync(stream, "light-vertical-line.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var line = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
        Assert.Equal("vertical-line", line.Style!["imageOcrShapeKind"]);
        Assert.Equal("rule-line", line.Style["imageOcrDetector"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsIsolatedClosedBoxAsRectangleShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 20, 101, 1, paint);
            canvas.DrawRect(20, 80, 101, 1, paint);
            canvas.DrawRect(20, 20, 1, 61, paint);
            canvas.DrawRect(120, 20, 1, 61, paint);
        }));
        var result = await converter.ConvertAsync(stream, "box.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var rect = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
        Assert.Equal(14.4, rect.X, 1);
        Assert.Equal(14.4, rect.Y, 1);
        Assert.Equal(72, rect.Width, 1);
        Assert.Equal(43.2, rect.Height, 1);
        Assert.Equal("rectangle", rect.Style!["imageOcrShapeKind"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "line");
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastClosedBoxAsRectangleShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeShapeImage(
            canvas =>
            {
                using var paint = new SKPaint { Color = new SKColor(0xB8, 0xC0, 0xCC) };
                canvas.DrawRect(20, 20, 101, 1, paint);
                canvas.DrawRect(20, 80, 101, 1, paint);
                canvas.DrawRect(20, 20, 1, 61, paint);
                canvas.DrawRect(120, 20, 1, 61, paint);
            },
            background: new SKColor(0xF8, 0xFA, 0xFC)));
        var result = await converter.ConvertAsync(stream, "light-box.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var rect = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
        Assert.Equal("rectangle", rect.Style!["imageOcrShapeKind"]);
        Assert.Equal("rule-rectangle", rect.Style["imageOcrDetector"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "line");
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("checked")]
    [InlineData("cross")]
    [InlineData("dot")]
    public async Task ConvertAsync_DetectsCheckboxStates(string state)
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeCheckboxImage(state));
        var result = await converter.ConvertAsync(stream, "checkbox.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var checkbox = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "checkbox");
        Assert.Equal(state, checkbox.CheckState);
        Assert.Equal(14.4, checkbox.X, 1);
        Assert.Equal(14.4, checkbox.Y, 1);
        Assert.Equal(13.68, checkbox.Width, 1);
        Assert.Equal(13.68, checkbox.Height, 1);
        Assert.Equal("checkbox", checkbox.Style!["imageOcrRole"]);
        Assert.Equal("rule-square", checkbox.Style["imageOcrDetector"]);
        Assert.Equal("20,20,19,19", checkbox.Style["sourceBoundsPx"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type is "rect" or "line");
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastCheckboxOnLightBackground()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeCheckboxImage(
            "empty",
            background: new SKColor(0xF8, 0xFA, 0xFC),
            rule: new SKColor(0xB8, 0xC0, 0xCC)));
        var result = await converter.ConvertAsync(stream, "light-checkbox.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var checkbox = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "checkbox");
        Assert.Equal("empty", checkbox.CheckState);
        Assert.Equal("rule-square", checkbox.Style!["imageOcrDetector"]);
    }

    [Fact]
    public async Task ConvertAsync_DoesNotGuessWideBoxAsCheckbox()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeWideBoxImage());
        var result = await converter.ConvertAsync(stream, "wide-box.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "checkbox");
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
    }

    [Fact]
    public async Task ConvertAsync_DetectsLabeledFieldWithLeftLabel()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Name", 12, 28, [
                new OcrWord { Text = "Name", Bounds = new OcrBoundingBox(12, 28, 32, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeFieldImage());
        var result = await converter.ConvertAsync(stream, "field.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var field = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "field");
        Assert.Equal("Name", field.FieldLabel);
        Assert.Equal("name", field.FieldName);
        Assert.Equal(43.2, field.X, 1);
        Assert.Equal(18, field.Y, 1);
        Assert.Equal("form-field", field.Style!["imageOcrRole"]);
        Assert.Equal("labeled-rectangle", field.Style["imageOcrDetector"]);
        Assert.Equal("60,25,120,25", field.Style["sourceBoundsPx"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "rect");
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastLabeledFieldOnLightBackground()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Name", 12, 28, [
                new OcrWord { Text = "Name", Bounds = new OcrBoundingBox(12, 28, 32, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeFieldImage(
            background: new SKColor(0xF8, 0xFA, 0xFC),
            rule: new SKColor(0xB8, 0xC0, 0xCC)));
        var result = await converter.ConvertAsync(stream, "light-field.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var field = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "field");
        Assert.Equal("Name", field.FieldLabel);
        Assert.Equal("labeled-rectangle", field.Style!["imageOcrDetector"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsLabeledFieldWithAboveLabel()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Email", 60, 8, [
                new OcrWord { Text = "Email", Bounds = new OcrBoundingBox(60, 8, 34, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeFieldImage());
        var result = await converter.ConvertAsync(stream, "field.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var field = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "field");
        Assert.Equal("Email", field.FieldLabel);
        Assert.Equal("email", field.FieldName);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "rect");
    }

    [Fact]
    public async Task ConvertAsync_KeepsUnlabeledRectangleAsRectShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeFieldImage());
        var result = await converter.ConvertAsync(stream, "field.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "field");
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
    }

    [Fact]
    public async Task ConvertAsync_DetectsSignatureLineWithLeftLabel()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Signature", 12, 42, [
                new OcrWord { Text = "Signature", Bounds = new OcrBoundingBox(12, 42, 52, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeSignatureLineImage());
        var result = await converter.ConvertAsync(stream, "signature.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var signature = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "signature");
        Assert.Equal("Signature", signature.SignatureLabel);
        Assert.Equal(57.6, signature.X, 1);
        Assert.Equal(28.8, signature.Y, 1);
        Assert.Equal("signature", signature.Style!["imageOcrRole"]);
        Assert.Equal("labeled-line", signature.Style["imageOcrDetector"]);
        Assert.Equal("80,50,120,1", signature.Style["sourceBoundsPx"]);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "line");
    }

    [Fact]
    public async Task ConvertAsync_DetectsLowContrastSignatureLineOnLightBackground()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Signature", 12, 42, [
                new OcrWord { Text = "Signature", Bounds = new OcrBoundingBox(12, 42, 52, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeSignatureLineImage(
            background: new SKColor(0xF8, 0xFA, 0xFC),
            rule: new SKColor(0xB8, 0xC0, 0xCC)));
        var result = await converter.ConvertAsync(stream, "light-signature.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var signature = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "signature");
        Assert.Equal("Signature", signature.SignatureLabel);
        Assert.Equal("labeled-line", signature.Style!["imageOcrDetector"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsSignatureLineWithAboveLabel()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Unterschrift", 80, 26, [
                new OcrWord { Text = "Unterschrift", Bounds = new OcrBoundingBox(80, 26, 78, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeSignatureLineImage());
        var result = await converter.ConvertAsync(stream, "signature.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var signature = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "signature");
        Assert.Equal("Unterschrift", signature.SignatureLabel);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "line");
    }

    [Fact]
    public async Task ConvertAsync_KeepsUnlabeledSignatureLineAsLineShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeSignatureLineImage());
        var result = await converter.ConvertAsync(stream, "signature.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "signature");
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
    }

    [Fact]
    public async Task ConvertAsync_DoesNotGuessSignatureFromUnsupportedLabel()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Total", 12, 42, [
                new OcrWord { Text = "Total", Bounds = new OcrBoundingBox(12, 42, 42, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeSignatureLineImage());
        var result = await converter.ConvertAsync(stream, "not-signature.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "signature");
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "line");
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
    }

    [Fact]
    public async Task ConvertAsync_DoesNotEmitTableRulesAsDuplicateShapeElements()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeTableGridImage());
        var result = await converter.ConvertAsync(stream, "grid.png", new ImageToPdfConversionOptions());

        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type is "line" or "rect");
    }

    [Fact]
    public async Task ConvertAsync_DetectsFilledRectangleAsRectShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeFilledRectangleImage(includeTextPixels: false));
        var result = await converter.ConvertAsync(stream, "filled-rect.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var rect = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
        Assert.Equal(21.6, rect.X, 1);
        Assert.Equal(14.4, rect.Y, 1);
        Assert.Equal(72, rect.Width, 1);
        Assert.Equal(28.8, rect.Height, 1);
        Assert.Equal("#60A5FA", rect.Style!["backgroundColor"]);
        Assert.Equal("transparent", rect.Style["borderColor"]);
        Assert.Equal("filled-rectangle", rect.Style["imageOcrShapeKind"]);
        Assert.Equal("connected-fill", rect.Style["imageOcrDetector"]);
        Assert.Equal("30,20,100,40", rect.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsFilledHeaderWithoutDroppingOcrText()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Header", 44, 30, [
                new OcrWord { Text = "Header", Bounds = new OcrBoundingBox(44, 30, 48, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeFilledRectangleImage(includeTextPixels: true));
        var result = await converter.ConvertAsync(stream, "filled-header.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var rect = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
        Assert.Equal("#60A5FA", rect.Style!["backgroundColor"]);
        Assert.Equal("filled-rectangle", rect.Style["imageOcrShapeKind"]);

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("Header", text.Content);
    }

    [Fact]
    public async Task ConvertAsync_DetectsCircleAsCircleShape()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeOvalImage(SKRect.Create(30, 20, 50, 50), complete: true));
        var result = await converter.ConvertAsync(stream, "circle.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var circle = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "circle");
        Assert.Equal("circle", circle.Style!["imageOcrShapeKind"]);
        Assert.Equal("oval-contour", circle.Style["imageOcrDetector"]);
        Assert.Equal("29,19,52,52", circle.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_DetectsEllipseAsCircleElementWithEllipseKind()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeOvalImage(SKRect.Create(24, 26, 86, 36), complete: true));
        var result = await converter.ConvertAsync(stream, "ellipse.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var ellipse = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "circle");
        Assert.Equal("ellipse", ellipse.Style!["imageOcrShapeKind"]);
        Assert.True(ellipse.Width > ellipse.Height);
        Assert.Equal("23,25,88,38", ellipse.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_DoesNotMapIncompleteArcAsCircle()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeOvalImage(SKRect.Create(30, 20, 50, 50), complete: false));
        var result = await converter.ConvertAsync(stream, "arc.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "circle");
    }

    [Fact]
    public async Task ConvertAsync_DetectsNonTextBitmapRegionAsImageElement()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeLogoRegionImage());
        var result = await converter.ConvertAsync(stream, "logo.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var images = result.Design.Pages[0].Elements.Where(e => e.Type == "image").ToList();
        var background = Assert.Single(images, e => e.Style!["imageOcrRole"].Equals("background"));
        Assert.True(background.Locked);

        var region = Assert.Single(images, e => e.Style!["imageOcrRole"].Equals("image-region"));
        Assert.False(region.Locked);
        Assert.StartsWith("data:image/png;base64,", region.Content);
        Assert.Equal("fill", region.FitMode);
        Assert.Equal("connected-region", region.Style!["imageOcrDetector"]);
        Assert.Equal("30,20,52,51", region.Style["sourceBoundsPx"]);
    }

    [Fact]
    public async Task ConvertAsync_DoesNotMapOcrTextAsImageRegion()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("TEXT", 30, 20, [
                new OcrWord { Text = "TEXT", Bounds = new OcrBoundingBox(30, 20, 52, 16), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeLogoRegionImage());
        var result = await converter.ConvertAsync(stream, "text-region.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));
        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
    }

    [Fact]
    public async Task ConvertAsync_DoesNotMapDetectedShapesAsImageRegions()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeFilledRectangleImage(includeTextPixels: false));
        var result = await converter.ConvertAsync(stream, "shape-region.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "rect");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));
    }

    [Fact]
    public async Task ConvertAsync_DoesNotMapTinyBitmapRegionAsImageElement()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([]));

        using var stream = new MemoryStream(MakeTinyImageRegionImage());
        var result = await converter.ConvertAsync(stream, "tiny-region.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("background"));
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));
    }

    [Fact]
    public async Task ConvertAsync_MapsMixedFormElementsWithoutDuplicateShapes()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Application", 20, 18, [
                new OcrWord { Text = "Application", Bounds = new OcrBoundingBox(20, 18, 78, 14), Confidence = 0.95 },
            ]),
            MakeOcrLine("Name", 12, 28, [
                new OcrWord { Text = "Name", Bounds = new OcrBoundingBox(12, 28, 32, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Agree", 48, 84, [
                new OcrWord { Text = "Agree", Bounds = new OcrBoundingBox(48, 84, 42, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Signature", 20, 98, [
                new OcrWord { Text = "Signature", Bounds = new OcrBoundingBox(20, 98, 62, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Item Price", 35, 145, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 145, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 145, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 175, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 175, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 175, 36, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeMixedFormImage());
        var result = await converter.ConvertAsync(stream, "mixed-form.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var elements = result.Design.Pages[0].Elements;
        Assert.Single(elements, e => e.Type == "table");
        Assert.Single(elements, e => e.Type == "checkbox");
        Assert.Single(elements, e => e.Type == "field");
        Assert.Single(elements, e => e.Type == "signature");
        Assert.Single(elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));
        Assert.Single(elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("background"));
        Assert.Equal(2, elements.Count(e => e.Type == "text"));
        Assert.DoesNotContain(elements, e => e.Type is "line" or "rect");

        var table = Assert.Single(elements, e => e.Type == "table");
        Assert.Equal(new[] { new[] { "Item", "Price" }, new[] { "Coffee", "3.50" } }, table.CellData);

        var checkbox = Assert.Single(elements, e => e.Type == "checkbox");
        Assert.Equal("checked", checkbox.CheckState);
        Assert.Equal("checkbox", checkbox.Style!["imageOcrRole"]);

        var field = Assert.Single(elements, e => e.Type == "field");
        Assert.Equal("Name", field.FieldLabel);

        var signature = Assert.Single(elements, e => e.Type == "signature");
        Assert.Equal("Signature", signature.SignatureLabel);
    }

    [Fact]
    public async Task ConvertAsync_DoesNotMapTextPixelsAsShapeElements()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("DENSE TEXT", 18, 18, [
                new OcrWord { Text = "DENSE", Bounds = new OcrBoundingBox(18, 18, 70, 28), Confidence = 0.95 },
                new OcrWord { Text = "TEXT", Bounds = new OcrBoundingBox(96, 18, 62, 28), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeDenseTextPixelImage());
        var result = await converter.ConvertAsync(stream, "dense-text.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("DENSE TEXT", text.Content);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type is "line" or "rect" or "circle" or "checkbox" or "field" or "signature" or "table");
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));
    }

    [Fact]
    public async Task ConvertAsync_AddsSourceDiagnosticsToMappedOcrElements()
    {
        var elements = new List<ElementDto>();
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeImage(220, 70), [
            MakeOcrLine("Black Red", 10, 10, [
                new OcrWord { Text = "Black", Bounds = new OcrBoundingBox(10, 10, 30, 12), Confidence = 0.95 },
                new OcrWord { Text = "Red", Bounds = new OcrBoundingBox(52, 10, 24, 12), Confidence = 0.94 },
            ]),
        ])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeTableGridImage(), [
            MakeOcrLine("Item Price", 35, 35, [
                new OcrWord { Text = "Item", Bounds = new OcrBoundingBox(35, 35, 40, 12), Confidence = 0.95 },
                new OcrWord { Text = "Price", Bounds = new OcrBoundingBox(125, 35, 45, 12), Confidence = 0.95 },
            ]),
            MakeOcrLine("Coffee 3.50", 35, 65, [
                new OcrWord { Text = "Coffee", Bounds = new OcrBoundingBox(35, 65, 54, 12), Confidence = 0.95 },
                new OcrWord { Text = "3.50", Bounds = new OcrBoundingBox(127, 65, 36, 12), Confidence = 0.95 },
            ]),
        ])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeShapeImage(canvas =>
        {
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 30, 120, 1, paint);
        }), [])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeFilledRectangleImage(includeTextPixels: false), [])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeOvalImage(SKRect.Create(30, 20, 50, 50), complete: true), [])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeCheckboxImage("checked"), [])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeFieldImage(), [
            MakeOcrLine("Name", 12, 28, [
                new OcrWord { Text = "Name", Bounds = new OcrBoundingBox(12, 28, 32, 12), Confidence = 0.95 },
            ]),
        ])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeSignatureLineImage(), [
            MakeOcrLine("Signature", 12, 42, [
                new OcrWord { Text = "Signature", Bounds = new OcrBoundingBox(12, 42, 52, 12), Confidence = 0.95 },
            ]),
        ])).Design.Pages[0].Elements);
        elements.AddRange((await ConvertWithFakeOcrAsync(MakeLogoRegionImage(), [])).Design.Pages[0].Elements);

        var mapped = elements
            .Where(e => e.Style is not null)
            .Where(e => !e.Style!["imageOcrRole"].Equals("background"))
            .ToList();

        Assert.Contains(mapped, e => e.Type == "text");
        Assert.Contains(mapped, e => e.Type == "table");
        Assert.Contains(mapped, e => e.Type == "line");
        Assert.Contains(mapped, e => e.Type == "rect");
        Assert.Contains(mapped, e => e.Type == "circle");
        Assert.Contains(mapped, e => e.Type == "checkbox");
        Assert.Contains(mapped, e => e.Type == "field");
        Assert.Contains(mapped, e => e.Type == "signature");
        Assert.Contains(mapped, e => e.Type == "image" && e.Style!["imageOcrRole"].Equals("image-region"));

        Assert.All(mapped, element =>
        {
            Assert.NotNull(element.Style);
            Assert.True(element.Style!.ContainsKey("imageOcrRole"), element.Type);
            Assert.True(element.Style.ContainsKey("imageOcrConfidence"), element.Type);
            Assert.True(element.Style.ContainsKey("imageOcrDetector"), element.Type);
            Assert.True(element.Style.ContainsKey("sourceBoundsPx"), element.Type);
        });
    }

    [Fact]
    public async Task ConvertAsync_EstimatesBlackTextColorFromOriginalImage()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Black", 10, 10, [
                new OcrWord { Text = "Black", Bounds = new OcrBoundingBox(10, 10, 30, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeColorSampleImage(SKColors.White, SKColors.Black, new SKRect(10, 10, 40, 22)));
        var result = await converter.ConvertAsync(stream, "black.png", new ImageToPdfConversionOptions());

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("#000000", text.Style!["color"]);
    }

    [Fact]
    public async Task ConvertAsync_EstimatesSaturatedTextColorFromOriginalImage()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Red", 10, 10, [
                new OcrWord { Text = "Red", Bounds = new OcrBoundingBox(10, 10, 30, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeColorSampleImage(SKColors.White, SKColors.Red, new SKRect(10, 10, 40, 22)));
        var result = await converter.ConvertAsync(stream, "red.png", new ImageToPdfConversionOptions());

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("#FF0000", text.Style!["color"]);
    }

    [Fact]
    public async Task ConvertAsync_UsesFallbackTextColorWhenOnlyBackgroundIsSampled()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Background", 10, 10, [
                new OcrWord { Text = "Background", Bounds = new OcrBoundingBox(10, 10, 70, 12), Confidence = 0.95 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeColorSampleImage(SKColors.White, SKColors.White, new SKRect(10, 10, 80, 22)));
        var result = await converter.ConvertAsync(stream, "background.png", new ImageToPdfConversionOptions());

        var text = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "text");
        Assert.Equal("#111827", text.Style!["color"]);
    }

    [Fact]
    public async Task ConvertAsync_SplitsColoredWordRunInsideLine()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Black Red Black", 10, 10, [
                new OcrWord { Text = "Black", Bounds = new OcrBoundingBox(10, 10, 30, 12), Confidence = 0.95 },
                new OcrWord { Text = "Red", Bounds = new OcrBoundingBox(48, 10, 24, 12), Confidence = 0.94 },
                new OcrWord { Text = "Black", Bounds = new OcrBoundingBox(82, 10, 30, 12), Confidence = 0.96 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeColorRunImage());
        var result = await converter.ConvertAsync(stream, "colored-run.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(["Black", "Red", "Black"], texts.Select(t => t.Content!).ToArray());
        Assert.Equal(["#000000", "#FF0000", "#000000"], texts.Select(t => (string)t.Style!["color"]).ToArray());
        Assert.All(texts, text => Assert.Equal("text-run", text.Style!["imageOcrRole"]));
        Assert.All(texts, text => Assert.Equal(true, text.Style!["imageOcrRunSplit"]));
    }

    [Fact]
    public async Task ConvertAsync_SplitsDifferentlySizedWordRunInsideLine()
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine([
            MakeOcrLine("Small Large", 10, 10, [
                new OcrWord { Text = "Small", Bounds = new OcrBoundingBox(10, 18, 32, 12), Confidence = 0.95 },
                new OcrWord { Text = "Large", Bounds = new OcrBoundingBox(52, 10, 52, 22), Confidence = 0.94 },
            ]),
        ]));

        using var stream = new MemoryStream(MakeSizeRunImage());
        var result = await converter.ConvertAsync(stream, "sized-run.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });

        var texts = result.Design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.Equal(["Small", "Large"], texts.Select(t => t.Content!).ToArray());
        Assert.All(texts, text => Assert.Equal("text-run", text.Style!["imageOcrRole"]));
        Assert.True((double)texts[1].Style!["fontSize"] > (double)texts[0].Style!["fontSize"]);
        Assert.Equal("10,18,32,12", texts[0].Style!["sourceBoundsPx"]);
        Assert.Equal("52,10,52,22", texts[1].Style!["sourceBoundsPx"]);
    }

    private static byte[] MakeImage(int width, int height) =>
        MakeImage(width, height, SKEncodedImageFormat.Png);

    private static byte[] MakeImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.LightSteelBlue };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, format == SKEncodedImageFormat.Jpeg ? 90 : 100);
        return data.ToArray();
    }

    private static byte[] MakeImageWithRectangle(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        using (var border = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
        })
        {
            canvas.Clear(SKColors.White);
            canvas.DrawRect(150, 20, 40, 40, border);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeColorSampleImage(SKColor background, SKColor sample, SKRect sampleRect)
    {
        using var bitmap = new SKBitmap(100, 60, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background);
            using var paint = new SKPaint { Color = sample };
            canvas.DrawRect(sampleRect, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeColorRunImage()
    {
        using var bitmap = new SKBitmap(140, 60, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var black = new SKPaint { Color = SKColors.Black };
            using var red = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(new SKRect(10, 10, 40, 22), black);
            canvas.DrawRect(new SKRect(48, 10, 72, 22), red);
            canvas.DrawRect(new SKRect(82, 10, 112, 22), black);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeSizeRunImage()
    {
        using var bitmap = new SKBitmap(140, 60, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var black = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(new SKRect(10, 18, 42, 30), black);
            canvas.DrawRect(new SKRect(52, 10, 104, 32), black);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeShapeImage(Action<SKCanvas> draw, SKColor? background = null)
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? SKColors.White);
            draw(canvas);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeTableGridImage()
        => MakeTableGridImage(SKColors.White, SKColors.Black);

    private static byte[] MakeTableGridImage(SKColor background, SKColor rule)
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background);
            using var paint = new SKPaint { Color = rule };

            canvas.DrawRect(20, 20, 161, 1, paint);
            canvas.DrawRect(20, 60, 161, 1, paint);
            canvas.DrawRect(20, 100, 161, 1, paint);
            canvas.DrawRect(20, 20, 1, 81, paint);
            canvas.DrawRect(100, 20, 1, 81, paint);
            canvas.DrawRect(180, 20, 1, 81, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeGappedAntialiasedLightTableGridImage()
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(0xF8, 0xFA, 0xFC));
            using var paint = new SKPaint
            {
                Color = new SKColor(0xE0, 0xE6, 0xEE),
                StrokeWidth = 1,
                IsAntialias = true,
            };

            foreach (var y in new[] { 20.5f, 60.5f, 100.5f })
            {
                canvas.DrawLine(20, y, 78, y, paint);
                canvas.DrawLine(81, y, 139, y, paint);
                canvas.DrawLine(142, y, 180, y, paint);
            }

            foreach (var x in new[] { 20.5f, 100.5f, 180.5f })
            {
                canvas.DrawLine(x, 20, x, 48, paint);
                canvas.DrawLine(x, 51, x, 78, paint);
                canvas.DrawLine(x, 81, x, 100, paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeLightFilledTableBackgroundImage()
    {
        using var bitmap = new SKBitmap(260, 140, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var header = new SKPaint { Color = new SKColor(0xF1, 0xF5, 0xF9) };
            using var zebra = new SKPaint { Color = new SKColor(0xF8, 0xFA, 0xFC) };
            canvas.DrawRect(20, 24, 210, 24, header);
            canvas.DrawRect(20, 52, 210, 24, zebra);
            canvas.DrawRect(20, 80, 210, 24, header);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeLargeTableGridImage()
    {
        using var bitmap = new SKBitmap(3200, 2600, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };

            canvas.DrawRect(200, 200, 1001, 2, paint);
            canvas.DrawRect(200, 400, 1001, 2, paint);
            canvas.DrawRect(200, 600, 1001, 2, paint);
            canvas.DrawRect(200, 200, 2, 401, paint);
            canvas.DrawRect(700, 200, 2, 401, paint);
            canvas.DrawRect(1200, 200, 2, 401, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeDarkTableGridImage()
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var panel = new SKPaint { Color = new SKColor(0x1F, 0x29, 0x37) };
            canvas.DrawRect(20, 20, 161, 81, panel);

            using var paint = new SKPaint { Color = new SKColor(0xF8, 0xFA, 0xFC) };
            canvas.DrawRect(20, 20, 161, 1, paint);
            canvas.DrawRect(20, 60, 161, 1, paint);
            canvas.DrawRect(20, 100, 161, 1, paint);
            canvas.DrawRect(20, 20, 1, 81, paint);
            canvas.DrawRect(100, 20, 1, 81, paint);
            canvas.DrawRect(180, 20, 1, 81, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeCheckboxImage(string state, SKColor? background = null, SKColor? rule = null)
    {
        using var bitmap = new SKBitmap(100, 70, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? SKColors.White);
            using var paint = new SKPaint { Color = rule ?? SKColors.Black };

            canvas.DrawRect(20, 20, 20, 1, paint);
            canvas.DrawRect(20, 39, 20, 1, paint);
            canvas.DrawRect(20, 20, 1, 20, paint);
            canvas.DrawRect(39, 20, 1, 20, paint);

            if (state == "checked")
            {
                using var stroke = new SKPaint { Color = rule ?? SKColors.Black, StrokeWidth = 2, IsAntialias = false };
                canvas.DrawLine(24, 30, 28, 35, stroke);
                canvas.DrawLine(28, 35, 36, 24, stroke);
            }
            else if (state == "cross")
            {
                using var stroke = new SKPaint { Color = rule ?? SKColors.Black, StrokeWidth = 2, IsAntialias = false };
                canvas.DrawLine(24, 24, 36, 36, stroke);
                canvas.DrawLine(36, 24, 24, 36, stroke);
            }
            else if (state == "dot")
            {
                canvas.DrawCircle(30, 30, 4, paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeWideBoxImage()
    {
        using var bitmap = new SKBitmap(110, 70, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };
            canvas.DrawRect(20, 20, 44, 1, paint);
            canvas.DrawRect(20, 38, 44, 1, paint);
            canvas.DrawRect(20, 20, 1, 19, paint);
            canvas.DrawRect(63, 20, 1, 19, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeFieldImage(SKColor? background = null, SKColor? rule = null)
    {
        using var bitmap = new SKBitmap(220, 90, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? SKColors.White);
            using var paint = new SKPaint { Color = rule ?? SKColors.Black };
            canvas.DrawRect(60, 25, 121, 1, paint);
            canvas.DrawRect(60, 50, 121, 1, paint);
            canvas.DrawRect(60, 25, 1, 26, paint);
            canvas.DrawRect(180, 25, 1, 26, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeSignatureLineImage(SKColor? background = null, SKColor? rule = null)
    {
        using var bitmap = new SKBitmap(240, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(background ?? SKColors.White);
            using var paint = new SKPaint { Color = rule ?? SKColors.Black };
            canvas.DrawRect(80, 50, 120, 1, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeFilledRectangleImage(bool includeTextPixels)
    {
        using var bitmap = new SKBitmap(180, 90, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var fill = new SKPaint { Color = new SKColor(0x60, 0xA5, 0xFA) };
            canvas.DrawRect(new SKRect(30, 20, 130, 60), fill);

            if (includeTextPixels)
            {
                using var text = new SKPaint { Color = SKColors.Black };
                canvas.DrawRect(new SKRect(44, 30, 92, 42), text);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeOvalImage(SKRect ovalBounds, bool complete)
    {
        using var bitmap = new SKBitmap(140, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = false,
            };

            if (complete)
                canvas.DrawOval(ovalBounds, paint);
            else
                canvas.DrawArc(ovalBounds, 20, 185, false, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeLogoRegionImage()
    {
        using var bitmap = new SKBitmap(140, 90, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var blue = new SKPaint { Color = new SKColor(0x25, 0x63, 0xEB) };
            using var green = new SKPaint { Color = new SKColor(0x16, 0xA3, 0x4A) };
            using var path = new SKPath();
            path.MoveTo(30, 20);
            path.LineTo(70, 28);
            path.LineTo(50, 72);
            path.LineTo(38, 42);
            path.Close();
            canvas.DrawPath(path, blue);
            canvas.DrawCircle(70, 46, 12, green);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeTinyImageRegionImage()
    {
        using var bitmap = new SKBitmap(80, 60, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = new SKColor(0x25, 0x63, 0xEB) };
            canvas.DrawRect(30, 20, 10, 10, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeMixedFormImage()
    {
        using var bitmap = new SKBitmap(320, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var black = new SKPaint { Color = SKColors.Black };

            canvas.DrawRect(60, 25, 121, 1, black);
            canvas.DrawRect(60, 60, 121, 1, black);
            canvas.DrawRect(60, 25, 1, 36, black);
            canvas.DrawRect(180, 25, 1, 36, black);

            canvas.DrawRect(20, 82, 20, 1, black);
            canvas.DrawRect(20, 101, 20, 1, black);
            canvas.DrawRect(20, 82, 1, 20, black);
            canvas.DrawRect(39, 82, 1, 20, black);
            using (var stroke = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = false })
            {
                canvas.DrawLine(24, 92, 28, 97, stroke);
                canvas.DrawLine(28, 97, 36, 86, stroke);
            }

            canvas.DrawRect(90, 105, 140, 1, black);

            canvas.DrawRect(20, 130, 161, 1, black);
            canvas.DrawRect(20, 170, 161, 1, black);
            canvas.DrawRect(20, 210, 161, 1, black);
            canvas.DrawRect(20, 130, 1, 81, black);
            canvas.DrawRect(100, 130, 1, 81, black);
            canvas.DrawRect(180, 130, 1, 81, black);

            using var blue = new SKPaint { Color = new SKColor(0x25, 0x63, 0xEB) };
            using var green = new SKPaint { Color = new SKColor(0x16, 0xA3, 0x4A) };
            using var path = new SKPath();
            path.MoveTo(250, 20);
            path.LineTo(290, 28);
            path.LineTo(270, 72);
            path.LineTo(258, 42);
            path.Close();
            canvas.DrawPath(path, blue);
            canvas.DrawCircle(290, 46, 12, green);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeDenseTextPixelImage()
    {
        using var bitmap = new SKBitmap(190, 80, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };

            canvas.DrawRect(18, 18, 70, 5, paint);
            canvas.DrawRect(18, 30, 58, 5, paint);
            canvas.DrawRect(18, 42, 70, 5, paint);
            canvas.DrawRect(96, 18, 62, 5, paint);
            canvas.DrawRect(118, 18, 5, 28, paint);
            canvas.DrawRect(136, 18, 5, 28, paint);
            canvas.DrawRect(96, 42, 62, 5, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] MakeIncompleteTableGridImage()
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };

            canvas.DrawRect(20, 20, 70, 1, paint);
            canvas.DrawRect(105, 20, 75, 1, paint);
            canvas.DrawRect(20, 60, 160, 1, paint);
            canvas.DrawRect(20, 100, 72, 1, paint);
            canvas.DrawRect(106, 100, 74, 1, paint);
            canvas.DrawRect(20, 20, 1, 81, paint);
            canvas.DrawRect(180, 20, 1, 81, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] ReadDataUriBytes(string? dataUri)
    {
        const string prefix = "data:image/png;base64,";
        Assert.NotNull(dataUri);
        Assert.StartsWith(prefix, dataUri);
        return Convert.FromBase64String(dataUri[prefix.Length..]);
    }

    private static SKBitmap DecodeBitmap(byte[] bytes)
    {
        using var stream = new SKMemoryStream(bytes);
        return SKBitmap.Decode(stream);
    }

    private static byte[] WithJfifDpi(byte[] jpeg, ushort xDpi, ushort yDpi)
    {
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new ArgumentException("Expected JPEG bytes.", nameof(jpeg));

        byte[] app0 =
        [
            0xFF, 0xE0,
            0x00, 0x10,
            (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00,
            0x01, 0x01,
            0x01,
            (byte)(xDpi >> 8), (byte)xDpi,
            (byte)(yDpi >> 8), (byte)yDpi,
            0x00, 0x00,
        ];

        var output = new byte[jpeg.Length + app0.Length];
        Buffer.BlockCopy(jpeg, 0, output, 0, 2);
        Buffer.BlockCopy(app0, 0, output, 2, app0.Length);
        Buffer.BlockCopy(jpeg, 2, output, 2 + app0.Length, jpeg.Length - 2);
        return output;
    }

    private static byte[] InjectExifOrientation(byte[] jpeg, ushort orientation)
    {
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new ArgumentException("Expected JPEG bytes.", nameof(jpeg));

        byte[] tiff =
        [
            0x49, 0x49,
            0x2A, 0x00,
            0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01,
            0x03, 0x00,
            0x01, 0x00, 0x00, 0x00,
            (byte)(orientation & 0xFF), (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        byte[] exifHeader = "Exif\0\0"u8.ToArray();
        var payloadLength = exifHeader.Length + tiff.Length + 2;
        byte[] app1 =
        [
            0xFF, 0xE1,
            (byte)(payloadLength >> 8), (byte)(payloadLength & 0xFF),
            .. exifHeader,
            .. tiff,
        ];

        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
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

    private static OcrLine MakeOcrLine(string text, int x, int y, IReadOnlyList<OcrWord> words)
    {
        var bounds = new OcrBoundingBox(
            x,
            y,
            words.Max(w => w.Bounds.X + w.Bounds.Width) - x,
            words.Max(w => w.Bounds.Y + w.Bounds.Height) - y);
        return new OcrLine
        {
            Text = text,
            Bounds = bounds,
            Confidence = words.Average(w => w.Confidence),
            Words = words,
        };
    }

    private static Task<ImageToPdfConversionResult> ConvertWithFakeOcrAsync(byte[] image, IReadOnlyList<OcrLine> lines)
    {
        var converter = new ImageToPdfConverter(new FakeOcrEngine(lines));
        using var stream = new MemoryStream(image);
        return converter.ConvertAsync(stream, "diagnostics.png", new ImageToPdfConversionOptions
        {
            SourceDpiX = 100,
            SourceDpiY = 100,
        });
    }

    private static string ResolveTableSamplePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "TableSamples", fileName);

    private static IReadOnlyList<OcrLine> LoadOcrFixtureLines(string path)
    {
        var json = File.ReadAllText(path);
        var fixture = JsonSerializer.Deserialize<IReadOnlyList<OcrLineFixture>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];

        return fixture
            .Select(line => new OcrLine
            {
                Text = line.Text,
                Bounds = line.Bounds.ToOcrBoundingBox(),
                Confidence = line.Confidence,
                Words = line.Words.Select(word => new OcrWord
                {
                    Text = word.Text,
                    Bounds = word.Bounds.ToOcrBoundingBox(),
                    Confidence = word.Confidence,
                }).ToArray(),
            })
            .ToArray();
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

    private sealed class CapturingOcrEngine : IOcrEngine
    {
        private readonly FakeOcrEngine _inner;

        public CapturingOcrEngine(IReadOnlyList<OcrLine> lines)
        {
            _inner = new FakeOcrEngine(lines);
        }

        public string Name => "CapturingOCR";
        public string Version => "test";
        public byte[] CapturedEncodedImageBytes { get; private set; } = [];
        public int CapturedWidthPx { get; private set; }
        public int CapturedHeightPx { get; private set; }

        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
            IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            CapturedEncodedImageBytes = pages[0].EncodedImageBytes;
            CapturedWidthPx = pages[0].WidthPx;
            CapturedHeightPx = pages[0].HeightPx;
            return _inner.RecognizeAsync(pages, options, cancellationToken);
        }
    }

    private sealed record OcrLineFixture(
        string Text,
        OcrBoundsFixture Bounds,
        double Confidence,
        IReadOnlyList<OcrWordFixture> Words);

    private sealed record OcrWordFixture(
        string Text,
        OcrBoundsFixture Bounds,
        double Confidence);

    private sealed record OcrBoundsFixture(int X, int Y, int Width, int Height)
    {
        public OcrBoundingBox ToOcrBoundingBox() => new(X, Y, Width, Height);
    }
}
