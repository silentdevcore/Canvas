using SkiaSharp;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 4: extracts text regions from the prepared image.
///
/// Pipeline:
///   Binary bitmap
///     → 8-connected component labelling (union-find)
///     → character candidate filter (size, aspect, fill ratio)
///     → line assembly (Y-centroid proximity)
///     → font size estimation (median blob height per line)
///     → text colour sampling (from original image)
///
/// Character content is recognized by a small built-in glyph recognizer for clean,
/// printed text. It is intentionally conservative and emits low-confidence glyphs
/// as '?' instead of guessing.
/// </summary>
public static class TextEngine
{
    // ── Tuning ────────────────────────────────────────────────────────────────

    public const int MinCharHeight = 4;
    public const int MaxCharHeight = 200;
    public const int MinCharWidth  = 2;
    public const int MaxCharWidth  = 300;
    public const double MaxAspectRatio   = 15.0;
    public const double MinFillRatio     = 0.05;
    public const double LineProximityFactor = 1.5;   // gap < factor × median height → same line


    // ── Entry point ───────────────────────────────────────────────────────────

    public static TextAnalysisResult Analyze(PreparedImage img)
    {
        // Pass A: dark text on light background (standard binary)
        var normalBlobs      = LabelConnectedComponents(img.Binary);
        var normalCandidates = FilterCharCandidates(normalBlobs, img.Binary);

        // Pass B: light text on dark background (inverted binary — e.g. white text in dark headers)
        using var inverted      = InvertBinary(img.Binary);
        var invertedBlobs       = LabelConnectedComponents(inverted);
        var invertedCandidates  = FilterCharCandidates(invertedBlobs, inverted);

        if (normalCandidates.Count == 0 && invertedCandidates.Count == 0)
            return new TextAnalysisResult { Lines = [] };

        var texts = new List<ImageTextPrimitive>();
        if (normalCandidates.Count > 0)
            texts.AddRange(BuildTextLines(AssembleLines(normalCandidates), img.Original, img.Binary));
        if (invertedCandidates.Count > 0)
            texts.AddRange(BuildTextLines(AssembleLines(invertedCandidates), img.Original, inverted));

        texts = MergeTextLines(texts);
        return new TextAnalysisResult { Lines = texts };
    }

    // ── Inverted binary helper ────────────────────────────────────────────────

    private static unsafe SKBitmap InvertBinary(SKBitmap binary)
    {
        var info = new SKImageInfo(binary.Width, binary.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var inv  = new SKBitmap(info);

        byte* src       = (byte*)binary.GetPixels().ToPointer();
        byte* dst       = (byte*)inv.GetPixels().ToPointer();
        int   srcStride = binary.RowBytes;
        int   dstStride = inv.RowBytes;

        for (int y = 0; y < binary.Height; y++)
        {
            byte* srcRow = src + y * srcStride;
            byte* dstRow = dst + y * dstStride;
            for (int x = 0; x < binary.Width; x++)
                dstRow[x] = srcRow[x] == 0 ? (byte)255 : (byte)0;
        }
        return inv;
    }

    // ── Text-line merging ─────────────────────────────────────────────────────

    private static List<ImageTextPrimitive> MergeTextLines(List<ImageTextPrimitive> lines)
    {
        var merged = new List<ImageTextPrimitive>();

        foreach (var line in SortTextLines(lines))
        {
            if (!ContainsLetterOrDigit(line))
                continue;

            bool duplicate = merged.Any(existing => OverlapRatio(line.Bounds, existing.Bounds) > 0.65);
            if (!duplicate)
                merged.Add(line);
        }

        return merged;
    }

    private static IEnumerable<ImageTextPrimitive> SortTextLines(List<ImageTextPrimitive> lines) =>
        lines.OrderBy(l => l, TextLinePositionComparer.Instance);

    private static bool ContainsLetterOrDigit(ImageTextPrimitive line) =>
        line.Words.SelectMany(w => w.Chars).Any(c => char.IsLetterOrDigit(c.Value));

    private static double OverlapRatio(SKRectI a, SKRectI b)
    {
        int ox = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        int oy = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        int overlap = ox * oy;
        int minArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return minArea <= 0 ? 0 : (double)overlap / minArea;
    }

    // ── 8-connected component labelling (union-find) ─────────────────────────

    public static unsafe List<BlobInfo> LabelConnectedComponents(SKBitmap binary)
    {
        int w = binary.Width, h = binary.Height;
        var labels = new int[w * h];
        var parent = new int[w * h + 1]; // union-find; index 0 unused
        int nextLabel = 1;

        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        byte* src    = (byte*)binary.GetPixels().ToPointer();
        int   stride = binary.RowBytes;

        // Allocate neighbours ONCE outside the hot loop — stackalloc inside a loop
        // would consume (width × height × 16 bytes) of stack space and overflow.
        Span<int> neighbours = stackalloc int[4];

        // First pass: assign provisional labels
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (src[y * stride + x] != 0) continue; // skip white pixels

                int above = y > 0 ? labels[(y - 1) * w + x]     : 0;
                int left  = x > 0 ? labels[y       * w + (x-1)] : 0;
                int aboveLeft  = (y > 0 && x > 0) ? labels[(y-1)*w + (x-1)] : 0;
                int aboveRight = (y > 0 && x < w-1) ? labels[(y-1)*w + (x+1)] : 0;

                int nc = 0;
                if (above      != 0) neighbours[nc++] = above;
                if (left       != 0) neighbours[nc++] = left;
                if (aboveLeft  != 0) neighbours[nc++] = aboveLeft;
                if (aboveRight != 0) neighbours[nc++] = aboveRight;

                int lbl;
                if (nc == 0)
                {
                    lbl = nextLabel++;
                    if (lbl >= parent.Length) Array.Resize(ref parent, parent.Length * 2);
                    parent[lbl] = lbl;
                }
                else
                {
                    lbl = Find(parent, neighbours[0]);
                    for (int ni = 1; ni < nc; ni++)
                        Union(parent, lbl, neighbours[ni]);
                }
                labels[y * w + x] = lbl;
            }
        }

