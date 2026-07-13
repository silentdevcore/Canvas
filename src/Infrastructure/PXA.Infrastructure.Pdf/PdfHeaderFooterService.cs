using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfHeaderFooterService : IHeaderFooterService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfHeaderFooterService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddHeadersAndFooters();
            return;
        }

        if (options is PXA.Pdf.PdfHeaderFooterOptions headerFooterOptions)
        {
            document.AddHeadersAndFooters(headerFooterOptions);
            return;
        }

        throw new ArgumentException("Options must be PXA.Pdf.PdfHeaderFooterOptions when provided.", nameof(options));
    }
}
