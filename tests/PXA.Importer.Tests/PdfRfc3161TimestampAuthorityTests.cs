using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Signing;

namespace PXA.Importer.Tests;

public sealed class PdfRfc3161TimestampAuthorityTests
{
    [Fact]
    public async Task GetTimestampTokenAsync_ShouldValidateResponseNonceImprintAndSignature()
    {
        var timestamp = DateTimeOffset.UtcNow;
        using var handler = new TimestampHandler(timestamp);
        using var client = new HttpClient(handler);
        var authority = new PdfRfc3161TimestampAuthority(
            client,
            new PdfRfc3161TimestampAuthorityOptions
            {
                Endpoint = new Uri("https://tsa.example.test/rfc3161"),
                RequireTrustedCertificateChain = false
            });
        var signatureDigest = SHA256.HashData("PXA signature value"u8);

        var token = await authority.GetTimestampTokenAsync(
            signatureDigest,
            HashAlgorithmName.SHA256);

        Assert.Equal(timestamp, token.Timestamp);
        Assert.Contains("PXA RFC3161 Test TSA", token.AuthoritySubject, StringComparison.Ordinal);
        Assert.NotEmpty(token.Cms);
        Assert.Equal("application/timestamp-query", handler.RequestContentType);
    }

    [Fact]
    public void Constructor_ShouldRejectNonHttpsEndpoint()
    {
        using var client = new HttpClient(new TimestampHandler(DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => new PdfRfc3161TimestampAuthority(
            client,
            new PdfRfc3161TimestampAuthorityOptions
            {
                Endpoint = new Uri("http://tsa.example.test/rfc3161")
            }));
    }

    private sealed class TimestampHandler : HttpMessageHandler
    {
        private const string TimestampInfoOid = "1.2.840.113549.1.9.16.1.4";
        private readonly X509Certificate2 _certificate;
        private readonly DateTimeOffset _timestamp;

        public TimestampHandler(DateTimeOffset timestamp)
        {
            _timestamp = timestamp;
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA RFC3161 Test TSA",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.8") },
                critical: true));
            _certificate = request.CreateSelfSigned(timestamp.AddDays(-1), timestamp.AddDays(1));
        }

        public string? RequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestContentType = request.Content?.Headers.ContentType?.MediaType;
            var requestBytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            Assert.True(Rfc3161TimestampRequest.TryDecode(requestBytes, out var timestampRequest, out var consumed));
            Assert.NotNull(timestampRequest);
            Assert.Equal(requestBytes.Length, consumed);

            var tokenInfo = new Rfc3161TimestampTokenInfo(
                new Oid("1.2.3.4.5"),
                timestampRequest.HashAlgorithmId,
                timestampRequest.GetMessageHash(),
                new byte[] { 1 },
                _timestamp,
                accuracyInMicroseconds: null,
                isOrdering: false,
                nonce: timestampRequest.GetNonce(),
                timestampAuthorityName: null,
                extensions: null);
            var cms = new SignedCms(
                new ContentInfo(new Oid(TimestampInfoOid), tokenInfo.Encode()),
                detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
            {
                IncludeOption = X509IncludeOption.EndCertOnly,
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1")
            };
            signer.SignedAttributes.Add(new AsnEncodedData(
                new Oid("1.2.840.113549.1.9.16.2.47"),
                BuildSigningCertificateV2(_certificate)));
            cms.ComputeSignature(signer);

            var responseWriter = new AsnWriter(AsnEncodingRules.DER);
            responseWriter.PushSequence();
            responseWriter.PushSequence();
            responseWriter.WriteInteger(0);
            responseWriter.PopSequence();
            responseWriter.WriteEncodedValue(cms.Encode());
            responseWriter.PopSequence();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseWriter.Encode())
            };
            response.Content.Headers.ContentType = new("application/timestamp-reply");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _certificate.Dispose();
            }

            base.Dispose(disposing);
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
    }
}