        // Second pass: resolve unions and collect blob statistics
        var blobs = new Dictionary<int, BlobStats>();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int lbl = labels[y * w + x];
                if (lbl == 0) continue;
                int root = Find(parent, lbl);
                labels[y * w + x] = root;

                if (!blobs.TryGetValue(root, out var b))
                    b = new BlobStats(x, y, x, y, 0);
                blobs[root] = new BlobStats(
                    Math.Min(b.MinX, x), Math.Min(b.MinY, y),
                    Math.Max(b.MaxX, x), Math.Max(b.MaxY, y),
                    b.PixelCount + 1);
            }
        }

        return blobs.Values
            .Select(b => new BlobInfo
            {
                Bounds     = new SKRectI(b.MinX, b.MinY, b.MaxX + 1, b.MaxY + 1),
                PixelCount = b.PixelCount,
            })
            .ToList();
    }

    // ── Union-find helpers ────────────────────────────────────────────────────

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
        return x;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb) parent[rb] = ra;
    }

    // ── Character candidate filtering ────────────────────────────────────────

    public static List<BlobInfo> FilterCharCandidates(List<BlobInfo> blobs, SKBitmap binary)
    {
        int totalPixels = binary.Width * binary.Height;
        var candidates  = new List<BlobInfo>();

        foreach (var blob in blobs)
        {
            int bw = blob.Bounds.Width;
            int bh = blob.Bounds.Height;

            double fillRatio = (double)blob.PixelCount / (bw * bh);
            if (IsPunctuationCandidate(blob, bw, bh, fillRatio, totalPixels))
            {
                candidates.Add(blob);
                continue;
            }

            if (bh < MinCharHeight || bh > MaxCharHeight) continue;
            if (bw < MinCharWidth  || bw > MaxCharWidth)  continue;

            double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
            if (aspect > MaxAspectRatio) continue;

            if (fillRatio < MinFillRatio) continue;

            // Skip blobs covering >5% of the total image (large filled shapes)
            if ((double)blob.PixelCount / totalPixels > 0.05) continue;

            candidates.Add(blob);
        }

        return candidates;
    }

    private static bool IsPunctuationCandidate(
        BlobInfo blob,
        int bw,
        int bh,
        double fillRatio,
        int totalPixels)
    {
        if (blob.PixelCount < 3) return false;
        if ((double)blob.PixelCount / totalPixels > 0.01) return false;
        if (fillRatio < 0.08) return false;

        double aspect = (double)Math.Max(bw, bh) / Math.Min(bw, bh);
        bool dotLike = bw >= 2 && bh >= 2 && bw <= 6 && bh <= 6 && aspect <= 2.0;
        bool dashLike = bw >= 6 && bw <= 30 && bh <= 5;
        bool strokeLike = bw <= 14 && bh >= 6 && bh <= MaxCharHeight;

        return dotLike || dashLike || strokeLike;
    }

    // ── Line assembly ─────────────────────────────────────────────────────────

    public static List<List<BlobInfo>> AssembleLines(List<BlobInfo> candidates)
    {
        if (candidates.Count == 0) return [];

        // Sort top-to-bottom, left-to-right
        var sorted = candidates
            .OrderBy(b => b.Bounds.Top)
            .ThenBy(b  => b.Bounds.Left)
            .ToList();

        double medianHeight = MedianHeight(sorted);
        double lineGap      = medianHeight * LineProximityFactor;

        var lines    = new List<List<BlobInfo>>();
        var current  = new List<BlobInfo> { sorted[0] };
        double lineY = sorted[0].CenterY;

        for (int i = 1; i < sorted.Count; i++)
        {
            var blob = sorted[i];
            if (Math.Abs(blob.CenterY - lineY) <= lineGap)
            {
                current.Add(blob);
                lineY = current.Average(b => b.CenterY); // running average
            }
            else
            {
                lines.Add(current);
                current = [blob];
                lineY   = blob.CenterY;
            }
        }
        if (current.Count > 0) lines.Add(current);

        // Sort each line left-to-right
        foreach (var line in lines)
            line.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));

        return lines;
    }

    // ── Text line construction ────────────────────────────────────────────────

    /// <summary>
    /// Builds one <see cref="ImageTextPrimitive"/> per detected text line.</summary>
    private static List<ImageTextPrimitive> BuildTextLines(
        List<List<BlobInfo>> lines, SKBitmap original, SKBitmap binary)
    {
        var result = new List<ImageTextPrimitive>();
        int zOrder = 0;

        foreach (var lineBlobs in lines)
        {
            double  fontSizePx = MedianHeight(lineBlobs);
            SKColor textColor  = SampleTextColor(original, lineBlobs);

            var words = RemoveDecorativeSymbolWords(BuildWords(lineBlobs, binary));
            if (words.Count == 0)
                continue;

            foreach (var run in SplitDistantWordRuns(words, fontSizePx))
            {
                var lineBounds = UnionBounds(run.Select(w => w.Bounds));

                result.Add(new ImageTextPrimitive
                {
                    Bounds     = lineBounds,
                    Words      = run,
                    FontSizePx = fontSizePx,
                    BaselineY  = EstimateBaselineY(run),
                    TextColor  = textColor,
                    ZOrder     = zOrder++,
                });
            }
        }

        return result;
    }

    private static IReadOnlyList<RecognizedWord> RemoveDecorativeSymbolWords(
        IReadOnlyList<RecognizedWord> words)
    {
        if (words.Count <= 1)
            return words;

        int start = 0;
        int end = words.Count - 1;

        while (start <= end && IsDecorativeSymbolWord(words[start]))
            start++;
        while (end >= start && IsDecorativeSymbolWord(words[end]))
            end--;

        if (start == 0 && end == words.Count - 1)
            return words;
        if (start > end)
            return [];

        return words.Skip(start).Take(end - start + 1).ToList();
    }

    private static bool IsDecorativeSymbolWord(RecognizedWord word)
    {
        var chars = word.Chars.ToList();
        if (chars.Count == 0 || chars.Count > 3)
            return false;

        bool hasLetterOrDigit = chars.Any(c => char.IsLetterOrDigit(c.Value));
        if (hasLetterOrDigit)
            return false;

        return chars.All(c => IsInlinePunctuation(c.Value) || c.Value is '?' or '|' or '_' or '\'' or '"' or '`');
    }

    private static IReadOnlyList<IReadOnlyList<RecognizedWord>> SplitDistantWordRuns(
        IReadOnlyList<RecognizedWord> words,
        double fontSizePx)
    {
        if (words.Count <= 1) return [words];

        double normalGap = MedianWordGap(words);
        double distantGap = normalGap > 0
            ? Math.Max(fontSizePx * 6.0, normalGap * 3.0)
            : fontSizePx * 6.0;

        var runs = new List<IReadOnlyList<RecognizedWord>>();
        var current = new List<RecognizedWord> { words[0] };

        for (int i = 1; i < words.Count; i++)
        {
            double gap = words[i].Bounds.Left - words[i - 1].Bounds.Right;
            if (gap >= distantGap)
            {
                runs.Add(current);
                current = [];
            }

            current.Add(words[i]);
        }

        if (current.Count > 0)
            runs.Add(current);

        return runs;
    }

    private static double MedianWordGap(IReadOnlyList<RecognizedWord> words)
    {
        var gaps = new List<double>();
        for (int i = 1; i < words.Count; i++)
        {
            double gap = words[i].Bounds.Left - words[i - 1].Bounds.Right;
            if (gap > 0)
                gaps.Add(gap);
        }

        if (gaps.Count < 3) return 0;
        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    private static double EstimateBaselineY(IReadOnlyList<RecognizedWord> words)
    {
        var bottoms = words
            .SelectMany(w => w.Chars)
            .Where(c => c.Value != '.')
            .Select(c => c.Bounds.Bottom)
            .OrderBy(y => y)
            .ToList();

        if (bottoms.Count == 0)
        {
            bottoms = words
                .SelectMany(w => w.Chars)
                .Select(c => c.Bounds.Bottom)
                .OrderBy(y => y)
                .ToList();
        }

        return bottoms.Count == 0 ? 0 : bottoms[bottoms.Count / 2];
    }

    private static IReadOnlyList<RecognizedWord> BuildWords(List<BlobInfo> lineBlobs, SKBitmap binary)
    {
        if (lineBlobs.Count == 0) return [];

        var ordered = lineBlobs.OrderBy(b => b.Bounds.Left).ToList();
        double medianWidth = MedianWidth(ordered);
        double wordGap = Math.Max(4, medianWidth * 0.75);

        var chars = ordered
            .Select(b => GlyphRecognizer.Recognize(binary, b))
            .ToList();
        chars = ApplyWordContextHeuristics(chars).ToList();

        var words = new List<RecognizedWord>();
        var current = new List<RecognizedChar> { chars[0] };

        for (int i = 1; i < chars.Count; i++)
        {
            double gap = chars[i].Bounds.Left - chars[i - 1].Bounds.Right;
            if (gap > wordGap && !ShouldKeepTogether(chars[i - 1].Value, chars[i].Value, gap, wordGap))
            {
                words.Add(BuildWord(current));
                current = [];
            }
            current.Add(chars[i]);
        }

        if (current.Count > 0)
            words.Add(BuildWord(current));

        return words;
    }

    private static RecognizedWord BuildWord(List<RecognizedChar> chars)
    {
        return new RecognizedWord
        {
            Chars = chars,
            Bounds = UnionBounds(chars.Select(c => c.Bounds)),
        };
    }

    private static bool ShouldKeepTogether(char previous, char next, double gap, double wordGap)
    {
        if (gap > wordGap * 2)
            return false;

        return IsInlinePunctuation(previous) ||
            IsInlinePunctuation(next);
    }

    private static bool IsInlinePunctuation(char value) =>
        value is '.' or ':' or '-' or '/' or ',';

    private static IReadOnlyList<RecognizedChar> ApplyWordContextHeuristics(List<RecognizedChar> chars)
    {
        if (chars.Count == 0) return chars;

        bool hasDigit = chars.Any(c => char.IsDigit(c.Value));
        bool digitLikeWord = hasDigit && chars.All(c =>
            char.IsDigit(c.Value) ||
            IsInlinePunctuation(c.Value) ||
            c.Value is 'l' or 'I' or '|' or '\'' or '}' or ']');
        bool hasLowercase = chars.Any(c => char.IsLower(c.Value));

        var result = new List<RecognizedChar>(chars.Count);
        for (int i = 0; i < chars.Count; i++)
        {
            var ch = chars[i];
            char value = ch.Value;

            if (i < chars.Count - 1 &&
                value is 'l' or 'I' &&
                chars[i + 1].Value == '.' &&
                HorizontallyOverlaps(ch.Bounds, chars[i + 1].Bounds))
            {
                result.Add(new RecognizedChar
                {
                    Value = hasLowercase || i > 0 ? 'i' : 'I',
                    Bounds = UnionBounds([ch.Bounds, chars[i + 1].Bounds]),
                    Confidence = Math.Min(ch.Confidence, chars[i + 1].Confidence),
                });
                i++;
                continue;
            }

            if (value == '.' &&
                i < chars.Count - 1 &&
                chars[i + 1].Value == '.' &&
                HorizontallyOverlaps(ch.Bounds, chars[i + 1].Bounds))
            {
                result.Add(new RecognizedChar
                {
                    Value = ':',
                    Bounds = UnionBounds([ch.Bounds, chars[i + 1].Bounds]),
                    Confidence = Math.Min(ch.Confidence, chars[i + 1].Confidence),
                });
                i++;
                continue;
            }

            if (digitLikeWord)
            {
                value = value switch
                {
                    'l' or 'I' or '|' => '1',
                    '\'' or '}' or ']' => '4',
                    _ => value,
                };
            }
            else
            {
                bool wordLooksMixedCase = hasLowercase || result.Any(c => char.IsLower(c.Value));
                if (i == 0 && value == 'l' && chars.Count > 1)
                    value = 'I';
                else if (wordLooksMixedCase)
                    value = value switch
                    {
                        'D' when i > 0 => 'n',
                        'V' when i > 0 => 'v',
                        'O' when i > 0 => 'o',
                        'C' when i > 0 => 'c',
                        'S' when i > 0 => 's',
                        _ => value,
                    };
            }

            if (!digitLikeWord && value == 'l')
            {
                char previous = i > 0 ? result[i - 1].Value : '\0';
                char next = i < chars.Count - 1 ? chars[i + 1].Value : '\0';
                if (previous is '-' or '/' && i == chars.Count - 1)
                    value = '1';
                else if (previous == 'o' && next is 'c' or 'C')
                    value = 'i';
                else if (i == chars.Count - 1 && chars.Count <= 3 && previous == 'l')
                    value = '1';
            }

            if (i > 0 && value == 'O' && result.Any(c => char.IsLower(c.Value)))
                value = 'o';

            result.Add(new RecognizedChar
            {
                Value = value,
                Bounds = ch.Bounds,
                Confidence = ch.Confidence,
            });
        }

        return result;
    }

    private static bool HorizontallyOverlaps(SKRectI a, SKRectI b)
    {
        int overlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        return overlap > 0;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double MedianHeight(List<BlobInfo> blobs)
    {
        if (blobs.Count == 0) return 12;
        var heights = blobs.Select(b => b.Bounds.Height).OrderBy(x => x).ToList();
        return heights[heights.Count / 2];
    }

    private static double MedianWidth(List<BlobInfo> blobs)
    {
        if (blobs.Count == 0) return 6;
        var widths = blobs.Select(b => b.Bounds.Width).OrderBy(x => x).ToList();
        return widths[widths.Count / 2];
    }

    private static SKColor SampleTextColor(SKBitmap original, List<BlobInfo> blobs)
    {
        long r = 0, g = 0, b = 0;
        int  n = 0;
        foreach (var blob in blobs.Take(5)) // sample first few blobs
        {
            int cx = Math.Clamp((blob.Bounds.Left + blob.Bounds.Right)  / 2, 0, original.Width  - 1);
            int cy = Math.Clamp((blob.Bounds.Top  + blob.Bounds.Bottom) / 2, 0, original.Height - 1);
            var c  = original.GetPixel(cx, cy);
            r += c.Red; g += c.Green; b += c.Blue; n++;
        }
        if (n == 0) return SKColors.Black;
        return new SKColor((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    private static SKRectI UnionBounds(IEnumerable<SKRectI> rects)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var r in rects)
        {
            if (r.Left   < minX) minX = r.Left;
            if (r.Top    < minY) minY = r.Top;
            if (r.Right  > maxX) maxX = r.Right;
            if (r.Bottom > maxY) maxY = r.Bottom;
        }
        return minX == int.MaxValue
            ? SKRectI.Empty
            : new SKRectI(minX, minY, maxX, maxY);
    }

    private record BlobStats(int MinX, int MinY, int MaxX, int MaxY, int PixelCount);

    private sealed class TextLinePositionComparer : IComparer<ImageTextPrimitive>
    {
        public static readonly TextLinePositionComparer Instance = new();

        public int Compare(ImageTextPrimitive? x, ImageTextPrimitive? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            double cx = (x.Bounds.Top + x.Bounds.Bottom) / 2.0;
            double cy = (y.Bounds.Top + y.Bounds.Bottom) / 2.0;
            double sameLineTolerance = Math.Max(x.Bounds.Height, y.Bounds.Height) * 0.75;
            if (Math.Abs(cx - cy) <= sameLineTolerance)
                return x.Bounds.Left.CompareTo(y.Bounds.Left);

            return x.Bounds.Top.CompareTo(y.Bounds.Top);
        }
    }
}

// ── Supporting types ──────────────────────────────────────────────────────────

public sealed class BlobInfo
{
    public required SKRectI Bounds     { get; init; }
    public required int     PixelCount { get; init; }
    public double CenterX => (Bounds.Left + Bounds.Right)  / 2.0;
    public double CenterY => (Bounds.Top  + Bounds.Bottom) / 2.0;
}

public sealed class TextAnalysisResult
{
    public required IReadOnlyList<ImageTextPrimitive> Lines { get; init; }
}
