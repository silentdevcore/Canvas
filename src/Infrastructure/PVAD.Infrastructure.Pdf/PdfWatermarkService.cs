using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Adapts text-watermark requests to the native PDF engine.</summary>
public sealed class PdfWatermarkService : IWatermarkService
{
    /// <summary>Stamps the specified nonblank text onto the native document.</summary>
    /// <param name="documentModel">The <see cref="PVAD.Pdf.PdfDocument"/> to modify.</param>
    /// <param name="text">Nonblank watermark text.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfWatermarkOptions"/>, or null for defaults.</param>
    /// <exception cref="ArgumentException">The model/options have the wrong type, or the text is blank.</exception>
    /// <remarks>Underlying PDF drawing operations retain their native license checks.</remarks>
    public void Apply(object documentModel, string text, object? options = null)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfWatermarkService.", nameof(documentModel));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Watermark text cannot be null or empty.", nameof(text));
        }

        if (options is null)
        {
            document.AddTextWatermark(text);
            return;
        }

        if (options is PVAD.Pdf.PdfWatermarkOptions watermarkOptions)
        {
            document.AddTextWatermark(text, watermarkOptions);
            return;
        }

        throw new ArgumentException("Options must be PVAD.Pdf.PdfWatermarkOptions when provided.", nameof(options));
    }
}
