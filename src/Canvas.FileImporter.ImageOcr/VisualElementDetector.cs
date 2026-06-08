using SkiaSharp;

namespace Canvas.FileImporter.ImageOcr;

// Pipeline stage 2: detects visual elements from image pixels — rule segments,
// rectangles/lines, checkboxes, filled rectangles, circles/ellipses, and image
// regions. Promoted verbatim from ImageToPdfConverter. Pure pixel analysis: it
// finds candidates and never makes final text-placement decisions (that is the
// fusion stage's job).
internal static class VisualElementDetector
{
    // Neighbor offsets for contrast sampling in GetRulePixelScore. Hoisted to a static
    // field so the rule scan (millions of pixels) does not allocate per call.
    private static readonly int[] RuleSampleOffsets = [-3, -2, 2, 3];

    // Above this many same-orientation rule segments, the O(H^2*V^2) rectangle pairing is
    // skipped (the image is too noisy/text-dense for reliable rectangle detection).
    private const int MaxRectangleRuleSegments = 400;

    // Default minimum luma contrast for a pixel to count as part of a rule. The main rule
    // scan passes options.RuleContrastThreshold; callers without options (checkboxes, tests)
    // use this.
    private const int DefaultRuleContrast = 18;

    // NOTE: detector methods take OcrPixels (a one-time pixel snapshot) rather than
    // SKBitmap so per-pixel reads stay in managed memory instead of marshalling.
    // ----- Rule segment detection -----

    public static IReadOnlyList<RuleSegment> DetectRuleSegments(OcrPixels bitmap) =>
        DetectRuleSegments(bitmap, DefaultRuleContrast);

    public static IReadOnlyList<RuleSegment> DetectRuleSegments(OcrPixels bitmap, int minContrast) =>
        DetectRuleSegments(
            bitmap,
            minHorizontalRun: Math.Max(16, bitmap.Width / 8),
            minVerticalRun: Math.Max(16, bitmap.Height / 8),
            minContrast: minContrast);

    public static IReadOnlyList<RuleSegment> DetectRuleSegments(OcrPixels bitmap, IReadOnlyList<OcrBoundingBox> bounds, int minContrast = DefaultRuleContrast)
    {
        if (bounds.Count == 0)
            return [];

        var segments = new List<RuleSegment>();
        foreach (var bound in bounds)
        {
            var left = Math.Clamp(bound.X, 0, bitmap.Width);
            var top = Math.Clamp(bound.Y, 0, bitmap.Height);
            var right = Math.Clamp(bound.X + bound.Width, left, bitmap.Width);
            var bottom = Math.Clamp(bound.Y + bound.Height, top, bitmap.Height);
            if (right <= left || bottom <= top)
                continue;

            var minHorizontalRun = Math.Max(16, (right - left) / 3);
            var minVerticalRun = Math.Max(16, (bottom - top) / 3);
            segments.AddRange(DetectRuleSegments(bitmap, left, top, right, bottom, minHorizontalRun, minVerticalRun, minContrast));
        }

        return segments
            .GroupBy(s => $"{s.Orientation}:{s.X}:{s.Y}:{s.Length}")
            .Select(g => g.OrderByDescending(s => s.Contrast).First())
            .ToList();
    }

    public static IReadOnlyList<RuleSegment> DetectRuleSegments(OcrPixels bitmap, int minHorizontalRun, int minVerticalRun, int minContrast = DefaultRuleContrast)
        => DetectRuleSegments(bitmap, 0, 0, bitmap.Width, bitmap.Height, minHorizontalRun, minVerticalRun, minContrast);

