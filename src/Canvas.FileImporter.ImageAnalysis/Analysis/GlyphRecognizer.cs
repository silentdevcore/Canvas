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
        var selection = SelectCandidate(matches, patch, blob.Bounds);
        bool resolved = selection.Score >= MinimumConfidence;

        return new RecognizedChar
        {
            Value = resolved ? selection.Ch : '?',
            Bounds = blob.Bounds,
            Confidence = resolved ? Math.Max(0, selection.Score) : 0,
            Diagnostics = new GlyphRecognitionDiagnostics
            {
                InitialCandidate = selection.InitialCandidate,
                SelectedCandidate = selection.Ch,
                Method = resolved ? selection.Method : "unresolved",
                Score = Math.Round(selection.Score, 3),
                EnclosedWhiteRegions = selection.EnclosedWhiteRegions,
                ProjectionReranked = selection.ProjectionReranked,
                ZoningReranked = selection.ZoningReranked,
                Signals = selection.Signals ?? EmptySignals(),
                DecisionWeights = selection.DecisionWeights ?? EmptySignals(),
            },
        };
    }

    public static int CountEnclosedWhiteRegionsForTest(SKBitmap binary, BlobInfo blob) =>
        CountEnclosedWhiteRegions(ExtractPatch(binary, blob.Bounds));

    public static double ProjectionProfileDistanceForTest(float[] patch, char candidate)
    {
        if (!CharacterTemplates.TryGetTemplate(candidate, out var template))
            return double.PositiveInfinity;

        return ProjectionProfileDistance(patch, template);
    }

    public static double ZoningDistanceForTest(float[] patch, char candidate)
    {
        if (!CharacterTemplates.TryGetTemplate(candidate, out var template))
            return double.PositiveInfinity;

        return ZoningDistance(patch, template);
    }

    public static float[] ExtractPatchForTest(SKBitmap binary, SKRectI bounds) =>
        ExtractPatch(binary, bounds);

    private sealed record Selection(
        char Ch,
        double Score,
        char InitialCandidate,
        string Method,
        int EnclosedWhiteRegions = 0,
        bool ProjectionReranked = false,
        bool ZoningReranked = false,
        IReadOnlyDictionary<string, double>? Signals = null,
        IReadOnlyDictionary<string, double>? DecisionWeights = null);

    private static Selection SelectCandidate(
        IReadOnlyList<(char ch, double score)> matches,
        float[] patch,
        SKRectI bounds)
    {
        if (matches.Count == 0)
            return CreateSelection('?', 0, '?', "none", 0, false, false, 0, 0, 0);

        var best = matches[0];
        char initialCandidate = best.ch;
        var bestAlnum = matches.FirstOrDefault(m => char.IsLetterOrDigit(m.ch));

        if (LooksLikeDot(bounds))
            return CreateSelection('.', best.score, initialCandidate, "punctuation", 0, false, false, best.score, 0, 0);
        if (LooksLikeDash(bounds, patch))
            return CreateSelection('-', best.score, initialCandidate, "punctuation", 0, false, false, best.score, 0, 0);
        if (LooksLikeSlash(bounds, patch))
            return CreateSelection('/', best.score, initialCandidate, "punctuation", 0, false, false, best.score, 0, 0);

        string method = "ncc";
        bool looksLikeFullHeightGlyph = bounds.Height >= 8 && bounds.Width >= 3;
        if (!char.IsLetterOrDigit(best.ch) &&
            looksLikeFullHeightGlyph &&
            bestAlnum != default &&
            bestAlnum.score >= best.score - 0.08)
        {
            best = bestAlnum;
            method = "ncc-alnum-fallback";
        }

        int holes = CountEnclosedWhiteRegions(patch);
        if (holes == 1 && best.ch == '8')
        {
            var zero = matches.FirstOrDefault(m => m.ch == '0');
            var upperO = matches.FirstOrDefault(m => m.ch == 'O');
            if (zero != default && zero.score >= best.score - 0.08)
                return CreateSelection('0', zero.score, initialCandidate, "holes", holes, false, false, zero.score, 0, 0);
            if (upperO != default && upperO.score >= best.score - 0.08)
                return CreateSelection('O', upperO.score, initialCandidate, "holes", holes, false, false, upperO.score, 0, 0);
        }

        var beforeProjection = best;
        var afterProjection = SelectByProjectionProfile(matches, patch, beforeProjection);
        if (afterProjection.ch != best.ch)
            method = "projection-profile";
        best = afterProjection;

        var beforeZoning = best;
        var afterZoning = SelectByZoning(matches, patch, beforeZoning);
        if (afterZoning.ch != best.ch)
            method = "zoning";
        best = afterZoning;

        if ((best.ch == '0' || best.ch == '3' || best.ch == '6') && LooksLikeFive(patch))
            return CreateSelection(
                '5',
                best.score,
                initialCandidate,
                "structural",
                holes,
                afterProjection.ch != beforeProjection.ch,
                false,
                best.score,
                ProjectionSignal(patch, '5'),
                ZoningSignal(patch, '5'));

        return CreateSelection(
            best.ch,
            best.score,
            initialCandidate,
            method,
            holes,
            afterProjection.ch != beforeProjection.ch,
            afterZoning.ch != beforeZoning.ch,
            best.score,
            ProjectionSignal(patch, best.ch),
            ZoningSignal(patch, best.ch));
    }

    private static Selection CreateSelection(
        char ch,
        double score,
        char initialCandidate,
        string method,
        int holes,
        bool projectionReranked,
        bool zoningReranked,
        double nccSignal,
        double projectionSignal,
        double zoningSignal)
    {
        double holeSignal = holes > 0 ? 1 : 0;
        double structuralSignal = method is "structural" or "punctuation" ? 1 : 0;
        var signals = new Dictionary<string, double>
        {
            ["ncc"] = RoundSignal(nccSignal),
            ["projection"] = RoundSignal(projectionSignal),
            ["zoning"] = RoundSignal(zoningSignal),
            ["holes"] = holeSignal,
            ["structural"] = structuralSignal,
        };

        var weights = BuildDecisionWeights(method, signals, projectionReranked, zoningReranked);

        return new Selection(
            ch,
            score,
            initialCandidate,
            method,
            holes,
            projectionReranked,
            zoningReranked,
            signals,
            weights);
    }

    private static IReadOnlyDictionary<string, double> EmptySignals() => new Dictionary<string, double>
    {
        ["ncc"] = 0,
        ["projection"] = 0,
        ["zoning"] = 0,
        ["holes"] = 0,
        ["structural"] = 0,
    };

    private static Dictionary<string, double> BuildDecisionWeights(
        string method,
        IReadOnlyDictionary<string, double> signals,
        bool projectionReranked,
        bool zoningReranked)
    {
        var raw = new Dictionary<string, double>
        {
            ["ncc"] = Math.Max(0.001, signals["ncc"]),
            ["projection"] = projectionReranked || method == "projection-profile" ? Math.Max(0.001, signals["projection"]) : 0,
            ["zoning"] = zoningReranked || method == "zoning" ? Math.Max(0.001, signals["zoning"]) : 0,
            ["holes"] = method == "holes" ? Math.Max(0.001, signals["holes"]) : 0,
            ["structural"] = method is "structural" or "punctuation" ? Math.Max(0.001, signals["structural"]) : 0,
        };

        double total = raw.Values.Sum();
        if (total <= 0)
            return raw.ToDictionary(kv => kv.Key, kv => 0d);

        return raw.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value / total, 3));
    }

    private static double ProjectionSignal(float[] patch, char ch)
    {
        if (!CharacterTemplates.TryGetTemplate(ch, out var template))
            return 0;

        return 1 - Math.Clamp(ProjectionProfileDistance(patch, template), 0, 1);
    }

    private static double ZoningSignal(float[] patch, char ch)
    {
        if (!CharacterTemplates.TryGetTemplate(ch, out var template))
            return 0;

        return 1 - Math.Clamp(ZoningDistance(patch, template), 0, 1);
    }

    private static double RoundSignal(double value) =>
        Math.Round(Math.Clamp(value, 0, 1), 3);

    private static (char ch, double score) SelectByProjectionProfile(
        IReadOnlyList<(char ch, double score)> matches,
        float[] patch,
        (char ch, double score) best)
    {
        if (!CharacterTemplates.TryGetTemplate(best.ch, out var bestTemplate))
            return best;

        double bestDistance = ProjectionProfileDistance(patch, bestTemplate);
        var selected = best;

        foreach (var candidate in matches)
        {
            if (candidate.ch == best.ch ||
                !char.IsLetterOrDigit(candidate.ch) ||
                candidate.score < best.score - 0.04 ||
                !CharacterTemplates.TryGetTemplate(candidate.ch, out var template))
                continue;

            double distance = ProjectionProfileDistance(patch, template);
            if (distance + 0.035 < bestDistance)
            {
                bestDistance = distance;
                selected = candidate;
            }
        }

        return selected;
    }

    private static (char ch, double score) SelectByZoning(
        IReadOnlyList<(char ch, double score)> matches,
        float[] patch,
        (char ch, double score) best)
    {
        if (!CharacterTemplates.TryGetTemplate(best.ch, out var bestTemplate))
            return best;

        double bestDistance = ZoningDistance(patch, bestTemplate);
        var selected = best;

        foreach (var candidate in matches)
        {
            if (candidate.ch == best.ch ||
                !CanUseZoningRerank(best.ch, candidate.ch) ||
                candidate.score < best.score - 0.035 ||
                !CharacterTemplates.TryGetTemplate(candidate.ch, out var template))
                continue;

            double distance = ZoningDistance(patch, template);
            if (distance + 0.04 < bestDistance)
            {
                bestDistance = distance;
                selected = candidate;
            }
        }

        return selected;
    }

    private static bool CanUseZoningRerank(char best, char candidate)
    {
        const string supported = "KVWXYZ";
        return supported.Contains(best) && supported.Contains(candidate);
    }

    private static double ProjectionProfileDistance(float[] a, float[] b)
    {
        double distance = 0;

        for (int y = 0; y < PatchSize; y++)
        {
            distance += Math.Abs(RowInkDensity(a, y) - RowInkDensity(b, y));
        }

        for (int x = 0; x < PatchSize; x++)
        {
            distance += Math.Abs(ColumnInkDensity(a, x) - ColumnInkDensity(b, x));
        }

        return distance / (PatchSize * 2);
    }

    private static double RowInkDensity(float[] patch, int y)
    {
        int ink = 0;
        for (int x = 0; x < PatchSize; x++)
        {
            if (patch[y * PatchSize + x] < 0.5f)
                ink++;
        }

        return ink / (double)PatchSize;
    }

    private static double ColumnInkDensity(float[] patch, int x)
    {
        int ink = 0;
        for (int y = 0; y < PatchSize; y++)
        {
            if (patch[y * PatchSize + x] < 0.5f)
                ink++;
        }

        return ink / (double)PatchSize;
    }

    private static double ZoningDistance(float[] a, float[] b)
    {
        const int zones = 4;
        const int zoneSize = PatchSize / zones;
        double distance = 0;

        for (int zy = 0; zy < zones; zy++)
        {
            for (int zx = 0; zx < zones; zx++)
            {
                int x0 = zx * zoneSize;
                int y0 = zy * zoneSize;
                int x1 = zx == zones - 1 ? PatchSize : x0 + zoneSize;
                int y1 = zy == zones - 1 ? PatchSize : y0 + zoneSize;
                distance += Math.Abs(
                    InkDensity(a, x0, y0, x1, y1) -
                    InkDensity(b, x0, y0, x1, y1));
            }
        }

        return distance / (zones * zones);
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

    private static int CountEnclosedWhiteRegions(float[] patch)
    {
        var visited = new bool[PatchSize * PatchSize];
        int count = 0;

        for (int y = 1; y < PatchSize - 1; y++)
        {
            for (int x = 1; x < PatchSize - 1; x++)
            {
                int index = y * PatchSize + x;
                if (visited[index] || patch[index] < 0.5f)
                    continue;

                var (touchesEdge, area) = FloodWhiteRegion(patch, visited, x, y);
                if (!touchesEdge && area >= 4)
                    count++;
            }
        }

        return count;
    }

    private static (bool touchesEdge, int area) FloodWhiteRegion(
        float[] patch,
        bool[] visited,
        int startX,
        int startY)
    {
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY * PatchSize + startX] = true;
        bool touchesEdge = false;
        int area = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            area++;
            if (x == 0 || y == 0 || x == PatchSize - 1 || y == PatchSize - 1)
                touchesEdge = true;

            TryVisitWhite(patch, visited, queue, x - 1, y);
            TryVisitWhite(patch, visited, queue, x + 1, y);
            TryVisitWhite(patch, visited, queue, x, y - 1);
            TryVisitWhite(patch, visited, queue, x, y + 1);
        }

        return (touchesEdge, area);
    }

    private static void TryVisitWhite(
        float[] patch,
        bool[] visited,
        Queue<(int x, int y)> queue,
        int x,
        int y)
    {
        if (x < 0 || y < 0 || x >= PatchSize || y >= PatchSize)
            return;

        int index = y * PatchSize + x;
        if (visited[index] || patch[index] < 0.5f)
            return;

        visited[index] = true;
        queue.Enqueue((x, y));
    }

    private static unsafe float[] ExtractPatch(SKBitmap binary, SKRectI bounds)
    {
        var patch = new float[PatchSize * PatchSize];
        Array.Fill(patch, 1f);

        bounds = TightenToInkBounds(binary, bounds);
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

    private static unsafe SKRectI TightenToInkBounds(SKBitmap binary, SKRectI bounds)
    {
        byte* src = (byte*)binary.GetPixels().ToPointer();
        int stride = binary.RowBytes;
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        for (int y = Math.Max(0, bounds.Top); y < Math.Min(binary.Height, bounds.Bottom); y++)
        {
            for (int x = Math.Max(0, bounds.Left); x < Math.Min(binary.Width, bounds.Right); x++)
            {
                if (src[y * stride + x] != 0)
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }

        return left == int.MaxValue
            ? bounds
            : new SKRectI(left, top, right, bottom);
    }
}
