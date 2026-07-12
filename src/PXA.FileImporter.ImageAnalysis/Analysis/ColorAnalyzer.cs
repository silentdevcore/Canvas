using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 2: analyses the colour content of a prepared image.
/// Produces the background colour, a dominant colour palette, and a list of
/// filled colour regions that will become PXA shape elements.
/// </summary>
public static class ColorAnalyzer
{
    /// <summary>Seed grid spacing in pixels for the flood-fill pass.</summary>
    private const int SeedSpacing = 20;

    /// <summary>Fine fallback scan spacing for foreground regions missed by the coarse seed grid.</summary>
    private const int AdaptiveSeedSpacing = 4;

    /// <summary>
    /// Maximum per-channel delta (0–255) for two colours to be considered "the same"
    /// when merging adjacent flood-fill regions.
    /// </summary>
    public const int ColorTolerance = 18;

    /// <summary>
    /// Minimum region area in pixels. Regions smaller than this are discarded as noise.
    /// </summary>
    public const int MinRegionPixels = 16;

    /// <summary>
    /// Regions covering more than this fraction of the image are treated as the
    /// background and not emitted as foreground elements.
    /// </summary>
    public const double BackgroundCoverageThreshold = 0.80;

    // ── Entry point ───────────────────────────────────────────────────────────

    public static ColorAnalysisResult Analyze(PreparedImage img)
    {
        SKColor background = DetectBackground(img.Original);
        var regions        = SegmentRegions(img.Original, background);
        var imageRegions   = DetectImageLikeRegions(img.Original, background, regions);
        var palette        = BuildPalette(img.Original);

        return new ColorAnalysisResult
        {
            Background     = background,
            DominantColors = palette,
            Regions        = regions.Concat(imageRegions).ToList(),
        };
    }

    // ── Background detection ─────────────────────────────────────────────────

    /// <summary>
    /// Samples every pixel in a 5-pixel border around the image and returns
    /// the most-frequent colour (majority vote bucketed into 32-level bins).
    /// </summary>
    public static SKColor DetectBackground(SKBitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        const int border = 5;

        var counts = new Dictionary<uint, int>();

        void Sample(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            uint key = QuantizeColor(bmp.GetPixel(x, y));
            counts.TryAdd(key, 0);
            counts[key]++;
        }

        for (int x = 0; x < w; x++)
            for (int b = 0; b < border; b++) { Sample(x, b); Sample(x, h - 1 - b); }

        for (int y = border; y < h - border; y++)
            for (int b = 0; b < border; b++) { Sample(b, y); Sample(w - 1 - b, y); }

        if (counts.Count == 0) return SKColors.White;

        uint dominant = counts.MaxBy(kv => kv.Value).Key;
        return UnquantizeColor(dominant);
    }

    // ── Dominant palette ──────────────────────────────────────────────────────

    /// <summary>
    /// Divides the image into a 16×16 grid, computes the median colour of each
    /// cell, then clusters into up to 8 hue bands.
    /// Returns the top colours sorted by coverage descending.
    /// </summary>
    public static IReadOnlyList<SKColor> BuildPalette(SKBitmap bmp)
    {
        const int gridSize = 16;
        int cellW = Math.Max(1, bmp.Width  / gridSize);
        int cellH = Math.Max(1, bmp.Height / gridSize);

        var buckets = new Dictionary<uint, int>();

        for (int gy = 0; gy < gridSize; gy++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                // Collect pixels for this cell
                int x0 = gx * cellW, y0 = gy * cellH;
                int x1 = Math.Min(x0 + cellW, bmp.Width);
                int y1 = Math.Min(y0 + cellH, bmp.Height);

                long rSum = 0, gSum = 0, bSum = 0;
                int  count = 0;
                for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                    {
                        var c = bmp.GetPixel(px, py);
                        rSum += c.Red; gSum += c.Green; bSum += c.Blue;
                        count++;
                    }

                if (count == 0) continue;
                var median = new SKColor((byte)(rSum / count), (byte)(gSum / count), (byte)(bSum / count));
                uint key   = QuantizeColor(median);
                buckets.TryAdd(key, 0);
                buckets[key]++;
            }
        }

