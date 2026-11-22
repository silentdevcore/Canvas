using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Adapts shared page-numbering requests to the native PDF engine.</summary>
public sealed class PdfPageNumberingService : IPageNumberingService
{
    /// <summary>Stamps page numbers onto the document using the requested format and placement.</summary>
    /// <param name="documentModel">The <see cref="PVAD.Pdf.PdfDocument"/> to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfPageNumberOptions"/>, or null for engine defaults.</param>
    /// <exception cref="ArgumentException">The model or non-null options have the wrong type.</exception>
    /// <remarks>Underlying PDF drawing operations retain their native license checks.</remarks>
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
