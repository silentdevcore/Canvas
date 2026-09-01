using Microsoft.Extensions.Options;
using PXA.Importer.Signing;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaRedactionAuditKeyOptions
{
    public const string SectionName = "PdfRedaction:AuditSigning";

    /// <summary>Path to a PKCS#12 file holding the dedicated audit signing key.</summary>
    public string? Pkcs12Path { get; init; }

    /// <summary>Password for the PKCS#12 file, supplied through configuration, never a request.</summary>
    public string? Pkcs12Password { get; init; }
}

public interface IPxaRedactionAuditKeyProvider
{
    /// <summary>The audit signing key, or null when the deployment has not configured one.</summary>
    IPdfSigningKey? Key { get; }
}

/// <summary>
/// Supplies the dedicated redaction audit signing key.
///
/// This is deliberately a different key from the document signing key: audit records and
/// signed documents are separate trust domains, so a compromise of one must not let an
/// attacker forge the other. When no key is configured, audit records remain hash-only and
/// the API reports that they are unsigned rather than implying provenance it cannot back.
/// </summary>
public sealed class PxaRedactionAuditKeyProvider : IPxaRedactionAuditKeyProvider, IDisposable
{
    private readonly PdfPkcs12SigningKey? _key;

    public PxaRedactionAuditKeyProvider(
        IOptions<PxaRedactionAuditKeyOptions> options,
        ILogger<PxaRedactionAuditKeyProvider> logger)
    {
        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.Pkcs12Path))
        {
            return;
        }

        if (!File.Exists(value.Pkcs12Path))
        {
            throw new InvalidOperationException(
                "The configured redaction audit signing key was not found.");
        }

        try
        {
            _key = new PdfPkcs12SigningKey(File.ReadAllBytes(value.Pkcs12Path), value.Pkcs12Password);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogCritical(
                "The configured redaction audit signing key could not be loaded.");
            throw new InvalidOperationException(
                "The configured redaction audit signing key could not be loaded.",
                exception);
        }
    }

    public IPdfSigningKey? Key => _key;

    public void Dispose() => _key?.Dispose();
}
