using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Signing;
using PXA.Importer.Trust;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaPdfLongTermValidationMaterialProvider(
    IPxaPdfTrustPolicyProvider trustPolicyProvider,
    IPdfRevocationEvidenceProvider revocationEvidenceProvider)
    : IPdfLongTermValidationMaterialProvider
{
    public async Task<PdfLongTermValidationMaterial> GetMaterialAsync(
        PdfLongTermValidationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var policy = trustPolicyProvider.Policy;
        var timestampCertificates = ReadTimestampCertificates(context.TimestampToken);
        try
        {
            var timestampSigner = timestampCertificates.FirstOrDefault(certificate =>
                certificate.Thumbprint.Equals(
                    context.TimestampToken.AuthorityThumbprint,
                    StringComparison.OrdinalIgnoreCase)) ??
                throw new CryptographicException("The RFC 3161 signer certificate is not embedded in the timestamp token.");
            var signerChain = BuildTrustedChain(
                context.SignerCertificate,
                timestampCertificates,
                policy,
                context.TimestampToken.Timestamp);
            var timestampChain = BuildTrustedChain(
                timestampSigner,
                timestampCertificates,
                policy,
                context.TimestampToken.Timestamp);
            var allChains = new[] { signerChain, timestampChain };
            try
            {
                var certificates = allChains
                    .SelectMany(static chain => chain.ChainElements.Cast<X509ChainElement>())
                    .Select(static element => element.Certificate.RawData)
                    .DistinctBy(Hash, StringComparer.Ordinal)
                    .ToArray();
                var trustAnchors = allChains
                    .Select(static chain => chain.ChainElements[^1].Certificate.RawData)
                    .DistinctBy(Hash, StringComparer.Ordinal)
                    .ToArray();
                var evidence = new List<PdfRevocationEvidence>();
                foreach (var chain in allChains)
                {
                    for (var index = 0; index < chain.ChainElements.Count - 1; index++)
                    {
                        var result = await revocationEvidenceProvider.GetEvidenceAsync(
                            new PdfRevocationEvidenceRequest(
                                chain.ChainElements[index].Certificate,
                                chain.ChainElements[index + 1].Certificate,
                                context.TimestampToken.Timestamp,
                                policy.RevocationMode),
                            cancellationToken).ConfigureAwait(false);
                        if (result.Status != PdfTrustStatus.Valid)
                        {
                            throw new CryptographicException(
                                "Complete validated revocation evidence is required for every non-root PAdES B-LT certificate.");
                        }
                        evidence.AddRange(result.Evidence.Where(static item => item.Status == PdfTrustStatus.Valid));
                    }
                }

                return new PdfLongTermValidationMaterial
                {
                    Certificates = certificates,
                    TrustAnchors = trustAnchors,
                    Crls = evidence
                        .Where(static item => item.Kind == PdfRevocationEvidenceKind.Crl)
                        .Select(static item => item.EncodedValue)
                        .DistinctBy(Hash, StringComparer.Ordinal)
                        .ToArray(),
                    OcspResponses = evidence
                        .Where(static item => item.Kind == PdfRevocationEvidenceKind.Ocsp)
                        .Select(static item => item.EncodedValue)
                        .DistinctBy(Hash, StringComparer.Ordinal)
                        .ToArray()
                };
            }
            finally
            {
                foreach (var chain in allChains)
                {
                    chain.Dispose();
                }
            }
        }
        finally
        {
            foreach (var certificate in timestampCertificates)
            {
                certificate.Dispose();
            }
        }
    }

    private static X509Chain BuildTrustedChain(
        X509Certificate2 certificate,
        IReadOnlyList<X509Certificate2> timestampCertificates,
        PdfSignatureValidationPolicy policy,
        DateTimeOffset validationTime)
    {
        var trustModes = new List<bool>();
        if (policy.UseSystemTrustStore)
        {
            trustModes.Add(false);
        }
        if (policy.CustomTrustAnchors.Count > 0 || !policy.UseSystemTrustStore)
        {
            trustModes.Add(true);
        }

        foreach (var useCustomTrust in trustModes)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationTime = validationTime.UtcDateTime;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            foreach (var candidate in timestampCertificates.Concat(policy.IntermediateCertificates)
                         .Where(candidate => !candidate.Thumbprint.Equals(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase)))
            {
                chain.ChainPolicy.ExtraStore.Add(candidate);
            }
            if (useCustomTrust)
            {
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.AddRange(policy.CustomTrustAnchors.ToArray());
            }
            if (chain.Build(certificate))
            {
                return chain;
            }
            chain.Dispose();
        }

        throw new CryptographicException("A complete trusted certificate chain is required for PAdES Baseline LT.");
    }

    private static IReadOnlyList<X509Certificate2> ReadTimestampCertificates(PdfTimestampToken timestampToken)
    {
        if (!Rfc3161TimestampToken.TryDecode(timestampToken.Cms, out var token, out var consumed) ||
            token is null || consumed != timestampToken.Cms.Length)
        {
            throw new CryptographicException("The RFC 3161 timestamp token cannot be decoded for PAdES Baseline LT.");
        }
        return token.AsSignedCms().Certificates
            .Cast<X509Certificate2>()
            .Select(static certificate => X509CertificateLoader.LoadCertificate(certificate.RawData))
            .ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
