namespace PXA.Infrastructure.Pdf;

public sealed class PdfPageNumberingService
{
    private readonly Canvas.Infrastructure.Pdf.PdfPageNumberingService inner = new();

    public void Apply(object documentModel, object? options = null) => inner.Apply(documentModel, options);
}

public sealed class PdfHeaderFooterService
{
    private readonly Canvas.Infrastructure.Pdf.PdfHeaderFooterService inner = new();

    public void Apply(object documentModel, object? options = null) => inner.Apply(documentModel, options);
}

public sealed class PdfWatermarkService
{
    private readonly Canvas.Infrastructure.Pdf.PdfWatermarkService inner = new();

    public void Apply(object documentModel, string text, object? options = null) => inner.Apply(documentModel, text, options);
}

public sealed class PdfTableOfContentsService
{
    private readonly Canvas.Infrastructure.Pdf.PdfTableOfContentsService inner = new();

    public void Apply(object documentModel, object? options = null) => inner.Apply(documentModel, options);
}

public sealed class PdfPageCoverageQueryService
{
    private readonly Canvas.Infrastructure.Pdf.PdfPageCoverageQueryService inner = new();

    public IReadOnlyList<int> GetPagesWithText(object documentModel) => inner.GetPagesWithText(documentModel);

    public IReadOnlyList<int> GetPagesWithImages(object documentModel) => inner.GetPagesWithImages(documentModel);

    public IReadOnlyList<int> GetPagesWithLinks(object documentModel) => inner.GetPagesWithLinks(documentModel);

    public IReadOnlyList<int> GetPagesWithShapes(object documentModel) => inner.GetPagesWithShapes(documentModel);
}
