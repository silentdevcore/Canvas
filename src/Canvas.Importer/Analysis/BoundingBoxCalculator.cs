using Canvas.Importer.Graphics;

namespace Canvas.Importer.Analysis;

public sealed class BoundingBoxCalculator
{
    public TextGeometry ComputeTextGeometry(PdfTextElement text)
    {
        var fontSize = Math.Max(text.FontSize, 1d);
        var width = Math.Max(fontSize * 0.35d, text.Text.Length * fontSize * 0.5d);
        var height = fontSize;
        var localBounds = new PdfRectangle(0, -height * 0.2d, width, height);
        var bounds = MatrixEngine.TransformBounds(localBounds, text.Transform);
        bounds = ApplyClip(bounds, text.ClippingPath, text.Transform);
        var baseline = MatrixEngine.TransformVector(new PdfVector(1, 0), text.Transform).Normalize();
        var rotation = MatrixEngine.ExtractRotationDegrees(text.Transform);
        text.Bounds = bounds;
        return new TextGeometry(bounds, rotation, baseline);
    }

    public PdfRectangle ComputePathBounds(PdfPathElement path)
    {
        var bounds = ComputeLocalPathBounds(path.Segments);
        bounds = MatrixEngine.TransformBounds(bounds, path.Transform);
        bounds = ApplyClip(bounds, path.ClippingPath, path.Transform);
        path.Bounds = bounds;
        return bounds;
    }

    public PdfRectangle ComputeImageBounds(PdfImageElement image)
    {
        var bounds = MatrixEngine.TransformBounds(new PdfRectangle(0, 0, 1, 1), image.Transform);
        bounds = ApplyClip(bounds, image.ClippingPath, image.Transform);
        image.Bounds = bounds;
        return bounds;
    }

    public PdfRectangle ComputeGroupBounds(PdfGroupElement group)
    {
        PdfRectangle? bounds = null;
        foreach (var child in group.Children)
        {
            var childBounds = ComputeElementBounds(child);
            bounds = bounds is null ? childBounds : bounds.Value.Union(childBounds);
        }

        var resolved = bounds ?? MatrixEngine.TransformBounds(new PdfRectangle(0, 0, 0, 0), group.Transform);
        group.Bounds = resolved;
        return resolved;
    }

    public PdfRectangle ComputeElementBounds(PdfGraphicsElement element)
    {
        return element switch
        {
            PdfTextElement text => ComputeTextGeometry(text).Bounds,
            PdfPathElement path => ComputePathBounds(path),
            PdfImageElement image => ComputeImageBounds(image),
            PdfGroupElement group => ComputeGroupBounds(group),
            _ => MatrixEngine.TransformBounds(new PdfRectangle(0, 0, 1, 1), element.Transform)
        };
    }

    private static PdfRectangle ComputeLocalPathBounds(IReadOnlyList<PdfPathSegment> segments)
    {
        var points = new List<PdfPoint>();
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case MoveToSegment move:
                    points.Add(move.Point);
                    break;
                case LineToSegment line:
                    points.Add(line.Point);
                    break;
                case CurveToSegment curve:
                    points.Add(curve.Control1);
                    points.Add(curve.Control2);
                    points.Add(curve.End);
                    break;
                case RectangleSegment rectangle:
                    points.Add(new PdfPoint(rectangle.Rectangle.Left, rectangle.Rectangle.Bottom));
                    points.Add(new PdfPoint(rectangle.Rectangle.Right, rectangle.Rectangle.Top));
                    break;
            }
        }

        if (points.Count == 0)
        {
            return new PdfRectangle(0, 0, 0, 0);
        }

        var left = points.Min(point => point.X);
        var right = points.Max(point => point.X);
        var bottom = points.Min(point => point.Y);
        var top = points.Max(point => point.Y);
        return new PdfRectangle(left, bottom, right - left, top - bottom);
    }

    private static PdfRectangle ApplyClip(PdfRectangle bounds, PdfClippingPath? clip, PdfMatrix transform)
    {
        if (clip is null)
        {
            return bounds;
        }

        var clipBounds = MatrixEngine.TransformBounds(ComputeLocalPathBounds(clip.Segments), transform);
        return bounds.Intersect(clipBounds) ?? bounds;
    }
}
