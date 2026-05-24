using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Analysis;

public sealed class PdfSceneGraph
{
    public List<PdfScenePage> Pages { get; } = [];
    public PdfDictionary Resources { get; init; } = new();
}

public sealed class PdfScenePage
{
    public PdfScenePage(int pageIndex, PdfRectangle? pageBounds)
    {
        PageIndex = pageIndex;
        PageBounds = pageBounds;
    }

    public int PageIndex { get; }
    public PdfRectangle? PageBounds { get; }
    public List<PdfSceneLayer> Layers { get; } = [];
    public List<VisualGroup> VisualGroups { get; } = [];
    public SemanticLayoutPage? Layout { get; set; }
    public ReadingOrderResult? ReadingOrder { get; set; }
}

public sealed class PdfSceneLayer
{
    public PdfSceneLayer(string name, IReadOnlyList<PrimitiveObject> objects)
    {
        Name = name;
        Objects = objects;
        Bounds = objects.Count == 0 ? new PdfRectangle(0, 0, 0, 0) : ReadingOrderEngine.Union(objects.Select(static item => item.Bounds));
    }

    public string Name { get; }
    public IReadOnlyList<PrimitiveObject> Objects { get; }
    public PdfRectangle Bounds { get; }
    public bool Visible { get; set; } = true;
}

public sealed class SceneGraphEngine
{
    private readonly PrimitiveBuilder _primitiveBuilder;
    private readonly ObjectClassifier _classifier;
    private readonly ReadingOrderEngine _readingOrder;
    private readonly GroupingEngine _grouping;
    private readonly SemanticLayoutEngine _layout;

    public SceneGraphEngine(
        PrimitiveBuilder? primitiveBuilder = null,
        ObjectClassifier? classifier = null,
        ReadingOrderEngine? readingOrder = null,
        GroupingEngine? grouping = null,
        SemanticLayoutEngine? layout = null)
    {
        _primitiveBuilder = primitiveBuilder ?? new PrimitiveBuilder();
        _classifier = classifier ?? new ObjectClassifier();
        _readingOrder = readingOrder ?? new ReadingOrderEngine();
        _grouping = grouping ?? new GroupingEngine();
        _layout = layout ?? new SemanticLayoutEngine();
    }

    public PdfSceneGraph Build(PdfDocumentModel document)
    {
        var graph = new PdfSceneGraph { Resources = document.Catalog.Dictionary };
        for (var i = 0; i < document.Pages.Count; i++)
        {
            graph.Pages.Add(BuildPage(i, document.Pages[i]));
        }

        return graph;
    }

    public PdfScenePage BuildPage(int pageIndex, PdfPageModel page)
    {
        var primitives = _primitiveBuilder.Build(page.GraphicsObjects).ToArray();
        _classifier.Classify(primitives);
        var readingOrder = _readingOrder.Analyze(primitives);
        var groups = _grouping.BuildGroups(primitives);
        var scenePage = new PdfScenePage(pageIndex, page.CropBox ?? page.MediaBox)
        {
            ReadingOrder = readingOrder,
            Layout = _layout.BuildPage(pageIndex, primitives, readingOrder, groups, page.CropBox ?? page.MediaBox)
        };

        scenePage.Layers.Add(new PdfSceneLayer("Content", primitives));
        scenePage.VisualGroups.AddRange(groups);
        return scenePage;
    }
}
