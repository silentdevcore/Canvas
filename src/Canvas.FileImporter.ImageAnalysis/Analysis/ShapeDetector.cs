using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 3: detects geometric shapes (axis-aligned rectangles, thin lines,
/// and approximate ellipses) from the preprocessed image.
///
/// Pipeline:
///   Grayscale → Sobel edge map → H/V line segments → rectangle assembly
///                                                   → thin-line elements
///   Binary    → connected-component circularity    → ellipse hints
/// </summary>
public static class ShapeDetector
{
    // ── Tuning constants ──────────────────────────────────────────────────────

    /// <summary>Sobel magnitude threshold (0–255) above which a pixel is an edge.</summary>
    public const int EdgeThreshold = 30;

    /// <summary>Minimum length in pixels for a line segment to be retained.</summary>
    public const int MinLineLength = 20;

    /// <summary>Maximum gap in pixels allowed within a single line segment.</summary>
    public const int MaxLineGap = 4;

    /// <summary>Pixel tolerance for aligning opposite rectangle sides.</summary>
    public const int RectAlignTolerance = 6;

    /// <summary>Circularity ratio threshold for ellipse classification.</summary>
    private const double EllipseCircularityMin = 0.60;

    /// <summary>Minimum bounding-box dimension (px) for an ellipse candidate.</summary>
    private const int MinEllipseSize = 12;

    // ── Entry point ───────────────────────────────────────────────────────────

    public static ShapeDetectionResult Detect(PreparedImage img, ColorAnalysisResult colors)
    {
        byte[] edgeMap = ComputeSobelEdgeMap(img.Grayscale);

        var hLines = FindHorizontalSegments(edgeMap, img.Width, img.Height);
        var vLines = FindVerticalSegments(edgeMap, img.Width, img.Height);
        var (gridHLines, gridVLines) = DetectGridSegments(hLines, vLines);

        var shapes = new List<ImageShapePrimitive>();
        int zOrder = 0;

        // Assemble closed rectangles from H+V segments
        var rects = AssembleRectangles(hLines, vLines);
        foreach (var r in rects)
        {
            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = r,
                Kind        = ShapeKind.Rect,
                StrokeColor = SampleStrokeColor(img.Original, r),
                FillColor   = SampleFillColor(img.Original, colors, r),
                StrokeWidth = 1,
                Confidence  = 0.85,
                ZOrder      = zOrder++,
            });
        }

        // Emit unmatched thin H-lines
        foreach (var seg in hLines)
        {
            bool isGridLine = gridHLines.Contains(seg);
            if (seg.UsedInRect && !isGridLine) continue;
            int h = seg.Thickness;
            if (h > 6) continue; // not a thin line
            var bounds = new SKRectI(seg.Start, seg.Y - h / 2, seg.End, seg.Y + h / 2 + 1);
            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = bounds,
                Kind        = ShapeKind.Line,
                StrokeColor = SampleStrokeColor(img.Original, bounds),
                StrokeWidth = Math.Max(1, h),
                Confidence  = 0.75,
                AnalysisType = isGridLine ? "grid-line" : null,
                ZOrder      = zOrder++,
            });
        }

        // Emit unmatched thin V-lines
        foreach (var seg in vLines)
        {
            bool isGridLine = gridVLines.Contains(seg);
            if (seg.UsedInRect && !isGridLine) continue;
            int w = seg.Thickness;
            if (w > 6) continue;
            var bounds = new SKRectI(seg.Y - w / 2, seg.Start, seg.Y + w / 2 + 1, seg.End);
            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = bounds,
                Kind        = ShapeKind.Line,
                StrokeColor = SampleStrokeColor(img.Original, bounds),
                StrokeWidth = Math.Max(1, w),
                Confidence  = 0.75,
                AnalysisType = isGridLine ? "grid-line" : null,
                ZOrder      = zOrder++,
            });
        }

        // Ellipses from binary connected components
        var ellipses = FindEllipses(img.Binary);
        foreach (var e in ellipses)
        {
            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = e,
                Kind        = ShapeKind.Ellipse,
                StrokeColor = SampleStrokeColor(img.Original, e),
                FillColor   = SampleFillColor(img.Original, colors, e),
                StrokeWidth = 1,
                Confidence  = 0.70,
                ZOrder      = zOrder++,
            });
        }

        return new ShapeDetectionResult { Shapes = shapes };
    }

    // ── Sobel edge map ────────────────────────────────────────────────────────

    /// <summary>
    /// Computes a Sobel edge magnitude map for a Gray8 bitmap.
    /// Returns a flat byte array (row-major) with values in [0, 255].
    /// </summary>
    public static unsafe byte[] ComputeSobelEdgeMap(SKBitmap gray)
    {
        int w = gray.Width, h = gray.Height;
        var edges = new byte[w * h];

        byte* src    = (byte*)gray.GetPixels().ToPointer();
        int   stride = gray.RowBytes;

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                // 3×3 neighbourhood pixels (row-major, Gray8 = 1 byte/pixel)
                int p00 = src[(y - 1) * stride + (x - 1)];
                int p01 = src[(y - 1) * stride +  x     ];
                int p02 = src[(y - 1) * stride + (x + 1)];
                int p10 = src[ y      * stride + (x - 1)];
                int p12 = src[ y      * stride + (x + 1)];
                int p20 = src[(y + 1) * stride + (x - 1)];
                int p21 = src[(y + 1) * stride +  x     ];
                int p22 = src[(y + 1) * stride + (x + 1)];

                int gx = -p00 + p02 - 2 * p10 + 2 * p12 - p20 + p22;
                int gy = -p00 - 2 * p01 - p02 + p20 + 2 * p21 + p22;

                int mag = (int)Math.Sqrt(gx * gx + gy * gy);
                edges[y * w + x] = (byte)Math.Min(255, mag);
            }
        }

        return edges;
    }

    // ── H/V segment detection ────────────────────────────────────────────────

    public static List<LineSegment> FindHorizontalSegments(byte[] edgeMap, int w, int h)
    {
        var segments = new List<LineSegment>();
        for (int y = 1; y < h - 1; y++)
        {
            int runStart = -1, runLen = 0;
            for (int x = 1; x < w - 1; x++)
            {
                bool isEdge = edgeMap[y * w + x] >= EdgeThreshold;
                if (isEdge)
                {
                    if (runStart < 0) runStart = x;
                    runLen++;
                }
                else
                {
                    // Gap handling
                    if (runStart >= 0 && runLen >= MinLineLength)
                        segments.Add(new LineSegment { Y = y, Start = runStart, End = x - 1, Thickness = 1 });
                    if (!isEdge && runLen > 0 && runLen < MaxLineGap)
                    {
                        // small gap — extend existing run
                    }
                    else { runStart = -1; runLen = 0; }
                }
            }
            if (runStart >= 0 && runLen >= MinLineLength)
                segments.Add(new LineSegment { Y = y, Start = runStart, End = w - 2, Thickness = 1 });
        }
        return MergeAdjacentSegments(segments, horizontal: true);
    }

    public static List<LineSegment> FindVerticalSegments(byte[] edgeMap, int w, int h)
    {
        var segments = new List<LineSegment>();
        for (int x = 1; x < w - 1; x++)
        {
            int runStart = -1, runLen = 0;
            for (int y = 1; y < h - 1; y++)
            {
                bool isEdge = edgeMap[y * w + x] >= EdgeThreshold;
                if (isEdge)
                {
                    if (runStart < 0) runStart = y;
                    runLen++;
                }
                else
                {
                    if (runStart >= 0 && runLen >= MinLineLength)
                        segments.Add(new LineSegment { Y = x, Start = runStart, End = y - 1, Thickness = 1 });
                    runStart = -1; runLen = 0;
                }
            }
            if (runStart >= 0 && runLen >= MinLineLength)
                segments.Add(new LineSegment { Y = x, Start = runStart, End = h - 2, Thickness = 1 });
        }
        return MergeAdjacentSegments(segments, horizontal: false);
    }

    /// <summary>
    /// Merges parallel line segments on adjacent rows/columns into one thicker segment.
    /// </summary>
    private static List<LineSegment> MergeAdjacentSegments(List<LineSegment> segments, bool horizontal)
    {
        if (segments.Count == 0) return segments;

        var sorted  = segments.OrderBy(s => s.Y).ThenBy(s => s.Start).ToList();
        var merged  = new List<LineSegment>();
        var current = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            bool adjacent  = next.Y == current.Y + 1;
            bool overlaps  = next.Start <= current.End + MaxLineGap &&
                             next.End   >= current.Start - MaxLineGap;
            if (adjacent && overlaps)
            {
                current = new LineSegment
                {
                    Y         = current.Y,
                    Start     = Math.Min(current.Start, next.Start),
                    End       = Math.Max(current.End,   next.End),
                    Thickness = current.Thickness + 1,
                };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);
        return merged;
    }

    // ── Rectangle assembly ────────────────────────────────────────────────────

    /// <summary>
    /// Finds axis-aligned rectangles by matching pairs of horizontal segments
    /// (top/bottom) with pairs of vertical segments (left/right) that form a
    /// closed boundary.
    /// </summary>
    public static List<SKRectI> AssembleRectangles(
        List<LineSegment> hSegs, List<LineSegment> vSegs)
    {
        var rects = new List<SKRectI>();
        int t = RectAlignTolerance;

        for (int hi = 0; hi < hSegs.Count; hi++)
        {
            var top = hSegs[hi];
            for (int hj = hi + 1; hj < hSegs.Count; hj++)
            {
                var bottom = hSegs[hj];
                if (bottom.Y <= top.Y) continue;

                // Top and bottom must roughly overlap in X
                int overlapStart = Math.Max(top.Start, bottom.Start);
                int overlapEnd   = Math.Min(top.End,   bottom.End);
                if (overlapEnd - overlapStart < MinLineLength) continue;

                // Find a left V-segment spanning [top.Y, bottom.Y]
                var lefts = vSegs.Where(v =>
                    v.Start <= top.Y    + t &&
                    v.End   >= bottom.Y - t &&
                    v.Y     >= overlapStart - t &&
                    v.Y     <= overlapStart + t * 3).ToList();

                var rights = vSegs.Where(v =>
                    v.Start <= top.Y    + t &&
                    v.End   >= bottom.Y - t &&
                    v.Y     >= overlapEnd   - t * 3 &&
                    v.Y     <= overlapEnd   + t).ToList();

                if (lefts.Count == 0 || rights.Count == 0) continue;

                var left  = lefts[0];
                var right = rights[^1];

                int rectW = right.Y - left.Y;
                int rectH = bottom.Y - top.Y;
                if (rectW < MinLineLength || rectH < MinLineLength) continue;

                var rect = new SKRectI(left.Y, top.Y, right.Y, bottom.Y);
                // Deduplicate near-identical rectangles
                if (rects.Any(r => RectsSimilar(r, rect))) continue;

                rects.Add(rect);
                top.UsedInRect    = true;
                bottom.UsedInRect = true;
                left.UsedInRect   = true;
                right.UsedInRect  = true;
            }
        }

        return rects;
    }

    private static bool RectsSimilar(SKRectI a, SKRectI b) =>
        Math.Abs(a.Left   - b.Left)   < RectAlignTolerance * 2 &&
        Math.Abs(a.Top    - b.Top)    < RectAlignTolerance * 2 &&
        Math.Abs(a.Right  - b.Right)  < RectAlignTolerance * 2 &&
        Math.Abs(a.Bottom - b.Bottom) < RectAlignTolerance * 2;

    private static (HashSet<LineSegment> horizontal, HashSet<LineSegment> vertical) DetectGridSegments(
        List<LineSegment> hLines,
        List<LineSegment> vLines)
    {
        var horizontal = new HashSet<LineSegment>();
        var vertical = new HashSet<LineSegment>();

        foreach (var h in hLines)
        {
            int intersections = vLines.Count(v => SegmentsIntersect(h, v));
            if (intersections >= 2)
                horizontal.Add(h);
        }

        foreach (var v in vLines)
        {
            int intersections = hLines.Count(h => SegmentsIntersect(h, v));
            if (intersections >= 2)
                vertical.Add(v);
        }

        return (horizontal, vertical);
    }

    private static bool SegmentsIntersect(LineSegment horizontal, LineSegment vertical)
    {
        const int tolerance = 3;
        return vertical.Y >= horizontal.Start - tolerance &&
               vertical.Y <= horizontal.End + tolerance &&
               horizontal.Y >= vertical.Start - tolerance &&
               horizontal.Y <= vertical.End + tolerance;
    }

    // ── Ellipse detection ─────────────────────────────────────────────────────

    /// <summary>
    /// Finds connected components in the binary bitmap whose shape is
    /// approximately circular (circularity ratio ≥ threshold).
    /// Uses a simple 4-connected labelling pass.
    /// </summary>
    public static unsafe List<SKRectI> FindEllipses(SKBitmap binary)
    {
        int w = binary.Width, h = binary.Height;
        var labels    = new int[w * h];
        int nextLabel = 1;
        var blobPixels = new Dictionary<int, (int count, int perim, int minX, int minY, int maxX, int maxY)>();

        byte* src    = (byte*)binary.GetPixels().ToPointer();
        int   stride = binary.RowBytes;

        // Single-pass 4-connected labelling (simplified — no union-find)
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                if (src[y * stride + x] != 0) continue; // only dark pixels

                int left = labels[y * w + (x - 1)];
                int up   = labels[(y - 1) * w + x];
                int lbl  = left != 0 ? left : (up != 0 ? up : nextLabel++);
                labels[y * w + x] = lbl;

                // Count perimeter pixels (4-connected boundary)
                bool isPerimeter =
                    src[(y - 1) * stride + x] == 255 ||
                    src[(y + 1) * stride + x] == 255 ||
                    src[y       * stride + (x - 1)] == 255 ||
                    src[y       * stride + (x + 1)] == 255;

                if (!blobPixels.TryGetValue(lbl, out var b))
                    b = (0, 0, x, y, x, y);
                blobPixels[lbl] = (
                    b.count + 1,
                    b.perim + (isPerimeter ? 1 : 0),
                    Math.Min(b.minX, x), Math.Min(b.minY, y),
                    Math.Max(b.maxX, x), Math.Max(b.maxY, y));
            }
        }

        var ellipses = new List<SKRectI>();
        foreach (var (_, blob) in blobPixels)
        {
            if (blob.count < 50) continue; // too small

            int bw = blob.maxX - blob.minX + 1;
            int bh = blob.maxY - blob.minY + 1;
            if (bw < MinEllipseSize || bh < MinEllipseSize) continue;

            double fillRatio = (double)blob.count / (bw * bh);
            if (fillRatio > 0.90) continue;

            // Circularity = 4π·Area / Perimeter²
            double circ = blob.perim > 0
                ? (4 * Math.PI * blob.count) / ((double)blob.perim * blob.perim)
                : 0;

            // Avoid classifying obvious rectangles
            double aspectRatio = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspectRatio > 3.0) continue;

            if (circ >= EllipseCircularityMin)
                ellipses.Add(new SKRectI(blob.minX, blob.minY, blob.maxX + 1, blob.maxY + 1));
        }

        return ellipses;
    }

    // ── Colour sampling helpers ───────────────────────────────────────────────

    private static SKColor SampleStrokeColor(SKBitmap bmp, SKRectI bounds)
    {
        // Sample the edge pixel along the top border of the bounds
        int cx = (bounds.Left + bounds.Right)  / 2;
        int y  = Math.Clamp(bounds.Top, 0, bmp.Height - 1);
        int x  = Math.Clamp(cx, 0, bmp.Width - 1);
        return bmp.GetPixel(x, y);
    }

    private static SKColor SampleFillColor(SKBitmap bmp, ColorAnalysisResult colors, SKRectI bounds)
    {
        // Sample the centre of the bounding box
        int cx = Math.Clamp((bounds.Left + bounds.Right)  / 2, 0, bmp.Width  - 1);
        int cy = Math.Clamp((bounds.Top  + bounds.Bottom) / 2, 0, bmp.Height - 1);
        var centre = bmp.GetPixel(cx, cy);

        // If centre matches background, look for a region fill instead
        if (ColorAnalyzer.ColorDistance(centre, colors.Background) <= ColorAnalyzer.ColorTolerance)
            return SKColors.Transparent;

        return centre;
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

public sealed class LineSegment
{
    /// <summary>
    /// For horizontal segments: the row Y.
    /// For vertical segments: the column X.
    /// </summary>
    public int Y         { get; init; }
    public int Start     { get; init; }
    public int End       { get; init; }
    public int Thickness { get; init; }
    public bool UsedInRect { get; set; }
}

public sealed class ShapeDetectionResult
{
    public IReadOnlyList<ImageShapePrimitive> Shapes { get; init; } = [];
}
