using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.Importer.Trust;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

public sealed class PvadPdfTrustPolicyProviderTests
{
    [Fact]
    public void Constructor_LoadsConfiguredTrustAnchorRelativeToContentRoot()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-trust-policy-");
        try
        {
            using var certificate = CreateCertificate();
            File.WriteAllBytes(Path.Combine(directory.FullName, "root.cer"), certificate.RawData);
            using var provider = new PvadPdfTrustPolicyProvider(
                Options.Create(new PvadPdfTrustPolicyOptions
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchorPaths = ["root.cer"],
                    RevocationMode = PdfCertificateRevocationMode.Offline
                }),
                new TestHostEnvironment(directory.FullName));

            var anchor = Assert.Single(provider.Policy.CustomTrustAnchors);
            Assert.Equal(certificate.Thumbprint, anchor.Thumbprint);
            Assert.False(provider.Policy.UseSystemTrustStore);
            Assert.Equal(PdfCertificateRevocationMode.Offline, provider.Policy.RevocationMode);
            Assert.False(provider.Policy.AllowCertificateDownloads);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Constructor_RejectsMissingConfiguredCertificate()
    {
        var directory = Directory.CreateTempSubdirectory("pxa-trust-policy-");
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                new PvadPdfTrustPolicyProvider(
                    Options.Create(new PvadPdfTrustPolicyOptions
                    {
                        CustomTrustAnchorPaths = ["missing.cer"]
                    }),
                    new TestHostEnvironment(directory.FullName)));

            Assert.Contains("does not exist", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA Trust Anchor Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
