using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Trust;

namespace PXA.Importer.Tests;

public sealed class PdfOcspEvidenceTests
{
    [Fact]
    public void Locator_ShouldReadOcspAuthorityInformationAccessUri()
    {
        using var fixture = OcspFixture.Create("https://ocsp.example.test/status");

        var endpoint = Assert.Single(PdfCertificateEvidenceLocator.GetOcspUris(fixture.Certificate));

        Assert.Equal("https://ocsp.example.test/status", endpoint.AbsoluteUri);
    }

    [Fact]
    public void CreateRequest_ShouldBindIssuerAndCertificateSerial()
    {
        using var fixture = OcspFixture.Create();

        var request = PdfOcspEvidence.CreateRequest(fixture.Certificate, fixture.Issuer);

        Assert.Equal(0x30, request[0]);
        Assert.True(request.AsSpan().IndexOf(NormalizeSerial(fixture.Certificate)) >= 0);
    }

    [Fact]
    public void ValidateResponse_ShouldAcceptIssuerSignedGoodStatus()
    {
        using var fixture = OcspFixture.Create();
        var response = fixture.CreateResponse(PdfTrustStatus.Valid, fixture.Now);

        var evidence = PdfOcspEvidence.ValidateResponse(
            response,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            TimeSpan.FromHours(24),
            "configured:test.ocsp");

        Assert.Equal(PdfRevocationEvidenceKind.Ocsp, evidence.Kind);
        Assert.Equal(PdfTrustStatus.Valid, evidence.Status);
        Assert.Equal(fixture.Now.AddHours(1), evidence.NextUpdate);
    }

    [Fact]
    public void ValidateResponse_ShouldReportRevokedAndRejectTamperingOrStaleEvidence()
    {
        using var fixture = OcspFixture.Create();
        var revoked = fixture.CreateResponse(PdfTrustStatus.Invalid, fixture.Now);
        var evidence = PdfOcspEvidence.ValidateResponse(
            revoked,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            TimeSpan.FromHours(24),
            "network:test");
        Assert.Equal(PdfTrustStatus.Invalid, evidence.Status);

        revoked[^1] ^= 0x01;
        Assert.Throws<CryptographicException>(() => PdfOcspEvidence.ValidateResponse(
            revoked,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            TimeSpan.FromHours(24),
            "network:test"));

        var stale = fixture.CreateResponse(PdfTrustStatus.Valid, fixture.Now.AddDays(-2));
        Assert.Throws<CryptographicException>(() => PdfOcspEvidence.ValidateResponse(
            stale,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            TimeSpan.FromHours(24),
            "cache:test"));
    }

    [Fact]
    public void ValidateHistoricalResponse_ShouldAcceptLaterEvidenceAndRespectRevocationTime()
    {
        using var fixture = OcspFixture.Create();
        var response = fixture.CreateResponse(PdfTrustStatus.Invalid, fixture.Now);

        var beforeRevocation = PdfOcspEvidence.ValidateHistoricalResponse(
            response,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now.AddMinutes(-10),
            TimeSpan.FromHours(24),
            "embedded:vri");
        var afterRevocation = PdfOcspEvidence.ValidateHistoricalResponse(
            response,
            fixture.Certificate,
            fixture.Issuer,
            fixture.Now,
            TimeSpan.FromHours(24),
            "embedded:vri");

        Assert.Equal(PdfTrustStatus.Valid, beforeRevocation.Status);
        Assert.Equal(PdfTrustStatus.Invalid, afterRevocation.Status);
    }

    private sealed class OcspFixture : IDisposable
    {
        private const string BasicResponseOid = "1.3.6.1.5.5.7.48.1.1";
        private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
        private const string Sha256WithRsaOid = "1.2.840.113549.1.1.11";

        private OcspFixture(
            DateTimeOffset now,
            RSA issuerKey,
            X509Certificate2 issuer,
            X509Certificate2 certificate)
        {
            Now = now;
            IssuerKey = issuerKey;
            Issuer = issuer;
            Certificate = certificate;
        }

        public DateTimeOffset Now { get; }
        public RSA IssuerKey { get; }
        public X509Certificate2 Issuer { get; }
        public X509Certificate2 Certificate { get; }

