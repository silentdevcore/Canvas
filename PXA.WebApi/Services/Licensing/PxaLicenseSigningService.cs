using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PXA.WebApi.Services.Licensing;

public sealed class PxaLicensingOptions
{
    public string KeyId { get; set; } = "pxa-development-1";
    public string PrivateKeyPath { get; set; } = "App_Data/licensing/private-key.pem";
    public string PublicKeyPath { get; set; } = "App_Data/licensing/public-key.pem";
}

public interface IPxaLicenseSigningService
{
    PxaSignedLicenseArtifact Sign(PxaOfflineLicenseEnvelope envelope);
    bool Verify(string envelopeJson, string signature);
    string PublicKeyPem { get; }
    string KeyId { get; }
}

public sealed class PxaLicenseSigningService : IPxaLicenseSigningService
{
    private static readonly object KeyCreationLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string privateKeyPem;

    public PxaLicenseSigningService(
        IOptions<PxaLicensingOptions> options,
        IWebHostEnvironment environment)
    {
        KeyId = options.Value.KeyId;
        var privatePath = ResolvePath(environment.ContentRootPath, options.Value.PrivateKeyPath);
        var publicPath = ResolvePath(environment.ContentRootPath, options.Value.PublicKeyPath);
        EnsureKeys(privatePath, publicPath, environment.IsDevelopment() || environment.IsEnvironment("Testing"));
        privateKeyPem = File.ReadAllText(privatePath);
        PublicKeyPem = File.ReadAllText(publicPath);
    }

    public string PublicKeyPem { get; }
    public string KeyId { get; }

    public PxaSignedLicenseArtifact Sign(PxaOfflineLicenseEnvelope envelope)
    {
        var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
        using var signer = ECDsa.Create();
        signer.ImportFromPem(privateKeyPem);
        var signature = signer.SignData(Encoding.UTF8.GetBytes(envelopeJson), HashAlgorithmName.SHA256);
        return new PxaSignedLicenseArtifact(envelopeJson, Convert.ToBase64String(signature), KeyId, "ECDSA_P256_SHA256");
    }

    public bool Verify(string envelopeJson, string signature)
    {
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(PublicKeyPem);
            return verifier.VerifyData(
                Encoding.UTF8.GetBytes(envelopeJson),
                Convert.FromBase64String(signature),
                HashAlgorithmName.SHA256);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private static string ResolvePath(string contentRoot, string configuredPath) =>
        Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(contentRoot, configuredPath);

    private static void EnsureKeys(string privatePath, string publicPath, bool mayCreate)
    {
        lock (KeyCreationLock)
        {
            if (File.Exists(privatePath) && File.Exists(publicPath))
                return;
            if (!mayCreate)
                throw new InvalidOperationException("Mounted offline-license signing keys are required outside Development.");
            Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(privatePath, key.ExportECPrivateKeyPem());
            File.WriteAllText(publicPath, key.ExportSubjectPublicKeyInfoPem());
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(privatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

public sealed record PxaOfflineLicenseEnvelope(
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
    IReadOnlyList<PxaOfflineLicenseEntitlement> Entitlements,
    DateTimeOffset IssuedAt);

public sealed record PxaOfflineLicenseEntitlement(
    string Capability,
    bool Enabled,
    long? Limit,
    string? Unit,
    DateTimeOffset? ExpiresAt);

public sealed record PxaSignedLicenseArtifact(string EnvelopeJson, string Signature, string KeyId, string Algorithm);
