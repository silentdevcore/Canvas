using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfPageNumberingService : IPageNumberingService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfPageNumberingService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddPageNumbers();
            return;
        }

        if (options is PXA.Pdf.PdfPageNumberOptions pageNumberOptions)
        {
            document.AddPageNumbers(pageNumberOptions);
            return;
        }

        throw new ArgumentException("Options must be PXA.Pdf.PdfPageNumberOptions when provided.", nameof(options));
    }
}
