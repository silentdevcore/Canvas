namespace PXA.Importer;

using System.Security.Cryptography.X509Certificates;

public sealed record PdfImporterOptions
{
    public bool LazyObjectLoading { get; init; } = true;
    public bool DeferredStreamDecoding { get; init; } = true;
    public bool ParsePagesInParallel { get; init; } = true;
    public int MaxParallelPageParsers { get; init; } = Environment.ProcessorCount;

    /// <summary>Bounds applied while parsing untrusted input.</summary>
    public Parsing.PdfParseLimits Limits { get; init; } = new();
}

public sealed class PdfImporter
{
    private readonly PdfImporterOptions _options;

    public PdfImporter(PdfImporterOptions? options = null)
    {
        _options = options ?? new PdfImporterOptions();
    }

    public async Task<Document.PdfDocumentModel> LoadAsync(Stream pdfStream, CancellationToken cancellationToken = default)
        => await LoadCoreAsync(pdfStream, password: null, cancellationToken).ConfigureAwait(false);

    /// <summary>Loads a password-protected PDF without persisting the supplied password.</summary>
    public async Task<Document.PdfDocumentModel> LoadAsync(
        Stream pdfStream,
        string password,
        CancellationToken cancellationToken = default)
        => await LoadCoreAsync(pdfStream, password, cancellationToken).ConfigureAwait(false);

    public async Task<Document.PdfDocumentModel> LoadAsync(
        Stream pdfStream,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
        => await LoadCoreAsync(pdfStream, password: null, certificate, cancellationToken).ConfigureAwait(false);

    private async Task<Document.PdfDocumentModel> LoadCoreAsync(
        Stream pdfStream,
        string? password,
        CancellationToken cancellationToken) =>
        await LoadCoreAsync(pdfStream, password, certificate: null, cancellationToken).ConfigureAwait(false);

    private async Task<Document.PdfDocumentModel> LoadCoreAsync(
        Stream pdfStream,
        string? password,
        X509Certificate2? certificate,
        CancellationToken cancellationToken)
    {
        using var owner = await Infrastructure.PdfBuffer.FromStreamAsync(pdfStream, cancellationToken).ConfigureAwait(false);
        var context = new Parsing.PdfParseContext(owner.Memory, _options, cancellationToken);
        var xref = new Xref.PdfCrossReferenceParser().Parse(context);
        var objectParser = new Parsing.PdfObjectParser(context, xref);
        Objects.PdfObjectGraph graph;
        if (Security.PdfDocumentDecryptor.IsEncrypted(xref))
        {
            if (password is null && certificate is null)
            {
                throw new Security.PdfPasswordException(
                    "The document is encrypted and requires a password or recipient certificate.");
            }

            graph = certificate is not null
                ? Security.PdfDocumentDecryptor.ParseAndDecrypt(context, xref, certificate).Graph
                : Security.PdfDocumentDecryptor.ParseAndDecrypt(context, xref, password!).Graph;
        }
        else
        {
            graph = objectParser.ParseDocumentGraph();
        }

        return new Document.PdfDocumentBuilder(
                new Content.PdfContentStreamParser(),
                new Graphics.PdfGraphicsInterpreter(),
                limits: _options.Limits)
            .Build(graph);
    }
}
