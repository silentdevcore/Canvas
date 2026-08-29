using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PxaPdfLongTermValidationMaterialProviderTests
{
    [Fact]
    public async Task GetMaterialAsync_UsesServerOwnedSignerAndTimestampTrustAnchors()
    {
        var now = DateTimeOffset.UtcNow;
        using var signer = CreateCertificate("PXA B-LT Signer", now, timestamping: false);
        using var timestampSigner = CreateCertificate("PXA B-LT TSA", now, timestamping: true);
        var timestampToken = CreateTimestampToken(timestampSigner, now);
        var evidenceProvider = new RejectingEvidenceProvider();
        var provider = new PxaPdfLongTermValidationMaterialProvider(
            new StaticTrustPolicyProvider(new PdfSignatureValidationPolicy
            {
                UseSystemTrustStore = false,
                CustomTrustAnchors = [signer, timestampSigner]
            }),
            evidenceProvider);

        var material = await provider.GetMaterialAsync(
            new PdfLongTermValidationContext(signer, timestampToken, now));

        Assert.Equal(2, material.Certificates.Count);
        Assert.Equal(2, material.TrustAnchors.Count);
        Assert.Empty(material.Crls);
        Assert.Empty(material.OcspResponses);
        Assert.Equal(0, evidenceProvider.RequestCount);
    }

    private static X509Certificate2 CreateCertificate(
        string commonName,
        DateTimeOffset now,
        bool timestamping)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign,
            true));
        if (timestamping)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.8") },
                true));
        }
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
    }

    private static PdfTimestampToken CreateTimestampToken(
        X509Certificate2 timestampSigner,
        DateTimeOffset timestamp)
    {
        var tokenInfo = new Rfc3161TimestampTokenInfo(
            new Oid("1.2.3.4.5"),
            new Oid("2.16.840.1.101.3.4.2.1"),
            SHA256.HashData("PXA signature"u8),
            new byte[] { 1 },
            timestamp,
            accuracyInMicroseconds: null,
            isOrdering: false,
            nonce: null,
            timestampAuthorityName: null,
            extensions: null);
        var cms = new SignedCms(
            new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), tokenInfo.Encode()),
            detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, timestampSigner)
        {
            IncludeOption = X509IncludeOption.EndCertOnly,
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1")
        };
        signer.SignedAttributes.Add(new AsnEncodedData(
            new Oid("1.2.840.113549.1.9.16.2.47"),
            BuildSigningCertificateV2(timestampSigner)));
        cms.ComputeSignature(signer);
        return new PdfTimestampToken(
            cms.Encode(),
            timestamp,
            timestampSigner.Subject,
            timestampSigner.Thumbprint);
    }

    private static byte[] BuildSigningCertificateV2(X509Certificate2 certificate)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.PushSequence();
        writer.PushSequence();
        writer.WriteOctetString(SHA256.HashData(certificate.RawData));
        writer.PopSequence();
        writer.PopSequence();
        writer.PopSequence();
        return writer.Encode();
    }

    private sealed class StaticTrustPolicyProvider(PdfSignatureValidationPolicy policy)
        : IPxaPdfTrustPolicyProvider
    {
        public PdfSignatureValidationPolicy Policy { get; } = policy;
    }

    private sealed class RejectingEvidenceProvider : IPdfRevocationEvidenceProvider
    {
        public int RequestCount { get; private set; }

        public Task<PdfRevocationEvidenceResult> GetEvidenceAsync(
            PdfRevocationEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(PdfRevocationEvidenceResult.Unavailable());
        }
    }
}
