using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Editing;

public sealed class PdfEditingSession
{
    public PdfEditingSession(PdfDocumentModel document)
    {
        Document = document;
    }

    public PdfDocumentModel Document { get; }

    public void Insert(PdfPageModel page, PdfGraphicsElement element) => page.Insert(element);

    public void ReplaceText(PdfTextElement element, string newText) => element.Text = newText;

    public void Move(PdfGraphicsElement element, PdfMatrix transform) => element.Transform = element.Transform.Multiply(transform);

    public void Delete(PdfGraphicsElement element) => element.IsDeleted = true;

    public void SetMetadata(string key, PdfObject value) => Document.Metadata[key] = value;
}
