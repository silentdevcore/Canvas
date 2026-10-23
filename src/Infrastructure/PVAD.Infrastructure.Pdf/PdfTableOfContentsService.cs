using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfTableOfContentsService : ITableOfContentsService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfTableOfContentsService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddTableOfContents();
            return;
        }

        if (options is PVAD.Pdf.PdfTableOfContentsOptions tocOptions)
        {
            document.AddTableOfContents(tocOptions);
            return;
        }

        throw new ArgumentException("Options must be PVAD.Pdf.PdfTableOfContentsOptions when provided.", nameof(options));
    }
}
