using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfWatermarkService : IWatermarkService
{
    public void Apply(object documentModel, string text, object? options = null)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfWatermarkService.", nameof(documentModel));
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

        if (options is PXA.Pdf.PdfWatermarkOptions watermarkOptions)
        {
            document.AddTextWatermark(text, watermarkOptions);
            return;
        }

        throw new ArgumentException("Options must be PXA.Pdf.PdfWatermarkOptions when provided.", nameof(options));
    }
}
