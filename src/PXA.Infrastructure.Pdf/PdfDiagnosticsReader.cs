namespace PXA.Infrastructure.Pdf;

/// <summary>
/// Power Dox Automation facade for reading PDF generation diagnostics.
/// </summary>
public sealed class PdfDiagnosticsReader
{
    private readonly Canvas.Infrastructure.Pdf.PdfDiagnosticsReader inner = new();

    public object? Read(object documentModel) => inner.Read(documentModel);
}
