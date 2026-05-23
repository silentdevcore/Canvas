namespace Canvas.Infrastructure.Pdf;

public sealed class PdfFacade
{
    private readonly PdfDocumentRenderer _renderer;
    private readonly FileOutputWriter _outputWriter;
    private readonly PdfPageNumberingService _pageNumbering;
    private readonly PdfHeaderFooterService _headerFooter;
    private readonly PdfWatermarkService _watermark;
    private readonly PdfTableOfContentsService _tableOfContents;
    private readonly PdfDiagnosticsReader _diagnostics;
    private readonly PdfPageCoverageQueryService _pageCoverage;

    public PdfFacade()
    {
        _renderer = new PdfDocumentRenderer();
        _outputWriter = new FileOutputWriter();
        _pageNumbering = new PdfPageNumberingService();
        _headerFooter = new PdfHeaderFooterService();
        _watermark = new PdfWatermarkService();
        _tableOfContents = new PdfTableOfContentsService();
        _diagnostics = new PdfDiagnosticsReader();
        _pageCoverage = new PdfPageCoverageQueryService();
    }

    public void GenerateToFile(object documentModel, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }

        _outputWriter.Write(outputPath, _renderer.Render(documentModel));
    }

    public void ApplyPageNumbering(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _pageNumbering.Apply(documentModel, options);
    }

    public void ApplyHeaderFooter(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _headerFooter.Apply(documentModel, options);
    }

    public void ApplyWatermark(object documentModel, string text, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Watermark text cannot be null or empty.", nameof(text));
        }

        _watermark.Apply(documentModel, text, options);
    }

    public void ApplyTableOfContents(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _tableOfContents.Apply(documentModel, options);
    }

    public object? ReadDiagnostics(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _diagnostics.Read(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithText(object documentModel)
    {
        return _pageCoverage.GetPagesWithText(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithImages(object documentModel)
    {
        return _pageCoverage.GetPagesWithImages(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
    {
        return _pageCoverage.GetPagesWithLinks(documentModel);
    }

    public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
    {
        return _pageCoverage.GetPagesWithShapes(documentModel);
    }
}
