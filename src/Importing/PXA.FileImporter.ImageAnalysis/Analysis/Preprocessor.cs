using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 1: normalises the input bitmap, produces grayscale and binarised
/// variants used by all subsequent analysis phases.
/// </summary>
public static class Preprocessor
{
    /// <summary>
    /// Maximum dimension (width or height) for the working-resolution bitmaps.
    /// Images larger than this are scaled down proportionally before analysis;
    /// smaller images are kept at their original size.
    /// </summary>
    public const int MaxWorkingDimension = 2400;

    /// <summary>
    /// Prepares an image for analysis.
    /// The caller is responsible for disposing the returned <see cref="PreparedImage"/>.
    /// </summary>
    public static PreparedImage Prepare(SKBitmap source)
    {
        // ── 1. Scale to working resolution ───────────────────────────────────
        var (scaled, scaleFactor) = ScaleToWorkingResolution(source);

        // ── 2. Grayscale ──────────────────────────────────────────────────────
        var grayscale = ToGrayscale(scaled);

        // ── 3. Binarise via Otsu's method, with a conservative adaptive
        // fallback for uneven backgrounds where global Otsu over-selects.
        int threshold = ComputeOtsuThreshold(grayscale);
        var binary    = Binarise(grayscale, threshold);
        if (ShouldUseAdaptiveThreshold(scaled, grayscale, binary))
        {
            binary.Dispose();
            binary = BinariseAdaptive(grayscale);
        }
        DespeckleInPlace(binary);

        return new PreparedImage
        {
            Original    = scaled,
            Grayscale   = grayscale,
            Binary      = binary,
            ScaleFactor = scaleFactor,
        };
    }

    // ── Scaling ───────────────────────────────────────────────────────────────

    private static (SKBitmap bitmap, double scaleFactor) ScaleToWorkingResolution(SKBitmap source)
    {
        int maxDim = Math.Max(source.Width, source.Height);
        if (maxDim <= MaxWorkingDimension)
        {
            // Clone so the PreparedImage owns its bitmap independently of the source.
            return (source.Copy(), 1.0);
        }

        double factor = (double)MaxWorkingDimension / maxDim;
        int newW = (int)Math.Round(source.Width  * factor);
        int newH = (int)Math.Round(source.Height * factor);

        var info   = new SKImageInfo(newW, newH, SKColorType.Rgba8888, SKAlphaType.Premul);
        var scaled = new SKBitmap(info);
        using var canvas = new SKCanvas(scaled);
        using var paint  = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawBitmap(source, new SKRect(0, 0, newW, newH), paint);

        return (scaled, factor);
    }

    // ── Grayscale conversion ─────────────────────────────────────────────────

    private static SKBitmap ToGrayscale(SKBitmap source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var gray = new SKBitmap(info);

        using var canvas = new SKCanvas(gray);

        // Luminance-weighted greyscale matrix  R=0.2126  G=0.7152  B=0.0722
        float[] matrix =
        [
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0,       0,       0,       1, 0,
        ];

        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        using var paint  = new SKPaint { ColorFilter = filter };
        canvas.DrawBitmap(source, 0, 0, paint);

        return gray;
    }

    // ── Otsu's global threshold ──────────────────────────────────────────────

