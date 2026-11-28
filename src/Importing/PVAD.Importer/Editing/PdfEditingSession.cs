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
        PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "document.edit");
        page.Insert(element);
    }

    public void ReplaceText(PdfTextElement element, string newText)
    {
        PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "document.edit");
        element.Text = newText;
    }

    public void Move(PdfGraphicsElement element, PdfMatrix transform)
    {
        PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "document.edit");
        element.Transform = element.Transform.Multiply(transform);
    }

    public void Delete(PdfGraphicsElement element)
    {
        PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "document.edit");
        element.IsDeleted = true;
    }

    public void SetMetadata(string key, PdfObject value)
    {
        PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "document.edit");
        Document.Metadata[key] = value;
    }
}
