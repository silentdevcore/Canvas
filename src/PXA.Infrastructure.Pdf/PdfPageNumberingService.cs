using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfPageNumberingService : IPageNumberingService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfPageNumberingService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddPageNumbers();
            return;
        }

        if (options is Canvas.Pdf.PdfPageNumberOptions pageNumberOptions)
        {
            document.AddPageNumbers(pageNumberOptions);
            return;
        }

        throw new ArgumentException("Options must be Canvas.Pdf.PdfPageNumberOptions when provided.", nameof(options));
    }
}
