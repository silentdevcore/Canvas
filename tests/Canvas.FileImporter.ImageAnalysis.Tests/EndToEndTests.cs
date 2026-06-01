using Canvas.FileImporter.ImageAnalysis;
using Canvas.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Tests;

/// <summary>
/// End-to-end tests: run the full 5-phase pipeline and assert on the
/// resulting DesignExportDto element types and counts.
/// </summary>
public class EndToEndTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>White background with a solid coloured rectangle and a line of text.</summary>
    private static SKBitmap MakeDocumentBitmap(
        int width = 400, int height = 200,
        string text = "Hello World",
        float fontSize = 22f)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        // Blue filled rectangle (shape candidate)
        using var rectPaint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = false };
        canvas.DrawRect(30, 120, 120, 50, rectPaint);

        // Black text at the top (text candidate)
        using var font  = new SKFont(SKTypeface.FromFamilyName("Courier New"), fontSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        canvas.DrawText(text, 30, fontSize + 10, font, paint);

        return bmp;
    }

    private static void AssertApproximately(double expected, double actual, double tolerance, string label)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"{label}: expected {expected} +/- {tolerance}, got {actual}");
    }

    // ── Full pipeline ─────────────────────────────────────────────────────────

    [Fact]
    public void FullPipeline_ImageWithColorRect_ProducesDesignWithShapeElement()
    {
        using var bmp    = MakeDocumentBitmap();
        var design       = ImageAnalysisFileImporter.Import(bmp, "test");

        Assert.NotNull(design);
        Assert.NotNull(design.Pages);
        Assert.NotEmpty(design.Pages);

        var elements = design.Pages[0].Elements;
        Assert.NotNull(elements);
        Assert.NotEmpty(elements);

        // Must contain at least one shape or rect element (from the blue rectangle)
        bool hasShape = elements.Any(e =>
            e.Type is "shape" or "rect" or "region");
        Assert.True(hasShape,
            $"Expected a shape/rect element. Types found: {string.Join(", ", elements.Select(e => e.Type))}");
    }

    [Fact]
    public void FullPipeline_ImageWithText_ProducesDesignWithTextElement()
    {
        using var bmp  = MakeDocumentBitmap(text: "Hello World", fontSize: 24f);
        var design     = ImageAnalysisFileImporter.Import(bmp, "test");

        var elements   = design.Pages[0].Elements;

        // Must contain at least one text element (from the rendered text)
        bool hasText = elements.Any(e => e.Type == "text");
        Assert.True(hasText,
            $"Expected a text element. Types found: {string.Join(", ", elements.Select(e => e.Type))}");
    }

    [Fact]
    public void FullPipeline_ImageWithText_TextElementHasPositiveFontSize()
    {
        using var bmp  = MakeDocumentBitmap(text: "Test", fontSize: 20f);
        var design     = ImageAnalysisFileImporter.Import(bmp, "test");

        var textEls = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.NotEmpty(textEls);

        foreach (var el in textEls)
        {
            Assert.True(el.Style.TryGetValue("fontSize", out var fs),
                "text element must have fontSize in Style");
            double size = Convert.ToDouble(fs);
            Assert.True(size > 0, $"fontSize must be > 0 but was {size}");
        }
    }

    [Fact]
    public void FullPipeline_ImageWithText_ProducesRecognizedGlyphContent()
    {
        using var bmp  = MakeDocumentBitmap(text: "Hello", fontSize: 28f);
        var design     = ImageAnalysisFileImporter.Import(bmp, "test");

        var textEls = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();
        Assert.True(textEls.Count > 0, "At least one text element expected");
        Assert.True(
            textEls.Any(el => el.Content == "Hello"),
            $"Expected exact content 'Hello'. Found: {string.Join(" | ", textEls.Select(el => el.Content))}");
    }

    [Fact]
    public void FullPipeline_PageSettings_ReflectsImageDimensions()
    {
        using var bmp  = MakeDocumentBitmap(width: 320, height: 240);
        var design     = ImageAnalysisFileImporter.Import(bmp, "test");

        // Page dimensions must match the image (scale factor = 1 for 320×240)
        Assert.True(design.PageSettings.Width  > 0, "PageSettings.Width must be > 0");
        Assert.True(design.PageSettings.Height > 0, "PageSettings.Height must be > 0");
        Assert.True(Math.Abs(design.PageSettings.Width  - 320) < 2, $"Expected width ≈320, got {design.PageSettings.Width}");
        Assert.True(Math.Abs(design.PageSettings.Height - 240) < 2, $"Expected height ≈240, got {design.PageSettings.Height}");
    }

    [Fact]
    public void FullPipeline_PageSettings_PreservesDetectedBackgroundColor()
    {
        using var bmp = new SKBitmap(220, 140, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(new SKColor(240, 248, 232));
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(60, 50, 50, 30, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "background");

        Assert.NotNull(design.PageSettings);
        Assert.Equal("#F0F8E8", design.PageSettings.BackgroundColor);
    }

    [Fact]
    public void ImportWithAnalysis_ImageWithTextAndShape_ReturnsDiagnostics()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            "diagnostic-test",
            includeDebugOverlay: false);

        Assert.NotNull(result.Design);
        Assert.Equal(400, result.Diagnostics.SourceWidthPx);
        Assert.Equal(200, result.Diagnostics.SourceHeightPx);
        Assert.True(result.Diagnostics.ColorRegionCount > 0);
        Assert.True(result.Diagnostics.TextLineCount > 0);
        Assert.True(result.Diagnostics.GlyphCount >= 5);
        Assert.True(result.Diagnostics.ElementCount > 0);
        Assert.Null(result.DebugOverlayPng);
    }

    [Fact]
    public void ImportWithAnalysis_WhenDebugOverlayRequested_ReturnsPngBytes()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            "overlay-test",
            includeDebugOverlay: true);

        Assert.NotNull(result.DebugOverlayPng);
        Assert.True(result.DebugOverlayPng.Length > 8);
        Assert.Equal(0x89, result.DebugOverlayPng[0]);
        Assert.Equal((byte)'P', result.DebugOverlayPng[1]);
        Assert.Equal((byte)'N', result.DebugOverlayPng[2]);
        Assert.Equal((byte)'G', result.DebugOverlayPng[3]);
    }

    [Fact]
    public void ImportWithAnalysis_DefaultOptions_DoesNotIncludeFallbackImageLayer()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(bmp, "no-fallback");

        Assert.DoesNotContain(result.Design.Pages[0].Elements, e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "fallback-image"));
    }

    [Fact]
    public void ImportWithAnalysis_WhenFallbackRequested_AddsLockedFallbackImageLayer()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            "fallback",
            targetWidthPt: null,
            targetHeightPt: null,
            options: new ImageAnalysisOptions { IncludeFallbackImageLayer = true });

        var fallback = Assert.Single(result.Design.Pages[0].Elements, e =>
            e.Type == "image" &&
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "fallback-image"));

        Assert.True(fallback.Locked);
        Assert.StartsWith("data:image/png;base64,", fallback.Content);
        Assert.Equal(0, fallback.X);
        Assert.Equal(0, fallback.Y);
        Assert.Equal(result.Design.PageSettings!.Width, fallback.Width);
        Assert.Equal(result.Design.PageSettings.Height, fallback.Height);
        Assert.Contains("Fallback image layer included.", result.Diagnostics.Warnings);
    }

    [Fact]
    public void ImportWithAnalysis_HighConfidenceThreshold_MarksLowConfidenceElements()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            "low-confidence",
            targetWidthPt: null,
            targetHeightPt: null,
            options: new ImageAnalysisOptions { LowConfidenceThreshold = 0.99 });

        Assert.Contains(result.Design.Pages[0].Elements, e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisLowConfidence", out var value) &&
            value is true);
        Assert.Contains("Some elements are below the configured confidence threshold.", result.Diagnostics.Warnings);
    }

    [Fact]
    public void FullPipeline_AllElements_HavePositiveDimensions()
    {
        using var bmp  = MakeDocumentBitmap();
        var design     = ImageAnalysisFileImporter.Import(bmp, "test");

        foreach (var el in design.Pages[0].Elements)
        {
            Assert.True(el.Width  > 0, $"Element {el.Id} ({el.Type}) has Width={el.Width}");
            Assert.True(el.Height > 0, $"Element {el.Id} ({el.Type}) has Height={el.Height}");
        }
    }

    [Fact]
    public void FullPipeline_AllElements_IncludeAnalysisMetadata()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);
        var design = ImageAnalysisFileImporter.Import(bmp, "metadata");

        Assert.NotEmpty(design.Pages[0].Elements);
        foreach (var el in design.Pages[0].Elements)
        {
            Assert.NotNull(el.Style);
            Assert.True(el.Style.ContainsKey("imageAnalysisType"), $"Element {el.Id} missing imageAnalysisType");
            Assert.True(el.Style.ContainsKey("imageAnalysisConfidence"), $"Element {el.Id} missing imageAnalysisConfidence");
            Assert.True(el.Style.ContainsKey("sourceBoundsPx"), $"Element {el.Id} missing sourceBoundsPx");

            double confidence = Convert.ToDouble(el.Style["imageAnalysisConfidence"]);
            Assert.InRange(confidence, 0, 1);
        }
    }

    [Fact]
    public void FullPipeline_PositionedText_MapsInkBoundsToCanvasCoordinates()
    {
        using var bmp = new SKBitmap(360, 160, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawText("Hello", 48, 82, font, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "positioned-text");
        var text = Assert.Single(design.Pages[0].Elements, e => e.Type == "text");

        Assert.Equal("Hello", text.Content);
        AssertApproximately(49, text.X, 4, "text x");
        AssertApproximately(62, text.Y, 5, "text y");
        AssertApproximately(84, text.Width, 8, "text width");
        Assert.True(text.Height >= 20, $"text height should cover ink and line height, got {text.Height}");
    }

    [Fact]
    public void FullPipeline_TextElement_IncludesBaselineMetadata()
    {
        using var bmp = new SKBitmap(360, 160, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawText("Hello", 48, 82, font, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "baseline-text");
        var text = Assert.Single(design.Pages[0].Elements, e => e.Type == "text");

        Assert.NotNull(text.Style);
        Assert.True(text.Style!.TryGetValue("baselineYPx", out var baseline));
        AssertApproximately(83, Convert.ToDouble(baseline), 5, "baseline metadata");
    }

    [Fact]
    public void FullPipeline_ColorRegion_MapsExpectedBoundsToCanvasShape()
    {
        using var bmp = new SKBitmap(240, 180, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.SteelBlue, IsAntialias = false };
            canvas.DrawRect(50, 40, 80, 60, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "positioned-region");
        var matchingShapes = design.Pages[0].Elements.Where(e =>
            e.Type == "shape" &&
            Math.Abs(e.X - 50) <= 1 &&
            Math.Abs(e.Y - 40) <= 1 &&
            Math.Abs(e.Width - 80) <= 1 &&
            Math.Abs(e.Height - 60) <= 1).ToList();
        Assert.True(
            matchingShapes.Count == 1,
            $"Expected one mapped shape, found {matchingShapes.Count}: " +
            string.Join(", ", matchingShapes.Select(e =>
                e.Style is not null && e.Style.TryGetValue("imageAnalysisType", out var kind)
                    ? kind?.ToString()
                    : "(none)")));
        var shape = matchingShapes[0];

        AssertApproximately(50, shape.X, 0.1, "shape x");
        AssertApproximately(40, shape.Y, 0.1, "shape y");
        AssertApproximately(80, shape.Width, 0.1, "shape width");
        AssertApproximately(60, shape.Height, 0.1, "shape height");

        var sourceBounds = Assert.IsType<Dictionary<string, object>>(shape.Style!["sourceBoundsPx"]);
        Assert.Equal(50, Convert.ToInt32(sourceBounds["x"]));
        Assert.Equal(40, Convert.ToInt32(sourceBounds["y"]));
        Assert.Equal(80, Convert.ToInt32(sourceBounds["width"]));
        Assert.Equal(60, Convert.ToInt32(sourceBounds["height"]));
    }

    [Fact]
    public void FullPipeline_SimpleGrid_MarksDetectedLinesAsGridLines()
    {
        using var bmp = new SKBitmap(240, 180, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false, StrokeWidth = 1 };
            for (int y = 40; y <= 120; y += 40)
                canvas.DrawLine(40, y, 200, y, paint);
            for (int x = 40; x <= 200; x += 80)
                canvas.DrawLine(x, 40, x, 120, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "grid");
        var gridLines = design.Pages[0].Elements.Where(e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "grid-line")).ToList();

        Assert.True(gridLines.Count >= 4, $"Expected grid lines, found {gridLines.Count}");
    }

    [Fact]
    public void FullPipeline_SolidWhiteImage_ReturnsEmptyOrMinimalDesign()
    {
        using var bmp = new SKBitmap(200, 150, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var c  = new SKCanvas(bmp)) c.Clear(SKColors.White);

        var design = ImageAnalysisFileImporter.Import(bmp, "blank");

        // White image has no shapes or text — design should have 0 elements
        var count = design.Pages[0].Elements.Count;
        Assert.True(count == 0,
            $"Expected 0 elements for blank white image but got {count}: " +
            string.Join(", ", design.Pages[0].Elements.Select(e => e.Type)));
    }

    // ── SceneAssembler unit tests ─────────────────────────────────────────────

    [Fact]
    public void SceneAssembler_EmptyInputs_ReturnsEmptyDesign()
    {
        var colors  = new ColorAnalysisResult
        {
            Background     = SKColors.White,
            DominantColors = [],
            Regions        = [],
        };
        var shapes = new ShapeDetectionResult { Shapes = [] };
        var texts  = new TextAnalysisResult  { Lines  = [] };

        var primitives = SceneAssembler.Assemble(colors, shapes, texts);
        var design     = SceneAssembler.ToDesign(primitives, SKColors.White, 200, 100, 1.0, "empty");

        Assert.Empty(design.Pages[0].Elements);
    }

    [Fact]
    public void SceneAssembler_WithColorRegion_EmitsShapeElement()
    {
        var region = new ColorRegion
        {
            Bounds     = new SKRectI(10, 10, 60, 60),
            FillColor  = SKColors.Red,
            Coverage   = 0.10,
            PixelCount = 2500,
        };
        var colors = new ColorAnalysisResult
        {
            Background     = SKColors.White,
            DominantColors = [SKColors.Red],
            Regions        = [region],
        };
        var shapes = new ShapeDetectionResult { Shapes = [] };
        var texts  = new TextAnalysisResult  { Lines  = [] };

        var primitives = SceneAssembler.Assemble(colors, shapes, texts);
        var design     = SceneAssembler.ToDesign(primitives, SKColors.White, 200, 200, 1.0, "test");

        Assert.Single(design.Pages[0].Elements);
        Assert.Equal("shape", design.Pages[0].Elements[0].Type);
    }

    [Fact]
    public void SceneAssembler_SuppressesSmallShapeInsideTextBounds()
    {
        var colors = EmptyColors();
        var shapes = new ShapeDetectionResult
        {
            Shapes =
            [
                new ImageShapePrimitive
                {
                    Bounds = new SKRectI(44, 24, 50, 40),
                    Kind = ShapeKind.Line,
                    StrokeColor = SKColors.Black,
                    StrokeWidth = 1,
                    Confidence = 0.75,
                },
            ],
        };
        var texts = new TextAnalysisResult
        {
            Lines = [TextLine("Hello", new SKRectI(30, 20, 118, 44))],
        };

        var primitives = SceneAssembler.Assemble(colors, shapes, texts);

        Assert.DoesNotContain(primitives, p => p is ImageShapePrimitive);
        Assert.Single(primitives, p => p is ImageTextPrimitive);
    }

    [Fact]
    public void SceneAssembler_KeepsPanelBehindText()
    {
        var colors = EmptyColors();
        var shapes = new ShapeDetectionResult
        {
            Shapes =
            [
                new ImageShapePrimitive
                {
                    Bounds = new SKRectI(20, 12, 180, 72),
                    Kind = ShapeKind.Rect,
                    FillColor = SKColors.LightGray,
                    StrokeColor = SKColors.Gray,
                    StrokeWidth = 1,
                    Confidence = 0.85,
                },
            ],
        };
        var texts = new TextAnalysisResult
        {
            Lines = [TextLine("Hello", new SKRectI(48, 28, 132, 50))],
        };

        var primitives = SceneAssembler.Assemble(colors, shapes, texts);

        Assert.Single(primitives, p => p is ImageShapePrimitive);
        Assert.Single(primitives, p => p is ImageTextPrimitive);
    }

    [Fact]
    public void SceneAssembler_KeepsLongRuleNearText()
    {
        var colors = EmptyColors();
        var shapes = new ShapeDetectionResult
        {
            Shapes =
            [
                new ImageShapePrimitive
                {
                    Bounds = new SKRectI(20, 46, 190, 48),
                    Kind = ShapeKind.Line,
                    StrokeColor = SKColors.Black,
                    StrokeWidth = 2,
                    Confidence = 0.75,
                },
            ],
        };
        var texts = new TextAnalysisResult
        {
            Lines = [TextLine("Hello", new SKRectI(48, 24, 132, 50))],
        };

        var primitives = SceneAssembler.Assemble(colors, shapes, texts);

        Assert.Single(primitives, p => p is ImageShapePrimitive);
        Assert.Single(primitives, p => p is ImageTextPrimitive);
    }

    [Fact]
    public void SceneAssembler_IntersectingLines_MarksGridLines()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult
            {
                Shapes =
                [
                    Line(20, 20, 180, 22),
                    Line(20, 70, 180, 72),
                    Line(20, 120, 180, 122),
                    Line(20, 20, 22, 120),
                    Line(100, 20, 102, 120),
                    Line(180, 20, 182, 120),
                ],
            },
            new TextAnalysisResult { Lines = [] });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 220, 160, 1.0, "grid");
        var gridLines = design.Pages[0].Elements.Where(e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "grid-line")).ToList();

        Assert.Equal(6, gridLines.Count);
    }

    [Fact]
    public void SceneAssembler_SingleRule_IsNotMarkedAsGridLine()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult
            {
                Shapes = [Line(20, 40, 180, 42)],
            },
            new TextAnalysisResult { Lines = [] });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 220, 100, 1.0, "rule");
        var line = Assert.Single(design.Pages[0].Elements);

        Assert.True(line.Style!.TryGetValue("imageAnalysisType", out var type));
        Assert.Equal("line", type);
    }

    [Fact]
    public void SceneAssembler_NearbyTextLines_AssignsSameTextBlock()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult { Shapes = [] },
            new TextAnalysisResult
            {
                Lines =
                [
                    TextLine("First", new SKRectI(32, 30, 120, 52)),
                    TextLine("Second", new SKRectI(34, 58, 150, 80)),
                ],
            });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 220, 120, 1.0, "paragraph");
        var texts = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();

        Assert.Equal(2, texts.Count);
        var firstStyle = Assert.IsType<Dictionary<string, object>>(texts[0].Style);
        var secondStyle = Assert.IsType<Dictionary<string, object>>(texts[1].Style);
        Assert.Equal(firstStyle["textBlockId"], secondStyle["textBlockId"]);
        Assert.Equal(0, Convert.ToInt32(firstStyle["textBlockLineIndex"]));
        Assert.Equal(1, Convert.ToInt32(secondStyle["textBlockLineIndex"]));
    }

    [Fact]
    public void SceneAssembler_DistantTextLines_AssignsDifferentTextBlocks()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult { Shapes = [] },
            new TextAnalysisResult
            {
                Lines =
                [
                    TextLine("First", new SKRectI(32, 30, 120, 52)),
                    TextLine("Second", new SKRectI(34, 120, 150, 142)),
                ],
            });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 220, 180, 1.0, "separate");
        var texts = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();

        Assert.Equal(2, texts.Count);
        var firstStyle = Assert.IsType<Dictionary<string, object>>(texts[0].Style);
        var secondStyle = Assert.IsType<Dictionary<string, object>>(texts[1].Style);
        Assert.NotEqual(firstStyle["textBlockId"], secondStyle["textBlockId"]);
        Assert.Equal(0, Convert.ToInt32(firstStyle["textBlockLineIndex"]));
        Assert.Equal(0, Convert.ToInt32(secondStyle["textBlockLineIndex"]));
    }

    private static ColorAnalysisResult EmptyColors() => new()
    {
        Background = SKColors.White,
        DominantColors = [],
        Regions = [],
    };

    private static ImageShapePrimitive Line(int left, int top, int right, int bottom) => new()
    {
        Bounds = new SKRectI(left, top, right, bottom),
        Kind = ShapeKind.Line,
        StrokeColor = SKColors.Black,
        StrokeWidth = Math.Max(1, Math.Min(right - left, bottom - top)),
        Confidence = 0.8,
    };

    private static ImageTextPrimitive TextLine(string text, SKRectI bounds)
    {
        var chars = text.Select((ch, index) => new RecognizedChar
        {
            Value = ch,
            Bounds = new SKRectI(bounds.Left + index * 10, bounds.Top, bounds.Left + index * 10 + 8, bounds.Bottom),
            Confidence = 0.9,
        }).ToList();

        return new ImageTextPrimitive
        {
            Bounds = bounds,
            Words = [new RecognizedWord { Chars = chars, Bounds = bounds }],
            FontSizePx = bounds.Height,
            BaselineY = bounds.Bottom,
            TextColor = SKColors.Black,
        };
    }
}
