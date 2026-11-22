using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Adapts a native PDF document to the shared byte-rendering contract.</summary>
public sealed class PdfDocumentRenderer : IDocumentRenderer
{
    /// <summary>Serializes the native document through its licensed PDF writer.</summary>
    /// <param name="documentModel">A <see cref="PVAD.Pdf.PdfDocument"/> instance.</param>
    /// <returns>The complete PDF file bytes.</returns>
    /// <exception cref="ArgumentException">The model is not a native PDF document.</exception>
    /// <exception cref="PVAD.Licensing.PvadLicenseException">Native PDF rights are missing or no longer valid.</exception>
    public byte[] Render(object documentModel)
    {
        if (documentModel is not PVAD.Pdf.PdfDocument pdfDocument)
        {
            throw new ArgumentException("Document model must be PVAD.Pdf.PdfDocument for PdfDocumentRenderer.", nameof(documentModel));
        }

        return pdfDocument.ToBytes();
    }
}