        return buckets
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => UnquantizeColor(kv.Key))
            .ToList();
    }

    // ── Region segmentation ───────────────────────────────────────────────────

    /// <summary>
    /// Performs scanline flood-fill from coarse seed points, then a fine adaptive
    /// fallback scan for foreground regions that fall between grid points.
    /// Adjacent pixels within <see cref="ColorTolerance"/> of the seed colour are
    /// merged into the same region.  Small regions (noise) and regions covering
    /// most of the image (background) are filtered out.
    /// </summary>
    public static IReadOnlyList<ColorRegion> SegmentRegions(SKBitmap bmp, SKColor background)
    {
        int w = bmp.Width, h = bmp.Height;
        var visited = new bool[w, h];
        var regions = new List<ColorRegion>();
        int totalPixels = w * h;

        void TryAddRegionFromSeed(int sx, int sy, bool adaptive)
        {
            if (visited[sx, sy]) return;

            SKColor seedColor = bmp.GetPixel(sx, sy);

            // Skip seeds that are the background colour
            if (ColorDistance(seedColor, background) <= ColorTolerance) return;

            var pixels = FloodFill(bmp, visited, sx, sy, seedColor);
            if (pixels.Count < MinRegionPixels) return;

            double coverage = (double)pixels.Count / totalPixels;
            if (coverage > BackgroundCoverageThreshold) return;

            var bounds = ComputeBounds(pixels);
            double fillRatio = pixels.Count / (double)Math.Max(1, bounds.Width * bounds.Height);
            if (adaptive && (bounds.Width < 12 || bounds.Height < 12 || fillRatio < 0.75)) return;

            var avgColor = AverageColor(bmp, pixels);

            regions.Add(new ColorRegion
            {
                Bounds = bounds,
                FillColor = avgColor,
                Coverage = coverage,
                PixelCount = pixels.Count,
                SourceKind = adaptive ? "adaptive-seed" : "coarse-seed",
            });
        }

        for (int sy = SeedSpacing / 2; sy < h; sy += SeedSpacing)
        {
            for (int sx = SeedSpacing / 2; sx < w; sx += SeedSpacing)
            {
                TryAddRegionFromSeed(sx, sy, adaptive: false);
            }
        }

        for (int sy = AdaptiveSeedSpacing / 2; sy < h; sy += AdaptiveSeedSpacing)
        {
            for (int sx = AdaptiveSeedSpacing / 2; sx < w; sx += AdaptiveSeedSpacing)
            {
                TryAddRegionFromSeed(sx, sy, adaptive: true);
            }
        }

        return MergeAdjacentRegions(regions, totalPixels);
    }

    public static IReadOnlyList<ColorRegion> MergeAdjacentRegions(
        IReadOnlyList<ColorRegion> regions,
        int totalPixels)
    {
        var merged = regions.ToList();
        bool changed;

        do
        {
            changed = false;
            for (int i = 0; i < merged.Count && !changed; i++)
            {
                for (int j = i + 1; j < merged.Count; j++)
                {
                    if (!ShouldMergeRegions(merged[i], merged[j]))
                        continue;

                    merged[i] = MergeRegions(merged[i], merged[j], totalPixels);
                    merged.RemoveAt(j);
                    changed = true;
                    break;
                }
            }
        } while (changed);

        return merged;
    }

    public static IReadOnlyList<ColorRegion> DetectImageLikeRegions(
        SKBitmap bmp,
        SKColor background,
        IReadOnlyList<ColorRegion>? existingRegions = null)
    {
        int w = bmp.Width, h = bmp.Height;
        int totalPixels = w * h;
        var visited = new bool[w, h];
        var regions = new List<ColorRegion>();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (visited[x, y])
                    continue;

                if (ColorDistance(bmp.GetPixel(x, y), background) <= ColorTolerance)
                {
                    visited[x, y] = true;
                    continue;
                }

                var pixels = FloodFillForeground(bmp, visited, x, y, background);
                if (pixels.Count < Math.Max(1200, totalPixels / 200))
                    continue;

                var bounds = ComputeBounds(pixels);
                if (bounds.Width < 40 || bounds.Height < 40)
                    continue;

                double coverage = (double)pixels.Count / totalPixels;
                if (coverage > BackgroundCoverageThreshold)
                    continue;

                double fillRatio = pixels.Count / (double)Math.Max(1, bounds.Width * bounds.Height);
                if (fillRatio < 0.45)
                    continue;

                if (existingRegions is not null && existingRegions.Any(r => RectMostlyCovers(r.Bounds, bounds)))
                    continue;

                if (!HasImageLikeColorVariation(bmp, pixels))
                    continue;

                regions.Add(new ColorRegion
                {
                    Bounds = bounds,
                    FillColor = AverageColor(bmp, pixels),
                    Coverage = coverage,
                    PixelCount = pixels.Count,
                    AnalysisType = "image-region",
                    Confidence = 0.58,
                    SourceKind = "foreground-variation",
                });
            }
        }

        return regions;
    }

    // ── Flood fill (scanline) ─────────────────────────────────────────────────

    private static List<(int x, int y)> FloodFill(
        SKBitmap bmp, bool[,] visited, int startX, int startY, SKColor seedColor)
    {
        int w = bmp.Width, h = bmp.Height;
        var pixels = new List<(int, int)>();
        var stack  = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (cx < 0 || cy < 0 || cx >= w || cy >= h) continue;
            if (visited[cx, cy]) continue;

            SKColor c = bmp.GetPixel(cx, cy);
            if (ColorDistance(c, seedColor) > ColorTolerance) continue;

            visited[cx, cy] = true;
            pixels.Add((cx, cy));

            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }

        return pixels;
    }

    private static List<(int x, int y)> FloodFillForeground(
        SKBitmap bmp, bool[,] visited, int startX, int startY, SKColor background)
    {
        int w = bmp.Width, h = bmp.Height;
        var pixels = new List<(int, int)>();
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (cx < 0 || cy < 0 || cx >= w || cy >= h) continue;
            if (visited[cx, cy]) continue;

            visited[cx, cy] = true;
            if (ColorDistance(bmp.GetPixel(cx, cy), background) <= ColorTolerance)
                continue;

            pixels.Add((cx, cy));

            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }

        return pixels;
    }

    // ── Colour math ───────────────────────────────────────────────────────────

    /// <summary>Chebyshev (max-channel) distance between two colours.</summary>
    public static int ColorDistance(SKColor a, SKColor b) =>
        Math.Max(Math.Abs(a.Red   - b.Red),
        Math.Max(Math.Abs(a.Green - b.Green),
                 Math.Abs(a.Blue  - b.Blue)));

    /// <summary>
    /// Quantizes a colour to a compact uint key using 32-level bins per channel
    /// (5 bits each, packed as R5G5B5 in 15 bits).
    /// </summary>
    private static uint QuantizeColor(SKColor c) =>
        (uint)((c.Red >> 3) << 10 | (c.Green >> 3) << 5 | (c.Blue >> 3));

    private static SKColor UnquantizeColor(uint key) =>
        new((byte)(((key >> 10) & 0x1F) << 3),
            (byte)(((key >>  5) & 0x1F) << 3),
            (byte)(((key      ) & 0x1F) << 3));

    private static SKRectI ComputeBounds(List<(int x, int y)> pixels)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (px, py) in pixels)
        {
            if (px < minX) minX = px;
            if (py < minY) minY = py;
            if (px > maxX) maxX = px;
            if (py > maxY) maxY = py;
        }
        return new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }

    private static SKColor AverageColor(SKBitmap bmp, List<(int x, int y)> pixels)
    {
        long r = 0, g = 0, b = 0;
        foreach (var (px, py) in pixels)
        {
            var c = bmp.GetPixel(px, py);
            r += c.Red; g += c.Green; b += c.Blue;
        }
        int n = pixels.Count;
        return new SKColor((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    private static bool HasImageLikeColorVariation(SKBitmap bmp, List<(int x, int y)> pixels)
    {
        byte minR = 255, minG = 255, minB = 255;
        byte maxR = 0, maxG = 0, maxB = 0;
        int step = Math.Max(1, pixels.Count / 2000);

        for (int i = 0; i < pixels.Count; i += step)
        {
            var (x, y) = pixels[i];
            var c = bmp.GetPixel(x, y);
            minR = Math.Min(minR, c.Red);
            minG = Math.Min(minG, c.Green);
            minB = Math.Min(minB, c.Blue);
            maxR = Math.Max(maxR, c.Red);
            maxG = Math.Max(maxG, c.Green);
            maxB = Math.Max(maxB, c.Blue);
        }

        int strongestRange = Math.Max(maxR - minR, Math.Max(maxG - minG, maxB - minB));
        int combinedRange = (maxR - minR) + (maxG - minG) + (maxB - minB);
        return strongestRange >= 48 && combinedRange >= 80;
    }

    private static bool RectMostlyCovers(SKRectI covering, SKRectI target)
    {
        double targetArea = Math.Max(1, target.Width * target.Height);
        int ox = Math.Max(0, Math.Min(covering.Right, target.Right) - Math.Max(covering.Left, target.Left));
        int oy = Math.Max(0, Math.Min(covering.Bottom, target.Bottom) - Math.Max(covering.Top, target.Top));
        double overlap = ox * oy;
        return overlap / targetArea >= 0.85;
    }

    private static bool ShouldMergeRegions(ColorRegion a, ColorRegion b)
    {
        if (a.AnalysisType != b.AnalysisType)
            return false;
        if (ColorDistance(a.FillColor, b.FillColor) > ColorTolerance)
            return false;

        return RectsTouchOrOverlap(a.Bounds, b.Bounds);
    }

    private static ColorRegion MergeRegions(ColorRegion a, ColorRegion b, int totalPixels)
    {
        int pixelCount = a.PixelCount + b.PixelCount;
        var bounds = new SKRectI(
            Math.Min(a.Bounds.Left, b.Bounds.Left),
            Math.Min(a.Bounds.Top, b.Bounds.Top),
            Math.Max(a.Bounds.Right, b.Bounds.Right),
            Math.Max(a.Bounds.Bottom, b.Bounds.Bottom));

        return new ColorRegion
        {
            Bounds = bounds,
            FillColor = WeightedAverage(a.FillColor, a.PixelCount, b.FillColor, b.PixelCount),
            Coverage = pixelCount / (double)Math.Max(1, totalPixels),
            PixelCount = pixelCount,
            AnalysisType = a.AnalysisType,
            Confidence = Math.Min(a.Confidence, b.Confidence),
            SourceKind = "merged-color-region",
        };
    }

    private static bool RectsTouchOrOverlap(SKRectI a, SKRectI b)
    {
        bool xTouches = a.Right >= b.Left && b.Right >= a.Left;
        bool yTouches = a.Bottom >= b.Top && b.Bottom >= a.Top;
        return xTouches && yTouches;
    }

    private static SKColor WeightedAverage(SKColor a, int aWeight, SKColor b, int bWeight)
    {
        int total = Math.Max(1, aWeight + bWeight);
        return new SKColor(
            (byte)((a.Red * aWeight + b.Red * bWeight) / total),
            (byte)((a.Green * aWeight + b.Green * bWeight) / total),
            (byte)((a.Blue * aWeight + b.Blue * bWeight) / total));
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

public sealed class ColorAnalysisResult
{
    public required SKColor Background { get; init; }
    public required IReadOnlyList<SKColor> DominantColors { get; init; }
    public required IReadOnlyList<ColorRegion> Regions { get; init; }
}

public sealed class ColorRegion
{
    public required SKRectI Bounds    { get; init; }
    public required SKColor FillColor { get; init; }
    public required double  Coverage  { get; init; }
    public required int     PixelCount { get; init; }
    public string? AnalysisType { get; init; }
    public double Confidence { get; init; } = 0.90;
    public string? SourceKind { get; init; }
}
