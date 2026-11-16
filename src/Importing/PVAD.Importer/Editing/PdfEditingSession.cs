using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Licensing;

namespace PVAD.Importer.Editing;

public sealed class PdfEditingSession
{
    public PdfEditingSession(PdfDocumentModel document)
    {
        Document = document;
    }

    public PdfDocumentModel Document { get; }

    public void Insert(PdfPageModel page, PdfGraphicsElement element)
    {
        PvadLicense.Require(PvadLicenseProducts.Importer);
        page.Insert(element);
    }

    public void ReplaceText(PdfTextElement element, string newText)
    {
        PvadLicense.Require(PvadLicenseProducts.Importer);
        element.Text = newText;
    }

    public void Move(PdfGraphicsElement element, PdfMatrix transform)
    {
        PvadLicense.Require(PvadLicenseProducts.Importer);
        element.Transform = element.Transform.Multiply(transform);
    }

    public void Delete(PdfGraphicsElement element)
    {
        PvadLicense.Require(PvadLicenseProducts.Importer);
        element.IsDeleted = true;
    }

    public void SetMetadata(string key, PdfObject value)
    {
        PvadLicense.Require(PvadLicenseProducts.Importer);
        Document.Metadata[key] = value;
    }
}
