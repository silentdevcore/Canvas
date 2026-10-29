using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    public byte[] Render(object documentModel)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument pdfDocument)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfDocumentRenderer.", nameof(documentModel));
        }

        return pdfDocument.ToBytes();
    }
}
