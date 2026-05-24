using Canvas.Importer.Content;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Graphics;

public abstract class PdfGraphicsElement
{
    protected PdfGraphicsElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand)
    {
        ZOrder = zOrder;
        Transform = transform;
        SourceCommand = sourceCommand;
    }

    public int ZOrder { get; set; }
    public PdfMatrix Transform { get; set; }
    public PdfRectangle? Bounds { get; set; }
    public PdfContentCommand SourceCommand { get; }
    public bool IsDeleted { get; set; }
    public PdfClippingPath? ClippingPath { get; set; }
}

public sealed class PdfTextElement : PdfGraphicsElement
{
    public PdfTextElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand, string text)
        : base(zOrder, transform, sourceCommand)
    {
        Text = text;
    }

    public string Text { get; set; }
    public string? FontResourceName { get; set; }
    public string? FontName { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public ReadOnlyMemory<byte> EmbeddedFontBytes { get; set; }
    public string? EmbeddedFontFormat { get; set; }
    public string? EmbeddedFontMimeType { get; set; }
    public bool UsesToUnicodeMap { get; set; }
    public bool IsSubsetFont { get; set; }
    public double FontSize { get; set; }
    public PdfColor FillColor { get; set; }
    public PdfColor StrokeColor { get; set; }
}

public sealed class PdfPathElement : PdfGraphicsElement
{
    public PdfPathElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand, IReadOnlyList<PdfPathSegment> segments)
        : base(zOrder, transform, sourceCommand)
    {
        Segments = [.. segments];
    }

    public List<PdfPathSegment> Segments { get; }
    public PdfColor StrokeColor { get; set; }
    public PdfColor FillColor { get; set; }
    public double LineWidth { get; set; }
}

public sealed class PdfImageElement : PdfGraphicsElement
{
    public PdfImageElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand, string resourceName)
        : base(zOrder, transform, sourceCommand)
    {
        ResourceName = resourceName;
    }

    public string ResourceName { get; set; }
    public ReadOnlyMemory<byte> ImageBytes { get; set; }
}

public sealed class PdfShadingElement : PdfGraphicsElement
{
    public PdfShadingElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand, string resourceName)
        : base(zOrder, transform, sourceCommand)
    {
        ResourceName = resourceName;
    }

    public string ResourceName { get; set; }
}

public sealed class PdfGroupElement : PdfGraphicsElement
{
    public PdfGroupElement(int zOrder, PdfMatrix transform, PdfContentCommand sourceCommand)
        : base(zOrder, transform, sourceCommand)
    {
    }

    public bool IsCompatibilitySection { get; set; }
    public string? MarkedContentTag { get; set; }
    public PdfObject? Properties { get; set; }
    public List<PdfGraphicsElement> Children { get; } = [];
}

public abstract record PdfPathSegment;

public sealed record PdfClippingPath(IReadOnlyList<PdfPathSegment> Segments, bool UsesEvenOddRule);

public sealed record MoveToSegment(PdfPoint Point) : PdfPathSegment;

public sealed record LineToSegment(PdfPoint Point) : PdfPathSegment;

public sealed record CurveToSegment(PdfPoint Control1, PdfPoint Control2, PdfPoint End) : PdfPathSegment;

public sealed record ClosePathSegment : PdfPathSegment;

public sealed record RectangleSegment(PdfRectangle Rectangle) : PdfPathSegment;
