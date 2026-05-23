using Canvas.Core.Abstractions;

namespace Canvas.Infrastructure.Pdf;

public sealed class PdfTableOfContentsService : ITableOfContentsService
{
    public void Apply(object documentModel, object? options = null)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfTableOfContentsService.", nameof(documentModel));
        }

        if (options is null)
        {
            document.AddTableOfContents();
            return;
        }

        if (options is Canvas.Pdf.PdfTableOfContentsOptions tocOptions)
        {
            document.AddTableOfContents(tocOptions);
            return;
        }

        throw new ArgumentException("Options must be Canvas.Pdf.PdfTableOfContentsOptions when provided.", nameof(options));
    }
}
