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
    public async Task ConvertAsync_StructuredLayoutBuildsSimpleTableFromAlignedOcrWords()
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

        var table = Assert.Single(result.Design.Pages[0].Elements, e => e.Type == "table");
        Assert.Equal(new[] { new[] { "Item", "Price" }, new[] { "Coffee", "3.50" } }, table.CellData);
        Assert.Equal(2, table.Style!["rows"]);
        Assert.Equal(2, table.Style["columns"]);
        Assert.True(table.HeaderRow);
        Assert.Equal(4.18, table.X, 1);
        Assert.Equal(4.18, table.Y, 1);
        Assert.DoesNotContain(result.Design.Pages[0].Elements, e => e.Type == "text");

        var document = DesignJsonMapper.MapToPdfDocument(result.Design);
        var pdf = document.ToBytes();
        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf[..4]));
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

    private static byte[] MakeTableGridImage()
    {
        using var bitmap = new SKBitmap(200, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black };

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

        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
            IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options,
            CancellationToken cancellationToken = default)
        {
            CapturedEncodedImageBytes = pages[0].EncodedImageBytes;
            return _inner.RecognizeAsync(pages, options, cancellationToken);
        }
    }
}