    private static IReadOnlyList<RuleSegment> DetectRuleSegments(
        OcrPixels bitmap,
        int left,
        int top,
        int right,
        int bottom,
        int minHorizontalRun,
        int minVerticalRun,
        int minContrast)
    {
        var segments = new List<RuleSegment>();
        const int maxInlineGap = 2;

        for (var y = top; y < bottom; y++)
        {
            var runStart = -1;
            var gap = 0;
            var contrastSum = 0.0;
            var rulePixelCount = 0;
            for (var x = left; x <= right; x++)
            {
                var score = x < right ? GetRulePixelScore(bitmap, x, y, RuleOrientation.Horizontal, minContrast) : null;
                if (score is not null)
                {
                    if (runStart < 0)
                        runStart = x;
                    gap = 0;
                    contrastSum += score.Value.Contrast;
                    rulePixelCount++;
                    continue;
                }

                if (runStart < 0)
                    continue;

                if (x < right && gap < maxInlineGap)
                {
                    gap++;
                    continue;
                }

                var end = x - gap;
                var length = end - runStart;
                if (length >= minHorizontalRun)
                    segments.Add(new RuleSegment(
                        RuleOrientation.Horizontal,
                        runStart,
                        y,
                        length,
                        rulePixelCount == 0 ? 0 : contrastSum / rulePixelCount));
                runStart = -1;
                gap = 0;
                contrastSum = 0;
                rulePixelCount = 0;
            }
        }

        for (var x = left; x < right; x++)
        {
            var runStart = -1;
            var gap = 0;
            var contrastSum = 0.0;
            var rulePixelCount = 0;
            for (var y = top; y <= bottom; y++)
            {
                var score = y < bottom ? GetRulePixelScore(bitmap, x, y, RuleOrientation.Vertical, minContrast) : null;
                if (score is not null)
                {
                    if (runStart < 0)
                        runStart = y;
                    gap = 0;
                    contrastSum += score.Value.Contrast;
                    rulePixelCount++;
                    continue;
                }

                if (runStart < 0)
                    continue;

                if (y < bottom && gap < maxInlineGap)
                {
                    gap++;
                    continue;
                }

                var end = y - gap;
                var length = end - runStart;
                if (length >= minVerticalRun)
                    segments.Add(new RuleSegment(
                        RuleOrientation.Vertical,
                        x,
                        runStart,
                        length,
                        rulePixelCount == 0 ? 0 : contrastSum / rulePixelCount));
                runStart = -1;
                gap = 0;
                contrastSum = 0;
                rulePixelCount = 0;
            }
        }

        return MergeRuleSegments(segments, maxGap: Math.Max(3, Math.Min(bitmap.Width, bitmap.Height) / 80));
    }

    private static IReadOnlyList<RuleSegment> MergeRuleSegments(IReadOnlyList<RuleSegment> segments, int maxGap)
    {
        var merged = new List<RuleSegment>();
        foreach (var group in segments.GroupBy(s => (s.Orientation, Axis: s.Orientation == RuleOrientation.Horizontal ? s.Y : s.X)))
        {
            RuleSegment? current = null;
            foreach (var segment in group.OrderBy(s => s.Orientation == RuleOrientation.Horizontal ? s.X : s.Y))
            {
                if (current is null)
                {
                    current = segment;
                    continue;
                }

                var currentStart = current.Orientation == RuleOrientation.Horizontal ? current.X : current.Y;
                var currentEnd = currentStart + current.Length;
                var segmentStart = segment.Orientation == RuleOrientation.Horizontal ? segment.X : segment.Y;
                if (segmentStart <= currentEnd + maxGap)
                {
                    var segmentEnd = segmentStart + segment.Length;
                    var contrast = ((current.Contrast * current.Length) + (segment.Contrast * segment.Length)) /
                                   Math.Max(1, current.Length + segment.Length);
                    current = current with
                    {
                        Length = Math.Max(current.Length, segmentEnd - currentStart),
                        Contrast = contrast,
                    };
                    continue;
                }

                merged.Add(current);
                current = segment;
            }

            if (current is not null)
                merged.Add(current);
        }

        return merged;
    }

    private static bool IsRulePixel(OcrPixels bitmap, int x, int y, RuleOrientation orientation, int minContrast = DefaultRuleContrast)
        => GetRulePixelScore(bitmap, x, y, orientation, minContrast) is not null;

