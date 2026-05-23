using Canvas.Core.Abstractions;

namespace Canvas.Infrastructure.Pdf;

public sealed class PdfHeaderFooterService : IHeaderFooterService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfHeaderFooterService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddHeadersAndFooters();
            return;
        }

        if (options is Canvas.Pdf.PdfHeaderFooterOptions headerFooterOptions)
        {
            document.AddHeadersAndFooters(headerFooterOptions);
            return;
        }

        throw new ArgumentException("Options must be Canvas.Pdf.PdfHeaderFooterOptions when provided.", nameof(options));
    }
}
