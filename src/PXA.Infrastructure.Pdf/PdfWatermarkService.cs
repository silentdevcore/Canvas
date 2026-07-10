using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfWatermarkService : IWatermarkService
{
    public void Apply(object documentModel, string text, object? options = null)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfWatermarkService.", nameof(documentModel));
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

        if (options is Canvas.Pdf.PdfWatermarkOptions watermarkOptions)
        {
            document.AddTextWatermark(text, watermarkOptions);
            return;
        }

        throw new ArgumentException("Options must be Canvas.Pdf.PdfWatermarkOptions when provided.", nameof(options));
    }
}
