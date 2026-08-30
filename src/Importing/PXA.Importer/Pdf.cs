using PXA.Importer.Document;

namespace PXA.Importer;

/// <summary>
/// Additive Power Dox Automation facade for importing existing PDF documents.
/// </summary>
public static class Pdf
{
    /// <summary>
    /// Loads a PDF stream into the current PXA importer document model.
    /// </summary>
    public static Task<PdfDocumentModel> LoadAsync(
        Stream pdfStream,
        PdfImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        return new PdfImporter(options?.ToPxaOptions()).LoadAsync(pdfStream, cancellationToken);
    }

    /// <summary>
    /// Loads a password-protected PDF without retaining the password in importer options.
    /// </summary>
    public static Task<PdfDocumentModel> LoadEncryptedAsync(
        Stream pdfStream,
        string password,
        PdfImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        ArgumentNullException.ThrowIfNull(password);

        return new PdfImporter(options?.ToPxaOptions()).LoadAsync(
            pdfStream,
            password,
            cancellationToken);
    }
}

/// <summary>
/// PXA-facing options for the current PDF importer.
/// </summary>
public sealed record PdfImportOptions
{
    public bool LazyObjectLoading { get; init; } = true;

    public bool DeferredStreamDecoding { get; init; } = true;

    public bool ParsePagesInParallel { get; init; } = true;

    public int MaxParallelPageParsers { get; init; } = Environment.ProcessorCount;

    internal PdfImporterOptions ToPxaOptions() => new()
    {
        LazyObjectLoading = LazyObjectLoading,
        DeferredStreamDecoding = DeferredStreamDecoding,
        ParsePagesInParallel = ParsePagesInParallel,
        MaxParallelPageParsers = MaxParallelPageParsers,
    };
}
