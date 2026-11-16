using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PVAD.WebApi.Services.Licensing;

/// <summary>Verifies existing server-license artifacts without loading or creating private keys.</summary>
public sealed class PvadLicenseSignatureVerifier : IPvadLicenseSignatureVerifier
{
    private readonly Lazy<string> publicKeyPem;

    public PvadLicenseSignatureVerifier(IOptions<PvadLicensingOptions> options, IWebHostEnvironment environment)
    {
        KeyId = options.Value.KeyId;
        var path = options.Value.PublicKeyPath;
        var root = environment.ContentRootPath;
        publicKeyPem = new Lazy<string>(() =>
        {
            try
            {
                using var key = PvadLicenseKeyFile.ReadPublicKey(root, path, out var pem);
                return pem;
            }
            catch (InvalidOperationException)
            {
                throw new PvadLicenseVerificationUnavailableException();
            }
        });
    }

    public string KeyId { get; }
    public string PublicKeyPem => publicKeyPem.Value;

    public bool Verify(string envelopeJson, string signature)
    {
        if (string.IsNullOrEmpty(envelopeJson) || string.IsNullOrEmpty(signature) || signature.Length > 256)
            return false;
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(PublicKeyPem);
            var signatureBytes = Convert.FromBase64String(signature);
            return signatureBytes.Length == 64 && key.VerifyData(
                Encoding.UTF8.GetBytes(envelopeJson), signatureBytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}

public sealed class PvadLicenseVerificationUnavailableException() : InvalidOperationException(
    "The configured public license verification key is unavailable.")
{
    public const string Code = "PVAD_LICENSE_VERIFIER_UNAVAILABLE";
}

internal static class PvadLicenseKeyFile
{
    internal static string Read(string root, string? configuredPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) throw new ArgumentException();
            var path = Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(root, configuredPath);
            using var file = File.OpenRead(path);
            using var buffer = new MemoryStream();
            Span<byte> block = stackalloc byte[4096];
            while (true)
            {
                var read = file.Read(block);
                if (read == 0) break;
                if (buffer.Length + read > 16 * 1024) throw new InvalidOperationException();
                buffer.Write(block[..read]);
            }
            return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException("The configured license key could not be loaded.");
        }
    }

    internal static ECDsa ReadPublicKey(string root, string path, out string pem)
    {
        pem = Read(root, path);
        ECDsa? key = null;
        try
        {
            if (pem.Contains("PRIVATE", StringComparison.OrdinalIgnoreCase)) throw new CryptographicException();
            key = ECDsa.Create();
            key.ImportFromPem(pem);
            if (key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
                throw new CryptographicException();
            return key;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            key?.Dispose();
            throw new InvalidOperationException("A public-only ECDSA P-256 license verification key is required.");
        }
    }
}
