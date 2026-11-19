using System.Security.Cryptography;
using PVAD.Licensing;

namespace PVAD.Importer.Signing;

internal static class PdfSigningDependencies
{
    internal static byte[] SignHashAdmitted(
        IPdfSigningKey key, ReadOnlySpan<byte> hash, HashAlgorithmName algorithm)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        return key is PdfPkcs12SigningKey builtin
            ? builtin.SignHashAdmitted(hash, algorithm)
            : key.SignHash(hash, algorithm);
    }

    internal static Task<PdfTimestampToken> GetTimestampTokenAdmittedAsync(
        IPdfTimestampAuthority authority, ReadOnlyMemory<byte> digest,
        HashAlgorithmName algorithm, CancellationToken cancellationToken)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        return authority is PdfRfc3161TimestampAuthority builtin
            ? builtin.GetTimestampTokenAdmittedAsync(digest, algorithm, cancellationToken)
            : authority.GetTimestampTokenAsync(digest, algorithm, cancellationToken);
    }
}
