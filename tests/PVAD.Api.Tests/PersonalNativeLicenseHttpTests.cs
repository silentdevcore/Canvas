using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Licensing;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class PersonalNativeLicenseHttpTests
{
    [Fact]
    public async Task Orgless_human_can_discover_and_download_only_personally_owned_licenses()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var licenseId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        using var root = new PvadApiTestFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            var database = "personal-native-http-" + Guid.NewGuid();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddSingleton(new Identity(userId, sessionId));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "PersonalNativeTest";
                options.DefaultChallengeScheme = "PersonalNativeTest";
            }).AddScheme<AuthenticationSchemeOptions, Authentication>("PersonalNativeTest", _ => { });
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string artifact;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            db.Users.Add(new PvadIdentityUser { Id = userId, UserName = "personal-only", DisplayName = "Personal only", EmailConfirmed = true });
            db.UserSessions.Add(new UserSession { Id = sessionId, UserId = userId, OrganizationId = null,
                IpAddressHash = "synthetic", UserAgent = "synthetic", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
            var row = License(userId, licenseId);
            artifact = row.ArtifactJson;
            db.NativeLibraryLicenses.Add(row);
            db.NativeLibraryLicenses.Add(License(Guid.NewGuid(), foreignId));
            await db.SaveChangesAsync();
        }
        var user = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/me");
        Assert.True(user.GetProperty("canReadPersonalNativeLicenses").GetBoolean());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("activeOrganizationId").ValueKind);
        Assert.Empty(user.GetProperty("organizations").EnumerateArray());
        Assert.DoesNotContain(user.GetProperty("permissions").EnumerateArray(), item => item.GetString() == PvadAccountPermissions.LicensesRead);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/account/native-licenses");
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(licenseId, item.GetProperty("id").GetGuid());
        Assert.Equal(userId, item.GetProperty("ownerUserId").GetGuid());
        using var download = await client.GetAsync($"/api/pvad/v1/account/native-licenses/{licenseId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(artifact, await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/pvad/v1/account/native-licenses/{foreignId}/download")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Service", "true");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/pvad/v1/account/native-licenses")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Service");
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pvad/v1/account/native-licenses")).StatusCode);
    }

    private static NativeLibraryLicense License(Guid owner, Guid id)
    {
        var now = PvadLicenseTimestamps.Normalize(DateTimeOffset.UtcNow);
        var payload = new PvadLicensePayload(2, id, "synthetic-private", PvadLicenseModel.Subscription,
            now, now, now.AddDays(30), null, false, [new(PvadLicenseProducts.Pdf, [])])
            { AccountScope = new("individualDeveloper", ["export.pdf"]) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var envelope = new PvadSignedNativeLicense(2, "synthetic-personal-http", PvadLicenseJson.Algorithm,
            Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        var artifact = JsonSerializer.Serialize(envelope, PvadLicenseJson.Options);
        return new() { Id = id, OwnerUserId = owner, OrganizationId = null, AuditEventId = Guid.NewGuid(),
            IssuedByUserId = Guid.NewGuid(), RequestId = Guid.NewGuid(), RequestSha256 = new string('a', 64),
            ArtifactJson = artifact, ArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(artifact))),
            LicenseModel = payload.Model.ToString(), CustomerReference = payload.CustomerReference, IssuedAt = now,
            ValidFrom = now, ValidUntil = payload.ValidUntil };
    }

    private sealed record Identity(Guid UserId, Guid SessionId);
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, Identity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, identity.UserId.ToString()), new(PvadClaimTypes.Session, identity.SessionId.ToString()) };
            if (Request.Headers.ContainsKey("X-Test-Service")) claims.Add(new(PvadClaimTypes.ServiceAccount, "synthetic"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
