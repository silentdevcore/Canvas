using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfWatermarkService : IWatermarkService
{
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
