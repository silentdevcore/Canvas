using Canvas.FileImporter.ImageAnalysis.Templates;
using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// First-pass glyph recognizer for clean, printed text. It normalizes a connected
/// component into a 32x32 patch and compares it against the built-in template atlas.
/// </summary>
public static class GlyphRecognizer
{
    private const int PatchSize = 32;
    private const int Padding = 2;
    private const double MinimumConfidence = 0.45;

    public static RecognizedChar Recognize(SKBitmap binary, BlobInfo blob)
    {
        var patch = ExtractPatch(binary, blob.Bounds);
        var matches = CharacterTemplates.MatchTop(patch, 8);
        var (ch, score) = SelectCandidate(matches, patch, blob.Bounds);
        bool resolved = score >= MinimumConfidence;

        return new RecognizedChar
        {
            Value = resolved ? ch : '?',
            Bounds = blob.Bounds,
            Confidence = resolved ? Math.Max(0, score) : 0,
        };
    }

    private static (char ch, double score) SelectCandidate(
        IReadOnlyList<(char ch, double score)> matches,
        float[] patch,
        SKRectI bounds)
    {
        if (matches.Count == 0) return ('?', 0);

        var best = matches[0];
        var bestAlnum = matches.FirstOrDefault(m => char.IsLetterOrDigit(m.ch));

        if (LooksLikeDot(bounds))
            return ('.', best.score);
        if (LooksLikeDash(bounds, patch))
            return ('-', best.score);
        if (LooksLikeSlash(bounds, patch))
            return ('/', best.score);

        bool looksLikeFullHeightGlyph = bounds.Height >= 8 && bounds.Width >= 3;
        if (!char.IsLetterOrDigit(best.ch) &&
            looksLikeFullHeightGlyph &&
            bestAlnum != default &&
            bestAlnum.score >= best.score - 0.08)
            best = bestAlnum;

        if (best.ch == '3' && LooksLikeFive(patch))
            return ('5', best.score);

        return best;
    }

    private static bool LooksLikeDot(SKRectI bounds) =>
        bounds.Width <= 6 && bounds.Height <= 6;

    private static bool LooksLikeDash(SKRectI bounds, float[] patch)
    {
        if (bounds.Width < 4 || bounds.Height > 5) return false;

        double middleInk = InkDensity(patch, 3, 12, 29, 20);
        double topInk = InkDensity(patch, 3, 0, 29, 10);
        double bottomInk = InkDensity(patch, 3, 22, 29, 32);
        return middleInk > 0.08 && middleInk > topInk * 2 && middleInk > bottomInk * 2;
    }

    private static bool LooksLikeSlash(SKRectI bounds, float[] patch)
    {
        if (bounds.Height < 8 || bounds.Width < 3) return false;

        int slashHits = 0;
        int backslashHits = 0;
        int ink = 0;

        for (int y = 0; y < PatchSize; y++)
        {
            for (int x = 0; x < PatchSize; x++)
            {
                if (patch[y * PatchSize + x] >= 0.5f) continue;
                ink++;

                double slashX = PatchSize - 1 - y;
                double backslashX = y;
                if (Math.Abs(x - slashX) <= 4) slashHits++;
                if (Math.Abs(x - backslashX) <= 4) backslashHits++;
            }
        }

        return ink > 0 &&
               (double)slashHits / ink > 0.45 &&
               slashHits > backslashHits * 1.4;
    }

    private static bool LooksLikeFive(float[] patch)
    {
        double upperLeft = InkDensity(patch, 0, 4, 13, 16);
        double upperRight = InkDensity(patch, 19, 4, 32, 16);
        double lowerLeft = InkDensity(patch, 0, 17, 13, 29);
        double lowerRight = InkDensity(patch, 19, 17, 32, 29);

        return upperLeft > upperRight * 1.25 && lowerRight >= lowerLeft;
    }

    private static double InkDensity(float[] patch, int x0, int y0, int x1, int y1)
    {
        int ink = 0;
        int total = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (patch[y * PatchSize + x] < 0.5f) ink++;
                total++;
            }
        }
        return total == 0 ? 0 : (double)ink / total;
    }

    private static unsafe float[] ExtractPatch(SKBitmap binary, SKRectI bounds)
    {
        var patch = new float[PatchSize * PatchSize];
        Array.Fill(patch, 1f);

        int bw = Math.Max(1, bounds.Width);
        int bh = Math.Max(1, bounds.Height);
        int targetW = PatchSize - Padding * 2;
        int targetH = PatchSize - Padding * 2;
        double scale = Math.Min((double)targetW / bw, (double)targetH / bh);
        int dw = Math.Max(1, (int)Math.Round(bw * scale));
        int dh = Math.Max(1, (int)Math.Round(bh * scale));
        int dx0 = (PatchSize - dw) / 2;
        int dy0 = (PatchSize - dh) / 2;

        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;

        for (int dy = 0; dy < dh; dy++)
        {
            int sy = Math.Clamp(bounds.Top + (int)Math.Floor(dy / scale), 0, binary.Height - 1);
            for (int dx = 0; dx < dw; dx++)
            {
                int sx = Math.Clamp(bounds.Left + (int)Math.Floor(dx / scale), 0, binary.Width - 1);
                patch[(dy0 + dy) * PatchSize + dx0 + dx] = src[sy * stride + sx] / 255f;
            }
        }

        return patch;
    }
}
