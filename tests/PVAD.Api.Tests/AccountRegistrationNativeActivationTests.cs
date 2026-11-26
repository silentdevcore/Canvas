using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Licensing;
using PVAD.Testing;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed partial class AccountRegistrationControllerTests
{
    [PostgreSqlFact]
    public async Task Native_automatic_registration_roundtrip_downloads_an_offline_license_with_strict_expiry()
    {
        var keyPath = PvadTestEnvironment.Read("PVAD_TEST_NATIVE_ISSUER_PRIVATE_KEY");
        var requireEmbedded = PvadTestEnvironment.Read("PVAD_REQUIRE_NATIVE_ISSUER") == "1";
        if (requireEmbedded) Assert.False(string.IsNullOrWhiteSpace(keyPath));
        Console.WriteLine("native_registration_embedded_trust=" + (keyPath is not null));
        foreach (var type in new[] { "Company", "IndividualDeveloper" })
        {
            await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await postgres.StartAsync();
            using var scratch = new RegistrationScratch();
            using var fallback = keyPath is null ? new RegistrationSigner() : null;
            var actorId = Guid.NewGuid();
            var keyId = "synthetic-registration";
            if (keyPath is not null)
            {
                using var integrated = JsonDocument.Parse(File.ReadAllBytes(PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")!));
                keyId = integrated.RootElement.GetProperty("keyId").GetString()!;
            }
            using var baseline = CreateFactory(postgres.GetConnectionString());
            using var factory = baseline.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeysDirectory"] = Path.Combine(scratch.Root, "protection"),
                    ["Storage:RootPath"] = Path.Combine(scratch.Root, "storage"),
                    ["Licensing:PrivateKeyPath"] = Path.Combine(scratch.Root, "server-private.pem"),
                    ["Licensing:PublicKeyPath"] = Path.Combine(scratch.Root, "server-public.pem"),
                    ["PdfViewer:AnnotationStoragePath"] = Path.Combine(scratch.Root, "viewer"),
                    ["NativeLicenseIssuer:Enabled"] = "true", ["NativeLicenseIssuer:KeyId"] = keyId,
                    ["NativeLicenseIssuer:PrivateKeyPath"] = keyPath ?? Path.Combine(Path.GetTempPath(), "not-read-registration-test.pem"),
                    ["NativeLicenseIssuer:AllowedUserIds:0"] = actorId.ToString(),
                    ["NativeLicenseAutomation:Enabled"] = "true", ["NativeLicenseAutomation:IssuerUserId"] = actorId.ToString(),
                    ["NativeLicenseAutomation:PolicyVersion"] = "synthetic-free-three-months-v1",
                    ["NativeLicenseAutomation:Model"] = "Subscription", ["NativeLicenseAutomation:ValidityMonths"] = "3",
                }));
                if (fallback is not null) builder.ConfigureServices(services => services.AddScoped(provider =>
                    new PvadNativeLicenseIssuanceService(provider.GetRequiredService<PvadDbContext>(), fallback,
                        provider.GetRequiredService<IOptions<PvadNativeIssuerOptions>>(), provider.GetRequiredService<IOptions<PvadStepUpOptions>>(),
                        provider.GetRequiredService<PvadSystemOperatorAccess>(), TimeProvider.System)));
            });
            await SeedRolesAsync(factory.Services);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                var role = new PvadIdentityRole { Name = PvadRoles.SystemAdministrator, NormalizedName = PvadRoles.SystemAdministrator.ToUpperInvariant(), IsSystemRole = true };
                db.Roles.Add(role);
                db.Users.Add(new PvadIdentityUser { Id = actorId, UserName = "synthetic-issuer", Email = "synthetic-issuer@native-registration.test", DisplayName = "Synthetic manufacturer", EmailConfirmed = true });
                db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = actorId, RoleId = role.Id });
                await db.SaveChangesAsync();
            }
            using var client = CreateClient(factory);
            var email = type.ToLowerInvariant() + "@native-registration.test";
            using var register = CreateCsrfRequest(HttpMethod.Post, "/api/pvad/v1/auth/register", await GetCsrfAsync(client),
                new { email, displayName = "Synthetic Customer", password = "Pvad-Customer-Password-42!", accountType = type,
                    companyName = type == "Company" ? "Synthetic Company" : null, acceptTerms = true, acceptPrivacy = true });
            Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                Assert.Empty(await db.NativeLicenseRegistrationJobs.ToListAsync());
                await scope.ServiceProvider.GetRequiredService<PvadMailProcessor>().ProcessPendingAsync(default);
            }
            var mail = Assert.Single(factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages);
            var token = GetToken(mail.TextBody);
            using var verify = CreateCsrfRequest(HttpMethod.Post, "/api/pvad/v1/auth/verify-email", await GetCsrfAsync(client), new { token });
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(verify)).StatusCode);
            using var repeated = CreateCsrfRequest(HttpMethod.Post, "/api/pvad/v1/auth/verify-email", await GetCsrfAsync(client), new { token });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(repeated)).StatusCode);
            await LoginAsync(client, email, "Pvad-Customer-Password-42!", HttpStatusCode.OK);
            var pending = await client.GetFromJsonAsync<NativeLicensePage>("/api/pvad/v1/account/native-licenses");
            Assert.True(pending!.AutomaticIssuancePending);
            Assert.Empty(pending.Items);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<PvadNativeLicenseAutomationProcessor>();
                Assert.True(await processor.ProcessNextAsync(default));
                Assert.False(await processor.ProcessNextAsync(default));
            }
            var page = await client.GetFromJsonAsync<NativeLicensePage>("/api/pvad/v1/account/native-licenses");
            Assert.False(page!.AutomaticIssuancePending);
            var license = Assert.Single(page.Items);
            var customer = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/me");
            Assert.NotEqual(actorId, customer.GetProperty("id").GetGuid());
            Assert.Equal(type == "IndividualDeveloper" ? customer.GetProperty("id").GetGuid() : (Guid?)null, license.OwnerUserId);
            Assert.Equal(type == "Company", license.OrganizationId is not null);
            using var download = await client.GetAsync($"/api/pvad/v1/account/native-licenses/{license.Id}/download");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            var bytes = await download.Content.ReadAsByteArrayAsync();
            var verifier = fallback?.Verifier ?? PvadLicense.CreateEmbeddedVerifier();
            var payload = verifier.Verify(bytes);
            Assert.Equal(payload.ValidFrom.AddMonths(3), payload.ValidUntil);
            Assert.Equal(0, payload.GracePeriodDays);
            Assert.DoesNotContain(payload.Grants, grant => grant.Product.StartsWith("migration."));
            Assert.False(payload.RedistributionAllowed);
            var clock = new RegistrationClock { Now = payload.IssuedAt > payload.ValidFrom ? payload.IssuedAt : payload.ValidFrom };
            var offline = new PvadLicenseAuthority(verifier, clock, payload.ValidFrom.AddYears(-1));
            offline.Load(new MemoryStream(bytes));
            offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf");
            clock.Now = payload.ValidUntil!.Value.AddTicks(-1);
            offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf");
            clock.Now = payload.ValidUntil.Value;
            Assert.Equal(PvadLicenseState.Expired, offline.GetStatus().State);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => offline.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf")).Code);
            await using var finalScope = factory.Services.CreateAsyncScope();
            var finalDb = finalScope.ServiceProvider.GetRequiredService<PvadDbContext>();
            Assert.Single(await finalDb.NativeLicenseRegistrationJobs.ToListAsync());
            var retained = await finalDb.NativeLibraryLicenses.SingleAsync();
            Assert.Equal(Encoding.UTF8.GetString(bytes), retained.ArtifactJson);
            Assert.Equal(actorId, retained.IssuedByUserId);
            Assert.Single(await finalDb.AuditEvents.Where(row => row.Action == "native-licenses.issue").ToListAsync());
        }
    }

    private sealed class RegistrationScratch : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "pvad-registration-acceptance-" + Guid.NewGuid().ToString("N"));
        public RegistrationScratch() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RegistrationClock : TimeProvider { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class RegistrationSigner : IPvadNativeLicenseIssuerSigner, IDisposable
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal PvadLicenseVerifier Verifier => new(new Dictionary<string, string> { ["synthetic-registration"] = key.ExportSubjectPublicKeyInfoPem() });
        public string Sign(PvadLicensePayload payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options);
            return JsonSerializer.Serialize(new PvadSignedNativeLicense(payload.SchemaVersion, "synthetic-registration", PvadLicenseJson.Algorithm,
                Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), PvadLicenseJson.Options);
        }
        public PvadLicensePayload Verify(string artifact) => Verifier.Verify(Encoding.UTF8.GetBytes(artifact));
        public void Dispose() => key.Dispose();
    }
}
