using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PXA.Importer.Signing;

public enum PdfPadesProfile
{
    BaselineB
}

public sealed record PdfSigningOptions
{
    public string FieldName { get; init; } = "Signature1";
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? ContactInfo { get; init; }
    public DateTimeOffset? SigningTime { get; init; }
    public PdfPadesProfile Profile { get; init; } = PdfPadesProfile.BaselineB;
    public int ContentsPlaceholderBytes { get; init; } = 32 * 1024;
}

public sealed record PdfSigningResult(
    byte[] Bytes,
    PdfPadesProfile Profile,
    string FieldName,
    string DigestAlgorithm,
    string SignerSubject,
    string CertificateThumbprint,
    DateTimeOffset SigningTime,
    int RevisionCount);

public interface IPdfSigningKey
{
    X509Certificate2 Certificate { get; }

    byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName digestAlgorithm);
}

public sealed class PdfPkcs12SigningKey : IPdfSigningKey, IDisposable
{
    private readonly X509Certificate2 _certificate;
    private readonly RSA _privateKey;

    public PdfPkcs12SigningKey(ReadOnlySpan<byte> pkcs12Bytes, string? password = null)
    {
        if (pkcs12Bytes.IsEmpty)
        {
            throw new ArgumentException("A non-empty PKCS#12 payload is required.", nameof(pkcs12Bytes));
        }

        var storageFlags = OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.EphemeralKeySet
            : X509KeyStorageFlags.DefaultKeySet;
        _certificate = X509CertificateLoader.LoadPkcs12(pkcs12Bytes, password, storageFlags);
        _privateKey = _certificate.GetRSAPrivateKey() ?? RejectUnsupportedKey(_certificate);
    }

    public X509Certificate2 Certificate => _certificate;

    public byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName digestAlgorithm) =>
        _privateKey.SignHash(hash, digestAlgorithm, RSASignaturePadding.Pkcs1);

    public void Dispose()
    {
        _privateKey.Dispose();
        _certificate.Dispose();
    }

    private static RSA RejectUnsupportedKey(X509Certificate2 certificate)
    {
        certificate.Dispose();
        throw new NotSupportedException("PAdES Baseline B currently requires an RSA private key.");
    }
}