    private static RulePixelScore? GetRulePixelScore(OcrPixels bitmap, int x, int y, RuleOrientation orientation, int minContrast)
    {
        var color = bitmap.GetPixel(x, y);
        if (OcrLayoutHelpers.IsDarkRulePixel(color))
            return new RulePixelScore(Math.Max(48, 255 - OcrLayoutHelpers.Luma(color)));

        if (color.Alpha < 180)
            return null;

        if (OcrLayoutHelpers.Saturation(color) > 0.20)
            return null;

        var luma = OcrLayoutHelpers.Luma(color);
        var offsets = RuleSampleOffsets;
        var sampleCount = 0;
        var sameDirection = 0;
        var lumaSum = 0.0;
        var nearContrast = 0.0;
        foreach (var offset in offsets)
        {
            var sampleX = orientation == RuleOrientation.Horizontal ? x : x + offset;
            var sampleY = orientation == RuleOrientation.Horizontal ? y + offset : y;
            if (sampleX < 0 || sampleY < 0 || sampleX >= bitmap.Width || sampleY >= bitmap.Height)
                continue;

            var sample = bitmap.GetPixel(sampleX, sampleY);
            if (sample.Alpha < 180)
                continue;

            var sampleLuma = OcrLayoutHelpers.Luma(sample);
            sampleCount++;
            lumaSum += sampleLuma;
            nearContrast = Math.Max(nearContrast, Math.Abs(luma - sampleLuma));
            if (Math.Abs(luma - sampleLuma) < minContrast)
                sameDirection++;
        }

        if (sampleCount < 2)
            return null;

        var backgroundLuma = lumaSum / sampleCount;
        var contrast = Math.Abs(luma - backgroundLuma);
        var strength = Math.Max(contrast, nearContrast);
        if (strength < minContrast)
            return null;

        if (sameDirection > sampleCount / 2)
            return null;

        return new RulePixelScore(strength);
    }

    // ----- Shapes (rectangles + lines) -----

    public static IReadOnlyList<OcrShapeCandidate> DetectShapes(IReadOnlyList<RuleSegment> segments)
    {
        if (segments.Count == 0)
            return [];

        var used = new HashSet<RuleSegment>();
        var rectangles = DetectRectangles(segments, used);
        var lines = segments
            .Where(s => !used.Contains(s))
            .Select(s =>
            {
                var bounds = s.Orientation == RuleOrientation.Horizontal
                    ? new OcrBoundingBox(s.X, s.Y, s.Length, 1)
                    : new OcrBoundingBox(s.X, s.Y, 1, s.Length);
                var kind = s.Orientation == RuleOrientation.Horizontal
                    ? OcrShapeKind.HorizontalLine
                    : OcrShapeKind.VerticalLine;
                return new OcrShapeCandidate(kind, bounds);
            });

        return rectangles.Concat(lines).ToList();
    }

    private static IReadOnlyList<OcrShapeCandidate> DetectRectangles(
        IReadOnlyList<RuleSegment> segments,
        HashSet<RuleSegment> used)
    {
        var rectangles = new List<OcrShapeCandidate>();
        var horizontal = segments
            .Where(s => s.Orientation == RuleOrientation.Horizontal)
            .OrderBy(s => s.Y)
            .ThenBy(s => s.X)
            .ToList();
        var vertical = segments
            .Where(s => s.Orientation == RuleOrientation.Vertical)
            .OrderBy(s => s.X)
            .ThenBy(s => s.Y)
            .ToList();

        // Rectangle pairing is O(H^2 * V^2). On text-dense or noisy images a fine-grained
        // rule scan yields thousands of short segments and this explodes, so bail out when
        // there are too many segments — rectangle/checkbox detection is unreliable there anyway.
        if (horizontal.Count > MaxRectangleRuleSegments || vertical.Count > MaxRectangleRuleSegments)
            return rectangles;

        foreach (var top in horizontal)
        {
            if (used.Contains(top))
                continue;

            foreach (var bottom in horizontal.Where(s => s.Y > top.Y + 8))
            {
                if (used.Contains(bottom))
                    continue;

                var leftX = Math.Max(top.X, bottom.X);
                var rightX = Math.Min(top.X + top.Length, bottom.X + bottom.Length);
                if (rightX - leftX < 12)
                    continue;

                foreach (var left in vertical.Where(s => !used.Contains(s) && s.X >= leftX - 1 && s.X <= rightX + 1))
                {
                    foreach (var right in vertical.Where(s => !used.Contains(s) && s.X > left.X + 8 && s.X >= leftX - 1 && s.X <= rightX + 1))
                    {
                        if (!VerticalCovers(left, top.Y, bottom.Y) ||
                            !VerticalCovers(right, top.Y, bottom.Y) ||
                            !HorizontalCovers(top, left.X, right.X) ||
                            !HorizontalCovers(bottom, left.X, right.X))
                            continue;

                        var bounds = new OcrBoundingBox(
                            left.X,
                            top.Y,
                            Math.Max(1, right.X - left.X),
                            Math.Max(1, bottom.Y - top.Y));
                        rectangles.Add(new OcrShapeCandidate(OcrShapeKind.Rectangle, bounds));
                        used.Add(top);
                        used.Add(bottom);
                        used.Add(left);
                        used.Add(right);
                        break;
                    }

                    if (used.Contains(top))
                        break;
                }

                if (used.Contains(top))
                    break;
            }
        }

        return rectangles;
    }

