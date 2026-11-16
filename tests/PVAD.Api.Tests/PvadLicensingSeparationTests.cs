using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class PvadLicensingSeparationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pvad-public-license-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public PvadLicensingSeparationTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(PublicPath, key.ExportSubjectPublicKeyInfoPem());
    }

    private string PublicPath => Path.Combine(root, "public.pem");
    private string PrivatePath => Path.Combine(root, "issuer-only", "private.pem");
    private PvadLicensingOptions Configuration(bool issuer = false) => new()
    {
        IssuerEnabled = issuer, KeyId = "synthetic-public-only", PublicKeyPath = PublicPath, PrivateKeyPath = PrivatePath,
    };
    private IWebHostEnvironment Environment(string name = "Testing") => new TestEnvironment(root, name);

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Production")]
    public void Verifier_only_mode_never_requires_or_creates_a_private_key(string environment)
    {
        var options = Options.Create(Configuration());
        var verifier = new PvadLicenseSignatureVerifier(options, Environment(environment));
        using var signer = new PvadLicenseSigningService(options, Environment(environment), verifier);
        var raw = "{ \"schemaVersion\": 1, \"retained\": \"synthetic\" }";
        Assert.False(signer.CanSign);
        Assert.True(verifier.Verify(raw, Sign(raw)));
        Assert.Equal(key.ExportSubjectPublicKeyInfoPem(), verifier.PublicKeyPem);
        Assert.Equal("PVAD_LICENSE_ISSUANCE_DISABLED", Assert.Throws<PvadLicenseSigningException>(() => signer.Sign(Envelope())).Code);
        Assert.False(Directory.Exists(Path.GetDirectoryName(PrivatePath)));
        Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Existing_schemas_validate_with_public_key_only_and_do_not_rewrite_artifacts(int schema)
    {
        var (license, subscription) = ExistingLicense(schema);
        var originalJson = license.EnvelopeJson;
        var originalSignature = license.Signature;
        var verifier = new PvadLicenseSignatureVerifier(Options.Create(Configuration()), Environment());
        var result = new PvadOfflineLicenseValidator(verifier).Validate(license, subscription, Now);
        Assert.True(result.Valid, result.Code);
        Assert.True(result.SignatureValid);
        Assert.Equal(originalJson, license.EnvelopeJson);
        Assert.Equal(originalSignature, license.Signature);
        Assert.False(File.Exists(PrivatePath));
    }

    [Theory]
    [InlineData("tampered", "PVAD_LICENSE_SIGNATURE_INVALID")]
    [InlineData("expired", "PVAD_LICENSE_EXPIRED")]
    [InlineData("future", "PVAD_LICENSE_NOT_YET_VALID")]
    [InlineData("revoked", "PVAD_LICENSE_REVOKED")]
    public void Public_only_validation_keeps_existing_rejection_diagnostics(string scenario, string expected)
    {
        var (license, subscription) = ExistingLicense(2, scenario);
        var verifier = new PvadLicenseSignatureVerifier(Options.Create(Configuration()), Environment());
        var result = new PvadOfflineLicenseValidator(verifier).Validate(license, subscription, Now);
        Assert.False(result.Valid);
        Assert.Equal(expected, result.Code);
        Assert.False(File.Exists(PrivatePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AA==")]
    public void Verifier_rejects_malformed_signature_encodings(string signature)
    {
        var verifier = new PvadLicenseSignatureVerifier(Options.Create(Configuration()), Environment());
        Assert.False(verifier.Verify("{}", signature));
    }

    [Fact]
    public void Enabled_issuer_loads_private_material_only_when_signing_and_checks_pair_match()
    {
        var options = Options.Create(Configuration(true));
        using var signer = new PvadLicenseSigningService(options, Environment());
        Assert.True(signer.CanSign);
        Assert.False(Directory.Exists(Path.GetDirectoryName(PrivatePath)));
        WritePrivateKey(key);
        var envelope = Envelope();
        var artifact = signer.Sign(envelope);
        Assert.Equal("ECDSA_P256_SHA256", artifact.Algorithm);
        Assert.Equal(options.Value.KeyId, artifact.KeyId);
        Assert.True(new PvadLicenseSignatureVerifier(options, Environment()).Verify(artifact.EnvelopeJson, artifact.Signature));

        using var foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        WritePrivateKey(foreign);
        using var mismatch = new PvadLicenseSigningService(options, Environment());
        var error = Assert.Throws<PvadLicenseSigningException>(() => mismatch.Sign(envelope));
        Assert.Equal("PVAD_LICENSE_ISSUER_UNAVAILABLE", error.Code);
        Assert.DoesNotContain(root, error.Message);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Production")]
    public void Issuer_does_not_generate_missing_keys_in_any_environment(string environment)
    {
        using var signer = new PvadLicenseSigningService(Options.Create(Configuration(true)), Environment(environment));
        Assert.Equal("PVAD_LICENSE_ISSUER_UNAVAILABLE", Assert.Throws<PvadLicenseSigningException>(() => signer.Sign(Envelope())).Code);
        Assert.False(Directory.Exists(Path.GetDirectoryName(PrivatePath)));
        Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Public_key_file_must_not_contain_a_private_key()
    {
        File.WriteAllText(PublicPath, key.ExportECPrivateKeyPem());
        var verifier = new PvadLicenseSignatureVerifier(Options.Create(Configuration()), Environment());
        var error = Assert.Throws<PvadLicenseVerificationUnavailableException>(() => verifier.PublicKeyPem);
        Assert.DoesNotContain("PRIVATE", error.Message);
        var (license, subscription) = ExistingLicense(2);
        Assert.Equal(PvadLicenseVerificationUnavailableException.Code,
            new PvadOfflineLicenseValidator(verifier).Validate(license, subscription, Now).Code);
    }

    [Fact]
    public void Signer_and_verifier_interfaces_are_separate()
    {
        Assert.False(typeof(IPvadLicenseSignatureVerifier).IsAssignableFrom(typeof(IPvadLicenseSigningService)));
        Assert.False(typeof(IPvadLicenseSignatureVerifier).IsAssignableFrom(typeof(PvadLicenseSigningService)));
        Assert.DoesNotContain(typeof(PvadLicenseSignatureVerifier).GetMethods(), method => method.Name == "Sign");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Even_system_administrators_cannot_issue_or_replace_on_a_verifier_server(bool replace)
    {
        using var signer = new PvadLicenseSigningService(Options.Create(Configuration()), Environment());
        var controller = new AdminLicensesController(null!, new Tenant(Guid.NewGuid(), Guid.NewGuid()), signer, null!, null!)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, PvadRoles.SystemAdministrator)], "synthetic")),
            } },
        };
        var response = replace
            ? await controller.ReplaceLicense(Guid.NewGuid(), new("synthetic"), default)
            : await controller.IssueLicense(new(Guid.NewGuid(), Now, Now.AddDays(1), 1, "1.1.0", "synthetic"), default);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(response.Result).Value);
        Assert.Equal(403, problem.Status);
        Assert.Equal("PVAD_LICENSE_ISSUANCE_DISABLED", problem.Extensions["code"]);
        Assert.False(File.Exists(PrivatePath));
    }

    [Fact]
    public async Task Actual_api_resolves_public_only_verifier_with_blank_private_path()
    {
        using var factory = new PvadApiTestFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:IssuerEnabled"] = "false", ["Licensing:PrivateKeyPath"] = "",
                ["Licensing:PublicKeyPath"] = PublicPath, ["Licensing:KeyId"] = "synthetic-public-only",
                ["DataProtection:KeysDirectory"] = Path.Combine(root, "data-protection"), ["OAuth:Enabled"] = "false",
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<PvadDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
                services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase("license-public-only-" + Guid.NewGuid()));
            });
        });
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/pvad/v1/licensing/public-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<LicensePublicKeyResponse>();
        Assert.Equal(key.ExportSubjectPublicKeyInfoPem(), data!.PublicKeyPem);
        Assert.IsType<PvadLicenseSignatureVerifier>(factory.Services.GetRequiredService<IPvadLicenseSignatureVerifier>());
        Assert.False(factory.Services.GetRequiredService<IPvadLicenseSigningService>().CanSign);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        var (license, subscription) = ExistingLicense(2, now: DateTimeOffset.UtcNow);
        db.AddRange(new Organization { Id = subscription.OrganizationId, Name = "Synthetic", Slug = "synthetic-public-only" }, subscription, license);
        await db.SaveChangesAsync();
        var admin = new AdminLicensesController(db, new Tenant(Guid.NewGuid(), subscription.OrganizationId),
            factory.Services.GetRequiredService<IPvadLicenseSigningService>(),
            factory.Services.GetRequiredService<PvadOfflineLicenseValidator>(), null!)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, PvadRoles.SystemAdministrator)], "synthetic")),
            } },
        };
        var list = Assert.IsType<OkObjectResult>((await admin.GetLicenses(default)).Result);
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<AdminLicenseResponse>>(list.Value));
        Assert.IsType<OkObjectResult>((await admin.GetLicense(license.Id, default)).Result);
        Assert.IsType<FileContentResult>(await admin.DownloadLicense(license.Id, default));
        var validated = Assert.IsType<OkObjectResult>((await admin.ValidateLicense(license.Id, "1.1.0", "synthetic")).Result);
        Assert.True(Assert.IsType<AdminLicenseValidationResponse>(validated.Value).Valid);
        Assert.False(File.Exists(PrivatePath));
    }

    [Fact]
    public void Missing_public_key_returns_an_actionable_problem_without_paths_or_key_generation()
    {
        File.Delete(PublicPath);
        var verifier = new PvadLicenseSignatureVerifier(Options.Create(Configuration()), Environment());
        var result = new LicensePublicKeyController { ControllerContext = new() { HttpContext = new DefaultHttpContext() } }.GetPublicKey(verifier);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result.Result).Value);
        Assert.Equal(503, problem.Status);
        Assert.Equal(PvadLicenseVerificationUnavailableException.Code, problem.Extensions["code"]);
        Assert.DoesNotContain(root, problem.Detail!);
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }

    private string Sign(string raw) => Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(raw), HashAlgorithmName.SHA256));

    private PvadOfflineLicenseEnvelope Envelope() => new(2, Guid.NewGuid(), "PVAD-SYNTHETIC", Guid.NewGuid(),
        "Synthetic", "Enterprise", "Company", "OnPremise", Now.AddDays(-1), Now.AddDays(10), 2, "1.1.0", "synthetic", [], Now);

    private (OfflineLicense, OrganizationSubscription) ExistingLicense(int schema, string? scenario = null, DateTimeOffset? now = null)
    {
        var subscription = new OrganizationSubscription { OrganizationId = Guid.NewGuid(), Status = SubscriptionStatus.Active,
            Edition = SubscriptionEdition.Enterprise,
            AccountType = SubscriptionAccountType.Company, DeploymentMode = SubscriptionDeploymentMode.OnPremise };
        var instant = now ?? Now;
        var envelope = Envelope() with { SchemaVersion = schema, OrganizationId = subscription.OrganizationId,
            ValidUntil = scenario == "expired" ? instant : instant.AddDays(10),
            ValidFrom = scenario == "future" ? instant.AddDays(1) : instant.AddDays(-1), IssuedAt = instant };
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var license = new OfflineLicense
        {
            Id = envelope.LicenseId, OrganizationId = envelope.OrganizationId, SubscriptionId = subscription.Id,
            LicenseNumber = envelope.LicenseNumber, EnvelopeJson = scenario == "tampered" ? json.Replace("Synthetic", "Changed") : json,
            Signature = Sign(json), KeyId = "synthetic-public-only", Algorithm = "ECDSA_P256_SHA256",
            ValidFrom = envelope.ValidFrom, ValidUntil = envelope.ValidUntil, IssuedAt = envelope.IssuedAt,
            InstanceLimit = envelope.InstanceLimit, Status = scenario == "revoked" ? OfflineLicenseStatus.Revoked : OfflineLicenseStatus.Active,
        };
        return (license, subscription);
    }

    private void WritePrivateKey(ECDsa signer)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrivatePath)!);
        File.WriteAllText(PrivatePath, signer.ExportECPrivateKeyPem());
    }

    public void Dispose() { key.Dispose(); Directory.Delete(root, recursive: true); }

    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : IPvadTenantContext;
    private sealed class TestEnvironment(string root, string name) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = name;
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
