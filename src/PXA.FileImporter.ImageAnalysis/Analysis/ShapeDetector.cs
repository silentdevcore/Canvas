using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Analysis;

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

    /// <summary>Minimum bounding-box dimension (px) for a filled rectangle candidate.</summary>
    private const int MinFilledRectSize = 16;

    /// <summary>Minimum bounding-box dimension (px) for complex icon-like clusters.</summary>
    private const int MinIconClusterSize = 18;

    /// <summary>Maximum bounding-box dimension (px) for complex icon-like clusters.</summary>
    private const int MaxIconClusterSize = 180;

    /// <summary>Minimum bounding-box dimension (px) for larger image-like clusters.</summary>
    private const int MinImageClusterSize = 60;

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

        // Filled rectangles from connected components, independent of edge closure.
        var roundedRects = FindRoundedRectangles(img.Binary);
        foreach (var rounded in roundedRects)
        {
            if (rects.Any(existing => RectsSimilar(existing, rounded.Bounds)))
                continue;

            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = rounded.Bounds,
                Kind        = ShapeKind.Rect,
                StrokeColor = SKColors.Transparent,
                FillColor   = SampleFillColor(img.Original, colors, rounded.Bounds),
                StrokeWidth = 0,
                Confidence  = 0.78,
                AnalysisType = "rounded-rect",
                CornerRadiusPx = rounded.Radius,
                ZOrder      = zOrder++,
            });
        }

        var filledRects = FindFilledRectangles(img.Binary);
        foreach (var r in filledRects)
        {
            if (rects.Any(existing => RectsSimilar(existing, r)) ||
                roundedRects.Any(existing => RectsSimilar(existing.Bounds, r)))
                continue;

            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = r,
                Kind        = ShapeKind.Rect,
                StrokeColor = SKColors.Transparent,
                FillColor   = SampleFillColor(img.Original, colors, r),
                StrokeWidth = 0,
                Confidence  = 0.80,
                AnalysisType = "filled-rect",
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

        var iconClusters = FindIconClusters(img.Binary);
        foreach (var icon in iconClusters)
        {
            if (shapes.Any(existing => IsMostlyCovered(icon, existing.Bounds)))
                continue;

            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = icon,
                Kind        = ShapeKind.Icon,
                StrokeColor = SampleStrokeColor(img.Original, icon),
                FillColor   = SKColors.Transparent,
                StrokeWidth = 1,
                Confidence  = 0.62,
                AnalysisType = "icon-cluster",
                ZOrder      = zOrder++,
            });
        }

        var imageClusters = FindImageClusters(img.Binary);
        foreach (var cluster in imageClusters)
        {
            if (iconClusters.Any(icon => IsMostlyCovered(cluster, icon)) ||
                shapes.Any(existing => IsMostlyCovered(cluster, existing.Bounds)))
                continue;

            shapes.Add(new ImageShapePrimitive
            {
                Bounds      = cluster,
                Kind        = ShapeKind.Icon,
                StrokeColor = SampleStrokeColor(img.Original, cluster),
                FillColor   = SKColors.Transparent,
                StrokeWidth = 1,
                Confidence  = 0.58,
                AnalysisType = "image-cluster",
                ZOrder      = zOrder++,
            });
        }

        // Ellipses from binary connected components
        var ellipses = FindEllipses(img.Binary);
        foreach (var e in ellipses)
        {
            if (iconClusters.Any(icon => IsMostlyCovered(e, icon)) ||
                imageClusters.Any(cluster => IsMostlyCovered(e, cluster)))
                continue;

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
            int runStart = -1, runLen = 0, gapLen = 0, gapRuns = 0, gapPixels = 0, lastEdge = -1;
            for (int x = 1; x < w - 1; x++)
            {
                bool isEdge = edgeMap[y * w + x] >= EdgeThreshold;
                if (isEdge)
                {
                    if (runStart < 0)
                    {
                        runStart = x;
                        gapRuns = 0;
                        gapPixels = 0;
                    }
                    else if (gapLen > 0)
                    {
                        gapRuns++;
                        gapPixels += gapLen;
                    }
                    runLen++;
                    gapLen = 0;
                    lastEdge = x;
                }
                else if (runStart >= 0)
                {
                    gapLen++;
                    if (gapLen > MaxLineGap)
                    {
                        if (IsDenseLineRun(runStart, lastEdge, runLen, gapRuns, gapPixels))
                            segments.Add(new LineSegment { Y = y, Start = runStart, End = lastEdge, Thickness = 1 });
                        runStart = -1;
                        runLen = 0;
                        gapLen = 0;
                        gapRuns = 0;
                        gapPixels = 0;
                        lastEdge = -1;
                    }
                }
            }
            if (runStart >= 0 && IsDenseLineRun(runStart, lastEdge, runLen, gapRuns, gapPixels))
                segments.Add(new LineSegment { Y = y, Start = runStart, End = lastEdge, Thickness = 1 });
        }
        return MergeAdjacentSegments(segments, horizontal: true);
    }

    public static List<LineSegment> FindVerticalSegments(byte[] edgeMap, int w, int h)
    {
        var segments = new List<LineSegment>();
        for (int x = 1; x < w - 1; x++)
        {
            int runStart = -1, runLen = 0, gapLen = 0, gapRuns = 0, gapPixels = 0, lastEdge = -1;
            for (int y = 1; y < h - 1; y++)
            {
                bool isEdge = edgeMap[y * w + x] >= EdgeThreshold;
                if (isEdge)
                {
                    if (runStart < 0)
                    {
                        runStart = y;
                        gapRuns = 0;
                        gapPixels = 0;
                    }
                    else if (gapLen > 0)
                    {
                        gapRuns++;
                        gapPixels += gapLen;
                    }
                    runLen++;
                    gapLen = 0;
                    lastEdge = y;
                }
                else if (runStart >= 0)
                {
                    gapLen++;
                    if (gapLen > MaxLineGap)
                    {
                        if (IsDenseLineRun(runStart, lastEdge, runLen, gapRuns, gapPixels))
                            segments.Add(new LineSegment { Y = x, Start = runStart, End = lastEdge, Thickness = 1 });
                        runStart = -1;
                        runLen = 0;
                        gapLen = 0;
                        gapRuns = 0;
                        gapPixels = 0;
                        lastEdge = -1;
                    }
                }
            }
            if (runStart >= 0 && IsDenseLineRun(runStart, lastEdge, runLen, gapRuns, gapPixels))
                segments.Add(new LineSegment { Y = x, Start = runStart, End = lastEdge, Thickness = 1 });
        }
        return MergeAdjacentSegments(segments, horizontal: false);
    }

    private static bool IsDenseLineRun(int start, int end, int edgeCount, int gapRuns, int gapPixels)
    {
        if (edgeCount < MinLineLength || end < start)
            return false;

        if (gapRuns > 1 || gapPixels > MaxLineGap)
            return false;

        int span = end - start + 1;
        return edgeCount / (double)span >= 0.85;
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

    private static bool IsMostlyCovered(SKRectI bounds, SKRectI coveringBounds)
    {
        double area = (double)bounds.Width * bounds.Height;
        if (area <= 0) return false;

        int ox = Math.Max(0, Math.Min(bounds.Right, coveringBounds.Right) -
                             Math.Max(bounds.Left, coveringBounds.Left));
        int oy = Math.Max(0, Math.Min(bounds.Bottom, coveringBounds.Bottom) -
                             Math.Max(bounds.Top, coveringBounds.Top));

        return (ox * oy) / area >= 0.80;
    }

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

    // ── Filled rectangle detection ────────────────────────────────────────────

    public static List<SKRectI> FindFilledRectangles(SKBitmap binary)
    {
        int totalPixels = binary.Width * binary.Height;
        var rects = new List<SKRectI>();

        foreach (var blob in TextEngine.LabelConnectedComponents(binary))
        {
            int bw = blob.Bounds.Width;
            int bh = blob.Bounds.Height;
            if (bw < MinFilledRectSize || bh < MinFilledRectSize)
                continue;

            int boxArea = bw * bh;
            double imageCoverage = (double)boxArea / totalPixels;
            if (imageCoverage > 0.50)
                continue;

            double fillRatio = (double)blob.PixelCount / boxArea;
            if (fillRatio < 0.85)
                continue;

            double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspect > 20)
                continue;

            if (rects.Any(existing => RectsSimilar(existing, blob.Bounds)))
                continue;

            rects.Add(blob.Bounds);
        }

        return rects;
    }

    public static List<RoundedRectCandidate> FindRoundedRectangles(SKBitmap binary)
    {
        int totalPixels = binary.Width * binary.Height;
        var rects = new List<RoundedRectCandidate>();

        foreach (var blob in TextEngine.LabelConnectedComponents(binary))
        {
            int bw = blob.Bounds.Width;
            int bh = blob.Bounds.Height;
            if (bw < MinFilledRectSize * 2 || bh < MinFilledRectSize * 2)
                continue;

            int boxArea = bw * bh;
            double imageCoverage = (double)boxArea / totalPixels;
            if (imageCoverage > 0.50)
                continue;

            double fillRatio = (double)blob.PixelCount / boxArea;
            if (fillRatio < 0.82 || fillRatio > 0.99)
                continue;

            double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspect > 8)
                continue;

            int corner = Math.Clamp(Math.Min(bw, bh) / 4, 8, 24);
            if (!HasSparseCorners(binary, blob.Bounds, corner))
                continue;

            if (!HasRoundedRectEdgeProfile(binary, blob.Bounds, corner))
                continue;

            double radius = EstimateCornerRadius(binary, blob.Bounds, corner);
            if (rects.Any(existing => RectsSimilar(existing.Bounds, blob.Bounds)))
                continue;

            rects.Add(new RoundedRectCandidate(blob.Bounds, radius));
        }

        return rects;
    }

    private static bool HasSparseCorners(SKBitmap binary, SKRectI bounds, int corner)
    {
        double threshold = 0.90;
        return InkRatio(binary, bounds.Left, bounds.Top, bounds.Left + corner, bounds.Top + corner) < threshold &&
               InkRatio(binary, bounds.Right - corner, bounds.Top, bounds.Right, bounds.Top + corner) < threshold &&
               InkRatio(binary, bounds.Left, bounds.Bottom - corner, bounds.Left + corner, bounds.Bottom) < threshold &&
               InkRatio(binary, bounds.Right - corner, bounds.Bottom - corner, bounds.Right, bounds.Bottom) < threshold;
    }

    private static bool HasRoundedRectEdgeProfile(SKBitmap binary, SKRectI bounds, int corner)
    {
        int yNearTop = Math.Min(bounds.Bottom - 1, bounds.Top + Math.Max(2, corner / 3));
        int yMid = (bounds.Top + bounds.Bottom) / 2;
        int xMid = (bounds.Left + bounds.Right) / 2;

        double nearTopRun = LongestDarkRunRatioInRow(binary, bounds, yNearTop);
        double midRowRun = LongestDarkRunRatioInRow(binary, bounds, yMid);
        double midColRun = LongestDarkRunRatioInColumn(binary, bounds, xMid);

        return nearTopRun >= 0.55 &&
               midRowRun >= 0.90 &&
               midColRun >= 0.90;
    }

    private static double EstimateCornerRadius(SKBitmap binary, SKRectI bounds, int maxRadius)
    {
        int y = Math.Min(bounds.Bottom - 1, bounds.Top + 1);
        int firstDark = FirstDarkPixelInRow(binary, bounds, y);
        if (firstDark < 0)
            return maxRadius;

        return Math.Clamp(firstDark - bounds.Left, 1, maxRadius);
    }

    private static unsafe double InkRatio(SKBitmap binary, int left, int top, int right, int bottom)
    {
        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;
        int ink = 0;
        int total = 0;

        for (int y = Math.Max(0, top); y < Math.Min(binary.Height, bottom); y++)
        {
            for (int x = Math.Max(0, left); x < Math.Min(binary.Width, right); x++)
            {
                if (src[y * stride + x] == 0)
                    ink++;
                total++;
            }
        }

        return total == 0 ? 0 : (double)ink / total;
    }

    private static unsafe double LongestDarkRunRatioInRow(SKBitmap binary, SKRectI bounds, int y)
    {
        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;
        int best = 0;
        int current = 0;
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            if (src[y * stride + x] == 0)
            {
                current++;
                best = Math.Max(best, current);
            }
            else
            {
                current = 0;
            }
        }

        return best / (double)Math.Max(1, bounds.Width);
    }

    private static unsafe double LongestDarkRunRatioInColumn(SKBitmap binary, SKRectI bounds, int x)
    {
        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;
        int best = 0;
        int current = 0;
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            if (src[y * stride + x] == 0)
            {
                current++;
                best = Math.Max(best, current);
            }
            else
            {
                current = 0;
            }
        }

        return best / (double)Math.Max(1, bounds.Height);
    }

    private static unsafe int FirstDarkPixelInRow(SKBitmap binary, SKRectI bounds, int y)
    {
        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            if (src[y * stride + x] == 0)
                return x;
        }

        return -1;
    }

    // ── Complex icon cluster detection ───────────────────────────────────────

    public static List<SKRectI> FindIconClusters(SKBitmap binary)
    {
        int totalPixels = binary.Width * binary.Height;
        var clusters = new List<SKRectI>();

        foreach (var blob in TextEngine.LabelConnectedComponents(binary))
        {
            int bw = blob.Bounds.Width;
            int bh = blob.Bounds.Height;
            if (bw < MinIconClusterSize || bh < MinIconClusterSize)
                continue;
            if (bw > MaxIconClusterSize || bh > MaxIconClusterSize)
                continue;

            int boxArea = bw * bh;
            double inkCoverage = (double)blob.PixelCount / totalPixels;
            if (inkCoverage > 0.25)
                continue;

            double fillRatio = (double)blob.PixelCount / boxArea;
            if (fillRatio < 0.10 || fillRatio > 0.74)
                continue;

            double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspect > 4.0)
                continue;

            if (LooksLikeLineOrTextGlyph(blob, fillRatio))
                continue;

            clusters.Add(blob.Bounds);
        }

        return clusters;
    }

    public static List<SKRectI> FindImageClusters(SKBitmap binary)
    {
        int totalPixels = binary.Width * binary.Height;
        var clusters = new List<SKRectI>();

        foreach (var blob in TextEngine.LabelConnectedComponents(binary))
        {
            int bw = blob.Bounds.Width;
            int bh = blob.Bounds.Height;
            if (bw < MinImageClusterSize || bh < MinImageClusterSize)
                continue;
            if (bw <= MaxIconClusterSize && bh <= MaxIconClusterSize)
                continue;

            int boxArea = bw * bh;
            double inkCoverage = (double)blob.PixelCount / totalPixels;
            if (inkCoverage > 0.35)
                continue;

            double fillRatio = (double)blob.PixelCount / boxArea;
            if (fillRatio < 0.08 || fillRatio > 0.72)
                continue;
            if (LooksLikeFilledPanel(binary, blob.Bounds, fillRatio))
                continue;
            if (LooksLikeRectangularComponent(binary, blob.Bounds))
                continue;

            double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspect > 6.0)
                continue;

            if (LooksLikeLineOrTextGlyph(blob, fillRatio))
                continue;

            clusters.Add(blob.Bounds);
        }

        return clusters;
    }

    private static bool LooksLikeFilledPanel(SKBitmap binary, SKRectI bounds, double fillRatio)
    {
        if (fillRatio < 0.60)
            return false;

        int yMid = (bounds.Top + bounds.Bottom) / 2;
        int xMid = (bounds.Left + bounds.Right) / 2;
        double midRowRun = LongestDarkRunRatioInRow(binary, bounds, yMid);
        double midColRun = LongestDarkRunRatioInColumn(binary, bounds, xMid);

        int corner = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 5, 8, 28);
        double cornerInk =
            InkRatio(binary, bounds.Left, bounds.Top, bounds.Left + corner, bounds.Top + corner) +
            InkRatio(binary, bounds.Right - corner, bounds.Top, bounds.Right, bounds.Top + corner) +
            InkRatio(binary, bounds.Left, bounds.Bottom - corner, bounds.Left + corner, bounds.Bottom) +
            InkRatio(binary, bounds.Right - corner, bounds.Bottom - corner, bounds.Right, bounds.Bottom);

        return midRowRun >= 0.85 &&
               midColRun >= 0.85 &&
               cornerInk / 4.0 >= 0.65;
    }

    private static bool LooksLikeRectangularComponent(SKBitmap binary, SKRectI bounds)
    {
        int inset = Math.Clamp(Math.Min(bounds.Width, bounds.Height) / 24, 1, 6);
        int topY = Math.Min(bounds.Bottom - 1, bounds.Top + inset);
        int bottomY = Math.Max(bounds.Top, bounds.Bottom - 1 - inset);
        int leftX = Math.Min(bounds.Right - 1, bounds.Left + inset);
        int rightX = Math.Max(bounds.Left, bounds.Right - 1 - inset);

        double topRun = LongestDarkRunRatioInRow(binary, bounds, topY);
        double bottomRun = LongestDarkRunRatioInRow(binary, bounds, bottomY);
        double leftRun = LongestDarkRunRatioInColumn(binary, bounds, leftX);
        double rightRun = LongestDarkRunRatioInColumn(binary, bounds, rightX);

        return topRun >= 0.80 &&
               bottomRun >= 0.80 &&
               leftRun >= 0.80 &&
               rightRun >= 0.80;
    }

    private static bool LooksLikeLineOrTextGlyph(BlobInfo blob, double fillRatio)
    {
        int bw = blob.Bounds.Width;
        int bh = blob.Bounds.Height;
        double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);

        if (aspect > 3.0)
            return true;
        if (Math.Min(bw, bh) <= 22 && fillRatio < 0.30)
            return true;

        return false;
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

public sealed record RoundedRectCandidate(SKRectI Bounds, double Radius);
