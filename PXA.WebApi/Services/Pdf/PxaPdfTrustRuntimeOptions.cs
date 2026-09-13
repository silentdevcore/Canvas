using Microsoft.Extensions.Options;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaPdfTrustRuntimeOptions
{
    public const string SectionName = "PdfTrust:Runtime";

    public string DeploymentMode { get; init; } = "Cloud";
    public bool OfflineMode { get; init; }
    public bool SigningEnabled { get; init; } = true;
    public bool RedactionEnabled { get; init; } = true;
    public bool ComplianceEnabled { get; init; } = true;
    public string KeyProviderAdapter { get; init; } = "Pkcs12Upload";
    public string? KeyProviderEndpoint { get; init; }
    public string? PdfAValidatorBinary { get; init; }
    public string? PdfUaValidatorBinary { get; init; }
    public bool RequireTimestampAuthorityForSigning { get; init; }
    public int AuditRetentionDays { get; init; } = 2555;
}

public sealed class PxaPdfTrustStartupGate(
    IOptions<PxaPdfTrustRuntimeOptions> runtimeOptions,
    IOptions<PxaPdfRevocationEvidenceOptions> revocationOptions,
    IOptions<PxaPdfTimestampAuthorityOptions> timestampOptions,
    IPxaPdfTrustPolicyProvider trustPolicyProvider,
    IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var runtime = runtimeOptions.Value;
        _ = trustPolicyProvider.Policy;

        if (runtime.OfflineMode && revocationOptions.Value.Enabled)
            throw new InvalidOperationException(
                "PdfTrust offline mode cannot enable network revocation retrieval. Configure local CRL/OCSP evidence instead.");
        if (runtime.SigningEnabled && runtime.RequireTimestampAuthorityForSigning &&
            string.IsNullOrWhiteSpace(timestampOptions.Value.Endpoint))
            throw new InvalidOperationException(
                "PDF signing requires a timestamp authority, but PdfTrust:TimestampAuthority:Endpoint is not configured.");
        if (runtime.KeyProviderAdapter is "Hsm" or "Kms" && string.IsNullOrWhiteSpace(runtime.KeyProviderEndpoint))
            throw new InvalidOperationException(
                $"PDF Trust key-provider adapter '{runtime.KeyProviderAdapter}' requires a configured endpoint.");

        ValidateExecutable(runtime.PdfAValidatorBinary, "PDF/A validator");
        ValidateExecutable(runtime.PdfUaValidatorBinary, "PDF/UA validator");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateExecutable(string? configuredPath, string purpose)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return;
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath, environment.ContentRootPath);
        if (!File.Exists(path))
            throw new InvalidOperationException($"The configured {purpose} binary does not exist: '{configuredPath}'.");
    }
}
