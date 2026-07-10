using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Templates;

/// <summary>
/// Built-in character template atlas for NCC-based character recognition.
/// Templates are 32×32 normalised float arrays (0=black, 1=white).
/// They are generated at first access by rendering printable ASCII characters
/// with SkiaSharp across a small set of system sans/serif/monospace font families.
/// </summary>
public static class CharacterTemplates
{
    private const int TemplateSize = 32;
    private const double NonPrimaryVariantScorePenalty = 0.05;
    public const string ProfileId = "builtin-basic-latin-font-atlas-v1";

    // Printable ASCII range 32–126 (space through tilde)
    private static readonly char[] Chars = Enumerable
        .Range(32, 95)
        .Select(i => (char)i)
        .ToArray();

    private static readonly Lazy<Dictionary<char, IReadOnlyList<float[]>>> _atlas =
        new(BuildAtlas, isThreadSafe: true);

    private sealed record FontSpec(string Family, SKFontStyleWeight Weight);

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
            .Select(kv => (
                ch: kv.Key,
                score: BestAdjustedScore(kv.Key, patch, kv.Value)))
            .OrderByDescending(m => m.score)
            .Take(Math.Max(1, count))
            .ToList();
    }

    public static bool TryGetTemplate(char ch, out float[] template)
    {
        if (_atlas.Value.TryGetValue(ch, out var templates) && templates.Count > 0)
        {
            template = templates[0];
            return true;
        }

        template = [];
        return false;
    }

    public static bool TryGetBestTemplate(char ch, float[] patch, out float[] template, out double score)
    {
        template = [];
        score = 0;

        if (!_atlas.Value.TryGetValue(ch, out var templates) || templates.Count == 0)
            return false;

        foreach (var candidate in templates)
        {
            double candidateScore = NormalizedCrossCorrelation(patch, candidate);
            if (candidateScore <= score)
                continue;

            template = candidate;
            score = candidateScore;
        }

        return template.Length > 0;
    }

    // ── Atlas generation ──────────────────────────────────────────────────────

    private static Dictionary<char, IReadOnlyList<float[]>> BuildAtlas()
    {
        var atlas = new Dictionary<char, IReadOnlyList<float[]>>(Chars.Length);

        // Use SkiaSharp to render each character at multiple font sizes and take
        // the one that fills the 32×32 template best.
        float[] fontSizes  = [12f, 16f, 20f, 24f];
        FontSpec[] fontSpecs =
        [
            new("Courier New", SKFontStyleWeight.Normal),
            new("Monospace", SKFontStyleWeight.Normal),
            new("Arial", SKFontStyleWeight.Normal),
            new("Helvetica", SKFontStyleWeight.Normal),
            new("sans-serif", SKFontStyleWeight.Normal),
            new("Times New Roman", SKFontStyleWeight.Normal),
            new("Times", SKFontStyleWeight.Normal),
            new("Georgia", SKFontStyleWeight.Normal),
            new("serif", SKFontStyleWeight.Normal),
            new("Courier New", SKFontStyleWeight.Bold),
            new("Arial", SKFontStyleWeight.Bold),
            new("Helvetica", SKFontStyleWeight.Bold),
            new("Times New Roman", SKFontStyleWeight.Bold),
            new("Times", SKFontStyleWeight.Bold),
            new("Georgia", SKFontStyleWeight.Bold),
        ];

        foreach (char ch in Chars)
        {
            if (ch == ' ')
            {
                // Space → all-white template
                var space = new float[TemplateSize * TemplateSize];
                Array.Fill(space, 1f);
                atlas[ch] = [space];
                continue;
            }

            var variants = new List<float[]>();

            foreach (var fontSpec in fontSpecs)
            {
                float[] best = RenderChar(ch, fontSpec, fontSizes[^1]);
                double bestFill = FillRatio(best);

                foreach (float sz in fontSizes)
                {
                    float[] candidate = RenderChar(ch, fontSpec, sz);
                    double fill = FillRatio(candidate);
                    // Prefer the size that has the most ink without clipping
                    if (fill > bestFill && fill < 0.95)
                    {
                        best = candidate;
                        bestFill = fill;
                    }
                }

                if (!variants.Any(existing => NormalizedCrossCorrelation(existing, best) > 0.995))
                    variants.Add(best);
            }

            atlas[ch] = variants;
        }

        return atlas;
    }

    private static double BestAdjustedScore(char ch, float[] patch, IReadOnlyList<float[]> templates)
    {
        double best = double.NegativeInfinity;
        for (int i = 0; i < templates.Count; i++)
        {
            double score = NormalizedCrossCorrelation(patch, templates[i]);
            if (i > 0)
                score -= VariantScorePenalty(ch);

            best = Math.Max(best, score);
        }

        return best;
    }

    private static double VariantScorePenalty(char ch) =>
        "lI1|Tt".Contains(ch) ? 0.10 : NonPrimaryVariantScorePenalty;

    private static float[] RenderChar(char ch, FontSpec fontSpec, float fontSize)
    {
        const int scratchSize = 64;
        var info   = new SKImageInfo(scratchSize, scratchSize,
                                     SKColorType.Gray8, SKAlphaType.Opaque);
        using var bmp    = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var font = new SKFont(
            SKTypeface.FromFamilyName(fontSpec.Family, fontSpec.Weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            fontSize);
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
