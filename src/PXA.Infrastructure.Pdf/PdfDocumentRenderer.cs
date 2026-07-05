namespace PXA.Infrastructure.Pdf;

/// <summary>
/// Power Dox Automation facade for rendering PDF document models to bytes.
/// </summary>
public sealed class PdfDocumentRenderer
{
    private readonly Canvas.Infrastructure.Pdf.PdfDocumentRenderer inner = new();

    public byte[] Render(object documentModel) => inner.Render(documentModel);
}
