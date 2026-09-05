using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PXA.Importer.Trust;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PxaPdfRevocationEvidenceProviderTests
{
    [Fact]
    public void AggregateResult_ShouldKeepAllEvidenceAndPreferConfirmedRevocation()
    {
        var now = DateTimeOffset.UtcNow;
        var result = PxaPdfRevocationEvidenceProvider.AggregateResult([
            new PdfRevocationEvidence(
                PdfRevocationEvidenceKind.Crl,
                PdfTrustStatus.Valid,
                "configured:issuer.crl",
                now,
                now.AddHours(1),
                [0x01]),
            new PdfRevocationEvidence(
                PdfRevocationEvidenceKind.Ocsp,
                PdfTrustStatus.Invalid,
                "configured:signer.ocsp",
                now.AddMinutes(-1),
                now.AddMinutes(30),
                [0x02])
        ]);

        Assert.Equal(PdfTrustStatus.Invalid, result.Status);
        Assert.Equal(2, result.Evidence.Count);
        Assert.Equal(PdfRevocationEvidenceKind.Ocsp, result.Evidence[0].Kind);
    }

    [Fact]
    public async Task GetEvidence_ShouldValidateDownloadAndReuseEncryptedCache()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-revocation-cache-");
        try
        {
            using var fixture = CrlFixture.Create("https://crl.example.test/root.crl");
            var crl = new CertificateRevocationListBuilder().Build(
                fixture.Issuer,
                new BigInteger(1),
                fixture.Now.AddHours(1),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1,
                fixture.Now.AddHours(-1));
            var handler = new CountingHandler(crl);
            var provider = CreateProvider(directory.FullName, handler, ["crl.example.test"]);
            var request = new PdfRevocationEvidenceRequest(
                fixture.Certificate,
                fixture.Issuer,
                fixture.Now,
                PdfCertificateRevocationMode.Online);

            var downloaded = await provider.GetEvidenceAsync(request);
            var cached = await provider.GetEvidenceAsync(request);

            Assert.Equal(PdfTrustStatus.Valid, downloaded.Status);
            Assert.Equal(PdfTrustStatus.Valid, cached.Status);
            Assert.Equal(1, handler.CallCount);
            var cacheFile = Assert.Single(Directory.GetFiles(Path.Combine(directory.FullName, "cache")));
            Assert.NotEqual(crl, await File.ReadAllBytesAsync(cacheFile));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetEvidence_ShouldRejectEndpointOutsideAllowlist()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-revocation-cache-");
        try
        {
            using var fixture = CrlFixture.Create("https://blocked.example.test/root.crl");
            var handler = new CountingHandler([0x30, 0x00]);
            var provider = CreateProvider(directory.FullName, handler, ["allowed.example.test"]);

            var result = await provider.GetEvidenceAsync(new PdfRevocationEvidenceRequest(
                fixture.Certificate,
                fixture.Issuer,
                fixture.Now,
                PdfCertificateRevocationMode.Online));

            Assert.Equal(PdfTrustStatus.Indeterminate, result.Status);
            Assert.Equal(0, handler.CallCount);
            Assert.Contains("allowlist", result.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetEvidence_ShouldPostBoundOcspRequestToAllowlistedEndpoint()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-revocation-cache-");
        try
        {
            using var fixture = CrlFixture.Create(null, "https://ocsp.example.test/status");
            var handler = new CountingHandler([0x30, 0x00]);
            var provider = CreateProvider(directory.FullName, handler, ["ocsp.example.test"]);

            var result = await provider.GetEvidenceAsync(new PdfRevocationEvidenceRequest(
                fixture.Certificate,
                fixture.Issuer,
                fixture.Now,
                PdfCertificateRevocationMode.Online));

            Assert.Equal(PdfTrustStatus.Indeterminate, result.Status);
            Assert.Equal(1, handler.CallCount);
            Assert.Equal(HttpMethod.Post, handler.LastMethod);
            Assert.Equal("application/ocsp-request", handler.LastContentType);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetIssuerCertificates_ShouldDownloadValidateAndReuseEncryptedAiaCache()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-aia-cache-");
        try
        {
            using var fixture = CrlFixture.Create(
                issuerEndpoint: "https://aia.example.test/issuer.cer");
            var handler = new CountingHandler(fixture.Issuer.RawData);
            var provider = CreateProvider(directory.FullName, handler, ["aia.example.test"]);
            var request = new PdfIssuerCertificateRequest(
                fixture.Certificate,
                fixture.Now,
                PdfCertificateRevocationMode.Online);

            var downloaded = await provider.GetIssuerCertificatesAsync(request);
            var cached = await provider.GetIssuerCertificatesAsync(request with
            {
                Mode = PdfCertificateRevocationMode.Offline
            });

            Assert.Single(downloaded.Certificates);
            Assert.Single(cached.Certificates);
            Assert.Equal(fixture.Issuer.RawData, downloaded.Certificates[0]);
            Assert.Equal(1, handler.CallCount);
            var cacheFile = Assert.Single(Directory.GetFiles(Path.Combine(directory.FullName, "cache")));
            Assert.NotEqual(fixture.Issuer.RawData, await File.ReadAllBytesAsync(cacheFile));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetIssuerCertificates_ShouldRejectAiaOutsideAllowlist()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-aia-cache-");
        try
        {
            using var fixture = CrlFixture.Create(
                issuerEndpoint: "https://blocked.example.test/issuer.cer");
            var handler = new CountingHandler(fixture.Issuer.RawData);
            var provider = CreateProvider(directory.FullName, handler, ["allowed.example.test"]);

            var result = await provider.GetIssuerCertificatesAsync(new PdfIssuerCertificateRequest(
                fixture.Certificate,
                fixture.Now,
                PdfCertificateRevocationMode.Online));

            Assert.Empty(result.Certificates);
            Assert.Equal(0, handler.CallCount);
            Assert.Contains("allowlist", result.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static PxaPdfRevocationEvidenceProvider CreateProvider(
        string contentRoot,
        HttpMessageHandler handler,
        string[] allowedHosts) => new(
        new TestHttpClientFactory(new HttpClient(handler)),
        new EphemeralDataProtectionProvider(),
        Options.Create(new PxaPdfRevocationEvidenceOptions
        {
            Enabled = true,
            AllowedHosts = allowedHosts,
            CacheDirectory = "cache",
            MaximumResponseBytes = 1024 * 1024
        }),
        new TestHostEnvironment(contentRoot),
        NullLogger<PxaPdfRevocationEvidenceProvider>.Instance);

    private sealed class CountingHandler(byte[] response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastContentType { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(response)
            });
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "PXA.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

        public static CrlFixture Create(
            string? crlEndpoint = null,
            string? ocspEndpoint = null,
            string? issuerEndpoint = null)
        {
            var now = DateTimeOffset.UtcNow;
            using var issuerKey = RSA.Create(2048);
            var issuerRequest = new CertificateRequest(
                "CN=PXA Evidence Test Root",
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
                "CN=PXA Evidence Test Signer",
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
            if (ocspEndpoint is not null || issuerEndpoint is not null)
            {
                leafRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(
                    ocspEndpoint is null ? [] : [ocspEndpoint],
                    issuerEndpoint is null ? [] : [issuerEndpoint],
                    false));
            }
            var certificate = leafRequest.Create(
                issuer,
                now.AddHours(-1),
                now.AddHours(1),
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
