using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadLicensingOptions
{
    public bool IssuerEnabled { get; set; }
    public string KeyId { get; set; } = "pvad-development-1";
    public string? PrivateKeyPath { get; set; } = "App_Data/licensing/private-key.pem";
    public string PublicKeyPath { get; set; } = "App_Data/licensing/public-key.pem";
    public int MetricsIntervalSeconds { get; set; } = 60;
}

public interface IPvadLicenseSignatureVerifier
{
    bool Verify(string envelopeJson, string signature);
    string PublicKeyPem { get; }
    string KeyId { get; }
}

public interface IPvadLicenseSigningService
{
    bool CanSign { get; }
    string KeyId { get; }
    PvadSignedLicenseArtifact Sign(PvadOfflineLicenseEnvelope envelope);
}

public sealed class PvadLicenseSigningService : IPvadLicenseSigningService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<ECDsa> signingKey;
    private readonly object signingLock = new();
    private bool disposed;

    public PvadLicenseSigningService(
        IOptions<PvadLicensingOptions> options,
        IWebHostEnvironment environment,
        IPvadLicenseSignatureVerifier? signatureVerifier = null)
    {
        var configuration = options.Value;
        CanSign = configuration.IssuerEnabled;
        KeyId = configuration.KeyId;
        var privatePath = configuration.PrivateKeyPath;
        var root = environment.ContentRootPath;
        var verifier = signatureVerifier ?? new PvadLicenseSignatureVerifier(options, environment);
        signingKey = new Lazy<ECDsa>(() => LoadKey(root, privatePath, verifier, KeyId));
    }

    public bool CanSign { get; }
    public string KeyId { get; }

    public PvadSignedLicenseArtifact Sign(PvadOfflineLicenseEnvelope envelope)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!CanSign)
                throw new PvadLicenseSigningException("PVAD_LICENSE_ISSUANCE_DISABLED");
            ArgumentNullException.ThrowIfNull(envelope);
            var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
            byte[] signature;
            lock (signingLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                signature = signingKey.Value.SignData(
                    Encoding.UTF8.GetBytes(envelopeJson), HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            PvadTelemetry.RecordLicensingOperation("sign", "completed", stopwatch.Elapsed);
            return new PvadSignedLicenseArtifact(
                envelopeJson,
                Convert.ToBase64String(signature),
                KeyId,
                "ECDSA_P256_SHA256");
        }
        catch
        {
            PvadTelemetry.RecordLicensingOperation("sign", "failed", stopwatch.Elapsed);
            throw;
        }
    }

    private static ECDsa LoadKey(string root, string? privatePath, IPvadLicenseSignatureVerifier verifier, string keyId)
    {
        ECDsa? key = null;
        try
        {
            if (verifier.KeyId != keyId) throw new CryptographicException();
            using var publicKey = ECDsa.Create();
            publicKey.ImportFromPem(verifier.PublicKeyPem);
            key = ECDsa.Create();
            key.ImportFromPem(PvadLicenseKeyFile.Read(root, privatePath));
            var parameters = key.ExportParameters(true);
            try
            {
                if (parameters.D is null || parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
                    throw new CryptographicException();
            }
            finally
            {
                if (parameters.D is not null)
                    CryptographicOperations.ZeroMemory(parameters.D);
            }
            if (!key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicKey.ExportSubjectPublicKeyInfo()))
                throw new CryptographicException();
            return key;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or InvalidOperationException)
        {
            key?.Dispose();
            throw new PvadLicenseSigningException("PVAD_LICENSE_ISSUER_UNAVAILABLE");
        }
    }

    public void Dispose()
    {
        lock (signingLock)
        {
            if (disposed) return;
            disposed = true;
            if (signingKey.IsValueCreated) signingKey.Value.Dispose();
        }
    }
}

public sealed class PvadLicenseSigningException(string code) : InvalidOperationException(
    code == "PVAD_LICENSE_ISSUANCE_DISABLED"
        ? "License issuance is disabled on this server."
        : "The configured license issuer is unavailable.")
{
    public string Code { get; } = code;
}

public sealed record PvadOfflineLicenseEnvelope(
    int SchemaVersion,
    Guid LicenseId,
    string LicenseNumber,
    Guid OrganizationId,
    string OrganizationName,
    string Edition,
    string AccountType,
    string DeploymentMode,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int InstanceLimit,
    string ProductVersion,
    string DeploymentId,
    IReadOnlyList<PvadOfflineLicenseEntitlement> Entitlements,
    DateTimeOffset IssuedAt);

public sealed record PvadOfflineLicenseEntitlement(
    string Capability,
    bool Enabled,
    long? Limit,
    string? Unit,
    DateTimeOffset? ExpiresAt);

public sealed record PvadSignedLicenseArtifact(string EnvelopeJson, string Signature, string KeyId, string Algorithm);
