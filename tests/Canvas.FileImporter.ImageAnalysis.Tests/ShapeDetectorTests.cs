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

    [Fact]
    public void FindHorizontalSegments_SmallGap_JoinsSingleSegment()
    {
        const int w = 160;
        const int h = 40;
        var edges = new byte[w * h];
        int y = 20;
        for (int x = 10; x <= 72; x++)
            edges[y * w + x] = 255;
        for (int x = 75; x <= 130; x++)
            edges[y * w + x] = 255;

        var hSegs = ShapeDetector.FindHorizontalSegments(edges, w, h);
        var seg = Assert.Single(hSegs, s => s.Y == y);

        Assert.True(seg.Start <= 10, $"Expected segment to start near 10, got {seg.Start}");
        Assert.True(seg.End >= 130, $"Expected segment to bridge the gap through 130, got {seg.End}");
    }

    [Fact]
    public void FindVerticalSegments_SmallGap_JoinsSingleSegment()
    {
        const int w = 60;
        const int h = 160;
        var edges = new byte[w * h];
        int x = 30;
        for (int y = 10; y <= 72; y++)
            edges[y * w + x] = 255;
        for (int y = 75; y <= 130; y++)
            edges[y * w + x] = 255;

        var vSegs = ShapeDetector.FindVerticalSegments(edges, w, h);
        var seg = Assert.Single(vSegs, s => s.Y == x);

        Assert.True(seg.Start <= 10, $"Expected segment to start near 10, got {seg.Start}");
        Assert.True(seg.End >= 130, $"Expected segment to bridge the gap through 130, got {seg.End}");
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

    [Fact]
    public void FindFilledRectangles_SolidRect_DetectsBounds()
    {
        using var bmp = SolidBitmap(220, 160, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(40, 36, 96, 52, paint);
        }
        using var prep = Prepare(bmp);

        var rects = ShapeDetector.FindFilledRectangles(prep.Binary);
        var rect = Assert.Single(rects);

        Assert.Equal(new SKRectI(40, 36, 136, 88), rect);
    }

    [Fact]
    public void FindFilledRectangles_SolidCircle_DoesNotReturnRect()
    {
        using var bmp = SolidBitmap(180, 140, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawCircle(90, 70, 28, paint);
        }
        using var prep = Prepare(bmp);

        var rects = ShapeDetector.FindFilledRectangles(prep.Binary);

        Assert.Empty(rects);
    }

    [Fact]
    public void FindRoundedRectangles_FilledRoundRect_DetectsRadiusAndBounds()
    {
        using var bmp = SolidBitmap(240, 180, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRoundRect(new SKRect(44, 36, 164, 104), 18, 18, paint);
        }
        using var prep = Prepare(bmp);

        var rects = ShapeDetector.FindRoundedRectangles(prep.Binary);
        var rect = Assert.Single(rects);

        Assert.InRange(rect.Bounds.Left, 43, 45);
        Assert.InRange(rect.Bounds.Top, 35, 37);
        Assert.InRange(rect.Bounds.Right, 163, 165);
        Assert.InRange(rect.Bounds.Bottom, 103, 105);
        Assert.InRange(rect.Radius, 8, 24);
    }

    [Fact]
    public void FindRoundedRectangles_SolidRect_DoesNotReturnRoundedRect()
    {
        using var bmp = SolidBitmap(220, 160, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(40, 36, 96, 52, paint);
        }
        using var prep = Prepare(bmp);

        var rects = ShapeDetector.FindRoundedRectangles(prep.Binary);

        Assert.Empty(rects);
    }

    [Fact]
    public void FindRoundedRectangles_SolidCircle_DoesNotReturnRoundedRect()
    {
        using var bmp = SolidBitmap(180, 140, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawCircle(90, 70, 28, paint);
        }
        using var prep = Prepare(bmp);

        var rects = ShapeDetector.FindRoundedRectangles(prep.Binary);

        Assert.Empty(rects);
    }

    [Fact]
    public void FindIconClusters_IrregularConnectedSymbol_DetectsCluster()
    {
        using var bmp = SolidBitmap(180, 140, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            using var path = new SKPath();
            path.MoveTo(90, 30);
            path.LineTo(103, 62);
            path.LineTo(138, 62);
            path.LineTo(110, 82);
            path.LineTo(122, 116);
            path.LineTo(90, 94);
            path.LineTo(58, 116);
            path.LineTo(70, 82);
            path.LineTo(42, 62);
            path.LineTo(77, 62);
            path.Close();
            canvas.DrawPath(path, paint);
        }
        using var prep = Prepare(bmp);

        var clusters = ShapeDetector.FindIconClusters(prep.Binary);
        var cluster = Assert.Single(clusters);

        Assert.InRange(cluster.Left, 40, 45);
        Assert.InRange(cluster.Top, 28, 32);
        Assert.InRange(cluster.Right, 136, 140);
        Assert.InRange(cluster.Bottom, 114, 118);
    }

    [Fact]
    public void FindImageClusters_LargeIrregularConnectedRegion_DetectsCluster()
    {
        using var bmp = SolidBitmap(360, 240, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
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
        using var prep = Prepare(bmp);

        var clusters = ShapeDetector.FindImageClusters(prep.Binary);
        var cluster = Assert.Single(clusters);

        Assert.InRange(cluster.Left, 40, 74);
        Assert.InRange(cluster.Top, 32, 56);
        Assert.InRange(cluster.Right, 280, 310);
        Assert.InRange(cluster.Bottom, 180, 212);
    }

    [Fact]
    public void FindImageClusters_SolidPanel_DoesNotReturnImageCluster()
    {
        using var bmp = SolidBitmap(360, 240, SKColors.White);
        using (var canvas = new SKCanvas(bmp))
        {
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(48, 40, 240, 128, paint);
        }
        using var prep = Prepare(bmp);

        var clusters = ShapeDetector.FindImageClusters(prep.Binary);

        Assert.Empty(clusters);
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
