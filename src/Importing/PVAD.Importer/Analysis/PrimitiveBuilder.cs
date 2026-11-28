using PVAD.Importer.Graphics;
using PVAD.Licensing;

namespace PVAD.Importer.Analysis;

public sealed class PrimitiveBuilder
{
    private readonly BoundingBoxCalculator _bounds;

    public PrimitiveBuilder(BoundingBoxCalculator? bounds = null)
    {
        _bounds = bounds ?? new BoundingBoxCalculator();
    }

    public IReadOnlyList<PrimitiveObject> Build(IEnumerable<PdfGraphicsElement> elements)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.PdfAnalysis);
        return BuildAdmitted(elements);
    }

    internal IReadOnlyList<PrimitiveObject> BuildAdmitted(IEnumerable<PdfGraphicsElement> elements)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        var primitives = new List<PrimitiveObject>();
        foreach (var element in elements.Where(static element => !element.IsDeleted).OrderBy(static element => element.ZOrder))
        {
            primitives.Add(BuildPrimitive(element));
        }

        return primitives;
    }

    private PrimitiveObject BuildPrimitive(PdfGraphicsElement element)
    {
        return element switch
        {
            PdfTextElement text => BuildText(text),
            PdfPathElement path when IsShape(path) => new PrimitiveShape(path, _bounds.ComputePathBoundsAdmitted(path), Snapshot(path)),
            PdfPathElement path => new PrimitivePath(path, _bounds.ComputePathBoundsAdmitted(path), Snapshot(path)),
            PdfImageElement image => new PrimitiveImage(image, _bounds.ComputeImageBoundsAdmitted(image), Snapshot(image)),
            PdfGroupElement group => BuildGroup(group),
            _ => new PrimitiveGroup(element, _bounds.ComputeElementBoundsAdmitted(element), Snapshot(element))
        };
    }

    private PrimitiveText BuildText(PdfTextElement text)
    {
        var geometry = _bounds.ComputeTextGeometryAdmitted(text);
        return new PrimitiveText(text, geometry.Bounds, geometry, Snapshot(text));
    }

    private PrimitiveGroup BuildGroup(PdfGroupElement group)
    {
        var primitive = new PrimitiveGroup(group, _bounds.ComputeGroupBoundsAdmitted(group), Snapshot(group))
        {
            Name = group.MarkedContentTag
        };

        primitive.Children.AddRange(BuildAdmitted(group.Children));
        return primitive;
    }

    private static bool IsShape(PdfPathElement path)
    {
        return path.Segments.Count == 1 && path.Segments[0] is RectangleSegment;
    }

    private static PrimitiveGraphicsStateSnapshot Snapshot(PdfGraphicsElement element)
    {
        return element switch
        {
            PdfTextElement text => new PrimitiveGraphicsStateSnapshot(text.FillColor, text.StrokeColor, 0, text.ClippingPath),
            PdfPathElement path => new PrimitiveGraphicsStateSnapshot(path.FillColor, path.StrokeColor, path.LineWidth, path.ClippingPath),
            _ => new PrimitiveGraphicsStateSnapshot(PdfColor.Black, PdfColor.Black, 0, element.ClippingPath)
        };
    }
}
