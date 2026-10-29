using PVAD.Importer.Analysis;
using PVAD.Importer.Graphics;

namespace PVAD.Importer.Redaction;

/// <summary>
/// Narrows an element's bounding box by the clipping paths in force when it was painted.
///
/// A clip can only ever shrink what is actually drawn. Classifying coverage from the raw
/// bounding box therefore reports content as covered that the page never paints, which
/// over-redacts, and reports a partial overlap where the visible part is fully contained,
/// which blocks a redaction that is in fact safe.
/// </summary>
internal static class PdfRedactionVisibility
{
    public static PdfRectangle EffectiveBounds(
        PdfGraphicsElement element,
        BoundingBoxCalculator bounds,
        IReadOnlyList<PdfGroupElement> ancestors,
        out bool isClipped)
    {
        var elementBounds = element.Bounds ?? bounds.ComputeElementBounds(element);
        isClipped = false;

        foreach (var clip in EnumerateClippingPaths(element, ancestors))
        {
            if (ComputeClipBounds(clip) is not { } area)
            {
                continue;
            }

            isClipped = true;
            elementBounds = Intersect(elementBounds, area);
        }

        return elementBounds;
    }

    private static IEnumerable<PdfClippingPath> EnumerateClippingPaths(
        PdfGraphicsElement element,
        IReadOnlyList<PdfGroupElement> ancestors)
    {
        if (element.ClippingPath is { } own)
        {
            yield return own;
        }

        foreach (var ancestor in ancestors)
        {
            if (ancestor.ClippingPath is { } inherited)
            {
                yield return inherited;
            }
        }
    }

    private static PdfRectangle? ComputeClipBounds(PdfClippingPath clip)
    {
        double left = double.MaxValue, bottom = double.MaxValue;
        double right = double.MinValue, top = double.MinValue;
        var any = false;

        void Include(PdfPoint point)
        {
            any = true;
            left = Math.Min(left, point.X);
            right = Math.Max(right, point.X);
            bottom = Math.Min(bottom, point.Y);
            top = Math.Max(top, point.Y);
        }

        foreach (var segment in clip.Segments)
        {
            switch (segment)
            {
                case MoveToSegment move:
                    Include(move.Point);
                    break;
                case LineToSegment line:
                    Include(line.Point);
                    break;
                case CurveToSegment curve:
                    // The control-point hull bounds the curve, so the clip is never reported
                    // as tighter than it really is.
                    Include(curve.Control1);
                    Include(curve.Control2);
                    Include(curve.End);
                    break;
                case RectangleSegment rectangle:
                    Include(new PdfPoint(rectangle.Rectangle.Left, rectangle.Rectangle.Bottom));
                    Include(new PdfPoint(rectangle.Rectangle.Right, rectangle.Rectangle.Top));
                    break;
            }
        }

        return any ? new PdfRectangle(left, bottom, right - left, top - bottom) : null;
    }

    private static PdfRectangle Intersect(PdfRectangle first, PdfRectangle second)
    {
        var left = Math.Max(first.Left, second.Left);
        var bottom = Math.Max(first.Bottom, second.Bottom);
        var right = Math.Min(first.Right, second.Right);
        var top = Math.Min(first.Top, second.Top);
        return right <= left || top <= bottom
            ? new PdfRectangle(left, bottom, 0, 0)
            : new PdfRectangle(left, bottom, right - left, top - bottom);
    }
}
