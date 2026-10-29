using PVAD.Importer.Document;
using System.Security.Cryptography.X509Certificates;

namespace PVAD.Importer;

/// <summary>
/// Additive PVADE facade for importing existing PDF documents.
/// </summary>
public static class Pdf
{
    /// <summary>
    /// Loads a PDF stream into the current PVAD importer document model.
    /// </summary>
    public static Task<PdfDocumentModel> LoadAsync(
        Stream pdfStream,
        PdfImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        return new PdfImporter(options?.ToPvadOptions()).LoadAsync(pdfStream, cancellationToken);
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

        return new PdfImporter(options?.ToPvadOptions()).LoadAsync(
            pdfStream,
            password,
            cancellationToken);
    }

    /// <summary>Loads a certificate-protected PDF using a certificate with its private key.</summary>
    public static Task<PdfDocumentModel> LoadEncryptedAsync(
        Stream pdfStream,
        X509Certificate2 certificate,
        PdfImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        ArgumentNullException.ThrowIfNull(certificate);
        return new PdfImporter(options?.ToPvadOptions()).LoadAsync(
            pdfStream,
            certificate,
            cancellationToken);
    }
}

/// <summary>
/// PVAD-facing options for the current PDF importer.
/// </summary>
public sealed record PdfImportOptions
{
    public bool LazyObjectLoading { get; init; } = true;

    public bool DeferredStreamDecoding { get; init; } = true;

    public bool ParsePagesInParallel { get; init; } = true;

    public int MaxParallelPageParsers { get; init; } = Environment.ProcessorCount;

    internal PdfImporterOptions ToPvadOptions() => new()
    {
        LazyObjectLoading = LazyObjectLoading,
        DeferredStreamDecoding = DeferredStreamDecoding,
        ParsePagesInParallel = ParsePagesInParallel,
        MaxParallelPageParsers = MaxParallelPageParsers,
    };
}
