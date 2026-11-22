namespace PVAD.Infrastructure.Pdf;

/// <summary>Combines PDF rendering, file output, page decoration and in-memory document inspection.</summary>
/// <remarks>Supply a <see cref="PVAD.Pdf.PdfDocument"/>, not Designer JSON. Native PDF rights are
/// checked by the underlying processing operations; constructing this facade does not activate a license.</remarks>
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

    /// <summary>Creates the built-in PDF service adapters without loading a license or writing files.</summary>
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

    /// <summary>Renders a PDF document and writes its bytes to the requested local file.</summary>
    /// <param name="documentModel">The native PDF document to render.</param>
    /// <param name="outputPath">Destination path; an existing file is overwritten after successful rendering.</param>
    /// <exception cref="ArgumentException">The model is not a native PDF document or the path is blank.</exception>
    /// <exception cref="PVAD.Licensing.PvadLicenseException">The native PDF operation is not licensed.</exception>
    public void GenerateToFile(object documentModel, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        }

        _outputWriter.Write(outputPath, _renderer.Render(documentModel));
    }

    /// <summary>Stamps page numbers using native PDF numbering options.</summary>
    /// <param name="documentModel">The native PDF document to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfPageNumberOptions"/> instance, or null for defaults.</param>
    public void ApplyPageNumbering(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _pageNumbering.Apply(documentModel, options);
    }

    /// <summary>Adds native PDF headers and footers.</summary>
    /// <param name="documentModel">The native PDF document to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfHeaderFooterOptions"/> instance, or null for defaults.</param>
    public void ApplyHeaderFooter(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _headerFooter.Apply(documentModel, options);
    }

    /// <summary>Stamps a text watermark onto the native document.</summary>
    /// <param name="documentModel">The native PDF document to modify.</param>
    /// <param name="text">Nonblank watermark text.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfWatermarkOptions"/> instance, or null for defaults.</param>
    public void ApplyWatermark(object documentModel, string text, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Watermark text cannot be null or empty.", nameof(text));
        }

        _watermark.Apply(documentModel, text, options);
    }

    /// <summary>Adds table-of-contents pages from the document's outline.</summary>
    /// <param name="documentModel">The native PDF document to modify.</param>
    /// <param name="options">A <see cref="PVAD.Pdf.PdfTableOfContentsOptions"/> instance, or null for defaults.</param>
    public void ApplyTableOfContents(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _tableOfContents.Apply(documentModel, options);
    }

    /// <summary>Reads the last generation diagnostics without rendering the document again.</summary>
    /// <param name="documentModel">The native PDF document to inspect.</param>
    /// <returns>Its last diagnostics snapshot, which may be null before rendering.</returns>
    public object? ReadDiagnostics(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        return _diagnostics.Read(documentModel);
    }

    /// <summary>Lists one-based page numbers containing native text elements, without performing OCR.</summary>
    /// <param name="documentModel">The native PDF document to inspect.</param>
    /// <returns>Matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithText(object documentModel)
    {
        return _pageCoverage.GetPagesWithText(documentModel);
    }

    /// <summary>Lists one-based page numbers containing native image elements.</summary>
    /// <param name="documentModel">The native PDF document to inspect.</param>
    /// <returns>Matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithImages(object documentModel)
    {
        return _pageCoverage.GetPagesWithImages(documentModel);
    }

    /// <summary>Lists one-based page numbers containing native link annotations.</summary>
    /// <param name="documentModel">The native PDF document to inspect.</param>
    /// <returns>Matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithLinks(object documentModel)
    {
        return _pageCoverage.GetPagesWithLinks(documentModel);
    }

    /// <summary>Lists one-based page numbers containing native shape elements.</summary>
    /// <param name="documentModel">The native PDF document to inspect.</param>
    /// <returns>Matching page numbers in document order.</returns>
    public IReadOnlyList<int> GetPagesWithShapes(object documentModel)
    {
        return _pageCoverage.GetPagesWithShapes(documentModel);
    }
}
