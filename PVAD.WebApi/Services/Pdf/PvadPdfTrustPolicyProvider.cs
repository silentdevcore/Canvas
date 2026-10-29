using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using PVAD.Importer.Trust;

namespace PVAD.WebApi.Services.Pdf;

public sealed class PvadPdfTrustPolicyOptions
{
    public const string SectionName = "PdfTrust:ValidationPolicy";

    public bool UseSystemTrustStore { get; init; } = true;
    public string[] CustomTrustAnchorPaths { get; init; } = [];
    public string[] IntermediateCertificatePaths { get; init; } = [];
    public PdfCertificateRevocationMode RevocationMode { get; init; } = PdfCertificateRevocationMode.NoCheck;
    public X509RevocationFlag RevocationFlag { get; init; } = X509RevocationFlag.ExcludeRoot;
    public bool AllowCertificateDownloads { get; init; }
    public int NetworkTimeoutSeconds { get; init; } = 10;
}

public interface IPvadPdfTrustPolicyProvider
{
    PdfSignatureValidationPolicy Policy { get; }
}

public sealed class PvadPdfTrustPolicyProvider : IPvadPdfTrustPolicyProvider, IDisposable
{
    private readonly X509Certificate2Collection _ownedCertificates = [];

    public PvadPdfTrustPolicyProvider(
        IOptions<PvadPdfTrustPolicyOptions> options,
        IHostEnvironment environment,
        IPdfRevocationEvidenceProvider? revocationEvidenceProvider = null,
        IPdfIssuerCertificateProvider? issuerCertificateProvider = null)
    {
        var configured = options.Value;
        var trustAnchors = LoadCertificates(
            configured.CustomTrustAnchorPaths,
            environment.ContentRootPath,
            "custom trust anchor");
        var intermediates = LoadCertificates(
            configured.IntermediateCertificatePaths,
            environment.ContentRootPath,
            "intermediate certificate");
        _ownedCertificates.AddRange(trustAnchors);
        _ownedCertificates.AddRange(intermediates);
        Policy = new PdfSignatureValidationPolicy
        {
            UseSystemTrustStore = configured.UseSystemTrustStore,
            CustomTrustAnchors = trustAnchors.Cast<X509Certificate2>().ToArray(),
            IntermediateCertificates = intermediates.Cast<X509Certificate2>().ToArray(),
            RevocationMode = configured.RevocationMode,
            RevocationFlag = configured.RevocationFlag,
            AllowCertificateDownloads = configured.AllowCertificateDownloads,
            NetworkTimeout = TimeSpan.FromSeconds(configured.NetworkTimeoutSeconds),
            RevocationEvidenceProvider = revocationEvidenceProvider,
            IssuerCertificateProvider = issuerCertificateProvider
        };
    }

    public PdfSignatureValidationPolicy Policy { get; }

    public void Dispose()
    {
        foreach (var certificate in _ownedCertificates)
        {
            certificate.Dispose();
        }

        _ownedCertificates.Clear();
    }

    private static X509Certificate2Collection LoadCertificates(
        IEnumerable<string> configuredPaths,
        string contentRootPath,
        string purpose)
    {
        var certificates = new X509Certificate2Collection();
        foreach (var configuredPath in configuredPaths)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new InvalidOperationException($"A configured PDF {purpose} path is empty.");
            }

            var path = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(configuredPath, contentRootPath);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"The configured PDF {purpose} file does not exist: '{configuredPath}'.");
            }

            if (Path.GetExtension(path).Equals(".pem", StringComparison.OrdinalIgnoreCase))
            {
                certificates.ImportFromPemFile(path);
            }
            else
            {
                certificates.Add(X509CertificateLoader.LoadCertificateFromFile(path));
            }
        }

        return certificates;
    }
}
