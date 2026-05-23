using Canvas.Core.Abstractions;

namespace Canvas.Infrastructure.Pdf;

public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    public byte[] Render(object documentModel)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument pdfDocument)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfDocumentRenderer.", nameof(documentModel));
        }

        return pdfDocument.ToBytes();
    }
}
