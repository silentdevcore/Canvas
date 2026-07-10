using PXA.Importer.Content;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;

namespace PXA.Importer.Analysis;

public sealed record ResolvedXObject(
    string ResourceName,
    string Subtype,
    PdfMatrix Transform,
    PdfRectangle Bounds,
    IReadOnlyList<PdfGraphicsElement> Children,
    PdfStreamObject SourceStream);

public sealed class XObjectResolver
{
    private readonly PdfObjectResolver _resolver;
    private readonly PdfContentStreamParser _contentParser;
    private readonly PdfGraphicsInterpreter _graphicsInterpreter;
    private readonly BoundingBoxCalculator _bounds;

    public XObjectResolver(
        PdfObjectResolver resolver,
        PdfContentStreamParser? contentParser = null,
        PdfGraphicsInterpreter? graphicsInterpreter = null,
        BoundingBoxCalculator? bounds = null)
    {
        _resolver = resolver;
        _contentParser = contentParser ?? new PdfContentStreamParser();
        _graphicsInterpreter = graphicsInterpreter ?? new PdfGraphicsInterpreter();
        _bounds = bounds ?? new BoundingBoxCalculator();
    }

    public ResolvedXObject? Resolve(string resourceName, PdfDictionary resources, PdfMatrix inheritedTransform, int recursionDepth = 0)
    {
        if (recursionDepth > 16 ||
            resources["XObject"] is not PdfDictionary xobjects ||
            xobjects[resourceName] is not { } xobjectValue ||
            _resolver.Resolve(xobjectValue) is not PdfStreamObject stream ||
            stream.Dictionary["Subtype"] is not PdfName subtype)
        {
            return null;
        }

        return subtype.Value switch
        {
            "Form" => ResolveForm(resourceName, stream, inheritedTransform, recursionDepth),
            "Image" => ResolveImage(resourceName, stream, inheritedTransform),
            _ => null
        };
    }

    public ResolvedXObject ResolveForm(string resourceName, PdfStreamObject form, PdfMatrix inheritedTransform, int recursionDepth = 0)
    {
        var matrix = ReadMatrix(form.Dictionary["Matrix"]) ?? PdfMatrix.Identity;
        var transform = inheritedTransform.Multiply(matrix);
        var commands = _contentParser.Parse(form.IsDecoded ? form.DecodedBytes : form.EncodedBytes);
        var children = _graphicsInterpreter.Interpret(commands);
        var bounds = children.Count == 0
            ? MatrixEngine.TransformBounds(ReadRectangle(form.Dictionary["BBox"]) ?? new PdfRectangle(0, 0, 0, 0), transform)
            : ReadingOrderEngine.Union(children.Select(child => _bounds.ComputeElementBounds(child)));

        return new ResolvedXObject(resourceName, "Form", transform, bounds, children, form);
    }

    public ResolvedXObject ResolveImage(string resourceName, PdfStreamObject image, PdfMatrix inheritedTransform)
    {
        var bounds = MatrixEngine.TransformBounds(new PdfRectangle(0, 0, 1, 1), inheritedTransform);
        return new ResolvedXObject(resourceName, "Image", inheritedTransform, bounds, [], image);
    }

    private static PdfMatrix? ReadMatrix(PdfObject? value)
    {
        if (value is not PdfArray { Items.Count: >= 6 } array)
        {
            return null;
        }

        return new PdfMatrix(Number(array.Items[0]), Number(array.Items[1]), Number(array.Items[2]), Number(array.Items[3]), Number(array.Items[4]), Number(array.Items[5]));
    }

    private static PdfRectangle? ReadRectangle(PdfObject? value)
    {
        if (value is not PdfArray { Items.Count: >= 4 } array)
        {
            return null;
        }

        var x1 = Number(array.Items[0]);
        var y1 = Number(array.Items[1]);
        var x2 = Number(array.Items[2]);
        var y2 = Number(array.Items[3]);
        return new PdfRectangle(x1, y1, x2 - x1, y2 - y1);
    }

    private static double Number(PdfObject value)
    {
        return value switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => 0
        };
    }
}