    private static bool HorizontalCovers(RuleSegment segment, int left, int right) =>
        segment.X <= left + 1 && segment.X + segment.Length >= right - 1;

    private static bool VerticalCovers(RuleSegment segment, int top, int bottom) =>
        segment.Y <= top + 1 && segment.Y + segment.Length >= bottom - 1;

    // ----- Checkboxes -----

    public static IReadOnlyList<OcrCheckboxCandidate> DetectCheckboxes(OcrPixels bitmap, IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        var segments = DetectRuleSegments(bitmap, minHorizontalRun: 8, minVerticalRun: 8);
        var used = new HashSet<RuleSegment>();
        var allRectangles = DetectRectangles(segments, used).ToList();
        var allRectangleBounds = allRectangles.Select(r => r.Bounds).ToArray();
        var rectangles = allRectangles
            .Where(r => IsLikelyCheckboxBounds(r.Bounds))
            .Where(r => !OcrLayoutHelpers.IsBoundsInsideAnyBounds(r.Bounds, excludedBounds))
            .GroupBy(r => $"{r.Bounds.X},{r.Bounds.Y},{r.Bounds.Width},{r.Bounds.Height}")
            .Select(g => g.First())
            .Where(r => !IsNestedCheckboxRectangle(r.Bounds, allRectangleBounds))
            .ToList();

        return rectangles
            .Select(r =>
            {
                var state = ClassifyCheckboxState(bitmap, r.Bounds);
                var confidence = state == "empty" ? 0.86 : 0.90;
                return new OcrCheckboxCandidate(r.Bounds, state, confidence);
            })
            .ToList();
    }

    public static bool IsLikelyCheckboxBounds(OcrBoundingBox bounds)
    {
        var width = Math.Max(1, bounds.Width);
        var height = Math.Max(1, bounds.Height);
        var size = Math.Max(width, height);
        var aspect = Math.Min(width, height) / (double)Math.Max(width, height);
        return size is >= 8 and <= 36 && aspect >= 0.72;
    }

    private static bool IsNestedCheckboxRectangle(OcrBoundingBox candidate, IReadOnlyList<OcrBoundingBox> rectangles) =>
        rectangles.Any(bounds =>
            !ReferenceEquals(candidate, bounds) &&
            bounds.Width > candidate.Width + 4 &&
            bounds.Height > candidate.Height + 4 &&
            OcrLayoutHelpers.IsBoundsInsideBounds(candidate, bounds));

