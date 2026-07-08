namespace Canvas.Importer;

public sealed record PdfImporterOptions
{
    public bool LazyObjectLoading { get; init; } = true;
    public bool DeferredStreamDecoding { get; init; } = true;
    public bool ParsePagesInParallel { get; init; } = true;
    public int MaxParallelPageParsers { get; init; } = Environment.ProcessorCount;
}

public sealed class PdfImporter
{
    private readonly PdfImporterOptions _options;

    /// <summary>
    /// Creates the legacy Canvas PDF importer.
    /// </summary>
    /// <remarks>
    /// Existing callers remain supported. New integrations should use
    /// <c>PXA.Importer.Pdf.LoadAsync(...)</c>.
    /// </remarks>
    [Obsolete(
        "Canvas.Importer.PdfImporter is the legacy compatibility entry point. Use PXA.Importer.Pdf.LoadAsync(...) for new code.",
        DiagnosticId = "PXA0002")]
    public PdfImporter(PdfImporterOptions? options = null)
    {
        _options = options ?? new PdfImporterOptions();
    }

    public async Task<Document.PdfDocumentModel> LoadAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var owner = await Infrastructure.PdfBuffer.FromStreamAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        var context = new Parsing.PdfParseContext(owner.Memory, _options);
        var xref = new Xref.PdfCrossReferenceParser().Parse(context);
        var objectParser = new Parsing.PdfObjectParser(context, xref);
        var graph = objectParser.ParseDocumentGraph();

        return new Document.PdfDocumentBuilder(new Content.PdfContentStreamParser(), new Graphics.PdfGraphicsInterpreter())
            .Build(graph);
    }
}