        public static OcspFixture Create(string? ocspEndpoint = null)
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            var issuerKey = RSA.Create(2048);
            var issuerRequest = new CertificateRequest(
                "CN=PXA OCSP Test Root",
                issuerKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            issuerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));
            var issuer = issuerRequest.CreateSelfSigned(now.AddDays(-3), now.AddDays(3));

            using var leafKey = RSA.Create(2048);
            var leafRequest = new CertificateRequest(
                "CN=PXA OCSP Test Signer",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            if (ocspEndpoint is not null)
            {
                leafRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(
                    [ocspEndpoint],
                    [],
                    false));
            }
            var certificate = leafRequest.Create(
                issuer,
                now.AddDays(-1),
                now.AddDays(1),
                RandomNumberGenerator.GetBytes(16));
            return new OcspFixture(now, issuerKey, issuer, certificate);
        }

        public byte[] CreateResponse(PdfTrustStatus status, DateTimeOffset thisUpdate)
        {
            var responseDataWriter = new AsnWriter(AsnEncodingRules.DER);
            responseDataWriter.PushSequence();
            responseDataWriter.WriteOctetString(
                SHA1.HashData(ReadSubjectPublicKey(Issuer)),
                new Asn1Tag(TagClass.ContextSpecific, 2));
            responseDataWriter.WriteGeneralizedTime(thisUpdate);
            responseDataWriter.PushSequence();
            responseDataWriter.PushSequence();
            responseDataWriter.PushSequence();
            WriteAlgorithmIdentifier(responseDataWriter, Sha256Oid);
            responseDataWriter.WriteOctetString(SHA256.HashData(Issuer.SubjectName.RawData));
            responseDataWriter.WriteOctetString(SHA256.HashData(ReadSubjectPublicKey(Issuer)));
            responseDataWriter.WriteIntegerUnsigned(NormalizeSerial(Certificate));
            responseDataWriter.PopSequence();
            if (status == PdfTrustStatus.Invalid)
            {
                responseDataWriter.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
                responseDataWriter.WriteGeneralizedTime(thisUpdate.AddMinutes(-5));
                responseDataWriter.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
            }
            else
            {
                responseDataWriter.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 0));
            }
            responseDataWriter.WriteGeneralizedTime(thisUpdate);
            responseDataWriter.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            responseDataWriter.WriteGeneralizedTime(thisUpdate.AddHours(1));
            responseDataWriter.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            responseDataWriter.PopSequence();
            responseDataWriter.PopSequence();
            responseDataWriter.PopSequence();
            var responseData = responseDataWriter.Encode();

            var signature = IssuerKey.SignData(
                responseData,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            var basicWriter = new AsnWriter(AsnEncodingRules.DER);
            basicWriter.PushSequence();
            basicWriter.WriteEncodedValue(responseData);
            WriteAlgorithmIdentifier(basicWriter, Sha256WithRsaOid);
            basicWriter.WriteBitString(signature);
            basicWriter.PopSequence();

            var responseWriter = new AsnWriter(AsnEncodingRules.DER);
            responseWriter.PushSequence();
            responseWriter.WriteEnumeratedValue(OcspResponseStatus.Successful);
            responseWriter.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            responseWriter.PushSequence();
            responseWriter.WriteObjectIdentifier(BasicResponseOid);
            responseWriter.WriteOctetString(basicWriter.Encode());
            responseWriter.PopSequence();
            responseWriter.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            responseWriter.PopSequence();
            return responseWriter.Encode();
        }

        public void Dispose()
        {
            Certificate.Dispose();
            Issuer.Dispose();
            IssuerKey.Dispose();
        }

        private static void WriteAlgorithmIdentifier(AsnWriter writer, string oid)
        {
            writer.PushSequence();
            writer.WriteObjectIdentifier(oid);
            writer.WriteNull();
            writer.PopSequence();
        }

        private enum OcspResponseStatus
        {
            Successful
        }
    }

    private static byte[] ReadSubjectPublicKey(X509Certificate2 certificate)
    {
        var reader = new AsnReader(certificate.PublicKey.ExportSubjectPublicKeyInfo(), AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        sequence.ReadEncodedValue();
        return sequence.ReadBitString(out _);
    }

    private static byte[] NormalizeSerial(X509Certificate2 certificate)
    {
        var serial = certificate.GetSerialNumber();
        Array.Reverse(serial);
        var offset = 0;
        while (offset < serial.Length - 1 && serial[offset] == 0)
        {
            offset++;
        }
        return serial[offset..];
    }
}
