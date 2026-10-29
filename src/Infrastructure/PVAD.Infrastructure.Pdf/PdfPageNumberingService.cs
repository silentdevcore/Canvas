using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfPageNumberingService : IPageNumberingService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfPageNumberingService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddPageNumbers();
            return;
        }

        if (options is PVAD.Pdf.PdfPageNumberOptions pageNumberOptions)
        {
            document.AddPageNumbers(pageNumberOptions);
            return;
        }

        throw new ArgumentException("Options must be PVAD.Pdf.PdfPageNumberOptions when provided.", nameof(options));
    }
}
