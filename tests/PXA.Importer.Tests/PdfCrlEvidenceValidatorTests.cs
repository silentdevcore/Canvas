using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Trust;

namespace PXA.Importer.Tests;

public sealed class PdfCrlEvidenceValidatorTests
{
    [Fact]
    public void Locator_ShouldReadHttpsCrlDistributionPoint()
    {
        using var fixture = CrlFixture.Create("https://crl.example.test/root.crl");

        var endpoint = Assert.Single(PdfCertificateEvidenceLocator.GetCrlDistributionPointUris(fixture.Certificate));

        Assert.Equal("https://crl.example.test/root.crl", endpoint.AbsoluteUri);
    }

    [Fact]
    public void Validate_ShouldAcceptCurrentIssuerSignedCrl()
    {
        using var fixture = CrlFixture.Create();
        var crl = new CertificateRevocationListBuilder().Build(
            fixture.Issuer,
            new BigInteger(1),
            fixture.Now.AddHours(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1,
            fixture.Now.AddHours(-1));

        var evidence = PdfCrlEvidenceValidator.Validate(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            "https://pki.example.test/root.crl");

        Assert.Equal(PdfTrustStatus.Valid, evidence.Status);
        Assert.Equal(PdfRevocationEvidenceKind.Crl, evidence.Kind);
        Assert.Equal(fixture.Now.AddHours(1), evidence.NextUpdate);
    }

    [Fact]
    public void Validate_ShouldReportRevokedCertificate()
    {
        using var fixture = CrlFixture.Create();
        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(fixture.Certificate, fixture.Now.AddMinutes(-10));
        var crl = builder.Build(
            fixture.Issuer,
            new BigInteger(2),
            fixture.Now.AddHours(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1,
            fixture.Now.AddHours(-1));

        var evidence = PdfCrlEvidenceValidator.Validate(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            "supplied:test");

        Assert.Equal(PdfTrustStatus.Invalid, evidence.Status);
    }

    [Fact]
    public void ValidateHistorical_ShouldAcceptPostSignatureCrlAndRespectRevocationTime()
    {
        using var fixture = CrlFixture.Create();
        var builder = new CertificateRevocationListBuilder();
        builder.AddEntry(fixture.Certificate, fixture.Now.AddMinutes(-10));
        var crl = builder.Build(
            fixture.Issuer,
            new BigInteger(4),
            fixture.Now.AddHours(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1,
            fixture.Now);

        var beforeRevocation = PdfCrlEvidenceValidator.ValidateHistorical(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now.AddMinutes(-30),
            "embedded:vri");
        var afterRevocation = PdfCrlEvidenceValidator.ValidateHistorical(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            "embedded:vri");

        Assert.Equal(PdfTrustStatus.Valid, beforeRevocation.Status);
        Assert.Equal(PdfTrustStatus.Invalid, afterRevocation.Status);
    }

    [Fact]
    public void Validate_ShouldRejectExpiredOrTamperedCrl()
    {
        using var fixture = CrlFixture.Create();
        var crl = new CertificateRevocationListBuilder().Build(
            fixture.Issuer,
            new BigInteger(3),
            fixture.Now.AddHours(-1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1,
            fixture.Now.AddHours(-2));

        Assert.Throws<CryptographicException>(() => PdfCrlEvidenceValidator.Validate(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            "supplied:test"));

        crl[^1] ^= 0x01;
        Assert.Throws<CryptographicException>(() => PdfCrlEvidenceValidator.Validate(
            crl,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now.AddHours(-1.5),
            "supplied:test"));
    }

    private sealed class CrlFixture : IDisposable
    {
        private CrlFixture(DateTimeOffset now, X509Certificate2 issuer, X509Certificate2 certificate)
        {
            Now = now;
            Issuer = issuer;
            Certificate = certificate;
        }

        public DateTimeOffset Now { get; }
        public X509Certificate2 Issuer { get; }
        public X509Certificate2 Certificate { get; }

        public static CrlFixture Create(string? crlEndpoint = null)
        {
            var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
            using var issuerKey = RSA.Create(2048);
            var issuerRequest = new CertificateRequest(
                "CN=PXA CRL Test Root",
                issuerKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            issuerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));
            var issuer = issuerRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));

            using var leafKey = RSA.Create(2048);
            var leafRequest = new CertificateRequest(
                "CN=PXA CRL Test Signer",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            if (crlEndpoint is not null)
            {
                leafRequest.CertificateExtensions.Add(
                    CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([crlEndpoint], false));
            }
            var certificate = leafRequest.Create(
                issuer,
                now.AddHours(-2),
                now.AddHours(2),
                RandomNumberGenerator.GetBytes(16));
            return new CrlFixture(now, issuer, certificate);
        }

        public void Dispose()
        {
            Certificate.Dispose();
            Issuer.Dispose();
        }
    }
}
