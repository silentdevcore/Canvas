using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Templates;

/// <summary>
/// Built-in character template atlas for NCC-based character recognition.
/// Templates are 32×32 normalised float arrays (0=black, 1=white).
/// They are generated at first access by rendering printable ASCII characters
/// with SkiaSharp using the system's default monospace fallback font.
/// </summary>
public static class CharacterTemplates
{
    private const int TemplateSize = 32;

    // Printable ASCII range 32–126 (space through tilde)
    private static readonly char[] Chars = Enumerable
        .Range(32, 95)
        .Select(i => (char)i)
        .ToArray();

    private static readonly Lazy<Dictionary<char, float[]>> _atlas =
        new(BuildAtlas, isThreadSafe: true);

    /// <summary>
    /// Finds the best matching character for the given 32×32 normalised float patch.
    /// Returns the character and its NCC score (0–1).
    /// </summary>
    public static (char ch, double score) Match(float[] patch)
    {
        var best = MatchTop(patch, 1).FirstOrDefault();
        return best == default ? ('?', 0) : best;
    }

    public static IReadOnlyList<(char ch, double score)> MatchTop(float[] patch, int count)
    {
        var atlas = _atlas.Value;

        return atlas
            .Select(kv => (ch: kv.Key, score: NormalizedCrossCorrelation(patch, kv.Value)))
            .OrderByDescending(m => m.score)
            .Take(Math.Max(1, count))
            .ToList();
    }

    public static bool TryGetTemplate(char ch, out float[] template) =>
        _atlas.Value.TryGetValue(ch, out template!);

    // ── Atlas generation ──────────────────────────────────────────────────────

    private static Dictionary<char, float[]> BuildAtlas()
    {
        var atlas = new Dictionary<char, float[]>(Chars.Length);

        // Use SkiaSharp to render each character at multiple font sizes and take
        // the one that fills the 32×32 template best.
        float[] fontSizes  = [12f, 16f, 20f, 24f];
        string[] fontNames = ["Courier New", "Monospace", "Arial", "Helvetica", "sans-serif"];

        string fontFamily = fontNames[0]; // prefer monospace for consistent widths

        foreach (char ch in Chars)
        {
            if (ch == ' ')
            {
                // Space → all-white template
                var space = new float[TemplateSize * TemplateSize];
                Array.Fill(space, 1f);
                atlas[ch] = space;
                continue;
            }

            float[] best     = RenderChar(ch, fontFamily, fontSizes[^1]);
            double  bestFill = FillRatio(best);

            foreach (float sz in fontSizes)
            {
                float[] candidate = RenderChar(ch, fontFamily, sz);
                double  fill      = FillRatio(candidate);
                // Prefer the size that has the most ink without clipping
                if (fill > bestFill && fill < 0.95)
                {
                    best     = candidate;
                    bestFill = fill;
                }
            }

            atlas[ch] = best;
        }

        return atlas;
    }

    private static float[] RenderChar(char ch, string fontFamily, float fontSize)
    {
        const int scratchSize = 64;
        var info   = new SKImageInfo(scratchSize, scratchSize,
                                     SKColorType.Gray8, SKAlphaType.Opaque);
        using var bmp    = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var font = new SKFont(SKTypeface.FromFamilyName(fontFamily), fontSize);
        using var paint = new SKPaint
        {
            Color        = SKColors.Black,
            IsAntialias  = false,
        };

        // Measure text to centre it
        float textWidth  = font.MeasureText(ch.ToString());
        float textHeight = fontSize;
        float x = (scratchSize - textWidth)  / 2f;
        float y = (scratchSize + textHeight * 0.75f) / 2f; // approximate baseline offset

        canvas.DrawText(ch.ToString(), x, y, font, paint);

        return NormalizeInkBounds(bmp);
    }

    // ── NCC ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalised cross-correlation between two float arrays of equal length.
    /// Returns a value in [–1, 1]; 1 = perfect match.
    /// </summary>
    public static double NormalizedCrossCorrelation(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        int n = a.Length;

        double meanA = 0, meanB = 0;
        for (int i = 0; i < n; i++) { meanA += a[i]; meanB += b[i]; }
        meanA /= n; meanB /= n;

        double num = 0, denA = 0, denB = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - meanA;
            double db = b[i] - meanB;
            num  += da * db;
            denA += da * da;
            denB += db * db;
        }

        double denom = Math.Sqrt(denA * denB);
        return denom < 1e-9 ? 0 : num / denom;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static unsafe float[] BitmapToFloatArray(SKBitmap bmp)
    {
        int n      = TemplateSize * TemplateSize;
        var result = new float[n];
        byte* ptr  = (byte*)bmp.GetPixels().ToPointer();
        int stride = bmp.RowBytes;
        for (int y = 0; y < TemplateSize; y++)
            for (int x = 0; x < TemplateSize; x++)
                result[y * TemplateSize + x] = ptr[y * stride + x] / 255f;
        return result;
    }

    private static unsafe float[] NormalizeInkBounds(SKBitmap bmp)
    {
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        int stride = bmp.RowBytes;
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                if (ptr[y * stride + x] >= 128) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (minX == int.MaxValue)
            return BitmapToFloatArray(bmp);

        var result = new float[TemplateSize * TemplateSize];
        Array.Fill(result, 1f);

        const int padding = 2;
        int bw = maxX - minX + 1;
        int bh = maxY - minY + 1;
        double scale = Math.Min((double)(TemplateSize - padding * 2) / bw,
                                (double)(TemplateSize - padding * 2) / bh);
        int dw = Math.Max(1, (int)Math.Round(bw * scale));
        int dh = Math.Max(1, (int)Math.Round(bh * scale));
        int dx0 = (TemplateSize - dw) / 2;
        int dy0 = (TemplateSize - dh) / 2;

        for (int dy = 0; dy < dh; dy++)
        {
            int sy = Math.Clamp(minY + (int)Math.Floor(dy / scale), 0, bmp.Height - 1);
            for (int dx = 0; dx < dw; dx++)
            {
                int sx = Math.Clamp(minX + (int)Math.Floor(dx / scale), 0, bmp.Width - 1);
                result[(dy0 + dy) * TemplateSize + dx0 + dx] = ptr[sy * stride + sx] / 255f;
            }
        }

        return result;
    }

    private static double FillRatio(float[] template)
    {
        // Fraction of pixels that are ink (luminance < 0.5)
        return template.Count(v => v < 0.5) / (double)template.Length;
    }
}