    private static string ClassifyCheckboxState(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var margin = Math.Max(2, (int)Math.Round(Math.Min(bounds.Width, bounds.Height) * 0.22));
        var left = Math.Clamp(bounds.X + margin, 0, bitmap.Width);
        var top = Math.Clamp(bounds.Y + margin, 0, bitmap.Height);
        var right = Math.Clamp(bounds.X + bounds.Width - margin, 0, bitmap.Width);
        var bottom = Math.Clamp(bounds.Y + bounds.Height - margin, 0, bitmap.Height);
        if (right <= left || bottom <= top)
            return "empty";

        var dark = 0;
        var diag = 0;
        var antiDiag = 0;
        var center = 0;
        var width = Math.Max(1, right - left);
        var height = Math.Max(1, bottom - top);
        var centerX = left + width / 2.0;
        var centerY = top + height / 2.0;
        var centerRadius = Math.Max(2.0, Math.Min(width, height) * 0.28);

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (!OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    continue;

                dark++;
                var localX = x - left;
                var localY = y - top;
                var expectedDiagY = localX * (height - 1) / (double)Math.Max(1, width - 1);
                var expectedAntiDiagY = (width - 1 - localX) * (height - 1) / (double)Math.Max(1, width - 1);
                if (Math.Abs(localY - expectedDiagY) <= 1.8)
                    diag++;
                if (Math.Abs(localY - expectedAntiDiagY) <= 1.8)
                    antiDiag++;
                if (Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2)) <= centerRadius)
                    center++;
            }
        }

        if (dark < Math.Max(3, width * height * 0.03))
            return "empty";

        if (center >= dark * 0.58)
            return "dot";

        if (diag >= dark * 0.28 && antiDiag >= dark * 0.28)
            return "cross";

        return "checked";
    }

    // ----- Field interior test (consumed by the fusion stage's field detection) -----

    public static bool HasMostlyEmptyInterior(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var margin = Math.Max(2, (int)Math.Round(Math.Min(bounds.Width, bounds.Height) * 0.18));
        var left = Math.Clamp(bounds.X + margin, 0, bitmap.Width);
        var top = Math.Clamp(bounds.Y + margin, 0, bitmap.Height);
        var right = Math.Clamp(bounds.X + bounds.Width - margin, 0, bitmap.Width);
        var bottom = Math.Clamp(bounds.Y + bounds.Height - margin, 0, bitmap.Height);
        if (right <= left || bottom <= top)
            return false;

        var total = 0;
        var dark = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                total++;
                if (OcrLayoutHelpers.IsLikelyTextPixel(bitmap.GetPixel(x, y)))
                    dark++;
            }
        }

        return total > 0 && dark / (double)total <= 0.035;
    }

    // ----- Filled rectangles -----

    public static IReadOnlyList<OcrShapeCandidate> DetectFilledRectangles(
        OcrPixels bitmap,
        IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        var background = OcrLayoutHelpers.EstimateBackgroundColor(bitmap);
        var visited = new bool[bitmap.Width * bitmap.Height];
        var result = new List<OcrShapeCandidate>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var index = y * bitmap.Width + x;
                if (visited[index] || !IsPotentialFillPixel(bitmap.GetPixel(x, y), background, 45))
                    continue;

                var component = FloodFillFillRegion(bitmap, x, y, background, visited, 45);
                if (component.PixelCount == 0 ||
                    !IsLikelyFilledRectangle(component.Bounds, component.PixelCount, bitmap, excludedBounds))
                    continue;

                var fillColor = EstimateFilledRectangleColor(bitmap, component.Bounds);
                result.Add(new OcrShapeCandidate(OcrShapeKind.FilledRectangle, component.Bounds, fillColor));
            }
        }

        return result;
    }

    // Detects large colored background blocks (header bars, pills, cards, header-row shading)
    // for the "text-background" layout mode. Unlike DetectFilledRectangles this does NOT exclude
    // text regions (we want the whole block, including under its text), uses a lower color
    // distance so subtle light fills are caught, and accepts a region when it is big enough and
    // predominantly fill-colored (text holes are fine — text is redrawn on top).
    public static IReadOnlyList<OcrShapeCandidate> DetectBackgroundFills(
        OcrPixels bitmap,
        ImageToPdfConversionOptions options)
    {
        var background = OcrLayoutHelpers.EstimateBackgroundColor(bitmap);
        var minDistance = Math.Max(1, options.BackgroundFillMinColorDistance);
        var imageArea = (double)bitmap.Width * bitmap.Height;
        var minArea = imageArea * options.BackgroundFillMinAreaFraction;
        var minWidth = bitmap.Width * options.BackgroundFillMinWidthFraction;
        var visited = new bool[bitmap.Width * bitmap.Height];
        var candidates = new List<OcrShapeCandidate>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var index = y * bitmap.Width + x;
                if (visited[index] || !IsPotentialFillPixel(bitmap.GetPixel(x, y), background, minDistance))
                    continue;

                var component = FloodFillFillRegion(bitmap, x, y, background, visited, minDistance);
                var bounds = component.Bounds;
                if (component.PixelCount == 0)
                    continue;

                // Big enough to be a background block, but not the whole page.
                var area = (double)bounds.Width * bounds.Height;
                if (area < minArea && bounds.Width < minWidth)
                    continue;
                if (bounds.Width >= bitmap.Width - 2 && bounds.Height >= bitmap.Height - 2)
                    continue;
                if (bounds.Height < 6)
                    continue;

                // Predominantly fill-colored across its bounding box (text holes allowed).
                var coverage = component.PixelCount / Math.Max(1.0, area);
                if (coverage < options.BackgroundFillMinCoverage)
                    continue;

                var fillColor = EstimateFilledRectangleColor(bitmap, bounds);
                candidates.Add(new OcrShapeCandidate(OcrShapeKind.FilledRectangle, bounds, fillColor));
            }
        }

        // Largest first; drop blocks that mostly sit inside an already-kept larger block.
        var kept = new List<OcrShapeCandidate>();
        foreach (var candidate in candidates.OrderByDescending(c => (long)c.Bounds.Width * c.Bounds.Height))
        {
            if (kept.Any(k => OcrLayoutHelpers.IsBoundsMostlyOverlappingAnyBounds(candidate.Bounds, [k.Bounds], 0.8)))
                continue;
            kept.Add(candidate);
        }

        return kept;
    }

    private static FillComponent FloodFillFillRegion(
        OcrPixels bitmap,
        int startX,
        int startY,
        SKColor background,
        bool[] visited,
        int minDistance)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        var left = startX;
        var top = startY;
        var right = startX;
        var bottom = startY;
        var count = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
                continue;

            var index = y * bitmap.Width + x;
            if (visited[index])
                continue;

            visited[index] = true;
            if (!IsPotentialFillPixel(bitmap.GetPixel(x, y), background, minDistance))
                continue;

            count++;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
        }

        return count == 0
            ? new FillComponent(new OcrBoundingBox(0, 0, 1, 1), 0)
            : new FillComponent(new OcrBoundingBox(left, top, Math.Max(1, right - left + 1), Math.Max(1, bottom - top + 1)), count);
    }

    private static bool IsLikelyFilledRectangle(
        OcrBoundingBox bounds,
        int pixelCount,
        OcrPixels bitmap,
        IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        if (OcrLayoutHelpers.IsBoundsInsideAnyBounds(bounds, excludedBounds) ||
            OcrLayoutHelpers.IsBoundsMostlyOverlappingAnyBounds(bounds, excludedBounds, 0.18))
            return false;

        if (bounds.Width < 18 || bounds.Height < 8)
            return false;

        if (bounds.Width >= bitmap.Width - 2 && bounds.Height >= bitmap.Height - 2)
            return false;

        var area = Math.Max(1, bounds.Width * bounds.Height);
        var density = pixelCount / (double)area;
        if (density < 0.82)
            return false;

        var aspect = Math.Max(bounds.Width, bounds.Height) / (double)Math.Max(1, Math.Min(bounds.Width, bounds.Height));
        return aspect <= 18;
    }

    private static bool IsPotentialFillPixel(SKColor color, SKColor background, int minDistance)
    {
        if (color.Alpha < 180)
            return false;

        return OcrLayoutHelpers.ColorDistance(color, background) >= minDistance;
    }

    private static string EstimateFilledRectangleColor(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var samples = new List<SKColor>();
        for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha < 180)
                    continue;

                var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
                if (luma >= 80)
                    samples.Add(color);
            }
        }

        if (samples.Count < Math.Max(4, bounds.Width * bounds.Height * 0.25))
        {
            samples.Clear();
            for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
            {
                for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);
                    if (color.Alpha >= 180)
                        samples.Add(color);
                }
            }
        }

        if (samples.Count == 0)
            return "#111827";

        var red = OcrLayoutHelpers.Median(samples.Select(c => c.Red));
        var green = OcrLayoutHelpers.Median(samples.Select(c => c.Green));
        var blue = OcrLayoutHelpers.Median(samples.Select(c => c.Blue));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    // ----- Circles / ellipses -----

    public static IReadOnlyList<OcrShapeCandidate> DetectCirclesAndEllipses(
        OcrPixels bitmap,
        IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        var visited = new bool[bitmap.Width * bitmap.Height];
        var result = new List<OcrShapeCandidate>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var index = y * bitmap.Width + x;
                if (visited[index] || !OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    continue;

                var component = FloodFillDarkRegion(bitmap, x, y, visited);
                if (component.PixelCount == 0 ||
                    !TryClassifyOvalContour(bitmap, component.Bounds, component.PixelCount, excludedBounds, out var kind))
                    continue;

                result.Add(new OcrShapeCandidate(kind, component.Bounds));
            }
        }

        return result;
    }

    private static FillComponent FloodFillDarkRegion(OcrPixels bitmap, int startX, int startY, bool[] visited)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        var left = startX;
        var top = startY;
        var right = startX;
        var bottom = startY;
        var count = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
                continue;

            var index = y * bitmap.Width + x;
            if (visited[index])
                continue;

            visited[index] = true;
            if (!OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                continue;

            count++;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
            queue.Enqueue((x + 1, y + 1));
            queue.Enqueue((x - 1, y - 1));
            queue.Enqueue((x + 1, y - 1));
            queue.Enqueue((x - 1, y + 1));
        }

        return count == 0
            ? new FillComponent(new OcrBoundingBox(0, 0, 1, 1), 0)
            : new FillComponent(new OcrBoundingBox(left, top, Math.Max(1, right - left + 1), Math.Max(1, bottom - top + 1)), count);
    }

    private static bool TryClassifyOvalContour(
        OcrPixels bitmap,
        OcrBoundingBox bounds,
        int pixelCount,
        IReadOnlyList<OcrBoundingBox> excludedBounds,
        out OcrShapeKind kind)
    {
        kind = OcrShapeKind.Ellipse;
        if (OcrLayoutHelpers.IsBoundsInsideAnyBounds(bounds, excludedBounds))
            return false;

        if (bounds.Width < 14 || bounds.Height < 14)
            return false;

        var aspect = Math.Min(bounds.Width, bounds.Height) / (double)Math.Max(bounds.Width, bounds.Height);
        if (aspect < 0.38)
            return false;

        var area = Math.Max(1, bounds.Width * bounds.Height);
        var density = pixelCount / (double)area;
        if (density is < 0.035 or > 0.42)
            return false;

        var (near, far) = CountOvalContourFitPixels(bitmap, bounds);
        if (near < pixelCount * 0.58 || far > pixelCount * 0.22)
            return false;

        if (!HasOvalQuadrantCoverage(bitmap, bounds))
            return false;

        if (HasLongStraightEdge(bitmap, bounds))
            return false;

        if (!HasMostlyEmptyInteriorForOval(bitmap, bounds))
            return false;

        kind = aspect >= 0.86 ? OcrShapeKind.Circle : OcrShapeKind.Ellipse;
        return true;
    }

    private static (int Near, int Far) CountOvalContourFitPixels(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var cx = bounds.X + (bounds.Width - 1) / 2.0;
        var cy = bounds.Y + (bounds.Height - 1) / 2.0;
        var rx = Math.Max(1, (bounds.Width - 1) / 2.0);
        var ry = Math.Max(1, (bounds.Height - 1) / 2.0);
        var near = 0;
        var far = 0;

        for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
            {
                if (!OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    continue;

                var normalized = Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2);
                if (Math.Abs(normalized - 1.0) <= 0.34)
                    near++;
                else if (Math.Abs(normalized - 1.0) > 0.62)
                    far++;
            }
        }

        return (near, far);
    }

    private static bool HasOvalQuadrantCoverage(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var cx = bounds.X + bounds.Width / 2.0;
        var cy = bounds.Y + bounds.Height / 2.0;
        var quadrants = new int[4];

        for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
            {
                if (!OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    continue;

                var quadrant = x < cx
                    ? y < cy ? 0 : 2
                    : y < cy ? 1 : 3;
                quadrants[quadrant]++;
            }
        }

        return quadrants.All(q => q >= 3);
    }

    private static bool HasLongStraightEdge(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var rowLimit = bounds.Width * 0.72;
        for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
        {
            var rowDark = 0;
            for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
            {
                if (OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    rowDark++;
            }

            if (rowDark >= rowLimit)
                return true;
        }

        var columnLimit = bounds.Height * 0.72;
        for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
        {
            var columnDark = 0;
            for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
            {
                if (OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    columnDark++;
            }

            if (columnDark >= columnLimit)
                return true;
        }

        return false;
    }

    private static bool HasMostlyEmptyInteriorForOval(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var cx = bounds.X + (bounds.Width - 1) / 2.0;
        var cy = bounds.Y + (bounds.Height - 1) / 2.0;
        var rx = Math.Max(1, (bounds.Width - 1) / 2.0);
        var ry = Math.Max(1, (bounds.Height - 1) / 2.0);
        var total = 0;
        var dark = 0;

        for (var y = bounds.Y; y < bounds.Y + bounds.Height && y < bitmap.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width && x < bitmap.Width; x++)
            {
                var normalized = Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2);
                if (normalized >= 0.64)
                    continue;

                total++;
                if (OcrLayoutHelpers.IsDarkRulePixel(bitmap.GetPixel(x, y)))
                    dark++;
            }
        }

        return total > 0 && dark / (double)total <= 0.08;
    }

    // ----- Image regions -----

    public static IReadOnlyList<OcrImageRegionCandidate> DetectImageRegions(
        OcrPixels bitmap,
        IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        var background = OcrLayoutHelpers.EstimateBackgroundColor(bitmap);
        var visited = new bool[bitmap.Width * bitmap.Height];
        var result = new List<OcrImageRegionCandidate>();

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var index = y * bitmap.Width + x;
                if (visited[index] ||
                    OcrLayoutHelpers.IsPointInsideAnyBounds(x, y, excludedBounds) ||
                    !IsPotentialImageRegionPixel(bitmap.GetPixel(x, y), background))
                    continue;

                var component = FloodFillImageRegion(bitmap, x, y, background, excludedBounds, visited);
                if (component.PixelCount == 0 ||
                    !IsLikelyImageRegion(component.Bounds, component.PixelCount, bitmap, excludedBounds))
                    continue;

                result.Add(new OcrImageRegionCandidate(component.Bounds, 0.78));
            }
        }

        return result;
    }

    private static FillComponent FloodFillImageRegion(
        OcrPixels bitmap,
        int startX,
        int startY,
        SKColor background,
        IReadOnlyList<OcrBoundingBox> excludedBounds,
        bool[] visited)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        var left = startX;
        var top = startY;
        var right = startX;
        var bottom = startY;
        var count = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
                continue;

            var index = y * bitmap.Width + x;
            if (visited[index])
                continue;

            visited[index] = true;
            if (OcrLayoutHelpers.IsPointInsideAnyBounds(x, y, excludedBounds) ||
                !IsPotentialImageRegionPixel(bitmap.GetPixel(x, y), background))
                continue;

            count++;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);

            queue.Enqueue((x + 1, y));
            queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1));
            queue.Enqueue((x, y - 1));
            queue.Enqueue((x + 1, y + 1));
            queue.Enqueue((x - 1, y - 1));
            queue.Enqueue((x + 1, y - 1));
            queue.Enqueue((x - 1, y + 1));
        }

        return count == 0
            ? new FillComponent(new OcrBoundingBox(0, 0, 1, 1), 0)
            : new FillComponent(new OcrBoundingBox(left, top, Math.Max(1, right - left + 1), Math.Max(1, bottom - top + 1)), count);
    }

    private static bool IsPotentialImageRegionPixel(SKColor color, SKColor background)
    {
        if (color.Alpha < 180)
            return false;

        return OcrLayoutHelpers.ColorDistance(color, background) >= 45;
    }

    private static bool IsLikelyImageRegion(
        OcrBoundingBox bounds,
        int pixelCount,
        OcrPixels bitmap,
        IReadOnlyList<OcrBoundingBox> excludedBounds)
    {
        if (OcrLayoutHelpers.IsBoundsInsideAnyBounds(bounds, excludedBounds))
            return false;

        if (bounds.Width < 12 || bounds.Height < 12)
            return false;

        if (bounds.Width >= bitmap.Width - 2 && bounds.Height >= bitmap.Height - 2)
            return false;

        var area = Math.Max(1, bounds.Width * bounds.Height);
        if (area < 240)
            return false;

        var density = pixelCount / (double)area;
        if (density is < 0.16 or > 0.96)
            return false;

        var aspect = Math.Max(bounds.Width, bounds.Height) / (double)Math.Max(1, Math.Min(bounds.Width, bounds.Height));
        return aspect <= 8;
    }
}
