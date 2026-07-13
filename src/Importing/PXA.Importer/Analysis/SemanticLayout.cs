using PXA.Importer.Graphics;

namespace PXA.Importer.Analysis;

public enum SemanticLayoutKind
{
    Page,
    Header,
    Footer,
    Paragraph,
    Table,
    TableCell,
    Figure,
    List,
    Label,
    FormField,
    Decoration
}

public class SemanticLayoutNode
{
    public SemanticLayoutNode(SemanticLayoutKind kind, PdfRectangle bounds)
    {
        Kind = kind;
        Bounds = bounds;
    }

    public SemanticLayoutKind Kind { get; set; }
    public PdfRectangle Bounds { get; set; }
    public string? Text { get; set; }
    public List<PrimitiveObject> Primitives { get; } = [];
    public List<SemanticLayoutNode> Children { get; } = [];
}

public sealed class SemanticLayoutPage : SemanticLayoutNode
{
    public SemanticLayoutPage(int pageIndex, PdfRectangle bounds)
        : base(SemanticLayoutKind.Page, bounds)
    {
        PageIndex = pageIndex;
    }

    public int PageIndex { get; }
}

public sealed class SemanticLayoutEngine
{
    private readonly TextReconstructionEngine _textReconstruction = new();

    public SemanticLayoutPage BuildPage(
        int pageIndex,
        IReadOnlyList<PrimitiveObject> primitives,
        ReadingOrderResult readingOrder,
        IReadOnlyList<VisualGroup> groups,
        PdfRectangle? pageBounds)
    {
        var bounds = pageBounds ?? (primitives.Count == 0 ? new PdfRectangle(0, 0, 0, 0) : ReadingOrderEngine.Union(primitives.Select(static primitive => primitive.Bounds)));
        var page = new SemanticLayoutPage(pageIndex, bounds);

        foreach (var paragraph in readingOrder.Paragraphs)
        {
            page.Children.Add(BuildParagraph(paragraph, bounds));
        }

        foreach (var group in groups.Where(static group => group.Kind is "LabelValue" or "Contained" or "IconText"))
        {
            page.Children.Add(BuildGroupNode(group));
        }

        foreach (var figure in primitives.Where(static primitive => primitive.Classification is PrimitiveClassification.Image or PrimitiveClassification.VectorIcon or PrimitiveClassification.MatrixBarcode or PrimitiveClassification.LinearBarcode))
        {
            page.Children.Add(new SemanticLayoutNode(SemanticLayoutKind.Figure, figure.Bounds)
            {
                Primitives = { figure }
            });
        }

        return page;
    }

    private SemanticLayoutNode BuildParagraph(ReadingParagraph paragraph, PdfRectangle pageBounds)
    {
        var kind = SemanticLayoutKind.Paragraph;
        if (paragraph.Bounds.Top > pageBounds.Top - pageBounds.Height * 0.15d)
        {
            kind = SemanticLayoutKind.Header;
        }
        else if (paragraph.Bounds.Bottom < pageBounds.Bottom + pageBounds.Height * 0.15d)
        {
            kind = SemanticLayoutKind.Footer;
        }

        var node = new SemanticLayoutNode(kind, paragraph.Bounds)
        {
            Text = _textReconstruction.BuildParagraphText(paragraph)
        };

        node.Primitives.AddRange(paragraph.Lines.SelectMany(static line => line.Texts).Cast<PrimitiveObject>());
        return node;
    }

    private static SemanticLayoutNode BuildGroupNode(VisualGroup group)
    {
        var kind = group.Kind switch
        {
            "LabelValue" => SemanticLayoutKind.Label,
            "Contained" => SemanticLayoutKind.FormField,
            _ => SemanticLayoutKind.Figure
        };

        var node = new SemanticLayoutNode(kind, group.Bounds);
        node.Primitives.AddRange(group.Objects);
        return node;
    }
}
