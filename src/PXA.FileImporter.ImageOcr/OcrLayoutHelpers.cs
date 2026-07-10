using SkiaSharp;

namespace PXA.FileImporter.ImageOcr;

// Cross-cutting leaf helpers shared by the image-OCR pipeline stages
// (VisualElementDetector, OcrVisualFusionEngine, CanvasElementBuilder) and the
// ImageToPdfConverter orchestrator. Promoted verbatim from ImageToPdfConverter.
internal static class OcrLayoutHelpers
{
    public static OcrBoundingBox UnionBounds(IEnumerable<OcrBoundingBox> boxes)
    {
        var list = boxes.ToList();
        if (list.Count == 0)
            return new OcrBoundingBox(0, 0, 1, 1);

        var left = list.Min(b => b.X);
        var top = list.Min(b => b.Y);
        var right = list.Max(b => b.X + b.Width);
        var bottom = list.Max(b => b.Y + b.Height);
        return new OcrBoundingBox(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public static OcrBoundingBox ExpandBounds(OcrBoundingBox bounds, int padding) =>
        new(
            Math.Max(0, bounds.X - padding),
            Math.Max(0, bounds.Y - padding),
            bounds.Width + padding * 2,
            bounds.Height + padding * 2);

    public static bool IsSegmentInsideAnyBounds(RuleSegment segment, IReadOnlyList<OcrBoundingBox> bounds) =>
        bounds.Any(bound => IsSegmentInsideBounds(segment, bound));

    public static bool IsBoundsInsideAnyBounds(OcrBoundingBox candidate, IReadOnlyList<OcrBoundingBox> bounds) =>
        bounds.Any(bound => IsBoundsInsideBounds(candidate, bound));

    public static bool IsBoundsMostlyOverlappingAnyBounds(
        OcrBoundingBox candidate,
        IReadOnlyList<OcrBoundingBox> bounds,
        double minOverlapRatio)
    {
        var candidateArea = Math.Max(1, candidate.Width * candidate.Height);
        return bounds.Any(bound =>
        {
            var left = Math.Max(candidate.X, bound.X);
            var top = Math.Max(candidate.Y, bound.Y);
            var right = Math.Min(candidate.X + candidate.Width, bound.X + bound.Width);
            var bottom = Math.Min(candidate.Y + candidate.Height, bound.Y + bound.Height);
            var overlap = Math.Max(0, right - left) * Math.Max(0, bottom - top);
            return overlap / (double)candidateArea >= minOverlapRatio;
        });
    }

    public static bool IsBoundsInsideBounds(OcrBoundingBox candidate, OcrBoundingBox bounds)
    {
        const int tolerance = 1;
        return candidate.X >= bounds.X - tolerance &&
               candidate.Y >= bounds.Y - tolerance &&
               candidate.X + candidate.Width <= bounds.X + bounds.Width + tolerance &&
               candidate.Y + candidate.Height <= bounds.Y + bounds.Height + tolerance;
    }

    public static bool IsSegmentInsideBounds(RuleSegment segment, OcrBoundingBox bounds)
    {
        const int tolerance = 1;
        return segment.Orientation == RuleOrientation.Horizontal
            ? segment.Y >= bounds.Y - tolerance &&
              segment.Y <= bounds.Y + bounds.Height + tolerance &&
              segment.X >= bounds.X - tolerance &&
              segment.X + segment.Length <= bounds.X + bounds.Width + tolerance
            : segment.X >= bounds.X - tolerance &&
              segment.X <= bounds.X + bounds.Width + tolerance &&
              segment.Y >= bounds.Y - tolerance &&
              segment.Y + segment.Length <= bounds.Y + bounds.Height + tolerance;
    }

    public static bool IsPointInsideAnyBounds(int x, int y, IReadOnlyList<OcrBoundingBox> bounds) =>
        bounds.Any(bound => x >= bound.X &&
                            y >= bound.Y &&
                            x < bound.X + bound.Width &&
                            y < bound.Y + bound.Height);

    public static int FindNearestColumn(double anchor, IReadOnlyList<double> columns, double tolerance)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < columns.Count; i++)
        {
            var distance = Math.Abs(columns[i] - anchor);
            if (distance < bestDistance)
            {
                bestIndex = i;
                bestDistance = distance;
            }
        }

        return bestDistance <= tolerance ? bestIndex : -1;
    }

    public static double Luma(SKColor color) =>
        0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;

    public static double Saturation(SKColor color) =>
        (Math.Max(color.Red, Math.Max(color.Green, color.Blue)) -
         Math.Min(color.Red, Math.Min(color.Green, color.Blue))) / 255.0;

    public static int ColorDistance(SKColor a, SKColor b) =>
        Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);

    public static bool IsDarkRulePixel(SKColor color)
    {
        if (color.Alpha < 180)
            return false;

        return Luma(color) < 80;
    }

    public static bool IsLikelyTextPixel(SKColor color)
    {
        if (color.Alpha < 180)
            return false;

        var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
        var saturation = (Math.Max(color.Red, Math.Max(color.Green, color.Blue)) -
                          Math.Min(color.Red, Math.Min(color.Green, color.Blue))) / 255.0;

        return luma < 130 || (luma < 210 && saturation > 0.35);
    }

    public static SKColor EstimateBackgroundColor(OcrPixels bitmap)
    {
        var samples = new[]
        {
            bitmap.GetPixel(0, 0),
            bitmap.GetPixel(Math.Max(0, bitmap.Width - 1), 0),
            bitmap.GetPixel(0, Math.Max(0, bitmap.Height - 1)),
            bitmap.GetPixel(Math.Max(0, bitmap.Width - 1), Math.Max(0, bitmap.Height - 1)),
        };
        return new SKColor(
            Median(samples.Select(c => c.Red)),
            Median(samples.Select(c => c.Green)),
            Median(samples.Select(c => c.Blue)),
            255);
    }

    public static byte Median(IEnumerable<byte> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    public static double EstimateTableColumnTolerance(OcrTableCandidate table)
    {
        var lineHeight = table.Lines.Average(l => Math.Max(1, l.Bounds.Height));
        var anchorGap = table.ColumnAnchors.Count < 2
            ? lineHeight * 2
            : table.ColumnAnchors.Zip(table.ColumnAnchors.Skip(1), (a, b) => b - a).Min();
        return Math.Max(12, Math.Min(anchorGap * 0.45, lineHeight * 2.25));
    }
}
