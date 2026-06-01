using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 2: analyses the colour content of a prepared image.
/// Produces the background colour, a dominant colour palette, and a list of
/// filled colour regions that will become Canvas shape elements.
/// </summary>
public static class ColorAnalyzer
{
    /// <summary>Seed grid spacing in pixels for the flood-fill pass.</summary>
    private const int SeedSpacing = 20;

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
        var palette        = BuildPalette(img.Original);

        return new ColorAnalysisResult
        {
            Background     = background,
            DominantColors = palette,
            Regions        = regions,
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
    /// Performs a scanline flood-fill from seed points placed on a regular grid.
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

        for (int sy = SeedSpacing / 2; sy < h; sy += SeedSpacing)
        {
            for (int sx = SeedSpacing / 2; sx < w; sx += SeedSpacing)
            {
                if (visited[sx, sy]) continue;

                SKColor seedColor = bmp.GetPixel(sx, sy);

                // Skip seeds that are the background colour
                if (ColorDistance(seedColor, background) <= ColorTolerance) continue;

                var pixels = FloodFill(bmp, visited, sx, sy, seedColor);
                if (pixels.Count < MinRegionPixels) continue;

                double coverage = (double)pixels.Count / totalPixels;
                if (coverage > BackgroundCoverageThreshold) continue;

                var bounds = ComputeBounds(pixels);
                var avgColor = AverageColor(bmp, pixels);

                regions.Add(new ColorRegion
                {
                    Bounds    = bounds,
                    FillColor = avgColor,
                    Coverage  = coverage,
                    PixelCount = pixels.Count,
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
}
