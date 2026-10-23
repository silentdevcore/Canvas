using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class PvadLicenseRenameCompatibilityTests
{
    [Theory]
    [InlineData("PXA-RETAINED-1", "pxa", false)]
    [InlineData("CUSTOM-RETAINED-1", "pxa", false)]
    [InlineData("PVAD-NEW-1", "pvad", true)]
    public void Download_identity_does_not_rewrite_signed_fields(string number, string identity, bool hasExactText)
    {
        const string raw = "{ \"licenseNumber\": \"synthetic\", \"custom\": \"PXA retained\" }";
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(raw), HashAlgorithmName.SHA256));
        var license = new OfflineLicense
        {
            LicenseNumber = number, EnvelopeJson = raw, Signature = signature,
            KeyId = "pxa-retained-signing-key", Algorithm = "ECDSA_P256_SHA256",
        };
        var result = PvadLicenseDownload.Create(license);
        Assert.Equal($"application/vnd.{identity}.license+json", result.ContentType);
        Assert.Equal($"{number}.{identity}-license.json", result.FileDownloadName);
        using var artifact = JsonDocument.Parse(result.FileContents);
        Assert.Equal(signature, artifact.RootElement.GetProperty("signature").GetString());
        Assert.Equal(license.KeyId, artifact.RootElement.GetProperty("keyId").GetString());
        Assert.Equal(hasExactText, artifact.RootElement.TryGetProperty("envelopeJson", out var exact));
        if (hasExactText)
        {
            Assert.Equal(raw, exact.GetString());
            Assert.True(key.VerifyData(Encoding.UTF8.GetBytes(exact.GetString()!),
                Convert.FromBase64String(signature), HashAlgorithmName.SHA256));
        }
        Assert.Equal(raw, license.EnvelopeJson);
        Assert.Equal(number, license.LicenseNumber);
    }

    [Fact]
    public void Retained_key_pair_verifies_old_signatures_after_service_restart()
    {
        WithDirectory(root =>
        {
            var options = Options.Create(new PvadLicensingOptions());
            var environment = new TestEnvironment { ContentRootPath = root };
            var first = new PvadLicenseSigningService(options, environment);
            const string raw = "{\"legacy\":\"PXA\"}";
            var privatePath = Path.Combine(root, options.Value.PrivateKeyPath);
            using var key = ECDsa.Create();
            key.ImportFromPem(File.ReadAllText(privatePath));
            var signature = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(raw), HashAlgorithmName.SHA256));
            var before = File.ReadAllBytes(privatePath);
            var restarted = new PvadLicenseSigningService(options, environment);
            Assert.Equal(first.PublicKeyPem, restarted.PublicKeyPem);
            Assert.Equal("pxa-development-1", restarted.KeyId);
            Assert.Equal(before, File.ReadAllBytes(privatePath));
            Assert.True(restarted.Verify(raw, signature));
            Assert.False(restarted.Verify(raw.Replace("PXA", "PVAD"), signature));
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Previously_signed_schema_remains_valid_without_rewriting_payload(int schema)
    {
        WithDirectory(root =>
        {
            var options = Options.Create(new PvadLicensingOptions());
            var environment = new TestEnvironment { ContentRootPath = root };
            var signer = new PvadLicenseSigningService(options, environment);
            var now = DateTimeOffset.UtcNow;
            var subscription = new OrganizationSubscription
            {
                Edition = SubscriptionEdition.Enterprise, AccountType = SubscriptionAccountType.Company,
                DeploymentMode = SubscriptionDeploymentMode.OnPremise,
            };
            var license = new OfflineLicense
            {
                OrganizationId = subscription.OrganizationId, SubscriptionId = subscription.Id,
                LicenseNumber = "PXA-SYNTHETIC-RETAINED", EnvelopeJson = "", Signature = "",
                KeyId = signer.KeyId, Algorithm = "ECDSA_P256_SHA256",
                ValidFrom = now.AddDays(-1), ValidUntil = now.AddDays(30), IssuedAt = now,
            };
            var artifact = signer.Sign(new PvadOfflineLicenseEnvelope(schema, license.Id,
                license.LicenseNumber, license.OrganizationId, "Synthetic Organization", "Enterprise",
                "Company", "OnPremise", license.ValidFrom, license.ValidUntil, license.InstanceLimit,
                "1.1.0", "synthetic-deployment", [], now));
            license.EnvelopeJson = artifact.EnvelopeJson;
            license.Signature = artifact.Signature;
            var restarted = new PvadLicenseSigningService(options, environment);
            var result = new PvadOfflineLicenseValidator(restarted).Validate(license, subscription, now);
            Assert.True(result.Valid);
            Assert.True(result.SignatureValid);
            Assert.Equal(artifact.EnvelopeJson, license.EnvelopeJson);
            Assert.Equal(artifact.Signature, license.Signature);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Incomplete_key_pair_is_never_replaced_even_in_development(bool privateExists)
    {
        WithDirectory(root =>
        {
            var options = new PvadLicensingOptions();
            var retained = Path.Combine(root, privateExists ? options.PrivateKeyPath : options.PublicKeyPath);
            Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
            File.WriteAllText(retained, "synthetic-retained-key-material");
            Assert.Throws<InvalidOperationException>(() => new PvadLicenseSigningService(
                Options.Create(options), new TestEnvironment { ContentRootPath = root }));
            Assert.Equal("synthetic-retained-key-material", File.ReadAllText(retained));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        });
    }

    private static void WithDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "pvad-license-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
