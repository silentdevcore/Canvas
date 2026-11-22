using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Builds native table-of-contents pages from the document's outline entries.</summary>
public sealed class PdfTableOfContentsService : ITableOfContentsService
{
    /// <summary>Adds table-of-contents pages using native placement and numbering options.</summary>
    /// <param name="documentModel">The <see cref="PVAD.Pdf.PdfDocument"/> to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfTableOfContentsOptions"/>, or null for defaults.</param>
    /// <exception cref="ArgumentException">The model or non-null options have the wrong type.</exception>
    /// <remarks>Page creation and drawing retain their native PDF license checks.</remarks>
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
