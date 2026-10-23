using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;

namespace PVAD.Importer.Editing;

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
