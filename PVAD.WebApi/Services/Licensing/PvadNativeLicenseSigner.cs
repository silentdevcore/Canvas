using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PVAD.Licensing;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadNativeIssuerOptions
{
    public const string SectionName = "NativeLicenseIssuer";
    public bool Enabled { get; set; }
    public string? KeyId { get; set; }
    public string? PrivateKeyPath { get; set; }
    public Guid[] AllowedUserIds { get; set; } = [];
}

/// <summary>Manufacturer signing component; never reuses the server-license key.</summary>
internal interface IPvadNativeLicenseIssuerSigner
{
    string Sign(PvadLicensePayload payload);
    PvadLicensePayload Verify(string artifact);
}

public sealed class PvadNativeLicenseSigner(IOptions<PvadNativeIssuerOptions> options) : IDisposable, IPvadNativeLicenseIssuerSigner
{
    private readonly PvadNativeIssuerOptions configuration = options.Value;
    private readonly object signingLock = new();
    private ECDsa? key;
    private bool disposed;

    string IPvadNativeLicenseIssuerSigner.Sign(PvadLicensePayload payload) => Sign(payload);
    PvadLicensePayload IPvadNativeLicenseIssuerSigner.Verify(string artifact) =>
        PvadLicense.CreateEmbeddedVerifier().Verify(System.Text.Encoding.UTF8.GetBytes(artifact));

    internal string Sign(PvadLicensePayload payload)
    {
        if (!configuration.Enabled) throw new PvadNativeIssuanceException("PVAD_NATIVE_ISSUANCE_DISABLED", 403);
        PvadLicenseVerifier.ValidatePayload(payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options);
        if (bytes.Length > PvadLicenseJson.MaximumPayloadBytes)
            throw new PvadNativeIssuanceException("PVAD_NATIVE_ISSUANCE_INVALID", 400);
        try
        {
            lock (signingLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                key ??= LoadKey();
                var artifact = JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(payload.SchemaVersion, configuration.KeyId!,
                    PvadLicenseJson.Algorithm, Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(
                        bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), PvadLicenseJson.Options);
                // A configured key must already be trusted by the actual shipped verifier.
                PvadLicense.CreateEmbeddedVerifier().Verify(artifact);
                return System.Text.Encoding.UTF8.GetString(artifact);
            }
        }
        catch (Exception error) when (error is PvadLicenseException or IOException or UnauthorizedAccessException or
            CryptographicException or ArgumentException or InvalidOperationException)
        {
            throw new PvadNativeIssuanceException("PVAD_NATIVE_ISSUER_UNAVAILABLE", 503);
        }
    }

    private ECDsa LoadKey()
    {
        if (string.IsNullOrWhiteSpace(configuration.KeyId) || configuration.KeyId.Length > 100 ||
            string.IsNullOrWhiteSpace(configuration.PrivateKeyPath) || !Path.IsPathRooted(configuration.PrivateKeyPath))
            throw new InvalidOperationException();
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(configuration.PrivateKeyPath) &
            (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException();
        var loaded = ECDsa.Create();
        try
        {
            loaded.ImportFromPem(PvadLicenseKeyFile.Read(string.Empty, configuration.PrivateKeyPath));
            var parameters = loaded.ExportParameters(true);
            try
            {
                if (parameters.D is null || parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
                    throw new CryptographicException();
            }
            finally { if (parameters.D is not null) CryptographicOperations.ZeroMemory(parameters.D); }
            return loaded;
        }
        catch { loaded.Dispose(); throw; }
    }

    public void Dispose()
    {
        lock (signingLock) { if (disposed) return; disposed = true; key?.Dispose(); }
    }
}

public sealed class PvadNativeIssuanceException(string code, int statusCode) : InvalidOperationException(
    "Native license issuance could not be completed.")
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
