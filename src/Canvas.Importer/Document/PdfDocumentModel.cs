using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Document;

public sealed class PdfDocumentModel
{
    public PdfObjectGraph ObjectGraph { get; init; } = new();
    public PdfCatalog Catalog { get; set; } = new();
    public List<PdfPageModel> Pages { get; } = [];
    public PdfDictionary Metadata { get; init; } = new();

    public void AddPage(PdfPageModel page) => Pages.Add(page);
}

public sealed class PdfCatalog
{
    public PdfObjectId? OriginalReference { get; init; }
    public PdfDictionary Dictionary { get; init; } = new();
}

public sealed class PdfPageModel
{
    public PdfPageModel(PdfObjectId? originalReference, PdfDictionary pageDictionary)
    {
        OriginalReference = originalReference;
        PageDictionary = pageDictionary;
    }

    public PdfObjectId? OriginalReference { get; }
    public PdfDictionary PageDictionary { get; }
    public PdfDictionary Resources { get; init; } = new();
    public PdfRectangle? MediaBox { get; init; }
    public PdfRectangle? CropBox { get; init; }
    public PdfRectangle? BleedBox { get; init; }
    public PdfRectangle? TrimBox { get; init; }
    public PdfRectangle? ArtBox { get; init; }
    public int Rotate { get; init; }
    public List<PdfStreamObject> ContentStreams { get; } = [];
    public List<PdfGraphicsElement> GraphicsObjects { get; } = [];
    public IEnumerable<PdfTextElement> TextObjects => GraphicsObjects.OfType<PdfTextElement>();

    public void Insert(PdfGraphicsElement element) => GraphicsObjects.Add(element);

    public void Delete(PdfGraphicsElement element) => element.IsDeleted = true;
}
