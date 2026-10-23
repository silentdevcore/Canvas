using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfHeaderFooterService : IHeaderFooterService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfHeaderFooterService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddHeadersAndFooters();
            return;
        }

        if (options is PVAD.Pdf.PdfHeaderFooterOptions headerFooterOptions)
        {
            document.AddHeadersAndFooters(headerFooterOptions);
            return;
        }

        throw new ArgumentException("Options must be PVAD.Pdf.PdfHeaderFooterOptions when provided.", nameof(options));
    }
}
