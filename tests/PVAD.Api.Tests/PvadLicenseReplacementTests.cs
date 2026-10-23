using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadLicenseReplacementTests
{
    [Fact]
    public async Task Replacement_requires_privileged_policy_CSRF_and_system_role()
    {
        var method = typeof(AdminLicensesController).GetMethod(nameof(AdminLicensesController.ReplaceLicense))!;
        Assert.Equal(PvadPermissions.LicensesManage, method.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.NotNull(method.GetCustomAttribute<PvadValidateAntiforgeryAttribute>());
        var controller = new AdminLicensesController(null!, new Tenant(Guid.NewGuid(), Guid.NewGuid()), null!, null!, null!)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(ClaimTypes.Role, PvadRoles.OrganizationAdministrator)], "synthetic")),
                },
            },
        };
        Assert.IsType<ForbidResult>((await controller.ReplaceLicense(Guid.NewGuid(), new("Synthetic reason"), default)).Result);
    }

    [PostgreSqlFact]
    public async Task Replacement_preserves_rights_and_signed_history_and_rejects_replay()
    {
        await using var harness = await Harness.CreateAsync();
        await using var db = new PvadDbContext(harness.Options);
        var before = await db.OfflineLicenses.AsNoTracking().SingleAsync();
        var controller = harness.Controller(db);
        var rebinding = await controller.ReplaceLicense(before.Id, new("Synthetic reason", "9.0.0"), default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(rebinding.Result).StatusCode);
        var response = await controller.ReplaceLicense(before.Id, new("Technical rename"), default);
        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var summary = Assert.IsType<AdminLicenseResponse>(created.Value);
        db.ChangeTracker.Clear();
        var previous = await db.OfflineLicenses.SingleAsync(value => value.Id == before.Id);
        var replacement = await db.OfflineLicenses.SingleAsync(value => value.Id == summary.Id);
        Assert.Equal(OfflineLicenseStatus.Replaced, previous.Status);
        Assert.Equal(before.EnvelopeJson, previous.EnvelopeJson);
        Assert.Equal(before.Signature, previous.Signature);
        Assert.Equal(before.KeyId, previous.KeyId);
        Assert.Equal(before.LicenseNumber, previous.LicenseNumber);
        Assert.StartsWith("PVAD-", replacement.LicenseNumber);
        Assert.Equal(before.OrganizationId, replacement.OrganizationId);
        Assert.Equal(before.SubscriptionId, replacement.SubscriptionId);
        Assert.Equal(before.ValidFrom, replacement.ValidFrom);
        Assert.Equal(before.ValidUntil, replacement.ValidUntil);
        Assert.Equal(before.InstanceLimit, replacement.InstanceLimit);
        var oldEnvelope = JsonSerializer.Deserialize<PvadOfflineLicenseEnvelope>(before.EnvelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var newEnvelope = JsonSerializer.Deserialize<PvadOfflineLicenseEnvelope>(replacement.EnvelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(oldEnvelope.Entitlements.ToArray(), newEnvelope.Entitlements.ToArray());
        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        var validator = new PvadOfflineLicenseValidator(harness.Signer);
        Assert.True(validator.Validate(replacement, subscription, DateTimeOffset.UtcNow).Valid);
        Assert.Equal("PXA_LICENSE_REPLACED", validator.Validate(previous, subscription, DateTimeOffset.UtcNow).Code);
        Assert.Single(await db.AuditEvents.Where(value => value.Action == "licenses.replace").ToArrayAsync());
        Assert.Single(await db.MailOutboxMessages.ToArrayAsync());
        var replay = await controller.ReplaceLicense(before.Id, new("Technical rename"), default);
        Assert.Equal(409, Assert.IsType<ObjectResult>(replay.Result).StatusCode);
        Assert.Equal(2, await db.OfflineLicenses.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Failure_after_database_write_rolls_back_old_new_audit_and_outbox()
    {
        await using var harness = await Harness.CreateAsync();
        var options = new DbContextOptionsBuilder<PvadDbContext>(harness.Options)
            .AddInterceptors(new FailAfterSave()).Options;
        await using (var failing = new PvadDbContext(options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Controller(failing)
                .ReplaceLicense(harness.LicenseId, new("Synthetic rollback"), default));
        }
        await using var verify = new PvadDbContext(harness.Options);
        Assert.Equal(OfflineLicenseStatus.Active, (await verify.OfflineLicenses.SingleAsync()).Status);
        Assert.Empty(await verify.AuditEvents.ToArrayAsync());
        Assert.Empty(await verify.MailOutboxMessages.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task Schema_one_upgrade_requires_explicit_binding_and_preserves_original()
    {
        await using var harness = await Harness.CreateAsync(1);
        await using var db = new PvadDbContext(harness.Options);
        var original = await db.OfflineLicenses.AsNoTracking().SingleAsync();
        var controller = harness.Controller(db);
        var missing = await controller.ReplaceLicense(original.Id, new("Schema upgrade"), default);
        Assert.Equal(400, Assert.IsType<BadRequestObjectResult>(missing.Result).StatusCode);
        Assert.Single(await db.OfflineLicenses.ToArrayAsync());
        var response = await controller.ReplaceLicense(original.Id,
            new("Schema upgrade", "1.1.0", "synthetic-deployment"), default);
        Assert.IsType<CreatedAtActionResult>(response.Result);
        db.ChangeTracker.Clear();
        Assert.Equal(original.EnvelopeJson, (await db.OfflineLicenses.SingleAsync(value => value.Id == original.Id)).EnvelopeJson);
        var replacement = await db.OfflineLicenses.SingleAsync(value => value.Id != original.Id);
        var envelope = JsonSerializer.Deserialize<PvadOfflineLicenseEnvelope>(replacement.EnvelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, envelope.SchemaVersion);
        Assert.Equal("1.1.0", envelope.ProductVersion);
        Assert.Equal("synthetic-deployment", envelope.DeploymentId);
    }

    [PostgreSqlFact]
    public async Task Concurrent_replacements_create_only_one_successor()
    {
        await using var harness = await Harness.CreateAsync();
        async Task<ActionResult<AdminLicenseResponse>> ReplaceAsync()
        {
            await using var db = new PvadDbContext(harness.Options);
            return await harness.Controller(db).ReplaceLicense(harness.LicenseId, new("Concurrent replacement"), default);
        }
        var responses = await Task.WhenAll(ReplaceAsync(), ReplaceAsync());
        Assert.Single(responses, value => value.Result is CreatedAtActionResult);
        Assert.Single(responses, value => value.Result is ObjectResult { StatusCode: 409 });
        await using var verify = new PvadDbContext(harness.Options);
        Assert.Equal(2, await verify.OfflineLicenses.CountAsync());
        Assert.Single(await verify.AuditEvents.Where(value => value.Action == "licenses.replace").ToArrayAsync());
        Assert.Single(await verify.MailOutboxMessages.ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task Unknown_signed_constraints_are_not_silently_removed()
    {
        await using var harness = await Harness.CreateAsync();
        await using var db = new PvadDbContext(harness.Options);
        var license = await db.OfflineLicenses.SingleAsync();
        var payload = System.Text.Json.Nodes.JsonNode.Parse(license.EnvelopeJson)!;
        payload["futureConstraint"] = "preserve-or-reject";
        license.EnvelopeJson = payload.ToJsonString();
        license.Signature = harness.Signer.SignText(license.EnvelopeJson);
        await db.SaveChangesAsync();
        var result = await harness.Controller(db).ReplaceLicense(license.Id, new("Unknown constraint"), default);
        Assert.Equal(409, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Single(await db.OfflineLicenses.ToArrayAsync());
        Assert.Empty(await db.AuditEvents.ToArrayAsync());
        Assert.Empty(await db.MailOutboxMessages.ToArrayAsync());
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic post-write failure");
    }

    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : IPvadTenantContext;

    private sealed class Harness : IAsyncDisposable
    {
        private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        public DbContextOptions<PvadDbContext> Options { get; private set; } = null!;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid LicenseId { get; private set; }
        public MemorySigner Signer { get; } = new();

        public static async Task<Harness> CreateAsync(int schema = 2)
        {
            var harness = new Harness();
            try { await harness.InitializeAsync(schema); return harness; }
            catch { await harness.DisposeAsync(); throw; }
        }

        private async Task InitializeAsync(int schema)
        {
            await postgres.StartAsync();
            Options = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
            await using var db = new PvadDbContext(Options);
            await db.Database.EnsureCreatedAsync();
            var org = new Organization { Id = OrganizationId, Name = "Synthetic Organization", Slug = "synthetic" };
            var subscription = new OrganizationSubscription
            {
                OrganizationId = org.Id, Edition = SubscriptionEdition.Enterprise,
                AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
                DeploymentMode = SubscriptionDeploymentMode.OnPremise,
            };
            var user = new PvadIdentityUser
            {
                Id = UserId, DisplayName = "Synthetic Administrator", UserName = "synthetic@example.test",
                Email = "synthetic@example.test", EmailConfirmed = true,
            };
            var role = new PvadIdentityRole { Id = Guid.NewGuid(), Name = PvadRoles.OrganizationAdministrator };
            var membership = new OrganizationMembership { UserId = UserId, OrganizationId = org.Id };
            db.AddRange(org, subscription, user, role, membership,
                new OrganizationMembershipRole { OrganizationMembershipId = membership.Id, RoleId = role.Id });
            var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var license = new OfflineLicense
            {
                OrganizationId = org.Id, SubscriptionId = subscription.Id, LicenseNumber = "PXA-SYNTHETIC-1",
                EnvelopeJson = "", Signature = "", KeyId = Signer.KeyId, Algorithm = "ECDSA_P256_SHA256",
                ValidFrom = now.AddDays(-1), ValidUntil = now.AddDays(10), IssuedAt = now,
                InstanceLimit = 2, IssuedByUserId = UserId,
            };
            var artifact = Signer.Sign(new(schema, license.Id, license.LicenseNumber, org.Id, org.Name,
                "Enterprise", "Company", "OnPremise", license.ValidFrom, license.ValidUntil, 2,
                schema == 1 ? "" : "1.1.0", schema == 1 ? "" : "synthetic-deployment",
                [new("designer", true, 5, "operations", null)], now));
            license.EnvelopeJson = artifact.EnvelopeJson;
            license.Signature = artifact.Signature;
            db.Add(license);
            await db.SaveChangesAsync();
            LicenseId = license.Id;
        }

        public AdminLicensesController Controller(PvadDbContext db)
        {
            var queue = new PvadMailQueue(db, new EphemeralDataProtectionProvider());
            var notifications = new OrganizationNotificationService(db, queue, Microsoft.Extensions.Options.Options.Create(new PvadMailOptions()));
            return new(db, new Tenant(UserId, OrganizationId), Signer, new(Signer), notifications)
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, PvadRoles.SystemAdministrator)], "synthetic")),
                    },
                },
            };
        }

        public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    }

    private sealed class MemorySigner : IPvadLicenseSigningService
    {
        private readonly string privateKey;
        public string PublicKeyPem { get; }
        public string KeyId => "pxa-synthetic-retained-key";
        public MemorySigner()
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            privateKey = key.ExportECPrivateKeyPem();
            PublicKeyPem = key.ExportSubjectPublicKeyInfoPem();
        }
        public PvadSignedLicenseArtifact Sign(PvadOfflineLicenseEnvelope envelope)
        {
            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new(json, SignText(json), KeyId, "ECDSA_P256_SHA256");
        }
        public string SignText(string json)
        {
            using var key = ECDsa.Create(); key.ImportFromPem(privateKey);
            return Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(json), HashAlgorithmName.SHA256));
        }
        public bool Verify(string envelopeJson, string signature)
        {
            using var key = ECDsa.Create(); key.ImportFromPem(PublicKeyPem);
            return key.VerifyData(Encoding.UTF8.GetBytes(envelopeJson), Convert.FromBase64String(signature), HashAlgorithmName.SHA256);
        }
    }
}
