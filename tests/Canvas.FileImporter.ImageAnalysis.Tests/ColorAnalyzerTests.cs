using Canvas.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Tests;

public class ColorAnalyzerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SKBitmap SolidBitmap(int w, int h, SKColor color)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var c = new SKCanvas(bmp);
        c.Clear(color);
        return bmp;
    }

    /// <summary>White canvas with a single solid-colour rectangle drawn on it.</summary>
    private static SKBitmap WhiteWithRect(int imgW, int imgH,
        int rx, int ry, int rw, int rh, SKColor color)
    {
        var bmp = new SKBitmap(imgW, imgH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = color, IsAntialias = false };
        canvas.DrawRect(rx, ry, rw, rh, paint);
        return bmp;
    }

    private static void AssertRectApproximately(SKRectI expected, SKRectI actual, int tolerance, string label)
    {
        Assert.True(Math.Abs(expected.Left - actual.Left) <= tolerance, $"{label} left: expected {expected.Left} +/- {tolerance}, got {actual.Left}");
        Assert.True(Math.Abs(expected.Top - actual.Top) <= tolerance, $"{label} top: expected {expected.Top} +/- {tolerance}, got {actual.Top}");
        Assert.True(Math.Abs(expected.Right - actual.Right) <= tolerance, $"{label} right: expected {expected.Right} +/- {tolerance}, got {actual.Right}");
        Assert.True(Math.Abs(expected.Bottom - actual.Bottom) <= tolerance, $"{label} bottom: expected {expected.Bottom} +/- {tolerance}, got {actual.Bottom}");
    }

    // ── Background detection ─────────────────────────────────────────────────

    [Fact]
    public void DetectBackground_SolidWhiteImage_ReturnsWhite()
    {
        using var bmp = SolidBitmap(200, 200, SKColors.White);
        var bg = ColorAnalyzer.DetectBackground(bmp);
        // Within quantisation tolerance of white (248-255 per channel)
        Assert.True(bg.Red   > 240, $"R={bg.Red}");
        Assert.True(bg.Green > 240, $"G={bg.Green}");
        Assert.True(bg.Blue  > 240, $"B={bg.Blue}");
    }

    [Fact]
    public void DetectBackground_SolidBlueImage_ReturnsBlue()
    {
        using var bmp = SolidBitmap(200, 200, SKColors.Blue);
        var bg = ColorAnalyzer.DetectBackground(bmp);
        Assert.True(bg.Blue  > 200, $"B={bg.Blue}");
        Assert.True(bg.Red   < 50,  $"R={bg.Red}");
        Assert.True(bg.Green < 50,  $"G={bg.Green}");
    }

    [Fact]
    public void DetectBackground_WhiteWithCenteredRect_IgnoresCentralColor()
    {
        // Blue rect in the middle — border should still read white
        using var bmp = WhiteWithRect(300, 300, 100, 100, 100, 100, SKColors.Blue);
        var bg = ColorAnalyzer.DetectBackground(bmp);
        Assert.True(bg.Red   > 220 && bg.Green > 220 && bg.Blue > 220,
            $"Expected near-white background, got ({bg.Red},{bg.Green},{bg.Blue})");
    }

    // ── Palette ───────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPalette_SolidColorImage_ReturnsOneColor()
    {
        using var bmp    = SolidBitmap(200, 200, SKColors.Red);
        var palette      = ColorAnalyzer.BuildPalette(bmp);
        Assert.NotEmpty(palette);
        var top = palette[0];
        Assert.True(top.Red > 200, $"Expected dominant red, got ({top.Red},{top.Green},{top.Blue})");
    }

    [Fact]
    public void BuildPalette_ReturnsAtMostEightColors()
    {
        using var bmp = SolidBitmap(400, 400, SKColors.Green);
        var palette   = ColorAnalyzer.BuildPalette(bmp);
        Assert.True(palette.Count <= 8);
    }

    // ── Region segmentation ───────────────────────────────────────────────────

    [Fact]
    public void SegmentRegions_SolidWhiteImage_ReturnsNoRegions()
    {
        using var bmp = SolidBitmap(200, 200, SKColors.White);
        var bg        = ColorAnalyzer.DetectBackground(bmp);
        var regions   = ColorAnalyzer.SegmentRegions(bmp, bg);
        // Everything is background — no foreground regions expected
        Assert.Empty(regions);
    }

    [Fact]
    public void SegmentRegions_LargeSolidRect_DetectsOneRegion()
    {
        // 60×60 blue square on a 200×200 white background
        using var bmp = WhiteWithRect(200, 200, 70, 70, 60, 60, SKColors.Blue);
        using var prep = Preprocessor.Prepare(bmp);
        var bg         = ColorAnalyzer.DetectBackground(prep.Original);
        var regions    = ColorAnalyzer.SegmentRegions(prep.Original, bg);

        Assert.NotEmpty(regions);
        var r = regions[0];
        Assert.True(r.FillColor.Blue  > 150, $"Expected blue region, got ({r.FillColor.Red},{r.FillColor.Green},{r.FillColor.Blue})");
        Assert.True(r.FillColor.Red   < 100);
        Assert.True(r.FillColor.Green < 100);
    }

    [Fact]
    public void SegmentRegions_LargeSolidRect_ReturnsExpectedBounds()
    {
        using var bmp = WhiteWithRect(240, 180, 50, 40, 80, 60, SKColors.SteelBlue);
        using var prep = Preprocessor.Prepare(bmp);
        var bg = ColorAnalyzer.DetectBackground(prep.Original);
        var regions = ColorAnalyzer.SegmentRegions(prep.Original, bg);

        var region = Assert.Single(regions);
        AssertRectApproximately(new SKRectI(50, 40, 130, 100), region.Bounds, 0, "region bounds");
    }

    [Fact]
    public void SegmentRegions_TinyRect_IsFilteredAsNoise()
    {
        // 3×3 red square — below MinRegionPixels, should be filtered
        using var bmp = WhiteWithRect(200, 200, 100, 100, 3, 3, SKColors.Red);
        var bg        = ColorAnalyzer.DetectBackground(bmp);
        var regions   = ColorAnalyzer.SegmentRegions(bmp, bg);
        Assert.True(regions.All(r => r.PixelCount >= ColorAnalyzer.MinRegionPixels));
    }

    [Fact]
    public void SegmentRegions_FullImageColor_IsFilteredAsBackground()
    {
        // A solid blue image — coverage is 100%, must be treated as background
        using var bmp  = SolidBitmap(200, 200, SKColors.Blue);
        var bg = SKColors.White; // pretend white is background so blue looks foreground
        var regions    = ColorAnalyzer.SegmentRegions(bmp, bg);
        Assert.True(regions.All(r => r.Coverage <= ColorAnalyzer.BackgroundCoverageThreshold));
    }

    [Fact]
    public void SegmentRegions_MultipleRects_DetectsMultipleRegions()
    {
        // Two separate 40×40 coloured squares on white
        using var bmp = new SKBitmap(300, 150, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var p1 = new SKPaint { Color = SKColors.Red,  IsAntialias = false };
            using var p2 = new SKPaint { Color = SKColors.Blue, IsAntialias = false };
            canvas.DrawRect(30,  55, 40, 40, p1);
            canvas.DrawRect(230, 55, 40, 40, p2);
        }
        var bg      = ColorAnalyzer.DetectBackground(bmp);
        var regions = ColorAnalyzer.SegmentRegions(bmp, bg);
        Assert.True(regions.Count >= 2,
            $"Expected at least 2 regions but found {regions.Count}");
    }

    // ── Colour distance ───────────────────────────────────────────────────────

    [Fact]
    public void ColorDistance_SameColor_IsZero()
    {
        Assert.Equal(0, ColorAnalyzer.ColorDistance(SKColors.Red, SKColors.Red));
    }

    [Fact]
    public void ColorDistance_BlackAndWhite_IsMaximum()
    {
        Assert.Equal(255, ColorAnalyzer.ColorDistance(SKColors.Black, SKColors.White));
    }

    [Fact]
    public void ColorDistance_SlightlyDifferent_IsWithinTolerance()
    {
        var a = new SKColor(100, 100, 100);
        var b = new SKColor(110, 108, 105);
        Assert.True(ColorAnalyzer.ColorDistance(a, b) <= ColorAnalyzer.ColorTolerance);
    }

    // ── Full Analyze ──────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_WhiteWithColorRect_ProducesResultWithRegion()
    {
        using var bmp  = WhiteWithRect(300, 200, 100, 70, 80, 60, SKColors.Green);
        using var prep = Preprocessor.Prepare(bmp);
        var result     = ColorAnalyzer.Analyze(prep);

        Assert.NotNull(result.Background);
        Assert.NotNull(result.DominantColors);
        Assert.NotNull(result.Regions);
        // Should detect at least the green rectangle
        Assert.NotEmpty(result.Regions);
    }
}
