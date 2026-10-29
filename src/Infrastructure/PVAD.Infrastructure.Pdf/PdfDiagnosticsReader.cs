using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

public sealed class PdfDiagnosticsReader : IDiagnosticsReader
{
    public object? Read(object documentModel)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfDiagnosticsReader.", nameof(documentModel));
        }

        return document.LastDiagnostics;
    }
}
