using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Applies native PDF headers and footers through the shared rendering abstraction.</summary>
public sealed class PdfHeaderFooterService : IHeaderFooterService
{
    /// <summary>Adds configured header/footer text to the native document.</summary>
    /// <param name="documentModel">The <see cref="PVAD.Pdf.PdfDocument"/> to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfHeaderFooterOptions"/>, or null for defaults.</param>
    /// <exception cref="ArgumentException">The model or non-null options have the wrong type.</exception>
    /// <remarks>Underlying PDF drawing operations retain their native license checks.</remarks>
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
