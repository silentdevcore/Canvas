using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    public byte[] Render(object documentModel)
    {
        if (documentModel is not PXA.Pdf.PdfDocument pdfDocument)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfDocumentRenderer.", nameof(documentModel));
        }

        return pdfDocument.ToBytes();
    }
}
