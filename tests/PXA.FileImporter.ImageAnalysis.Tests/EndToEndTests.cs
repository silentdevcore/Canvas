using PXA.Core.Contracts;
using PXA.FileImporter.ImageAnalysis;
using PXA.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;
using System.Security.Cryptography;
using System.Text.Json;

namespace PXA.FileImporter.ImageAnalysis.Tests;

/// <summary>
/// End-to-end tests: run the full 5-phase pipeline and assert on the
/// resulting DesignExportDto element types and counts.
/// </summary>
public class EndToEndTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = true,
    };

    public sealed record BenchmarkCase(
        string Name,
        Func<SKBitmap> CreateBitmap,
        string ExpectedText,
        int ExpectedTextLines,
        int ExpectedElementCount,
        IReadOnlyList<SKRectI> ExpectedShapeBounds,
        double MinTextLineRecall = 0.75,
        double MinGlyphExactMatchRate = 0.70,
        double MinShapeIoU = 0.35,
        double MaxElementCountNoise = 1.25);

    public sealed record BenchmarkMetrics(
        double TextLineDetectionRecall,
        double GlyphExactMatchRate,
        double ShapeIoU,
        double ElementCountNoise);

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

    private static string PageProperty(IEnumerable<CustomDocumentPropertyDto> properties, string name) =>
        Assert.Single(properties, p => p.Name == name).Value;

    private static SKBitmap MakeCleanScreenshotBitmap()
    {
        var bmp = new SKBitmap(520, 260, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(248, 250, 252));

        using var panelPaint = new SKPaint { Color = SKColors.White, IsAntialias = false };
        using var accentPaint = new SKPaint { Color = new SKColor(37, 99, 235), IsAntialias = false };
        using var linePaint = new SKPaint { Color = new SKColor(203, 213, 225), IsAntialias = false, StrokeWidth = 1 };
        using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Courier New"), 20f);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

        canvas.DrawRect(32, 32, 456, 180, panelPaint);
        canvas.DrawRect(32, 32, 456, 8, accentPaint);
        canvas.DrawLine(32, 96, 488, 96, linePaint);
        canvas.DrawText("Invoice", 56, 78, font, textPaint);
        canvas.DrawText("Total 12345", 56, 142, smallFont, textPaint);

        return bmp;
    }

    private static SKBitmap MakeInvoiceTableBitmap()
    {
        var bmp = new SKBitmap(560, 320, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var gridPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false, StrokeWidth = 1 };
        using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 24f);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Courier New"), 18f);
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

        canvas.DrawText("Invoice", 40, 52, font, textPaint);
        for (int y = 88; y <= 208; y += 40)
            canvas.DrawLine(40, y, 520, y, gridPaint);
        for (int x = 40; x <= 520; x += 160)
            canvas.DrawLine(x, 88, x, 208, gridPaint);

        canvas.DrawText("Item", 56, 116, smallFont, textPaint);
        canvas.DrawText("Qty", 220, 116, smallFont, textPaint);
        canvas.DrawText("Price", 372, 116, smallFont, textPaint);
        canvas.DrawText("Pen", 56, 156, smallFont, textPaint);
        canvas.DrawText("2", 220, 156, smallFont, textPaint);
        canvas.DrawText("12.50", 372, 156, smallFont, textPaint);
        canvas.DrawText("Total", 56, 248, font, textPaint);
        canvas.DrawText("25.00", 372, 248, font, textPaint);

        return bmp;
    }

    private static SKBitmap MakeScannedDocumentBitmap()
    {
        var bmp = new SKBitmap(520, 320, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(244, 244, 236));

        using var paperShadow = new SKPaint { Color = new SKColor(220, 220, 210), IsAntialias = false };
        using var paperPaint = new SKPaint { Color = new SKColor(252, 252, 246), IsAntialias = false };
        using var linePaint = new SKPaint { Color = new SKColor(80, 80, 80), StrokeWidth = 1, IsAntialias = false };
        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Times New Roman"), 28f);
        using var bodyFont = new SKFont(SKTypeface.FromFamilyName("Times New Roman"), 20f);
        using var textPaint = new SKPaint { Color = new SKColor(20, 20, 20), IsAntialias = false };

        canvas.DrawRect(48, 42, 420, 232, paperShadow);
        canvas.DrawRect(42, 36, 420, 232, paperPaint);
        canvas.DrawText("Invoice", 72, 88, titleFont, textPaint);
        canvas.DrawText("Total 12345", 72, 132, bodyFont, textPaint);
        canvas.DrawLine(72, 154, 392, 154, linePaint);
        canvas.DrawText("Price 25.00", 72, 192, bodyFont, textPaint);

        return bmp;
    }

    private static SKBitmap MakeMobilePhotoBitmap()
    {
        var bmp = new SKBitmap(480, 320, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(226, 230, 235));

        using var phonePaint = new SKPaint { Color = new SKColor(248, 250, 252), IsAntialias = false };
        using var headerPaint = new SKPaint { Color = new SKColor(28, 48, 78), IsAntialias = false };
        using var accentPaint = new SKPaint { Color = new SKColor(37, 99, 235), IsAntialias = false };
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 24f);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Arial"), 18f);
        using var whiteText = new SKPaint { Color = SKColors.White, IsAntialias = false };
        using var darkText = new SKPaint { Color = new SKColor(18, 18, 18), IsAntialias = false };

        canvas.DrawRect(92, 30, 296, 252, phonePaint);
        canvas.DrawRect(92, 30, 296, 62, headerPaint);
        canvas.DrawText("Header", 118, 70, font, whiteText);
        canvas.DrawRect(118, 120, 118, 42, accentPaint);
        canvas.DrawText("Invoice", 118, 190, font, darkText);
        canvas.DrawText("Total 12345", 118, 228, smallFont, darkText);

        return bmp;
    }

    private static SKBitmap MakeDarkHeaderBitmap()
    {
        var bmp = new SKBitmap(420, 220, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var headerPaint = new SKPaint { Color = new SKColor(24, 24, 24), IsAntialias = false };
        using var rulePaint = new SKPaint { Color = new SKColor(210, 210, 210), StrokeWidth = 1, IsAntialias = false };
        using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Courier New"), 20f);
        using var whiteText = new SKPaint { Color = SKColors.White, IsAntialias = false };
        using var darkText = new SKPaint { Color = SKColors.Black, IsAntialias = false };

        canvas.DrawRect(0, 0, 420, 82, headerPaint);
        canvas.DrawText("Header", 32, 54, font, whiteText);
        canvas.DrawLine(32, 112, 388, 112, rulePaint);
        canvas.DrawText("Total 12345", 32, 152, smallFont, darkText);

        return bmp;
    }

    private static string BuildRecognitionQualitySnapshot(ImageAnalysisImportResult result)
    {
        var elements = result.Design.Pages[0].Elements;
        var textElements = elements
            .Where(e => e.Type == "text")
            .OrderBy(e => e.Y)
            .ThenBy(e => e.X)
            .Select(e => new
            {
                e.Content,
                X = Math.Round(e.X, 1),
                Y = Math.Round(e.Y, 1),
                W = Math.Round(e.Width, 1),
                H = Math.Round(e.Height, 1),
                Confidence = e.Style is not null && e.Style.TryGetValue("imageAnalysisConfidence", out var confidence)
                    ? Math.Round(Convert.ToDouble(confidence), 3)
                    : 0,
            })
            .ToList();

        var elementTypes = elements
            .GroupBy(e => e.Type)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var analysisTypes = elements
            .Where(e => e.Style is not null && e.Style.TryGetValue("imageAnalysisType", out _))
            .GroupBy(e => e.Style!["imageAnalysisType"]?.ToString() ?? "")
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        return JsonSerializer.Serialize(new
        {
            Diagnostics = new
            {
                result.Diagnostics.ColorRegionCount,
                result.Diagnostics.ShapeCount,
                result.Diagnostics.TextLineCount,
                result.Diagnostics.WordCount,
                result.Diagnostics.GlyphCount,
                result.Diagnostics.LowConfidenceGlyphCount,
                result.Diagnostics.ElementCount,
                Warnings = result.Diagnostics.Warnings,
            },
            ElementTypes = elementTypes,
            AnalysisTypes = analysisTypes,
            Text = textElements,
        }, SnapshotJsonOptions);
    }

    public static IEnumerable<object[]> BenchmarkCases()
    {
        yield return [new BenchmarkCase(
            "clean-screenshot",
            MakeCleanScreenshotBitmap,
            "Invoice Total 12345",
            ExpectedTextLines: 2,
            ExpectedElementCount: 9,
            ExpectedShapeBounds: [new SKRectI(32, 32, 488, 212), new SKRectI(32, 32, 488, 40)],
            MinShapeIoU: 0.10)];

        yield return [new BenchmarkCase(
            "invoice-table",
            MakeInvoiceTableBitmap,
            "Invoice Item Qty Price Pen 2 12.50 Total 25.00",
            ExpectedTextLines: 9,
            ExpectedElementCount: 33,
            ExpectedShapeBounds: [new SKRectI(40, 88, 520, 208)],
            MinShapeIoU: 0.20,
            MinGlyphExactMatchRate: 0.69,
            MaxElementCountNoise: 1.50)];

        yield return [new BenchmarkCase(
            "scanned-document",
            MakeScannedDocumentBitmap,
            "Invoice Total 12345 Price 25.00",
            ExpectedTextLines: 3,
            ExpectedElementCount: 7,
            ExpectedShapeBounds: [new SKRectI(42, 36, 462, 268)],
            MinTextLineRecall: 0.65,
            MinGlyphExactMatchRate: 0.65,
            MinShapeIoU: 0.20,
            MaxElementCountNoise: 2.00)];

        yield return [new BenchmarkCase(
            "mobile-photo",
            MakeMobilePhotoBitmap,
            "Header Invoice Total 12345",
            ExpectedTextLines: 3,
            ExpectedElementCount: 8,
            ExpectedShapeBounds: [new SKRectI(92, 30, 388, 282), new SKRectI(92, 30, 388, 92)],
            MinTextLineRecall: 0.65,
            MinGlyphExactMatchRate: 0.60,
            MinShapeIoU: 0.20,
            MaxElementCountNoise: 2.00)];

        yield return [new BenchmarkCase(
            "dark-header",
            MakeDarkHeaderBitmap,
            "Header Total 12345",
            ExpectedTextLines: 2,
            ExpectedElementCount: 5,
            ExpectedShapeBounds: [new SKRectI(0, 0, 420, 82)],
            MinShapeIoU: 0.20,
            MaxElementCountNoise: 2.00)];
    }

    private static BenchmarkMetrics CalculateBenchmarkMetrics(ImageAnalysisImportResult result, BenchmarkCase benchmark)
    {
        string actualText = NormalizeText(string.Join(" ", result.Design.Pages[0].Elements
            .Where(e => e.Type == "text")
            .OrderBy(e => e.Y)
            .ThenBy(e => e.X)
            .Select(e => e.Content ?? "")));
        string expectedText = NormalizeText(benchmark.ExpectedText);
        int maxTextLength = Math.Max(expectedText.Length, actualText.Length);
        double glyphExactMatchRate = maxTextLength == 0
            ? 1
            : Math.Max(0, 1 - EditDistance(expectedText, actualText) / (double)maxTextLength);

        double textLineRecall = benchmark.ExpectedTextLines == 0
            ? 1
            : Math.Min(1, result.Diagnostics.TextLineCount / (double)benchmark.ExpectedTextLines);

        double shapeIoU = benchmark.ExpectedShapeBounds.Count == 0
            ? 1
            : AverageBestShapeIoU(result.Design.Pages[0].Elements, benchmark.ExpectedShapeBounds);

        double elementNoise = benchmark.ExpectedElementCount == 0
            ? result.Diagnostics.ElementCount
            : Math.Abs(result.Diagnostics.ElementCount - benchmark.ExpectedElementCount) / (double)benchmark.ExpectedElementCount;

        return new BenchmarkMetrics(
            Math.Round(textLineRecall, 3),
            Math.Round(glyphExactMatchRate, 3),
            Math.Round(shapeIoU, 3),
            Math.Round(elementNoise, 3));
    }

    private static string BuildOverlaySnapshot(ImageAnalysisImportResult result)
    {
        Assert.NotNull(result.DebugOverlayPng);
        using var overlay = SKBitmap.Decode(result.DebugOverlayPng);
        byte[] hash = SHA256.HashData(result.DebugOverlayPng);

        return JsonSerializer.Serialize(new
        {
            overlay.Width,
            overlay.Height,
            Bytes = result.DebugOverlayPng.Length,
            Sha256 = Convert.ToHexString(hash)[..16],
        }, SnapshotJsonOptions);
    }

    private static string NormalizeText(string text) =>
        string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static double AverageBestShapeIoU(IReadOnlyList<ElementDto> elements, IReadOnlyList<SKRectI> expectedBounds)
    {
        var candidates = elements
            .Where(e => e.Type != "text")
            .Select(TryGetSourceBounds)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (candidates.Count == 0)
            return 0;

        return expectedBounds.Average(expected =>
            candidates.Max(candidate => IoU(expected, candidate)));
    }

    private static SKRectI? TryGetSourceBounds(ElementDto element)
    {
        if (element.Style is null ||
            !element.Style.TryGetValue("sourceBoundsPx", out var boundsObj) ||
            boundsObj is not Dictionary<string, object> bounds)
            return null;

        int x = Convert.ToInt32(bounds["x"]);
        int y = Convert.ToInt32(bounds["y"]);
        int width = Convert.ToInt32(bounds["width"]);
        int height = Convert.ToInt32(bounds["height"]);
        return new SKRectI(x, y, x + width, y + height);
    }

    private static double IoU(SKRectI a, SKRectI b)
    {
        int left = Math.Max(a.Left, b.Left);
        int top = Math.Max(a.Top, b.Top);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        int intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        int union = a.Width * a.Height + b.Width * b.Height - intersection;
        return union <= 0 ? 0 : intersection / (double)union;
    }

    private static int EditDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
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
            Assert.NotNull(el.Style);
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
        var settings   = Assert.IsType<PageSettingsDto>(design.PageSettings);

        // Page dimensions must match the image (scale factor = 1 for 320×240)
        Assert.True(settings.Width  > 0, "PageSettings.Width must be > 0");
        Assert.True(settings.Height > 0, "PageSettings.Height must be > 0");
        Assert.True(Math.Abs(settings.Width  - 320) < 2, $"Expected width ≈320, got {settings.Width}");
        Assert.True(Math.Abs(settings.Height - 240) < 2, $"Expected height ≈240, got {settings.Height}");
    }

    [Fact]
    public void FullPipeline_SourceDpi_MapsImagePixelsToPagePointsAndMetadata()
    {
        using var bmp = MakeDocumentBitmap(width: 300, height: 150);
        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            "dpi-test",
            targetWidthPt: null,
            targetHeightPt: null,
            options: new ImageAnalysisOptions
            {
                SourceDpiX = 150,
                SourceDpiY = 150,
            });

        var settings = Assert.IsType<PageSettingsDto>(result.Design.PageSettings);
        AssertApproximately(144, settings.Width, 0.1, "page width from source dpi");
        AssertApproximately(72, settings.Height, 0.1, "page height from source dpi");
        Assert.Equal("pt", settings.Unit);

        var properties = Assert.IsType<List<CustomDocumentPropertyDto>>(settings.CustomProperties);
        Assert.Equal("explicit-dpi", PageProperty(properties, "imageAnalysis.pageScaleSource"));
        Assert.Equal("150", PageProperty(properties, "imageAnalysis.sourceDpiX"));
        Assert.Equal("150", PageProperty(properties, "imageAnalysis.sourceDpiY"));
        Assert.Equal("300", PageProperty(properties, "imageAnalysis.sourceWidthPx"));
        Assert.Equal("150", PageProperty(properties, "imageAnalysis.sourceHeightPx"));
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
            options: new ImageAnalysisOptions { IncludeDebugOverlay = false });

        Assert.NotNull(result.Design);
        Assert.Equal(400, result.Diagnostics.SourceWidthPx);
        Assert.Equal(200, result.Diagnostics.SourceHeightPx);
        Assert.True(result.Diagnostics.ColorRegionCount > 0);
        Assert.True(result.Diagnostics.TextLineCount > 0);
        Assert.True(result.Diagnostics.GlyphCount >= 5);
        Assert.Equal("builtin-basic-latin-font-atlas-v1", result.Diagnostics.GlyphTemplateProfile);
        Assert.Equal("benchmark-gated", result.Diagnostics.RecognitionReadiness);
        Assert.Equal("synthetic-business-documents-v1", result.Diagnostics.RecognitionFidelityScope);
        Assert.True(result.Diagnostics.RuntimeMs > 0);
        Assert.True(result.Diagnostics.LowConfidenceGlyphRate >= 0);
        Assert.True(result.Diagnostics.LowConfidenceGlyphRate <= 1);
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
            options: new ImageAnalysisOptions { IncludeDebugOverlay = true });

        Assert.NotNull(result.DebugOverlayPng);
        Assert.True(result.DebugOverlayPng.Length > 8);
        Assert.Equal(0x89, result.DebugOverlayPng[0]);
        Assert.Equal((byte)'P', result.DebugOverlayPng[1]);
        Assert.Equal((byte)'N', result.DebugOverlayPng[2]);
        Assert.Equal((byte)'G', result.DebugOverlayPng[3]);
    }

    [Theory]
    [InlineData("clean-screenshot")]
    [InlineData("invoice-table")]
    public void DebugOverlay_SavedVisualSnapshot_MatchesDigest(string name)
    {
        using var bmp = name == "clean-screenshot"
            ? MakeCleanScreenshotBitmap()
            : MakeInvoiceTableBitmap();

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bmp,
            $"overlay-{name}",
            options: new ImageAnalysisOptions { IncludeDebugOverlay = true });

        string snapshot = BuildOverlaySnapshot(result);
        string expected = name switch
        {
            "clean-screenshot" => """
            {
              "Width": 520,
              "Height": 260,
              "Bytes": 6539,
              "Sha256": "EB4E9EA7EF8D907B"
            }
            """,
            _ => """
            {
              "Width": 560,
              "Height": 320,
              "Bytes": 11171,
              "Sha256": "DB86A4B22DB682D7"
            }
            """,
        };

        Assert.Equal(expected, snapshot);
    }

    [Theory]
    [MemberData(nameof(BenchmarkCases))]
    public void BenchmarkCases_MeetQualityMetrics(BenchmarkCase benchmark)
    {
        using var bmp = benchmark.CreateBitmap();
        var result = ImageAnalysisFileImporter.ImportWithAnalysis(bmp, $"benchmark-{benchmark.Name}");
        var metrics = CalculateBenchmarkMetrics(result, benchmark);

        Assert.True(
            metrics.TextLineDetectionRecall >= benchmark.MinTextLineRecall,
            $"{benchmark.Name} text-line recall {metrics.TextLineDetectionRecall} below {benchmark.MinTextLineRecall}");
        Assert.True(
            metrics.GlyphExactMatchRate >= benchmark.MinGlyphExactMatchRate,
            $"{benchmark.Name} glyph exact-match rate {metrics.GlyphExactMatchRate} below {benchmark.MinGlyphExactMatchRate}");
        Assert.True(
            metrics.ShapeIoU >= benchmark.MinShapeIoU,
            $"{benchmark.Name} shape IoU {metrics.ShapeIoU} below {benchmark.MinShapeIoU}");
        Assert.True(
            metrics.ElementCountNoise <= benchmark.MaxElementCountNoise,
            $"{benchmark.Name} element count noise {metrics.ElementCountNoise} above {benchmark.MaxElementCountNoise}");
        Assert.True(result.Diagnostics.RuntimeMs > 0);
        Assert.True(result.Diagnostics.MemoryDeltaBytes != long.MinValue);
    }

    [Fact]
    public void ProductionReadiness_BenchmarkPortfolio_MeetsReleaseGate()
    {
        var results = BenchmarkCases()
            .Select(row => Assert.IsType<BenchmarkCase>(Assert.Single(row)))
            .Select(benchmark =>
            {
                using var bmp = benchmark.CreateBitmap();
                var result = ImageAnalysisFileImporter.ImportWithAnalysis(bmp, $"readiness-{benchmark.Name}");
                return (benchmark, result, metrics: CalculateBenchmarkMetrics(result, benchmark));
            })
            .ToList();

        Assert.All(results, item =>
        {
            Assert.Equal("benchmark-gated", item.result.Diagnostics.RecognitionReadiness);
            Assert.Equal("synthetic-business-documents-v1", item.result.Diagnostics.RecognitionFidelityScope);
            Assert.InRange(item.result.Diagnostics.LowConfidenceGlyphRate, 0, 0.25);
            Assert.InRange(item.result.Diagnostics.RuntimeMs, 0.001, 5000);
        });

        Assert.True(results.Average(r => r.metrics.TextLineDetectionRecall) >= 0.85);
        Assert.True(results.Average(r => r.metrics.GlyphExactMatchRate) >= 0.70);
        Assert.True(results.Average(r => r.metrics.ShapeIoU) >= 0.18);
        Assert.True(results.Average(r => r.metrics.ElementCountNoise) <= 1.25);
    }

    [Fact]
    public void RecognitionQuality_CleanScreenshot_MatchesSnapshot()
    {
        using var bmp = MakeCleanScreenshotBitmap();

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(bmp, "quality-clean-screenshot");
        string snapshot = BuildRecognitionQualitySnapshot(result);

        const string expected = """
        {
          "Diagnostics": {
            "ColorRegionCount": 2,
            "ShapeCount": 14,
            "TextLineCount": 2,
            "WordCount": 3,
            "GlyphCount": 17,
            "LowConfidenceGlyphCount": 0,
            "ElementCount": 9,
            "Warnings": []
          },
          "ElementTypes": {
            "rect": 5,
            "shape": 2,
            "text": 2
          },
          "AnalysisTypes": {
            "color-region": 2,
            "line": 5,
            "text": 2
          },
          "Text": [
            {
              "Content": "Invoice",
              "X": 59,
              "Y": 60,
              "W": 113,
              "H": 19,
              "Confidence": 0.639
            },
            {
              "Content": "Total 12345",
              "X": 57,
              "Y": 129,
              "W": 129,
              "H": 16.9,
              "Confidence": 0.717
            }
          ]
        }
        """;
        Assert.Equal(expected, snapshot);
    }

    [Fact]
    public void RecognitionQuality_InvoiceTable_MatchesSnapshot()
    {
        using var bmp = MakeInvoiceTableBitmap();

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(bmp, "quality-invoice-table");
        string snapshot = BuildRecognitionQualitySnapshot(result);

        const string expected = """
        {
          "Diagnostics": {
            "ColorRegionCount": 0,
            "ShapeCount": 35,
            "TextLineCount": 9,
            "WordCount": 9,
            "GlyphCount": 38,
            "LowConfidenceGlyphCount": 1,
            "ElementCount": 33,
            "Warnings": [
              "Some glyphs were low-confidence or unresolved."
            ]
          },
          "ElementTypes": {
            "rect": 18,
            "shape": 6,
            "text": 9
          },
          "AnalysisTypes": {
            "grid-line": 16,
            "line": 2,
            "rect": 6,
            "text": 9
          },
          "Text": [
            {
              "Content": "Invoice",
              "X": 43,
              "Y": 37,
              "W": 96,
              "H": 15,
              "Confidence": 0.614
            },
            {
              "Content": "Qty",
              "X": 221,
              "Y": 105,
              "W": 31,
              "H": 14,
              "Confidence": 0.608
            },
            {
              "Content": "Item",
              "X": 58,
              "Y": 106,
              "W": 41,
              "H": 10.4,
              "Confidence": 0.688
            },
            {
              "Content": "Price",
              "X": 374,
              "Y": 106,
              "W": 50,
              "H": 10.4,
              "Confidence": 0.535
            },
            {
              "Content": "2",
              "X": 221,
              "Y": 145,
              "W": 8,
              "H": 13,
              "Confidence": 0.617
            },
            {
              "Content": "12.50",
              "X": 374,
              "Y": 145,
              "W": 50,
              "H": 13,
              "Confidence": 0.71
            },
            {
              "Content": "Pen",
              "X": 58,
              "Y": 146,
              "W": 30,
              "H": 13,
              "Confidence": 0.62
            },
            {
              "Content": "Total",
              "X": 58,
              "Y": 233,
              "W": 68,
              "H": 19.5,
              "Confidence": 0.63
            },
            {
              "Content": "25.00",
              "X": 374,
              "Y": 233,
              "W": 68,
              "H": 19.5,
              "Confidence": 0.77
            }
          ]
        }
        """;
        Assert.Equal(expected, snapshot);
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
    public void FullPipeline_TextElement_IncludesGlyphDiagnosticsMetadata()
    {
        using var bmp = MakeDocumentBitmap(text: "Hello", fontSize: 28f);
        var design = ImageAnalysisFileImporter.Import(bmp, "glyph-diagnostics");
        var text = Assert.Single(design.Pages[0].Elements, e => e.Type == "text" && e.Content == "Hello");

        Assert.NotNull(text.Style);
        Assert.True(text.Style!.TryGetValue("imageAnalysisGlyphs", out var glyphsObj));
        var glyphs = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object>>>(glyphsObj).ToList();

        Assert.Equal(5, glyphs.Count);
        Assert.All(glyphs, glyph =>
        {
            Assert.True(glyph.ContainsKey("value"));
            Assert.True(glyph.ContainsKey("confidence"));
            Assert.True(glyph.ContainsKey("boundsPx"));
            Assert.True(glyph.ContainsKey("initialCandidate"));
            Assert.True(glyph.ContainsKey("selectedCandidate"));
            Assert.True(glyph.ContainsKey("method"));
            Assert.True(glyph.ContainsKey("score"));
            Assert.True(glyph.ContainsKey("signals"));
            Assert.True(glyph.ContainsKey("decisionWeights"));
        });
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
    public void FullPipeline_GradientRegion_ExportsImageRegionMetadata()
    {
        using var bmp = new SKBitmap(260, 180, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
        }

        for (int y = 40; y < 130; y++)
        {
            for (int x = 48; x < 208; x++)
            {
                byte r = (byte)(40 + (x - 48) * 120 / 159);
                byte g = (byte)(70 + (y - 40) * 80 / 89);
                byte b = (byte)(180 - (x - 48) * 60 / 159);
                bmp.SetPixel(x, y, new SKColor(r, g, b));
            }
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "gradient-region");
        var region = Assert.Single(design.Pages[0].Elements, e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "image-region"));

        Assert.Equal("shape", region.Type);
        Assert.Equal("foreground-variation", region.Style!["imageAnalysisSource"]);
        AssertApproximately(48, region.X, 0.1, "region x");
        AssertApproximately(40, region.Y, 0.1, "region y");
        AssertApproximately(160, region.Width, 0.1, "region width");
        AssertApproximately(90, region.Height, 0.1, "region height");

        var sourceBounds = Assert.IsType<Dictionary<string, object>>(region.Style!["sourceBoundsPx"]);
        Assert.Equal(48, Convert.ToInt32(sourceBounds["x"]));
        Assert.Equal(40, Convert.ToInt32(sourceBounds["y"]));
        Assert.Equal(160, Convert.ToInt32(sourceBounds["width"]));
        Assert.Equal(90, Convert.ToInt32(sourceBounds["height"]));
    }

    [Fact]
    public void FullPipeline_RoundedRect_ExportsBorderRadiusMetadata()
    {
        using var bmp = new SKBitmap(260, 180, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRoundRect(new SKRect(48, 42, 168, 108), 18, 18, paint);
        }

        var design = ImageAnalysisFileImporter.Import(bmp, "rounded-rect");
        var rounded = Assert.Single(design.Pages[0].Elements.Where(e =>
            e.Type == "shape" &&
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "rounded-rect")));

        Assert.True(rounded.Style!.ContainsKey("borderRadius"));
        Assert.InRange(Convert.ToDouble(rounded.Style["borderRadius"]), 1, 24);

        var sourceBounds = Assert.IsType<Dictionary<string, object>>(rounded.Style["sourceBoundsPx"]);
        Assert.InRange(Convert.ToInt32(sourceBounds["x"]), 47, 49);
        Assert.InRange(Convert.ToInt32(sourceBounds["y"]), 41, 43);
        Assert.InRange(Convert.ToInt32(sourceBounds["width"]), 118, 122);
        Assert.InRange(Convert.ToInt32(sourceBounds["height"]), 64, 68);
    }

    [Fact]
    public void FullPipeline_IrregularSymbol_ExportsIconClusterMetadata()
    {
        using var bmp = new SKBitmap(220, 160, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            using var path = new SKPath();
            path.MoveTo(110, 36);
            path.LineTo(124, 72);
            path.LineTo(164, 72);
            path.LineTo(132, 94);
            path.LineTo(146, 132);
            path.LineTo(110, 108);
            path.LineTo(74, 132);
            path.LineTo(88, 94);
            path.LineTo(56, 72);
            path.LineTo(96, 72);
            path.Close();
            canvas.DrawPath(path, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        Assert.NotEmpty(ShapeDetector.FindIconClusters(prep.Binary));
        var colors = ColorAnalyzer.Analyze(prep);
        var shapes = ShapeDetector.Detect(prep, colors);
        Assert.Contains(shapes.Shapes, s => s.AnalysisType == "icon-cluster");

        var design = ImageAnalysisFileImporter.Import(bmp, "icon-cluster");
        var icon = Assert.Single(design.Pages[0].Elements, e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "icon-cluster"));

        Assert.Equal("shape", icon.Type);
        Assert.True(icon.Style!.TryGetValue("sourceBoundsPx", out var boundsObj));
        var sourceBounds = Assert.IsType<Dictionary<string, object>>(boundsObj);
        Assert.InRange(Convert.ToInt32(sourceBounds["x"]), 54, 58);
        Assert.InRange(Convert.ToInt32(sourceBounds["y"]), 34, 38);
        Assert.InRange(Convert.ToInt32(sourceBounds["width"]), 106, 112);
        Assert.InRange(Convert.ToInt32(sourceBounds["height"]), 94, 100);
    }

    [Fact]
    public void FullPipeline_LargeIrregularRegion_ExportsImageClusterMetadata()
    {
        using var bmp = new SKBitmap(360, 240, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            using var path = new SKPath();
            path.MoveTo(72, 54);
            path.LineTo(138, 34);
            path.LineTo(208, 62);
            path.LineTo(284, 48);
            path.LineTo(308, 112);
            path.LineTo(260, 160);
            path.LineTo(284, 204);
            path.LineTo(184, 184);
            path.LineTo(112, 210);
            path.LineTo(90, 146);
            path.LineTo(42, 118);
            path.Close();
            canvas.DrawPath(path, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        Assert.NotEmpty(ShapeDetector.FindImageClusters(prep.Binary));
        var colors = ColorAnalyzer.Analyze(prep);
        var shapes = ShapeDetector.Detect(prep, colors);
        Assert.Contains(shapes.Shapes, s => s.AnalysisType == "image-cluster");

        var design = ImageAnalysisFileImporter.Import(bmp, "image-cluster");
        var cluster = Assert.Single(design.Pages[0].Elements, e =>
            e.Style is not null &&
            e.Style.TryGetValue("imageAnalysisType", out var type) &&
            Equals(type, "image-cluster"));

        Assert.Equal("shape", cluster.Type);
        Assert.True(cluster.Style!.TryGetValue("sourceBoundsPx", out var boundsObj));
        var sourceBounds = Assert.IsType<Dictionary<string, object>>(boundsObj);
        Assert.InRange(Convert.ToInt32(sourceBounds["x"]), 40, 74);
        Assert.InRange(Convert.ToInt32(sourceBounds["y"]), 32, 56);
        Assert.InRange(Convert.ToInt32(sourceBounds["width"]), 230, 270);
        Assert.InRange(Convert.ToInt32(sourceBounds["height"]), 150, 180);
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
        Assert.True(gridLines.Count(e => Equals(e.Style!["imageAnalysisGridOrientation"], "horizontal")) >= 2);
        Assert.True(gridLines.Count(e => Equals(e.Style!["imageAnalysisGridOrientation"], "vertical")) >= 2);

        Assert.All(gridLines, line =>
        {
            var sourceBounds = Assert.IsType<Dictionary<string, object>>(line.Style!["sourceBoundsPx"]);
            Assert.True(Convert.ToInt32(sourceBounds["width"]) >= 1);
            Assert.True(Convert.ToInt32(sourceBounds["height"]) >= 1);

            var gridBounds = Assert.IsType<Dictionary<string, object>>(line.Style["imageAnalysisGridBoundsPx"]);
            Assert.InRange(Convert.ToInt32(gridBounds["x"]), 35, 45);
            Assert.InRange(Convert.ToInt32(gridBounds["y"]), 35, 45);
            Assert.InRange(Convert.ToInt32(gridBounds["width"]), 150, 170);
            Assert.InRange(Convert.ToInt32(gridBounds["height"]), 80, 90);
        });
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
    public void SceneAssembler_DarkPanel_KeepsRealShapeButSuppressesSmallStrokeArtifact()
    {
        var colors = new ColorAnalysisResult
        {
            Background = SKColors.White,
            DominantColors = [SKColors.Black],
            Regions =
            [
                new ColorRegion
                {
                    Bounds = new SKRectI(10, 10, 210, 90),
                    FillColor = new SKColor(22, 28, 38),
                    Coverage = 0.30,
                    PixelCount = 16000,
                },
            ],
        };
        var shapes = new ShapeDetectionResult
        {
            Shapes =
            [
                new ImageShapePrimitive
                {
                    Bounds = new SKRectI(34, 30, 72, 34),
                    Kind = ShapeKind.Line,
                    StrokeColor = SKColors.White,
                    StrokeWidth = 2,
                    Confidence = 0.62,
                },
                new ImageShapePrimitive
                {
                    Bounds = new SKRectI(120, 28, 196, 68),
                    Kind = ShapeKind.Rect,
                    FillColor = new SKColor(70, 92, 120),
                    StrokeColor = SKColors.Transparent,
                    StrokeWidth = 0,
                    Confidence = 0.80,
                    AnalysisType = "filled-rect",
                },
            ],
        };

        var primitives = SceneAssembler.Assemble(colors, shapes, new TextAnalysisResult { Lines = [] });

        Assert.DoesNotContain(primitives, p =>
            p is ImageShapePrimitive s && s.Bounds == new SKRectI(34, 30, 72, 34));
        Assert.Contains(primitives, p =>
            p is ImageShapePrimitive s && s.Bounds == new SKRectI(120, 28, 196, 68));
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
        Assert.All(gridLines, line =>
        {
            Assert.True(line.Style!.ContainsKey("imageAnalysisGridId"));
            Assert.True(line.Style.ContainsKey("imageAnalysisGridOrientation"));
            Assert.True(line.Style.ContainsKey("imageAnalysisGridBoundsPx"));
            Assert.Equal(1, Convert.ToInt32(line.Style["imageAnalysisGridId"]));
        });
        Assert.Equal(3, gridLines.Count(line => Equals(line.Style!["imageAnalysisGridOrientation"], "horizontal")));
        Assert.Equal(3, gridLines.Count(line => Equals(line.Style!["imageAnalysisGridOrientation"], "vertical")));

        var gridBounds = Assert.IsType<Dictionary<string, object>>(gridLines[0].Style!["imageAnalysisGridBoundsPx"]);
        Assert.Equal(20, Convert.ToInt32(gridBounds["x"]));
        Assert.Equal(20, Convert.ToInt32(gridBounds["y"]));
        Assert.Equal(162, Convert.ToInt32(gridBounds["width"]));
        Assert.Equal(102, Convert.ToInt32(gridBounds["height"]));
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

    [Fact]
    public void SceneAssembler_RaggedParagraph_AssignsSingleTextBlockAcrossShortIndentedLine()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult { Shapes = [] },
            new TextAnalysisResult
            {
                Lines =
                [
                    TextLine("Long first line", new SKRectI(32, 30, 260, 52)),
                    TextLine("note", new SKRectI(210, 58, 242, 80)),
                    TextLine("Continuation", new SKRectI(34, 86, 246, 108)),
                ],
            });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 320, 150, 1.0, "ragged-paragraph");
        var texts = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();

        Assert.Equal(["Long first line", "note", "Continuation"], texts.Select(t => t.Content ?? "").ToArray());
        Assert.Single(texts.Select(t => Convert.ToInt32(t.Style!["textBlockId"])).Distinct());
        Assert.Equal([0, 1, 2], texts.Select(t => Convert.ToInt32(t.Style!["textBlockLineIndex"])).ToArray());
    }

    [Fact]
    public void SceneAssembler_TwoColumns_AssignsColumnMajorTextBlocks()
    {
        var primitives = SceneAssembler.Assemble(
            EmptyColors(),
            new ShapeDetectionResult { Shapes = [] },
            new TextAnalysisResult
            {
                Lines =
                [
                    TextLine("LeftA", new SKRectI(32, 30, 110, 52)),
                    TextLine("RightA", new SKRectI(260, 30, 360, 52)),
                    TextLine("LeftB", new SKRectI(34, 60, 112, 82)),
                    TextLine("RightB", new SKRectI(262, 60, 362, 82)),
                ],
            });

        var design = SceneAssembler.ToDesign(primitives, SKColors.White, 420, 120, 1.0, "columns");
        var texts = design.Pages[0].Elements.Where(e => e.Type == "text").ToList();

        Assert.Equal(["LeftA", "LeftB", "RightA", "RightB"], texts.Select(t => t.Content ?? "").ToArray());
        Assert.Equal(1, Convert.ToInt32(texts[0].Style!["textBlockId"]));
        Assert.Equal(0, Convert.ToInt32(texts[0].Style!["textBlockLineIndex"]));
        Assert.Equal(1, Convert.ToInt32(texts[1].Style!["textBlockId"]));
        Assert.Equal(1, Convert.ToInt32(texts[1].Style!["textBlockLineIndex"]));
        Assert.Equal(2, Convert.ToInt32(texts[2].Style!["textBlockId"]));
        Assert.Equal(0, Convert.ToInt32(texts[2].Style!["textBlockLineIndex"]));
        Assert.Equal(2, Convert.ToInt32(texts[3].Style!["textBlockId"]));
        Assert.Equal(1, Convert.ToInt32(texts[3].Style!["textBlockLineIndex"]));
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
