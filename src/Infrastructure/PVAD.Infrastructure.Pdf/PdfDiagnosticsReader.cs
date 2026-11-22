using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Reads an existing native document's last generation diagnostics without rendering it.</summary>
public sealed class PdfDiagnosticsReader : IDiagnosticsReader
{
    /// <summary>Returns the stored diagnostics snapshot without starting a licensed processing operation.</summary>
    /// <param name="documentModel">The <see cref="PVAD.Pdf.PdfDocument"/> to inspect.</param>
    /// <returns>The last generation diagnostics, or null before a snapshot is available.</returns>
    /// <exception cref="ArgumentException">The model is not a native PDF document.</exception>
    public object? Read(object documentModel)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument document)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfDiagnosticsReader.", nameof(documentModel));
        }

        return document.LastDiagnostics;
    }
}
