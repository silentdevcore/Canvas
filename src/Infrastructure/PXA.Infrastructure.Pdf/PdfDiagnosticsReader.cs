using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfDiagnosticsReader : IDiagnosticsReader
{
    public object? Read(object documentModel)
    {
        if (documentModel is not PXA.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PXA.Pdf.PdfDocument for PdfDiagnosticsReader.", nameof(documentModel));
        }

        return document.LastDiagnostics;
    }
}
