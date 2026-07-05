namespace PXA.Infrastructure.Pdf;

/// <summary>
/// Power Dox Automation facade for common PDF infrastructure operations.
/// </summary>
public sealed class PdfFacade
{
    private readonly Canvas.Infrastructure.Pdf.PdfFacade inner = new();

    public void GenerateToFile(object documentModel, string outputPath) =>
        inner.GenerateToFile(documentModel, outputPath);

    public void ApplyPageNumbering(object documentModel, object? options = null) =>
        inner.ApplyPageNumbering(documentModel, options);

    public void ApplyHeaderFooter(object documentModel, object? options = null) =>
        inner.ApplyHeaderFooter(documentModel, options);

    public void ApplyWatermark(object documentModel, string text, object? options = null) =>
        inner.ApplyWatermark(documentModel, text, options);

    public void ApplyTableOfContents(object documentModel, object? options = null) =>
        inner.ApplyTableOfContents(documentModel, options);

    public object? ReadDiagnostics(object documentModel) => inner.ReadDiagnostics(documentModel);

    public IReadOnlyList<int> GetPagesWithText(object documentModel) => inner.GetPagesWithText(documentModel);

    public IReadOnlyList<int> GetPagesWithImages(object documentModel) => inner.GetPagesWithImages(documentModel);

    public IReadOnlyList<int> GetPagesWithLinks(object documentModel) => inner.GetPagesWithLinks(documentModel);

    public IReadOnlyList<int> GetPagesWithShapes(object documentModel) => inner.GetPagesWithShapes(documentModel);
}
