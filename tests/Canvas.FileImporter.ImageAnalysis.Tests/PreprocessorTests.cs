using Canvas.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Tests;

public class PreprocessorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a solid-colour RGBA8888 bitmap.</summary>
    private static SKBitmap SolidBitmap(int width, int height, SKColor color)
    {
        var bmp    = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var c = new SKCanvas(bmp);
        c.Clear(color);
        return bmp;
    }

    // ── Output dimensions ─────────────────────────────────────────────────────

    [Fact]
    public void SmallImage_IsNotScaled()
    {
        using var src    = SolidBitmap(800, 600, SKColors.White);
        using var result = Preprocessor.Prepare(src);

        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Equal(1.0, result.ScaleFactor, precision: 3);
    }

    [Fact]
    public void LargeImage_IsScaledDown()
    {
        // 4800 × 3600 should be halved to 2400 × 1800
        using var src    = SolidBitmap(4800, 3600, SKColors.White);
        using var result = Preprocessor.Prepare(src);

        Assert.Equal(2400, result.Width);
        Assert.Equal(1800, result.Height);
        Assert.Equal(0.5, result.ScaleFactor, precision: 3);
    }

    [Fact]
    public void ExactlyMaxDimension_IsNotScaled()
    {
        using var src    = SolidBitmap(Preprocessor.MaxWorkingDimension, 100, SKColors.Gray);
        using var result = Preprocessor.Prepare(src);

        Assert.Equal(Preprocessor.MaxWorkingDimension, result.Width);
        Assert.Equal(1.0, result.ScaleFactor, precision: 3);
    }

    // ── Output bitmaps ────────────────────────────────────────────────────────

    [Fact]
    public void PreparedImage_HasThreeNonNullBitmaps()
    {
        using var src    = SolidBitmap(200, 100, SKColors.LightBlue);
        using var result = Preprocessor.Prepare(src);

        Assert.NotNull(result.Original);
        Assert.NotNull(result.Grayscale);
        Assert.NotNull(result.Binary);
    }

    [Fact]
    public void GrayscaleBitmap_IsGray8ColorType()
    {
        using var src    = SolidBitmap(100, 100, SKColors.Red);
        using var result = Preprocessor.Prepare(src);

        Assert.Equal(SKColorType.Gray8, result.Grayscale.ColorType);
    }

    [Fact]
    public void BinaryBitmap_IsGray8ColorType()
    {
        using var src    = SolidBitmap(100, 100, SKColors.Blue);
        using var result = Preprocessor.Prepare(src);

        Assert.Equal(SKColorType.Gray8, result.Binary.ColorType);
    }

    [Fact]
    public void BinaryBitmap_ContainsOnlyBlackAndWhite()
    {
        using var src    = SolidBitmap(64, 64, SKColors.Gray);
        using var result = Preprocessor.Prepare(src);

        var bmp = result.Binary;
        for (int y = 0; y < bmp.Height; y += 4)
        {
            for (int x = 0; x < bmp.Width; x += 4)
            {
                var px = bmp.GetPixel(x, y);
                // Gray8 maps to ARGB with R=G=B=value
                bool isBlack = px.Red == 0   && px.Green == 0   && px.Blue == 0;
                bool isWhite = px.Red == 255 && px.Green == 255 && px.Blue == 255;
                Assert.True(isBlack || isWhite, $"Unexpected colour {px} at ({x},{y})");
            }
        }
    }

    // ── Otsu threshold ────────────────────────────────────────────────────────

    // ── Helpers: Gray8 bitmaps filled directly (canvas can't render to Gray8) ───

    private static unsafe SKBitmap Gray8Bitmap(int w, int h, byte fill)
    {
        var info = new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
        var bmp  = new SKBitmap(info);
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        int   len = bmp.RowBytes * bmp.Height;
        new Span<byte>(ptr, len).Fill(fill);
        return bmp;
    }

    private static unsafe SKBitmap Gray8HalfHalf(int w, int h, byte left, byte right)
    {
        var info = new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
        var bmp  = new SKBitmap(info);
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        int stride = bmp.RowBytes;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                ptr[y * stride + x] = x < w / 2 ? left : right;
        return bmp;
    }

    private static SKBitmap GradientBitmap(int w, int h, byte left, byte right)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte v = (byte)Math.Round(left + (right - left) * (x / (double)(w - 1)));
                bmp.SetPixel(x, y, new SKColor(v, v, v));
            }
        }
        return bmp;
    }

    private static SKBitmap WhiteBitmapWithBlackPixels(int w, int h, params (int x, int y)[] pixels)
    {
        var bmp = SolidBitmap(w, h, SKColors.White);
        foreach (var (x, y) in pixels)
            bmp.SetPixel(x, y, SKColors.Black);
        return bmp;
    }

    private static unsafe int CountBlackPixels(SKBitmap bmp)
    {
        int count = 0;
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        int stride = bmp.RowBytes;

        for (int y = 0; y < bmp.Height; y++)
        {
            byte* row = ptr + y * stride;
            for (int x = 0; x < bmp.Width; x++)
                if (row[x] == 0) count++;
        }

        return count;
    }

    [Fact]
    public void OtsuThreshold_PureWhiteImage_ReturnsHighThreshold()
    {
        using var g8 = Gray8Bitmap(200, 200, 255);
        int t = Preprocessor.ComputeOtsuThreshold(g8);
        // A uniform white image — all pixels above midpoint
        Assert.True(t >= 127, $"Expected threshold ≥ 127 for white image but got {t}");
    }

    [Fact]
    public void OtsuThreshold_PureBlackImage_ReturnsLowThreshold()
    {
        using var g8 = Gray8Bitmap(200, 200, 0);
        int t = Preprocessor.ComputeOtsuThreshold(g8);
        // A uniform black image — trivially below midpoint
        Assert.True(t <= 10, $"Expected threshold ≤ 10 for black image but got {t}");
    }

    [Fact]
    public void OtsuThreshold_HalfDarkHalfLight_ReturnsThresholdBetweenTwoModes()
    {
        // Use {50, 200} — two distinct modes; optimal threshold must sit between them
        using var g8 = Gray8HalfHalf(256, 100, 50, 200);
        int t = Preprocessor.ComputeOtsuThreshold(g8);
        Assert.InRange(t, 50, 199);
    }

    // ── White image → all white binary ───────────────────────────────────────

    [Fact]
    public void WhiteImage_BinaryBitmap_AllWhite()
    {
        using var src    = SolidBitmap(100, 100, SKColors.White);
        using var result = Preprocessor.Prepare(src);

        var bmp = result.Binary;
        // A pure white image: all pixels should be white in the binary output
        for (int y = 0; y < bmp.Height; y += 5)
        {
            for (int x = 0; x < bmp.Width; x += 5)
            {
                var px = bmp.GetPixel(x, y);
                Assert.Equal(255, (int)px.Red);
            }
        }
    }

    [Fact]
    public void UnevenBackground_AdaptiveThreshold_DoesNotTurnDarkSideIntoForeground()
    {
        using var src = GradientBitmap(160, 80, 150, 240);
        using var canvas = new SKCanvas(src);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(new SKRect(24, 20, 30, 60), paint);

        using var result = Preprocessor.Prepare(src);

        int blackPixels = CountBlackPixels(result.Binary);
        double blackRatio = blackPixels / (double)(result.Binary.Width * result.Binary.Height);

        Assert.True(blackRatio < 0.08, $"Expected sparse foreground, got black ratio {blackRatio:P2}");
        Assert.Equal(255, result.Binary.GetPixel(8, 40).Red);
        Assert.Equal(0, result.Binary.GetPixel(26, 40).Red);
        Assert.Equal(255, result.Binary.GetPixel(140, 40).Red);
    }

    [Fact]
    public void Denoise_RemovesSinglePixelSpeckles()
    {
        using var src = WhiteBitmapWithBlackPixels(
            80, 60,
            (10, 10),
            (42, 8),
            (65, 40));

        using var result = Preprocessor.Prepare(src);

        Assert.Equal(0, CountBlackPixels(result.Binary));
    }

    [Fact]
    public void Denoise_RemovesTinyThinComponents()
    {
        using var src = WhiteBitmapWithBlackPixels(
            80, 60,
            (10, 10), (10, 11),
            (42, 8), (43, 8), (44, 8));

        using var result = Preprocessor.Prepare(src);

        Assert.Equal(0, CountBlackPixels(result.Binary));
    }

    [Fact]
    public void Denoise_PreservesSmallPunctuationDot()
    {
        using var src = WhiteBitmapWithBlackPixels(
            80, 60,
            (24, 30), (25, 30),
            (24, 31), (25, 31));

        using var result = Preprocessor.Prepare(src);

        Assert.Equal(4, CountBlackPixels(result.Binary));
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        using var src = SolidBitmap(50, 50, SKColors.Green);
        var result    = Preprocessor.Prepare(src);
        var ex = Record.Exception(result.Dispose);
        Assert.Null(ex);
    }
}
