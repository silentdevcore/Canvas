using Canvas.Importer.Content;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Analysis;

public enum PrimitiveKind
{
    Text,
    Path,
    Shape,
    Image,
    Shading,
    Group
}

public enum PrimitiveClassification
{
    Unknown,
    Text,
    VectorIcon,
    SymbolFontIcon,
    Image,
    Barcode,
    LinearBarcode,
    MatrixBarcode,
    Separator,
    Decoration,
    TableLine,
    TableCell,
    Button,
    Label,
    Value,
    Figure
}

public sealed record PrimitiveGraphicsStateSnapshot(
    PdfColor FillColor,
    PdfColor StrokeColor,
    double LineWidth,
    PdfClippingPath? ClipPath);

public abstract class PrimitiveObject
{
    protected PrimitiveObject(
        PrimitiveKind kind,
        PdfGraphicsElement sourceElement,
        PdfRectangle bounds,
        PrimitiveGraphicsStateSnapshot graphicsState)
    {
        Kind = kind;
        SourceElement = sourceElement;
        SourceOperator = sourceElement.SourceCommand;
        Transform = sourceElement.Transform;
        ZOrder = sourceElement.ZOrder;
        Bounds = bounds;
        GraphicsState = graphicsState;
        Classification = PrimitiveClassification.Unknown;
    }

    public PrimitiveKind Kind { get; }
    public PdfGraphicsElement SourceElement { get; }
    public PdfContentCommand SourceOperator { get; }
    public PdfMatrix Transform { get; set; }
    public int ZOrder { get; set; }
    public PdfRectangle Bounds { get; set; }
    public PrimitiveGraphicsStateSnapshot GraphicsState { get; }
    public PrimitiveClassification Classification { get; set; }
    public PdfObjectId? OriginalObjectReference => SourceElement.SourceCommand.Operands.OfType<PdfReference>().FirstOrDefault()?.Id;
    public string? ResourceName { get; protected set; }
    public List<PrimitiveObject> Children { get; } = [];
}

public sealed class PrimitiveText : PrimitiveObject
{
    public PrimitiveText(PdfTextElement source, PdfRectangle bounds, TextGeometry geometry, PrimitiveGraphicsStateSnapshot graphicsState)
        : base(PrimitiveKind.Text, source, bounds, graphicsState)
    {
        Text = source.Text;
        Geometry = geometry;
        FontName = source.FontName;
        FontResourceName = source.FontResourceName;
        FontSize = source.FontSize;
        Bold = source.Bold;
        Italic = source.Italic;
        Classification = PrimitiveClassification.Text;
    }

    public string Text { get; set; }
    public TextGeometry Geometry { get; set; }
    public string? FontName { get; }
    public string? FontResourceName { get; }
    public double FontSize { get; }
    public bool Bold { get; }
    public bool Italic { get; }
}

public sealed class PrimitivePath : PrimitiveObject
{
    public PrimitivePath(PdfPathElement source, PdfRectangle bounds, PrimitiveGraphicsStateSnapshot graphicsState)
        : base(PrimitiveKind.Path, source, bounds, graphicsState)
    {
        Segments = source.Segments;
    }

    public IReadOnlyList<PdfPathSegment> Segments { get; }
}

public sealed class PrimitiveShape : PrimitiveObject
{
    public PrimitiveShape(PdfPathElement source, PdfRectangle bounds, PrimitiveGraphicsStateSnapshot graphicsState)
        : base(PrimitiveKind.Shape, source, bounds, graphicsState)
    {
        Segments = source.Segments;
    }

    public IReadOnlyList<PdfPathSegment> Segments { get; }
}

public sealed class PrimitiveImage : PrimitiveObject
{
    public PrimitiveImage(PdfImageElement source, PdfRectangle bounds, PrimitiveGraphicsStateSnapshot graphicsState)
        : base(PrimitiveKind.Image, source, bounds, graphicsState)
    {
        ResourceName = source.ResourceName;
        ImageBytes = source.ImageBytes;
        Classification = PrimitiveClassification.Image;
    }

    public ReadOnlyMemory<byte> ImageBytes { get; }
}

public sealed class PrimitiveGroup : PrimitiveObject
{
    public PrimitiveGroup(PdfGraphicsElement source, PdfRectangle bounds, PrimitiveGraphicsStateSnapshot graphicsState)
        : base(PrimitiveKind.Group, source, bounds, graphicsState)
    {
    }

    public string? Name { get; set; }
}

public sealed record PrimitiveLayer(string Name, IReadOnlyList<PrimitiveObject> Objects);

public sealed class PrimitivePage
{
    public PrimitivePage(int pageIndex, IReadOnlyList<PrimitiveObject> primitives)
    {
        PageIndex = pageIndex;
        Primitives = primitives;
    }

    public int PageIndex { get; }
    public IReadOnlyList<PrimitiveObject> Primitives { get; }
}