    /// <summary>
    /// Computes the optimal binarisation threshold using Otsu's method on the
    /// grayscale histogram. Returns a value in [0, 255].
    /// </summary>
    public static int ComputeOtsuThreshold(SKBitmap gray)
    {
        // Build 256-bin intensity histogram
        var hist = new long[256];
        unsafe
        {
            byte* ptr = (byte*)gray.GetPixels().ToPointer();
            int   len = gray.Width * gray.Height;

            // Gray8 bitmaps store one byte per pixel
            int stride = gray.RowBytes;
            for (int y = 0; y < gray.Height; y++)
            {
                byte* row = ptr + y * stride;
                for (int x = 0; x < gray.Width; x++)
                    hist[row[x]]++;
            }
        }

        long total = gray.Width * (long)gray.Height;

        // Find the occupied intensity range
        int firstBin = 0, lastBin = 255;
        for (int i = 0;   i < 256; i++) if (hist[i] > 0) { firstBin = i; break; }
        for (int i = 255; i >= 0;  i--) if (hist[i] > 0) { lastBin  = i; break; }

        // Uniform image — no meaningful variance to maximise.
        // Return a threshold that preserves appearance:
        //   uniform dark → threshold=0 (pixels ≤ 0 = only value 0 → black)
        //   uniform light → threshold=lastBin-1 (pixels > lastBin-1 = white)
        if (firstBin == lastBin)
            return firstBin == 0 ? 0 : firstBin - 1;

        // Sum of all intensities
        long sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += i * hist[i];

        long sumBg  = 0;
        long cntBg  = 0;
        double maxVariance = 0;
        int bestThreshold  = 128;

        for (int t = 0; t < 256; t++)
        {
            cntBg  += hist[t];
            if (cntBg == 0) continue;

            long cntFg = total - cntBg;
            if (cntFg == 0) break;

            sumBg += t * hist[t];

            double meanBg = (double)sumBg / cntBg;
            double meanFg = (double)(sumAll - sumBg) / cntFg;
            double diff   = meanBg - meanFg;

            double variance = (double)cntBg * cntFg * diff * diff;
            if (variance > maxVariance)
            {
                maxVariance    = variance;
                bestThreshold  = t;
            }
        }

        return bestThreshold;
    }

    // ── Binarisation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a Gray8 bitmap to a binary (black/white) Gray8 bitmap.
    /// Pixels ≤ <paramref name="threshold"/> become black (0); others become white (255).
    /// </summary>
    private static SKBitmap Binarise(SKBitmap gray, int threshold)
    {
        var info   = new SKImageInfo(gray.Width, gray.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var binary = new SKBitmap(info);

        unsafe
        {
            byte* src    = (byte*)gray.GetPixels().ToPointer();
            byte* dst    = (byte*)binary.GetPixels().ToPointer();
            int   srcStride = gray.RowBytes;
            int   dstStride = binary.RowBytes;

            for (int y = 0; y < gray.Height; y++)
            {
                byte* srcRow = src + y * srcStride;
                byte* dstRow = dst + y * dstStride;
                for (int x = 0; x < gray.Width; x++)
                    dstRow[x] = srcRow[x] <= threshold ? (byte)0 : (byte)255;
            }
        }

        return binary;
    }

    private static bool ShouldUseAdaptiveThreshold(SKBitmap original, SKBitmap gray, SKBitmap binary)
    {
        if (!IsMostlyNeutral(original))
            return false;

        var (min, max) = ComputeIntensityRange(gray);
        if (max - min < 40)
            return false;

        double blackRatio = CountBlackPixels(binary) / (double)(binary.Width * binary.Height);
        return blackRatio > 0.35;
    }

    private static bool IsMostlyNeutral(SKBitmap bitmap)
    {
        long chroma = 0;
        int count = 0;
        int step = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 200);

        for (int y = 0; y < bitmap.Height; y += step)
        {
            for (int x = 0; x < bitmap.Width; x += step)
            {
                var c = bitmap.GetPixel(x, y);
                int max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
                int min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                chroma += max - min;
                count++;
            }
        }

        return count == 0 || (double)chroma / count <= 8.0;
    }

