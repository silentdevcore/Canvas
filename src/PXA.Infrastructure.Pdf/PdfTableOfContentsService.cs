using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfTableOfContentsService : ITableOfContentsService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfTableOfContentsService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddTableOfContents();
            return;
        }

        if (options is PXA.Pdf.PdfTableOfContentsOptions tocOptions)
        {
            document.AddTableOfContents(tocOptions);
            return;
        }

        throw new ArgumentException("Options must be PXA.Pdf.PdfTableOfContentsOptions when provided.", nameof(options));
    }
}
