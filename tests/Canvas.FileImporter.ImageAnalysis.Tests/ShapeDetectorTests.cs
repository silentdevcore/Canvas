using Canvas.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Tests;

public class ShapeDetectorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SKBitmap SolidBitmap(int w, int h, SKColor color)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var c = new SKCanvas(bmp);
        c.Clear(color);
        return bmp;
    }

    /// <summary>White image with a stroked (not filled) rectangle border.</summary>
    private static SKBitmap WhiteWithRectBorder(int imgW, int imgH,
        int rx, int ry, int rw, int rh, SKColor strokeColor, int strokeWidth = 2)
    {
        var bmp = new SKBitmap(imgW, imgH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint
        {
            Color       = strokeColor,
            IsAntialias = false,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
        };
        canvas.DrawRect(rx, ry, rw, rh, paint);
        return bmp;
    }

    private static PreparedImage Prepare(SKBitmap bmp) => Preprocessor.Prepare(bmp);

    // ── Sobel edge map ────────────────────────────────────────────────────────

    [Fact]
    public void SobelEdgeMap_SolidWhite_AllEdgesNearZero()
    {
        using var bmp  = SolidBitmap(100, 100, SKColors.White);
        using var prep = Prepare(bmp);
        var edges      = ShapeDetector.ComputeSobelEdgeMap(prep.Grayscale);

        // No edges expected in a uniform image
        int maxEdge = edges.Max();
        Assert.True(maxEdge < ShapeDetector.EdgeThreshold,
            $"Expected no strong edges but max was {maxEdge}");
    }

    [Fact]
    public void SobelEdgeMap_ImageWithHardEdge_HasStrongEdge()
    {
        // Left half black, right half white — hard vertical edge in the middle
        var bmp = new SKBitmap(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var p = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(0, 0, 50, 100, p);
        }
        using var prep = Prepare(bmp);
        bmp.Dispose();

        var edges = ShapeDetector.ComputeSobelEdgeMap(prep.Grayscale);
        int maxEdge = edges.Max();
        Assert.True(maxEdge >= ShapeDetector.EdgeThreshold,
            $"Expected strong edge ≥ {ShapeDetector.EdgeThreshold} but max was {maxEdge}");
    }

    [Fact]
    public void SobelEdgeMap_OutputSize_MatchesInput()
    {
        using var bmp  = SolidBitmap(80, 60, SKColors.Gray);
        using var prep = Prepare(bmp);
        var edges      = ShapeDetector.ComputeSobelEdgeMap(prep.Grayscale);
        Assert.Equal(prep.Width * prep.Height, edges.Length);
    }

    // ── Horizontal segment detection ──────────────────────────────────────────

    [Fact]
    public void FindHorizontalSegments_ImageWithHorizontalLine_DetectsSegment()
    {
        // Thin black horizontal line across a white image
        var bmp = new SKBitmap(200, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var p = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawLine(10, 50, 190, 50, p);
        }
        using var prep = Prepare(bmp);
        bmp.Dispose();

        var edges = ShapeDetector.ComputeSobelEdgeMap(prep.Grayscale);
        var hSegs = ShapeDetector.FindHorizontalSegments(edges, prep.Width, prep.Height);

        Assert.NotEmpty(hSegs);
        Assert.True(hSegs.Any(s => s.End - s.Start >= ShapeDetector.MinLineLength),
            "No segment long enough found");
    }

    [Fact]
    public void FindHorizontalSegments_SolidWhite_ReturnsNoSegments()
    {
        using var bmp  = SolidBitmap(200, 100, SKColors.White);
        using var prep = Prepare(bmp);
        var edges      = ShapeDetector.ComputeSobelEdgeMap(prep.Grayscale);
        var hSegs      = ShapeDetector.FindHorizontalSegments(edges, prep.Width, prep.Height);
        Assert.Empty(hSegs);
    }

    // ── Rectangle assembly ────────────────────────────────────────────────────

    [Fact]
    public void AssembleRectangles_NoSegments_ReturnsEmpty()
    {
        var rects = ShapeDetector.AssembleRectangles([], []);
        Assert.Empty(rects);
    }

    [Fact]
    public void Detect_ImageWithDrawnRectBorder_DetectsRectOrLines()
    {
        // Draw a clear black rectangle border on white — expect at least shape elements
        using var bmp  = WhiteWithRectBorder(300, 200, 60, 50, 180, 100, SKColors.Black, 3);
        using var prep = Prepare(bmp);
        var colors     = ColorAnalyzer.Analyze(prep);
        var result     = ShapeDetector.Detect(prep, colors);

        Assert.NotNull(result.Shapes);
        // Should detect at least the four border edges as shapes or lines
        Assert.NotEmpty(result.Shapes);
    }

    // ── Ellipse detection ─────────────────────────────────────────────────────

    [Fact]
    public void FindEllipses_SolidWhiteBinary_ReturnsNoEllipses()
    {
        var info = new SKImageInfo(200, 200, SKColorType.Gray8, SKAlphaType.Opaque);
        using var g8 = new SKBitmap(info);
        // Fill all white (255) — no dark connected components
        unsafe
        {
            byte* ptr = (byte*)g8.GetPixels().ToPointer();
            new System.Span<byte>(ptr, g8.RowBytes * g8.Height).Fill(255);
        }
        var ellipses = ShapeDetector.FindEllipses(g8);
        Assert.Empty(ellipses);
    }

    // ── Full detection pipeline ───────────────────────────────────────────────

    [Fact]
    public void Detect_SolidWhiteImage_ReturnsNoShapes()
    {
        using var bmp    = SolidBitmap(200, 200, SKColors.White);
        using var prep   = Prepare(bmp);
        var colors       = ColorAnalyzer.Analyze(prep);
        var result       = ShapeDetector.Detect(prep, colors);
        Assert.Empty(result.Shapes);
    }

    [Fact]
    public void Detect_ReturnsShapePrimitives_WithValidBounds()
    {
        using var bmp  = WhiteWithRectBorder(400, 300, 80, 60, 240, 180, SKColors.DarkGray, 4);
        using var prep = Prepare(bmp);
        var colors     = ColorAnalyzer.Analyze(prep);
        var result     = ShapeDetector.Detect(prep, colors);

        foreach (var shape in result.Shapes)
        {
            Assert.True(shape.Bounds.Width  > 0, "Shape width must be > 0");
            Assert.True(shape.Bounds.Height > 0, "Shape height must be > 0");
            Assert.True(shape.Confidence    > 0, "Confidence must be > 0");
        }
    }
}