    private static unsafe (int min, int max) ComputeIntensityRange(SKBitmap gray)
    {
        int min = 255;
        int max = 0;
        byte* ptr = (byte*)gray.GetPixels().ToPointer();
        int stride = gray.RowBytes;

        for (int y = 0; y < gray.Height; y++)
        {
            byte* row = ptr + y * stride;
            for (int x = 0; x < gray.Width; x++)
            {
                byte v = row[x];
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }

        return (min, max);
    }

    private static unsafe int CountBlackPixels(SKBitmap binary)
    {
        int count = 0;
        byte* ptr = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;

        for (int y = 0; y < binary.Height; y++)
        {
            byte* row = ptr + y * stride;
            for (int x = 0; x < binary.Width; x++)
                if (row[x] == 0) count++;
        }

        return count;
    }

    private static unsafe SKBitmap BinariseAdaptive(SKBitmap gray)
    {
        const int radius = 15;
        const int offset = 8;

        int w = gray.Width;
        int h = gray.Height;
        var integral = new long[(w + 1) * (h + 1)];

        byte* src = (byte*)gray.GetPixels().ToPointer();
        int srcStride = gray.RowBytes;

        for (int y = 1; y <= h; y++)
        {
            long rowSum = 0;
            byte* row = src + (y - 1) * srcStride;
            for (int x = 1; x <= w; x++)
            {
                rowSum += row[x - 1];
                integral[y * (w + 1) + x] = integral[(y - 1) * (w + 1) + x] + rowSum;
            }
        }

        var info = new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
        var binary = new SKBitmap(info);
        byte* dst = (byte*)binary.GetPixels().ToPointer();
        int dstStride = binary.RowBytes;

        for (int y = 0; y < h; y++)
        {
            int top = Math.Max(0, y - radius);
            int bottom = Math.Min(h - 1, y + radius);
            byte* srcRow = src + y * srcStride;
            byte* dstRow = dst + y * dstStride;

            for (int x = 0; x < w; x++)
            {
                int left = Math.Max(0, x - radius);
                int right = Math.Min(w - 1, x + radius);
                int x0 = left;
                int y0 = top;
                int x1 = right + 1;
                int y1 = bottom + 1;
                long sum = integral[y1 * (w + 1) + x1]
                    - integral[y0 * (w + 1) + x1]
                    - integral[y1 * (w + 1) + x0]
                    + integral[y0 * (w + 1) + x0];
                int area = (x1 - x0) * (y1 - y0);
                double localMean = (double)sum / area;

                dstRow[x] = srcRow[x] + offset < localMean ? (byte)0 : (byte)255;
            }
        }

        return binary;
    }

    private static unsafe void DespeckleInPlace(SKBitmap binary)
    {
        int w = binary.Width;
        int h = binary.Height;
        var remove = new List<int>();

        byte* ptr = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;

        for (int y = 0; y < h; y++)
        {
            byte* row = ptr + y * stride;
            for (int x = 0; x < w; x++)
            {
                if (row[x] != 0)
                    continue;

                if (CountBlackNeighbours(ptr, stride, w, h, x, y) == 0)
                    remove.Add(y * w + x);
            }
        }

        foreach (int index in remove)
        {
            int y = index / w;
            int x = index % w;
            ptr[y * stride + x] = 255;
        }

        RemoveTinyThinComponents(ptr, stride, w, h);
    }

    private static unsafe void RemoveTinyThinComponents(byte* ptr, int stride, int w, int h)
    {
        var visited = new bool[w * h];
        var remove = new List<int>();
        var queue = new Queue<(int x, int y)>();
        var component = new List<(int x, int y)>();

        for (int y = 0; y < h; y++)
        {
            byte* row = ptr + y * stride;
            for (int x = 0; x < w; x++)
            {
                int startIndex = y * w + x;
                if (visited[startIndex] || row[x] != 0)
                    continue;

                component.Clear();
                queue.Clear();
                queue.Enqueue((x, y));
                visited[startIndex] = true;
                int minX = x, maxX = x, minY = y, maxY = y;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    component.Add((cx, cy));
                    minX = Math.Min(minX, cx);
                    maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy);
                    maxY = Math.Max(maxY, cy);

                    for (int yy = Math.Max(0, cy - 1); yy <= Math.Min(h - 1, cy + 1); yy++)
                    {
                        byte* nrow = ptr + yy * stride;
                        for (int xx = Math.Max(0, cx - 1); xx <= Math.Min(w - 1, cx + 1); xx++)
                        {
                            int index = yy * w + xx;
                            if (visited[index] || nrow[xx] != 0)
                                continue;

                            visited[index] = true;
                            queue.Enqueue((xx, yy));
                        }
                    }
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (component.Count <= 3 && (width == 1 || height == 1))
                    remove.AddRange(component.Select(p => p.y * w + p.x));
            }
        }

        foreach (int index in remove)
        {
            int y = index / w;
            int x = index % w;
            ptr[y * stride + x] = 255;
        }
    }

    private static unsafe int CountBlackNeighbours(byte* ptr, int stride, int w, int h, int x, int y)
    {
        int count = 0;
        for (int yy = Math.Max(0, y - 1); yy <= Math.Min(h - 1, y + 1); yy++)
        {
            byte* row = ptr + yy * stride;
            for (int xx = Math.Max(0, x - 1); xx <= Math.Min(w - 1, x + 1); xx++)
            {
                if (xx == x && yy == y)
                    continue;
                if (row[xx] == 0)
                    count++;
            }
        }

        return count;
    }
}
