using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
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
using PVAD.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace PVAD.Api.Tests;

public sealed partial class PvadNativeLicenseIssuanceTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task Default_customer_mode_rejects_issuance_without_database_or_key_access()
    {
        var configuration = Options.Create(new PvadNativeIssuerOptions());
        using var signer = new PvadNativeLicenseSigner(configuration);
        var service = new PvadNativeLicenseIssuanceService(null!, signer, configuration, Options.Create(new PvadStepUpOptions()), null!, TimeProvider.System);
        var error = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(new(), null!, default));
        Assert.Equal("PVAD_NATIVE_ISSUANCE_DISABLED", error.Code);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void Native_signer_never_generates_or_trusts_foreign_keys_and_sanitizes_failures()
    {
        var path = Path.Combine(Path.GetTempPath(), "native-issuer-negative-" + Guid.NewGuid().ToString("N"));
        var configuration = Options.Create(new PvadNativeIssuerOptions { Enabled = true, KeyId = "untrusted-synthetic", PrivateKeyPath = path });
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var payload = new PvadLicensePayload(1, Guid.NewGuid(), "synthetic", PvadLicenseModel.Trial, now, now, now.AddDays(30), null,
            false, [new(PvadLicenseProducts.Pdf, [])]);
        try
        {
            using (var missing = new PvadNativeLicenseSigner(configuration))
                Assert.Equal("PVAD_NATIVE_ISSUER_UNAVAILABLE", Assert.Throws<PvadNativeIssuanceException>(() => missing.Sign(payload)).Code);
            Assert.False(File.Exists(path));
            using var foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(path, foreign.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            using var unknown = new PvadNativeLicenseSigner(configuration);
            var error = Assert.Throws<PvadNativeIssuanceException>(() => unknown.Sign(payload));
            Assert.Equal(503, error.StatusCode);
            Assert.DoesNotContain(path, error.Message);
            Assert.Null(error.InnerException);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
                using var exposed = new PvadNativeLicenseSigner(configuration);
                Assert.Equal(503, Assert.Throws<PvadNativeIssuanceException>(() => exposed.Sign(payload)).StatusCode);
            }
        }
        finally { File.Delete(path); }
    }

    [PostgreSqlFact]
    public async Task Classified_accounts_receive_signed_v2_rights_and_renewals_preserve_scope()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var subscription = new OrganizationSubscription
        {
            OrganizationId = harness.OrganizationId, AccountType = SubscriptionAccountType.IndividualDeveloper, Status = SubscriptionStatus.Active,
        };
        db.OrganizationSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        foreach (var kind in new[] { SubscriptionAccountType.IndividualDeveloper, SubscriptionAccountType.Company })
        {
            subscription.AccountType = kind;
            await db.SaveChangesAsync();
            var request = kind == SubscriptionAccountType.IndividualDeveloper
                ? harness.Request() with { OrganizationId = null, OwnerUserId = harness.UserId }
                : harness.Request();
            var service = harness.Service(db);
            var result = await service.IssueAsync(harness.Principal(), request, default);
            var stored = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == result.Id);
            var payload = harness.Signer.Verify(stored.ArtifactJson);
            Assert.Equal(2, payload.SchemaVersion);
            using var envelope = JsonDocument.Parse(stored.ArtifactJson);
            Assert.Equal(2, envelope.RootElement.GetProperty("schemaVersion").GetInt32());
            var scope = Assert.IsType<PvadLicenseAccountScope>(payload.AccountScope);
            Assert.Equal(kind == SubscriptionAccountType.Company ? "company" : "individualDeveloper", scope.AccountType);
            Assert.Contains("export.pdf", scope.Capabilities);
            Assert.Contains("migration.pdf.code", scope.Capabilities); // Explicit manufacturer-admin grant.
            Assert.DoesNotContain("migration.report.designer", scope.Capabilities);
            Assert.Equal(kind == SubscriptionAccountType.Company, scope.Capabilities.Contains("import.pdf"));
            Assert.Equal(kind == SubscriptionAccountType.Company, scope.Capabilities.Contains("export.png"));
            var renewed = await service.RenewAsync(harness.Principal(), result.Id,
                new RenewNativeLicenseRequest(Guid.NewGuid(), harness.Now.AddDays(60), null, "renewal"), default);
            var renewedRow = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == renewed.Id);
            var renewedScope = harness.Signer.Verify(renewedRow.ArtifactJson).AccountScope!;
            Assert.Equal(scope.AccountType, renewedScope.AccountType);
            Assert.Equal(scope.Capabilities, renewedScope.Capabilities);
            var mismatched = request with { RequestId = Guid.NewGuid(), AccountScope = new(
                kind == SubscriptionAccountType.Company ? "individualDeveloper" : "company", ["export.pdf"]) };
            var error = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), mismatched, default));
            Assert.Equal("PVAD_NATIVE_ISSUANCE_INVALID", error.Code);
        }
    }

    [PostgreSqlFact]
    public async Task Personal_ownership_supports_orgless_access_and_survives_workspace_changes()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var service = harness.Service(db);
        var personal = await service.IssueAsync(harness.Principal(), harness.Request() with
            { OrganizationId = null, OwnerUserId = harness.UserId }, default);
        Assert.Null(personal.OrganizationId);
        Assert.Equal(harness.UserId, personal.OwnerUserId);
        var original = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.Id == personal.Id);
        Assert.Equal("individualDeveloper", harness.Signer.Verify(original.ArtifactJson).AccountScope!.AccountType);
        var company = await service.IssueAsync(harness.Principal(), harness.Request(), default);
        var foreignUser = Guid.NewGuid();
        db.Users.Add(new PvadIdentityUser { Id = foreignUser, DisplayName = "Other private owner", UserName = foreignUser.ToString(), EmailConfirmed = true });
        await db.SaveChangesAsync();
        var foreign = await service.IssueAsync(harness.Principal(), harness.Request() with
            { OrganizationId = null, OwnerUserId = foreignUser }, default);
        var customer = harness.Principal();
        // No license-reading or manufacturer role/permission is needed to read one's own artifact.
        foreach (var identity in customer.Identities)
        foreach (var claim in identity.Claims.Where(claim => claim.Type == ClaimTypes.Role || claim.Type == PvadClaimTypes.Permission).ToArray())
            identity.RemoveClaim(claim);
        var controller = new AccountNativeLicensesController(db, new Tenant(harness.UserId, null), new FixedClock(harness.Now), harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions()))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = customer } } };
        Assert.Equal(personal.Id, Assert.Single(Assert.IsType<NativeLicensePage>(Assert.IsType<OkObjectResult>((await controller.List()).Result).Value).Items).Id);
        Assert.IsType<NotFoundResult>(await controller.Download(company.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Download(foreign.Id, default));
        var file = Assert.IsType<FileContentResult>(await controller.Download(personal.Id, default));
        Assert.Equal(original.ArtifactJson, Encoding.UTF8.GetString(file.FileContents));
        var renewal = await service.RenewAsync(harness.Principal(), personal.Id,
            new(Guid.NewGuid(), harness.Now.AddDays(60), null, "renewal"), default);
        Assert.Null(renewal.OrganizationId);
        Assert.Equal(harness.UserId, renewal.OwnerUserId);
        Assert.Equal(original.ArtifactJson, (await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.Id == personal.Id)).ArtifactJson);
        var session = await db.UserSessions.SingleAsync();
        session.OrganizationId = harness.OrganizationId;
        await db.SaveChangesAsync();
        // No membership in this workspace: personal ownership still works; its company license stays hidden.
        var switched = new AccountNativeLicensesController(db, new Tenant(harness.UserId, harness.OrganizationId), new FixedClock(harness.Now), harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions()))
            { ControllerContext = controller.ControllerContext };
        Assert.IsType<FileContentResult>(await switched.Download(personal.Id, default));
        Assert.IsType<NotFoundResult>(await switched.Download(company.Id, default));
        customer.Identities.First().AddClaim(new(PvadClaimTypes.ServiceAccount, "synthetic"));
        Assert.IsType<ForbidResult>(await switched.Download(personal.Id, default));
        customer.Identities.First().RemoveClaim(customer.FindFirst(PvadClaimTypes.ServiceAccount)!);
        session.RevokedAt = harness.Now;
        await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await switched.Download(personal.Id, default));
    }

    [PostgreSqlFact]
    public async Task Personal_issuance_rejects_ambiguous_unavailable_or_overprivileged_owners()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var service = harness.Service(db);
        foreach (var invalid in new[]
        {
            harness.Request() with { OrganizationId = null },
            harness.Request() with { OwnerUserId = harness.UserId },
            harness.Request() with { OrganizationId = null, OwnerUserId = Guid.Empty },
        })
            Assert.Equal("PVAD_NATIVE_OWNER_INVALID", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), invalid, default))).Code);
        var user = new PvadIdentityUser { Id = Guid.NewGuid(), UserName = "personal-target", DisplayName = "Private target", EmailConfirmed = false };
        db.Users.Add(user);
        db.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = SubscriptionAccountType.IndividualDeveloper });
        await db.SaveChangesAsync();
        Assert.Equal("PVAD_NATIVE_PERSONAL_OWNER_REQUIRED", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), harness.Request(), default))).Code);
        foreach (var target in new[] { user.Id, Guid.NewGuid() })
            Assert.Equal("PVAD_NATIVE_OWNER_UNAVAILABLE", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), harness.Request() with { OrganizationId = null, OwnerUserId = target }, default))).Code);
        user.EmailConfirmed = true; user.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal("PVAD_NATIVE_OWNER_UNAVAILABLE", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), harness.Request() with { OrganizationId = null, OwnerUserId = user.Id }, default))).Code);
        user.IsActive = true; await db.SaveChangesAsync();
        var forbidden = harness.Request() with { OrganizationId = null, OwnerUserId = user.Id, AccountScope = new("company", ["export.pdf"]) };
        Assert.Equal("PVAD_NATIVE_ISSUANCE_INVALID", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), forbidden, default))).Code);
        Assert.Empty(await db.NativeLibraryLicenses.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Retained_private_organization_artifacts_remain_idempotent_and_renewable_without_reassignment()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var request = harness.Request() with { AccountScope = new("individualDeveloper", ["export.pdf", "migration.pdf.code"]) };
        var payload = new PvadLicensePayload(2, Guid.NewGuid(), request.CustomerReference, request.Model,
            harness.Now, request.ValidFrom, request.ValidUntil, request.UpdatesUntil, false, request.Grants) { AccountScope = request.AccountScope };
        var artifact = harness.Signer.Sign(payload);
        var audit = new AuditEvent { ActorUserId = harness.UserId, OrganizationId = harness.OrganizationId,
            Action = "native-licenses.issue", TargetType = "native-library-license", TargetId = payload.LicenseId.ToString(), Outcome = "succeeded" };
        // Retained pre-migration artifact: never manufacture a new org-owned
        // private artifact through the current issuance API.
        var row = new NativeLibraryLicense { Id = payload.LicenseId, OrganizationId = harness.OrganizationId,
            IssuedByUserId = harness.UserId, AuditEventId = audit.Id, RequestId = request.RequestId,
            RequestSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { operation = "issue", request }, PvadLicenseJson.Options)))),
            ArtifactJson = artifact, ArtifactSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(artifact))),
            LicenseModel = payload.Model.ToString(), CustomerReference = payload.CustomerReference, IssuedAt = payload.IssuedAt,
            ValidFrom = payload.ValidFrom, ValidUntil = payload.ValidUntil, UpdatesUntil = payload.UpdatesUntil };
        db.AuditEvents.Add(audit); db.NativeLibraryLicenses.Add(row); await db.SaveChangesAsync();
        var service = harness.Service(db);
        Assert.Equal(row.Id, (await service.IssueAsync(harness.Principal(), request, default)).Id);
        Assert.Single(await db.AuditEvents.ToListAsync());
        var rejected = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.IssueAsync(harness.Principal(), request with { RequestId = Guid.NewGuid() }, default));
        Assert.Equal("PVAD_NATIVE_PERSONAL_OWNER_REQUIRED", rejected.Code);
        var renewed = await service.RenewAsync(harness.Principal(), row.Id, new(Guid.NewGuid(), harness.Now.AddDays(60), null, "renewal"), default);
        Assert.Equal(harness.OrganizationId, renewed.OrganizationId);
        Assert.Null(renewed.OwnerUserId);
        var retained = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == row.Id);
        Assert.Equal(artifact, retained.ArtifactJson);
        Assert.Equal("individualDeveloper", harness.Signer.Verify((await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == renewed.Id)).ArtifactJson).AccountScope!.AccountType);
    }

    [PostgreSqlFact]
    public async Task Ownership_migration_preserves_legacy_artifacts_and_database_constraints()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var service = harness.Service(db);
        var company = await service.IssueAsync(harness.Principal(), harness.Request(), default);
        var before = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261120054204_AddNativeLibraryLicenses");
        await migrator.MigrateAsync();
        var after = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync();
        Assert.Equal(before.ArtifactJson, after.ArtifactJson);
        Assert.Equal(before.ArtifactSha256, after.ArtifactSha256);
        Assert.Equal(before.OrganizationId, after.OrganizationId);
        Assert.Null(after.OwnerUserId);
        var personal = await service.IssueAsync(harness.Principal(), harness.Request() with { OrganizationId = null, OwnerUserId = harness.UserId }, default);
        foreach (var ownership in new[]
        {
            "jsonb_build_object('OrganizationId', NULL, 'OwnerUserId', NULL)",
            $"jsonb_build_object('OrganizationId', '{harness.OrganizationId}', 'OwnerUserId', '{harness.UserId}')",
            $"jsonb_build_object('OrganizationId', NULL, 'OwnerUserId', '{harness.UserId}', 'PreviousLicenseId', '{company.Id}')",
        })
        {
            // Only fixture-generated GUIDs enter this SQL. Copying a retained row
            // isolates the owner constraint/trigger from unrelated required fields.
#pragma warning disable EF1002, EF1003
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO administration.native_library_licenses SELECT (jsonb_populate_record(NULL::administration.native_library_licenses, to_jsonb(source) || jsonb_build_object('Id', gen_random_uuid()) || " + ownership + ")).* FROM administration.native_library_licenses source WHERE source.\"Id\" = {0}", company.Id));
#pragma warning restore EF1002, EF1003
            Assert.Equal("23514", error.SqlState);
        }
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync("20261120054204_AddNativeLibraryLicenses"));
        Assert.Equal(2, await db.NativeLibraryLicenses.CountAsync());
        Assert.IsType<FileContentResult>(await new AccountNativeLicensesController(db, new Tenant(harness.UserId, null), new FixedClock(harness.Now), harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions()))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = harness.Principal() } } }.Download(personal.Id, default));
    }

    [Fact]
    public async Task Registration_automation_is_disabled_without_database_or_key_access_and_requires_explicit_terms()
    {
        var processor = new PvadNativeLicenseAutomationProcessor(null!, null!, Options.Create(new PvadNativeLicenseAutomationOptions()),
            Options.Create(new PvadNativeIssuerOptions()), TimeProvider.System);
        Assert.False(await processor.ProcessNextAsync(default));
        Assert.False(new PvadNativeLicenseAutomationOptions { Enabled = true }.IsValid());
        Assert.False(new PvadNativeLicenseAutomationOptions { Enabled = true, IssuerUserId = Guid.NewGuid(), PolicyVersion = "free-1", Model = PvadLicenseModel.Perpetual }.IsValid());
    }

    [Fact]
    public void Free_launch_uses_calendar_months_and_rejects_mixed_duration_units()
    {
        var policy = new PvadNativeLicenseAutomationOptions { Enabled = true, IssuerUserId = Guid.NewGuid(), PolicyVersion = "free-three-months-v1",
            Model = PvadLicenseModel.Subscription, ValidityMonths = 3 };
        Assert.True(policy.IsValid());
        var start = new DateTimeOffset(2027, 8, 31, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2027, 11, 30, 12, 0, 0, TimeSpan.Zero), policy.EndFrom(start));
        Assert.NotEqual(start.AddDays(90), policy.EndFrom(start));
        policy.ValidityDays = 90;
        Assert.False(policy.IsValid());
    }

    [PostgreSqlFact]
    public async Task Registration_automation_preserves_owner_limits_and_recovers_after_a_committed_issuance()
    {
        foreach (var type in new[] { SubscriptionAccountType.Company, SubscriptionAccountType.IndividualDeveloper })
        {
            await using var harness = await Harness.CreateAsync(output);
            await using var db = new PvadDbContext(harness.Database);
            var customerId = Guid.NewGuid();
            db.Users.Add(new PvadIdentityUser { Id = customerId, UserName = customerId.ToString(), DisplayName = "Registration customer", EmailConfirmed = true });
            var subscription = new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = type, Status = SubscriptionStatus.Active };
            db.OrganizationSubscriptions.Add(subscription);
            db.OrganizationMemberships.Add(new OrganizationMembership { UserId = customerId, OrganizationId = harness.OrganizationId });
            db.SubscriptionEntitlements.Add(new SubscriptionEntitlement { SubscriptionId = subscription.Id, Capability = "migration", Source = EntitlementSource.NegotiatedOverride, Enabled = true });
            await db.SaveChangesAsync();
            var queue = new PvadNativeLicenseRegistrationQueue(db);
            await queue.EnqueueAsync(customerId, subscription, Guid.NewGuid(), default);
            await queue.EnqueueAsync(customerId, subscription, Guid.NewGuid(), default);
            Assert.Single(await db.NativeLicenseRegistrationJobs.ToListAsync());
            var policy = Options.Create(new PvadNativeLicenseAutomationOptions { Enabled = true, IssuerUserId = harness.UserId, PolicyVersion = "free-1", Model = PvadLicenseModel.Subscription, ValidityDays = 365 });
            var processor = new PvadNativeLicenseAutomationProcessor(db, harness.Service(db), policy, Options.Create(harness.Options()), TimeProvider.System);
            Assert.True(await processor.ProcessNextAsync(default));
            var job = await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync();
            Assert.Equal(NativeLicenseRegistrationStatus.Completed, job.Status);
            var license = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync();
            Assert.Equal(job.LicenseId, license.Id);
            Assert.Equal(type == SubscriptionAccountType.Company ? harness.OrganizationId : (Guid?)null, license.OrganizationId);
            Assert.Equal(type == SubscriptionAccountType.IndividualDeveloper ? customerId : (Guid?)null, license.OwnerUserId);
            var payload = harness.Signer.Verify(license.ArtifactJson);
            Assert.False(payload.RedistributionAllowed);
            Assert.DoesNotContain(payload.Grants, grant => grant.Product.StartsWith("migration."));
            Assert.DoesNotContain(payload.AccountScope!.Capabilities, capability => capability.StartsWith("migration."));
            Assert.Equal(type == SubscriptionAccountType.Company, payload.AccountScope.Capabilities.Contains("import.pdf"));
            var firstArtifact = license.ArtifactJson;
            // Simulate a crash after native ledger commit, before queue completion.
            await db.NativeLicenseRegistrationJobs.ExecuteUpdateAsync(set => set
                .SetProperty(value => value.Status, NativeLicenseRegistrationStatus.Processing)
                .SetProperty(value => value.LeaseUntil, harness.Now.AddMinutes(-1))
                .SetProperty(value => value.LeaseId, Guid.NewGuid()).SetProperty(value => value.LicenseId, (Guid?)null));
            Assert.True(await processor.ProcessNextAsync(default));
            Assert.Single(await db.NativeLibraryLicenses.ToListAsync());
            Assert.Equal(firstArtifact, (await db.NativeLibraryLicenses.AsNoTracking().SingleAsync()).ArtifactJson);
            Assert.Single(await db.AuditEvents.Where(value => value.Action == "native-licenses.issue").ToListAsync());
        }
    }

    [PostgreSqlFact]
    public async Task Registration_automation_retries_transient_signing_and_never_issues_to_a_disabled_owner()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var subscription = new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = SubscriptionAccountType.IndividualDeveloper, Status = SubscriptionStatus.Active };
        db.OrganizationSubscriptions.Add(subscription); await db.SaveChangesAsync();
        await new PvadNativeLicenseRegistrationQueue(db).EnqueueAsync(harness.UserId, subscription, Guid.NewGuid(), default);
        var policy = Options.Create(new PvadNativeLicenseAutomationOptions { Enabled = true, IssuerUserId = harness.UserId, PolicyVersion = "free-1", Model = PvadLicenseModel.Trial, ValidityDays = 30 });
        var flaky = new FirstFailureSigner(harness.Signer);
        var processor = new PvadNativeLicenseAutomationProcessor(db, harness.Service(db, overrideSigner: flaky, issuerClock: TimeProvider.System), policy,
            Options.Create(harness.Options()), TimeProvider.System);
        Assert.True(await processor.ProcessNextAsync(default));
        var job = await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync();
        Assert.Equal(NativeLicenseRegistrationStatus.Pending, job.Status);
        Assert.Equal("PVAD_NATIVE_ISSUER_UNAVAILABLE", job.FailureCode);
        Assert.Empty(await db.NativeLibraryLicenses.ToListAsync());
        var contract = job.ContractJson;
        await db.NativeLicenseRegistrationJobs.ExecuteUpdateAsync(set => set.SetProperty(value => value.ScheduledAt, harness.Now.AddMinutes(-1)));
        Assert.True(await processor.ProcessNextAsync(default));
        Assert.Equal(contract, (await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync()).ContractJson);
        Assert.Single(await db.NativeLibraryLicenses.ToListAsync());
        Assert.Equal(NativeLicenseRegistrationStatus.Completed, (await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync()).Status);
    }

    [PostgreSqlFact]
    public async Task Registration_automation_has_one_claimant_and_rechecks_eligibility()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using (var db = new PvadDbContext(harness.Database))
        {
            var subscription = new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active };
            db.OrganizationSubscriptions.Add(subscription);
            db.OrganizationMemberships.Add(new OrganizationMembership { UserId = harness.UserId, OrganizationId = harness.OrganizationId });
            await db.SaveChangesAsync();
            await new PvadNativeLicenseRegistrationQueue(db).EnqueueAsync(harness.UserId, subscription, Guid.NewGuid(), default);
            (await db.Users.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
        }
        async Task<bool> Process()
        {
            await using var db = new PvadDbContext(harness.Database);
            return await new PvadNativeLicenseAutomationProcessor(db, harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions
                { Enabled = true, IssuerUserId = harness.UserId, PolicyVersion = "free-1", Model = PvadLicenseModel.Subscription, ValidityDays = 365 }),
                Options.Create(harness.Options()), TimeProvider.System).ProcessNextAsync(default);
        }
        var results = await Task.WhenAll(Process(), Process());
        Assert.Equal(1, results.Count(value => value));
        await using var verify = new PvadDbContext(harness.Database);
        Assert.Empty(await verify.NativeLibraryLicenses.ToListAsync());
        var job = await verify.NativeLicenseRegistrationJobs.SingleAsync();
        Assert.Equal(NativeLicenseRegistrationStatus.DeadLetter, job.Status);
        Assert.Equal("PVAD_NATIVE_AUTOMATION_ACCOUNT_UNAVAILABLE", job.FailureCode);
    }

    [PostgreSqlFact]
    public async Task Registration_automation_pins_policy_and_retains_queue_history()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var subscription = new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = SubscriptionAccountType.IndividualDeveloper, Status = SubscriptionStatus.Active };
        db.OrganizationSubscriptions.Add(subscription); await db.SaveChangesAsync();
        await new PvadNativeLicenseRegistrationQueue(db).EnqueueAsync(harness.UserId, subscription, Guid.NewGuid(), default);
        var settings = new PvadNativeLicenseAutomationOptions { Enabled = true, IssuerUserId = harness.UserId, PolicyVersion = "free-1", Model = PvadLicenseModel.Subscription, ValidityDays = 365 };
        var processor = new PvadNativeLicenseAutomationProcessor(db, harness.Service(db, overrideSigner: new FirstFailureSigner(harness.Signer)),
            Options.Create(settings), Options.Create(harness.Options()), TimeProvider.System);
        Assert.True(await processor.ProcessNextAsync(default));
        var contract = (await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync()).ContractJson;
        settings.PolicyVersion = "free-2";
        await db.NativeLicenseRegistrationJobs.ExecuteUpdateAsync(set => set.SetProperty(value => value.ScheduledAt, harness.Now.AddMinutes(-1)));
        Assert.True(await processor.ProcessNextAsync(default));
        var retained = await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleAsync();
        Assert.Equal(contract, retained.ContractJson);
        Assert.Equal(NativeLicenseRegistrationStatus.DeadLetter, retained.Status);
        Assert.Equal("PVAD_NATIVE_AUTOMATION_POLICY_CHANGED", retained.FailureCode);
        Assert.Empty(await db.NativeLibraryLicenses.ToListAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261126180911_AddPersonalNativeLicenseOwnership"));
        Assert.Single(await db.NativeLicenseRegistrationJobs.ToListAsync());
    }

    private sealed class FirstFailureSigner(IPvadNativeLicenseIssuerSigner inner) : IPvadNativeLicenseIssuerSigner
    {
        private bool failed;
        public string Sign(PvadLicensePayload payload)
        {
            if (!failed) { failed = true; throw new PvadNativeIssuanceException("PVAD_NATIVE_ISSUER_UNAVAILABLE", 503); }
            return inner.Sign(payload);
        }
        public PvadLicensePayload Verify(string artifact) => inner.Verify(artifact);
    }

    [PostgreSqlFact]
    public async Task Authorized_issue_preserves_all_models_grants_and_audit_atomically()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        foreach (var model in Enum.GetValues<PvadLicenseModel>())
        {
            var request = harness.Request(model);
            var before = PvadLicense.GetStatus();
            var result = await harness.Service(db).IssueAsync(harness.Principal(), request, default);
            var persisted = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == result.Id);
            var payload = harness.Signer.Verify(persisted.ArtifactJson);
            Assert.Equal(result.Id, payload.LicenseId);
            Assert.Equal(request.Model, payload.Model);
            Assert.Equal(JsonSerializer.Serialize(request.Grants, Json), JsonSerializer.Serialize(payload.Grants, Json));
            Assert.Equal(request.ValidFrom, payload.ValidFrom);
            Assert.Equal(request.ValidUntil, payload.ValidUntil);
            Assert.Equal(request.UpdatesUntil, payload.UpdatesUntil);
            Assert.Equal(before, PvadLicense.GetStatus());
            var audit = await db.AuditEvents.SingleAsync(value => value.Id == persisted.AuditEventId);
            Assert.Equal("native-licenses.issue", audit.Action);
            Assert.DoesNotContain(payload.CustomerReference, audit.DetailsJson!);
            Assert.DoesNotContain(persisted.ArtifactJson, audit.DetailsJson!);
            var replay = await harness.Service(db).IssueAsync(harness.Principal(), request, default);
            Assert.Equal(result.Id, replay.Id);
        }
        Assert.Equal(3, await db.NativeLibraryLicenses.CountAsync());
        Assert.Equal(3, await db.AuditEvents.CountAsync());
        Assert.Empty(await db.OfflineLicenses.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Renewal_preserves_original_rights_bytes_and_idempotency_without_revoking_old_artifacts()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var issuer = harness.Service(db);
        var request = harness.Request();
        var issued = await issuer.IssueAsync(harness.Principal(), request, default);
        var original = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync();
        var renewal = new RenewNativeLicenseRequest(Guid.NewGuid(), harness.Now.AddDays(90), null, "renewal");
        var renewed = await issuer.RenewAsync(harness.Principal(), issued.Id, renewal, default);
        Assert.Equal(renewed.Id, (await issuer.RenewAsync(harness.Principal(), issued.Id, renewal, default)).Id);
        db.ChangeTracker.Clear();
        var retained = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == original.Id);
        Assert.Equal(original.ArtifactJson, retained.ArtifactJson);
        Assert.Equal(original.ArtifactSha256, retained.ArtifactSha256);
        var next = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(value => value.Id == renewed.Id);
        var firstPayload = harness.Signer.Verify(original.ArtifactJson);
        var nextPayload = harness.Signer.Verify(next.ArtifactJson);
        Assert.Equal(firstPayload.CustomerReference, nextPayload.CustomerReference);
        Assert.Equal(firstPayload.Model, nextPayload.Model);
        Assert.Equal(firstPayload.ValidFrom, nextPayload.ValidFrom);
        Assert.Equal(JsonSerializer.Serialize(firstPayload.Grants, Json), JsonSerializer.Serialize(nextPayload.Grants, Json));
        Assert.Equal(issued.Id, next.PreviousLicenseId);
        Assert.Equal(2, await db.AuditEvents.CountAsync());
        var duplicate = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.RenewAsync(harness.Principal(), issued.Id,
            renewal with { RequestId = Guid.NewGuid() }, default));
        Assert.Equal(409, duplicate.StatusCode);
        var changed = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.IssueAsync(harness.Principal(),
            request with { CustomerReference = "different-customer" }, default));
        Assert.Equal("PVAD_NATIVE_REQUEST_CONFLICT", changed.Code);
    }

    [PostgreSqlFact]
    public async Task Perpetual_updates_can_extend_but_trial_renewal_and_shorter_terms_are_rejected()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var issuer = harness.Service(db);
        var perpetual = await issuer.IssueAsync(harness.Principal(), harness.Request(PvadLicenseModel.Perpetual), default);
        var extended = await issuer.RenewAsync(harness.Principal(), perpetual.Id,
            new(Guid.NewGuid(), null, harness.Now.AddYears(2), "renewal"), default);
        Assert.Null(extended.ValidUntil);
        Assert.Equal(harness.Now.AddYears(2), extended.UpdatesUntil);
        var trial = await issuer.IssueAsync(harness.Principal(), harness.Request(PvadLicenseModel.Trial), default);
        Assert.Equal(400, (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.RenewAsync(harness.Principal(), trial.Id,
            new(Guid.NewGuid(), harness.Now.AddDays(60), null, "renewal"), default))).StatusCode);
        var subscription = await issuer.IssueAsync(harness.Principal(), harness.Request(), default);
        Assert.Equal(400, (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.RenewAsync(harness.Principal(), subscription.Id,
            new(Guid.NewGuid(), harness.Now.AddDays(2), null, "renewal"), default))).StatusCode);
        Assert.Equal(4, await db.NativeLibraryLicenses.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Issuer_requires_allowlist_real_global_role_permission_and_live_recent_verified_user_session()
    {
        await using var harness = await Harness.CreateAsync(output);
        foreach (var scenario in new[] { "disabled", "allowlist", "permission", "role-claim", "service", "expired", "old", "revoked", "inactive", "email", "stored-role", "locked", "operator" })
        {
            await using var db = new PvadDbContext(harness.Database);
            var configuration = harness.Options();
            var principal = harness.Principal();
            var user = await db.Users.SingleAsync();
            var session = await db.UserSessions.SingleAsync();
            var operatorOptions = new PvadAdminSecurityOptions();
            switch (scenario)
            {
                case "disabled": configuration.Enabled = false; break;
                case "allowlist": configuration.AllowedUserIds = []; break;
                case "permission": ((ClaimsIdentity)principal.Identity!).RemoveClaim(principal.FindFirst(PvadClaimTypes.Permission)!); break;
                case "role-claim": ((ClaimsIdentity)principal.Identity!).RemoveClaim(principal.FindFirst(ClaimTypes.Role)!); break;
                case "service": ((ClaimsIdentity)principal.Identity!).AddClaim(new(PvadClaimTypes.ServiceAccount, "synthetic")); break;
                case "expired": session.ExpiresAt = harness.Now; break;
                case "old": session.CreatedAt = harness.Now.AddHours(-1); break;
                case "revoked": session.RevokedAt = harness.Now; break;
                case "inactive": user.IsActive = false; break;
                case "email": user.EmailConfirmed = false; break;
                case "stored-role": db.UserRoles.RemoveRange(await db.UserRoles.ToListAsync()); break;
                case "locked": user.LockoutEnd = harness.Now.AddHours(1); break;
                case "operator": operatorOptions.RequireExplicitSystemOperators = true; break;
            }
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => harness.Service(db, configuration, operatorOptions)
                .IssueAsync(principal, harness.Request(), default));
            Assert.Equal(403, error.StatusCode);
            Assert.Empty(await db.NativeLibraryLicenses.ToListAsync());
            Assert.Empty(await db.AuditEvents.ToListAsync());
            await harness.ResetIdentityAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Invalid_providers_terms_and_inactive_customers_do_not_create_licenses_or_audit()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var request = harness.Request();
        foreach (var invalid in new[]
        {
            request with { RequestId = Guid.Empty }, request with { Model = (PvadLicenseModel)99 },
            request with { Grants = [new(PvadLicenseProducts.PdfCodeMigration, ["auto"])] },
            request with { Grants = [new(PvadLicenseProducts.PdfCodeMigration, ["aspose"])] },
            request with { Grants = [new(PvadLicenseProducts.PdfCodeMigration, ["unknown"])] },
            request with { ValidFrom = request.ValidFrom.ToOffset(TimeSpan.FromHours(1)) },
            request with { ValidUntil = harness.Now }, request with { CustomerReference = "invalid\nreference" },
        }) Assert.Equal(400, (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => harness.Service(db)
            .IssueAsync(harness.Principal(), invalid, default))).StatusCode);
        var organization = await db.Organizations.SingleAsync(); organization.Status = OrganizationStatus.Suspended;
        await db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => harness.Service(db)
            .IssueAsync(harness.Principal(), request, default))).StatusCode);
        Assert.Empty(await db.NativeLibraryLicenses.ToListAsync()); Assert.Empty(await db.AuditEvents.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Failure_after_save_rolls_back_license_and_audit_together()
    {
        await using var harness = await Harness.CreateAsync(output);
        var failingOptions = new DbContextOptionsBuilder<PvadDbContext>(harness.Database).AddInterceptors(new FailAfterSave()).Options;
        await using (var failing = new PvadDbContext(failingOptions))
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service(failing).IssueAsync(harness.Principal(), harness.Request(), default));
        await using var verify = new PvadDbContext(harness.Database);
        Assert.Empty(await verify.NativeLibraryLicenses.ToListAsync()); Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Concurrent_renewal_produces_one_immutable_successor_and_one_audit()
    {
        await using var harness = await Harness.CreateAsync(output);
        NativeLicenseResponse issued;
        await using (var db = new PvadDbContext(harness.Database))
            issued = await harness.Service(db).IssueAsync(harness.Principal(), harness.Request(), default);
        async Task<int> Renew()
        {
            await using var db = new PvadDbContext(harness.Database);
            try { await harness.Service(db).RenewAsync(harness.Principal(), issued.Id, new(Guid.NewGuid(), harness.Now.AddDays(90), null, "renewal"), default); return 201; }
            catch (PvadNativeIssuanceException error) { return error.StatusCode; }
        }
        var results = await Task.WhenAll(Renew(), Renew());
        Assert.Equal(new[] { 201, 409 }, results.Order().ToArray());
        await using var verify = new PvadDbContext(harness.Database);
        Assert.Equal(2, await verify.NativeLibraryLicenses.CountAsync()); Assert.Equal(2, await verify.AuditEvents.CountAsync());
        var retained = await verify.NativeLibraryLicenses.SingleAsync(value => value.Id == issued.Id);
        retained.CustomerReference = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.SaveChangesAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE administration.native_library_licenses SET \"CustomerReference\" = 'changed' WHERE \"Id\" = {issued.Id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => verify.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM administration.native_library_licenses WHERE \"Id\" = {issued.Id}"));
    }

    [PostgreSqlFact]
    public async Task Actual_http_routes_require_auth_csrf_recent_session_and_manufacturer_access()
    {
        await using var harness = await Harness.CreateAsync(output);
        using var factory = new PvadApiTestFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["OAuth:Enabled"] = "false", ["NativeLicenseIssuer:Enabled"] = "false" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>(); services.RemoveAll<DbContextOptions<PvadDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
                services.AddDbContext<PvadDbContext>(options => options.UseNpgsql(harness.Connection));
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock(harness.Now));
                services.RemoveAll<PvadNativeLicenseIssuanceService>();
                services.AddScoped(provider => harness.Service(provider.GetRequiredService<PvadDbContext>()));
                services.RemoveAll<IClaimsTransformation>(); services.AddTransient<IClaimsTransformation, PassClaims>();
                services.AddSingleton(harness);
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "native-fixture"; options.DefaultChallengeScheme = "native-fixture"; })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("native-fixture", _ => { });
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        const string path = "/api/pvad/v1/admin/native-licenses";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, harness.Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pvad/v1/account/native-licenses")).StatusCode);
        client.DefaultRequestHeaders.Add("native-actor", "operator");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, harness.Request())).StatusCode);
        var csrf = (await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/csrf")).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(harness.Request()) };
        request.Headers.Add("X-PVAD-CSRF", csrf);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<NativeLicenseResponse>();
        var download = await client.GetAsync(path + "/" + result!.Id + "/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("no-store", download.Headers.CacheControl!.ToString());
        Assert.Equal(result.Id, harness.Signer.Verify(await download.Content.ReadAsStringAsync()).LicenseId);
        var runbook = await client.GetAsync("/api/pvad/v1/admin/operator/documentation/native-library-license-issuance");
        Assert.Equal(HttpStatusCode.OK, runbook.StatusCode);
        Assert.Contains("no-store", runbook.Headers.CacheControl!.ToString());
        Assert.Contains("Native Library License Issuance", await runbook.Content.ReadAsStringAsync());
        await using (var customerDb = new PvadDbContext(harness.Database))
        {
            customerDb.OrganizationMemberships.Add(new() { UserId = harness.UserId, OrganizationId = harness.OrganizationId });
            (await customerDb.UserSessions.SingleAsync()).OrganizationId = harness.OrganizationId;
            await customerDb.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Remove("native-actor"); client.DefaultRequestHeaders.Add("native-actor", "customer");
        var own = await client.GetFromJsonAsync<NativeLicensePage>("/api/pvad/v1/account/native-licenses?organizationId=" + Guid.NewGuid());
        Assert.Equal(result.Id, Assert.Single(own!.Items).Id);
        var customerDownload = await client.GetAsync($"/api/pvad/v1/account/native-licenses/{result.Id}/download");
        Assert.Equal(HttpStatusCode.OK, customerDownload.StatusCode);
        Assert.Contains("no-store", customerDownload.Headers.CacheControl!.ToString());
        Assert.Equal(await download.Content.ReadAsStringAsync(), await customerDownload.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/pvad/v1/account/native-licenses/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/pvad/v1/account/native-licenses?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/capabilities")).StatusCode);
        client.DefaultRequestHeaders.Remove("native-actor"); client.DefaultRequestHeaders.Add("native-actor", "other");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/" + result.Id + "/download")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Manufacturer_catalog_pagination_filters_and_details_preserve_exact_native_grants()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var service = harness.Service(db);
        var first = await service.IssueAsync(harness.Principal(), harness.Request(), default);
        await service.IssueAsync(harness.Principal(), harness.Request(PvadLicenseModel.Trial), default);
        var capabilities = await service.CapabilitiesAsync(harness.Principal(), default);
        Assert.True(capabilities.Enabled);
        Assert.Contains(capabilities.Products, product => product.Id == PvadLicenseProducts.PdfCodeMigration && product.Providers.Contains("aspose-pdf"));
        Assert.DoesNotContain(capabilities.Products.SelectMany(product => product.Providers), provider => provider == "auto");
        var page = await service.ListAsync(harness.Principal(), harness.OrganizationId, 1, 1, default);
        Assert.Single(page.Items); Assert.Equal(2, page.Total);
        Assert.Empty((await service.ListAsync(harness.Principal(), Guid.NewGuid(), 1, 25, default)).Items);
        Assert.Equal(400, (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => service.ListAsync(harness.Principal(), null, 0, 25, default))).StatusCode);
        var details = await service.DetailsAsync(harness.Principal(), first.Id, default);
        Assert.Equal("synthetic-customer", details.CustomerReference);
        Assert.Equal(2, details.Grants.Length); Assert.Null(details.RenewedByLicenseId);
        var disabled = harness.Options(); disabled.Enabled = false;
        Assert.False((await harness.Service(db, disabled).CapabilitiesAsync(harness.Principal(), default)).Enabled);
    }

    [PostgreSqlFact]
    public async Task Customer_download_is_tenant_scoped_and_works_without_manufacturer_keys_or_issuance()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var own = await harness.Service(db).IssueAsync(harness.Principal(), harness.Request(), default);
        var foreignId = Guid.NewGuid();
        db.Organizations.Add(new Organization { Id = foreignId, Name = "Other synthetic customer", Slug = "other-native" });
        db.OrganizationMemberships.Add(new OrganizationMembership { UserId = harness.UserId, OrganizationId = harness.OrganizationId });
        var session = await db.UserSessions.SingleAsync(); session.OrganizationId = harness.OrganizationId;
        await db.SaveChangesAsync();
        var other = await harness.Service(db).IssueAsync(harness.Principal(), harness.Request() with { OrganizationId = foreignId }, default);
        var principal = harness.CustomerPrincipal();
        var controller = new AccountNativeLicensesController(db, new Tenant(harness.UserId, harness.OrganizationId), new FixedClock(harness.Now), harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions()))
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = principal } } };
        var response = Assert.IsType<OkObjectResult>((await controller.List(cancellationToken: default)).Result);
        Assert.Equal(own.Id, Assert.Single(Assert.IsType<NativeLicensePage>(response.Value).Items).Id);
        Assert.IsType<NotFoundResult>((await controller.Get(other.Id, default)).Result);
        Assert.IsType<NotFoundResult>(await controller.Download(other.Id, default));
        var metadata = Assert.IsType<NativeLicenseDetails>(Assert.IsType<OkObjectResult>((await controller.Get(own.Id, default)).Result).Value);
        Assert.Empty(metadata.CustomerReference);
        var file = Assert.IsType<FileContentResult>(await controller.Download(own.Id, default));
        Assert.Equal("application/json", file.ContentType);
        var record = await db.NativeLibraryLicenses.SingleAsync(row => row.Id == own.Id);
        Assert.Equal(record.ArtifactJson, Encoding.UTF8.GetString(file.FileContents));
        Assert.Equal(own.Id, harness.Signer.Verify(Encoding.UTF8.GetString(file.FileContents)).LicenseId);
        var readClaim = principal.FindFirst(claim => claim.Type == PvadClaimTypes.Permission && claim.Value == PvadAccountPermissions.LicensesRead)!;
        principal.Identities.First().RemoveClaim(readClaim);
        var personalOnly = Assert.IsType<NativeLicensePage>(Assert.IsType<OkObjectResult>((await controller.List()).Result).Value);
        Assert.Empty(personalOnly.Items);
        Assert.IsType<NotFoundResult>(await controller.Download(own.Id, default));
        principal.Identities.First().AddClaim(readClaim);
        var disabled = harness.Options(); disabled.Enabled = false;
        Assert.False((await harness.Service(db, disabled).CapabilitiesAsync(principal, default)).Enabled);
        Assert.IsType<FileContentResult>(await controller.Download(own.Id, default));
        var crossContext = new AccountNativeLicensesController(db, new Tenant(harness.UserId, foreignId), new FixedClock(harness.Now), harness.Service(db), Options.Create(new PvadNativeLicenseAutomationOptions()))
        { ControllerContext = controller.ControllerContext };
        Assert.IsType<ForbidResult>(await crossContext.Download(other.Id, default));
        session.RevokedAt = harness.Now; await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.Download(own.Id, default));
        session.RevokedAt = null; await db.SaveChangesAsync();
        var membership = await db.OrganizationMemberships.SingleAsync(); membership.Status = OrganizationMembershipStatus.Removed;
        await db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await controller.Download(own.Id, default));
    }

    [Fact]
    public void Customer_native_routes_require_authentication_and_check_ownership_without_manufacturer_permissions()
    {
        var attribute = Assert.Single(typeof(AccountNativeLicensesController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false));
        Assert.Null(((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)attribute).Policy);
        Assert.Equal(3, typeof(AccountNativeLicensesController).GetMethods().Count(method => method.GetCustomAttributes(typeof(HttpGetAttribute), false).Length > 0));
        Assert.True(typeof(AdminNativeLicensesController).GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), false)
            .Cast<ApiExplorerSettingsAttribute>().Single().IgnoreApi);
    }

    private sealed record Tenant(Guid? UserId, Guid? OrganizationId) : IPvadTenantContext;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Synthetic post-save failure.");
    }
    private sealed class PassClaims : IClaimsTransformation { public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal); }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, Harness harness) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("native-actor", out var actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = actor == "customer" ? harness.CustomerPrincipal() : harness.Principal(actor == "operator" ? harness.UserId : Guid.NewGuid());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        public DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid OrganizationId { get; } = Guid.NewGuid();
        private Guid SessionId { get; } = Guid.NewGuid();
        private Guid RoleId { get; } = Guid.NewGuid();
        public DbContextOptions<PvadDbContext> Database { get; private set; } = null!;
        public IPvadNativeLicenseIssuerSigner Signer { get; private set; } = null!;
        public string Connection => postgres.GetConnectionString();
        private string? KeyPath => PVAD.Testing.PvadTestEnvironment.Read("PVAD_TEST_NATIVE_ISSUER_PRIVATE_KEY");
        public PvadNativeIssuerOptions Options() => new() { Enabled = true, AllowedUserIds = [UserId],
            KeyId = KeyPath is null ? "synthetic-unit-issuer" : ReadTestKeyId(),
            PrivateKeyPath = KeyPath };

        private static string? ReadTestKeyId()
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(PVAD.Testing.PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")!));
            return document.RootElement.GetProperty("keyId").GetString();
        }

        public static async Task<Harness> CreateAsync(ITestOutputHelper output)
        {
            var harness = new Harness();
            try
            {
                await harness.postgres.StartAsync();
                harness.Database = new DbContextOptionsBuilder<PvadDbContext>().UseNpgsql(harness.Connection).Options;
                await using var db = new PvadDbContext(harness.Database);
                await db.Database.MigrateAsync();
                db.AddRange(new Organization { Id = harness.OrganizationId, Name = "Synthetic Native Customer", Slug = "native-customer" },
                    new PvadIdentityUser { Id = harness.UserId, DisplayName = "Synthetic Issuer", UserName = "issuer@example.test", Email = "issuer@example.test", EmailConfirmed = true },
                    new PvadIdentityRole { Id = harness.RoleId, Name = PvadRoles.SystemAdministrator, IsSystemRole = true },
                    new IdentityUserRole<Guid> { UserId = harness.UserId, RoleId = harness.RoleId },
                    new UserSession { Id = harness.SessionId, UserId = harness.UserId, IpAddressHash = "synthetic", UserAgent = "test", CreatedAt = harness.Now.AddMinutes(-1), ExpiresAt = harness.Now.AddHours(1) });
                await db.SaveChangesAsync();
                var trusted = harness.KeyPath is not null;
                if (PVAD.Testing.PvadTestEnvironment.Read("PVAD_REQUIRE_NATIVE_ISSUER") == "1") Assert.True(trusted, "The explicit native-issuer run requires its ephemeral trusted key.");
                harness.Signer = trusted ? new PvadNativeLicenseSigner(Microsoft.Extensions.Options.Options.Create(harness.Options())) : new MemorySigner();
                output.WriteLine("native_trusted_signer=" + trusted);
                return harness;
            }
            catch { await harness.DisposeAsync(); throw; }
        }

        public ClaimsPrincipal Principal(Guid? userId = null) => new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, (userId ?? UserId).ToString()), new(ClaimTypes.Role, PvadRoles.SystemAdministrator),
            new(PvadClaimTypes.Permission, PvadPermissions.LicensesManage), new(PvadClaimTypes.Session, SessionId.ToString())], "synthetic-native"));
        public ClaimsPrincipal CustomerPrincipal() => new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, UserId.ToString()), new(PvadClaimTypes.ActiveOrganization, OrganizationId.ToString()),
            new(PvadClaimTypes.Permission, PvadAccountPermissions.LicensesRead), new(PvadClaimTypes.Session, SessionId.ToString())], "synthetic-customer"));
        public IssueNativeLicenseRequest Request(PvadLicenseModel model = PvadLicenseModel.Subscription) => new(Guid.NewGuid(), OrganizationId,
            "synthetic-customer", model, Now, model == PvadLicenseModel.Perpetual ? null : Now.AddDays(30),
            model == PvadLicenseModel.Perpetual ? Now.AddYears(1) : null, false,
            [new(PvadLicenseProducts.Pdf, []), new(PvadLicenseProducts.PdfCodeMigration, ["aspose-pdf"])], "purchase");
        public PvadNativeLicenseIssuanceService Service(PvadDbContext db, PvadNativeIssuerOptions? configuration = null, PvadAdminSecurityOptions? security = null, IPvadNativeLicenseIssuerSigner? overrideSigner = null, TimeProvider? issuerClock = null) =>
            new(db, overrideSigner ?? Signer, Microsoft.Extensions.Options.Options.Create(configuration ?? Options()), Microsoft.Extensions.Options.Options.Create(new PvadStepUpOptions()),
                new(Microsoft.Extensions.Options.Options.Create(security ?? new())), issuerClock ?? new FixedClock(Now));
        public async Task ResetIdentityAsync()
        {
            await using var db = new PvadDbContext(Database);
            var user = await db.Users.SingleAsync(); user.IsActive = true; user.EmailConfirmed = true; user.LockoutEnd = null;
            var session = await db.UserSessions.SingleAsync(); session.ExpiresAt = Now.AddHours(1); session.CreatedAt = Now.AddMinutes(-1); session.RevokedAt = null;
            if (!await db.UserRoles.AnyAsync()) db.UserRoles.Add(new() { UserId = UserId, RoleId = RoleId });
            await db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() { (Signer as IDisposable)?.Dispose(); await postgres.DisposeAsync(); }
    }

    // Ordinary API runs test business transactions without a manufacturer key.
    // The explicit native-issuer runner replaces this with the real embedded-trust signer.
    private sealed class MemorySigner : IPvadNativeLicenseIssuerSigner, IDisposable
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public string Sign(PvadLicensePayload payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
            return JsonSerializer.Serialize(new PvadSignedNativeLicense(payload.SchemaVersion, "synthetic-unit-issuer", "ECDSA_P256_SHA256_P1363",
                Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), Json);
        }
        public PvadLicensePayload Verify(string artifact)
        {
            var envelope = JsonSerializer.Deserialize<PvadSignedNativeLicense>(artifact, Json)!;
            var bytes = Convert.FromBase64String(envelope.Payload);
            Assert.True(key.VerifyData(bytes, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            return JsonSerializer.Deserialize<PvadLicensePayload>(bytes, Json)!;
        }
        public void Dispose() => key.Dispose();
    }
}
