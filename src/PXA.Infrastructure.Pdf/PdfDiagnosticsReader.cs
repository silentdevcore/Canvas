using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfDiagnosticsReader : IDiagnosticsReader
{
    public object? Read(object documentModel)
    {
        if (documentModel is not Canvas.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be Canvas.Pdf.PdfDocument for PdfDiagnosticsReader.", nameof(documentModel));
        }

        return document.LastDiagnostics;
    }
}
