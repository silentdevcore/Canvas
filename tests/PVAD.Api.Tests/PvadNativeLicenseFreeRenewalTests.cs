using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed partial class PvadNativeLicenseIssuanceTests
{
    private static async Task<(NativeLibraryLicense License, PvadNativeLicenseAutomationOptions Policy)> PrepareFreeRenewalAsync(
        Harness harness, PvadDbContext db, SubscriptionAccountType type)
    {
        var subscription = new OrganizationSubscription { OrganizationId = harness.OrganizationId, AccountType = type,
            Edition = SubscriptionEdition.Trial, Status = SubscriptionStatus.Trialing, TrialEndsAt = harness.Now.AddDays(30) };
        db.OrganizationSubscriptions.Add(subscription);
        db.OrganizationMemberships.Add(new OrganizationMembership { UserId = harness.UserId, OrganizationId = harness.OrganizationId });
        await db.SaveChangesAsync();
        await new PvadNativeLicenseRegistrationQueue(db).EnqueueAsync(harness.UserId, subscription, Guid.NewGuid(), default);
        await db.NativeLicenseRegistrationJobs.ExecuteUpdateAsync(set => set.SetProperty(job => job.ScheduledAt, harness.Now));
        var policy = new PvadNativeLicenseAutomationOptions { Enabled = true, FreeRenewalEnabled = true, IssuerUserId = harness.UserId,
            PolicyVersion = "synthetic-free-renewal-v1", Model = PvadLicenseModel.Subscription, ValidityMonths = 3 };
        var clock = new FixedClock(harness.Now.AddSeconds(1));
        var processor = new PvadNativeLicenseAutomationProcessor(db, harness.Service(db, issuerClock: clock), Options.Create(policy), Options.Create(harness.Options()), clock);
        Assert.True(await processor.ProcessNextAsync(default));
        return (await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(), policy);
    }

    [PostgreSqlFact]
    public async Task Free_renewal_preserves_owner_restricts_rights_and_retries_across_multiple_terms()
    {
        foreach (var type in new[] { SubscriptionAccountType.Company, SubscriptionAccountType.IndividualDeveloper })
        {
            await using var harness = await Harness.CreateAsync(output);
            await using var db = new PvadDbContext(harness.Database);
            var (previous, policy) = await PrepareFreeRenewalAsync(harness, db, type);
            var originalBytes = previous.ArtifactJson;
            var early = harness.Service(db, issuerClock: new FixedClock(previous.ValidUntil!.Value.AddTicks(-1)));
            Assert.False((await early.FreeRenewalStatusAsync(previous, harness.UserId, policy, default))!.CanRenew);
            Assert.Equal("PVAD_NATIVE_FREE_RENEWAL_TOO_EARLY", (await Assert.ThrowsAsync<PvadNativeIssuanceException>(() =>
                early.RenewFreeAsync(previous, harness.UserId, policy, default))).Code);
            // Native renewal stays possible after the independent Cloud trial ends.
            var subscription = await db.OrganizationSubscriptions.SingleAsync();
            subscription.Status = SubscriptionStatus.Expired; await db.SaveChangesAsync();
            var atExpiry = harness.Service(db, issuerClock: new FixedClock(previous.ValidUntil.Value));
            var renewed = await atExpiry.RenewFreeAsync(previous, harness.UserId, policy, default);
            var retried = await atExpiry.RenewFreeAsync(previous, harness.UserId, policy, default);
            Assert.Equal(renewed.Id, retried.Id);
            Assert.Equal(previous.ValidUntil.Value, renewed.ValidFrom);
            Assert.Equal(renewed.ValidFrom.AddMonths(3), renewed.ValidUntil);
            Assert.Equal(previous.OwnerUserId, renewed.OwnerUserId);
            Assert.Equal(previous.OrganizationId, renewed.OrganizationId);
            var successor = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.Id == renewed.Id);
            var payload = harness.Signer.Verify(successor.ArtifactJson);
            Assert.Equal(0, payload.GracePeriodDays);
            Assert.False(payload.RedistributionAllowed);
            Assert.Equal(harness.Signer.Verify(originalBytes).AccountScope!.Capabilities, payload.AccountScope!.Capabilities);
            Assert.DoesNotContain(payload.Grants, grant => grant.Product.StartsWith("migration."));
            Assert.Null(await atExpiry.FreeRenewalStatusAsync(previous, harness.UserId, policy, default));
            var audit = await db.AuditEvents.SingleAsync(row => row.Id == successor.AuditEventId);
            Assert.Contains("RequestedByUserId", audit.DetailsJson);
            var later = harness.Service(db, issuerClock: new FixedClock(successor.ValidUntil!.Value.AddDays(10)));
            var next = await later.RenewFreeAsync(successor, harness.UserId, policy, default);
            Assert.Equal(successor.ValidUntil.Value.AddDays(10), next.ValidFrom);
            Assert.Equal(next.ValidFrom.AddMonths(3), next.ValidUntil);
            Assert.Equal(3, await db.NativeLibraryLicenses.CountAsync());
            Assert.Equal(2, await db.AuditEvents.CountAsync(row => row.Action == "native-licenses.renew"));
            Assert.Equal(originalBytes, (await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.Id == previous.Id)).ArtifactJson);
        }
    }

    [PostgreSqlFact]
    public async Task Free_renewal_rechecks_account_policy_issuer_and_provenance()
    {
        await using var harness = await Harness.CreateAsync(output);
        await using var db = new PvadDbContext(harness.Database);
        var (previous, policy) = await PrepareFreeRenewalAsync(harness, db, SubscriptionAccountType.Company);
        var clock = new FixedClock(previous.ValidUntil!.Value);
        var issuer = harness.Service(db, issuerClock: clock);
        async Task Rejected() => Assert.NotNull(await Record.ExceptionAsync(() => issuer.RenewFreeAsync(previous, harness.UserId, policy, default)));
        policy.FreeRenewalEnabled = false; await Rejected(); policy.FreeRenewalEnabled = true;
        policy.PolicyVersion = "changed"; await Rejected(); policy.PolicyVersion = "synthetic-free-renewal-v1";
        policy.ValidityMonths = 6; await Rejected(); policy.ValidityMonths = 3;
        var user = await db.Users.SingleAsync(); user.IsActive = false; await db.SaveChangesAsync(); await Rejected(); user.IsActive = true;
        user.EmailConfirmed = false; await db.SaveChangesAsync(); await Rejected(); user.EmailConfirmed = true;
        user.LockoutEnd = clock.GetUtcNow().AddDays(1); await db.SaveChangesAsync(); await Rejected(); user.LockoutEnd = null;
        var member = await db.OrganizationMemberships.SingleAsync(); member.Status = OrganizationMembershipStatus.Removed;
        await db.SaveChangesAsync(); await Rejected(); member.Status = OrganizationMembershipStatus.Active;
        var organization = await db.Organizations.SingleAsync(); organization.Status = OrganizationStatus.Suspended;
        await db.SaveChangesAsync(); await Rejected(); organization.Status = OrganizationStatus.Active;
        var subscription = await db.OrganizationSubscriptions.SingleAsync(); subscription.Status = SubscriptionStatus.Suspended;
        await db.SaveChangesAsync(); await Rejected(); subscription.Status = SubscriptionStatus.Active;
        await db.SaveChangesAsync();
        var disabled = harness.Options(); disabled.AllowedUserIds = [];
        await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => harness.Service(db, disabled, issuerClock: clock)
            .RenewFreeAsync(previous, harness.UserId, policy, default));
        await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.RenewFreeAsync(previous, Guid.NewGuid(), policy, default));
        // A manual manufacturer artifact cannot enter the automatic free workflow.
        var manual = await harness.Service(db).IssueAsync(harness.Principal(), harness.Request() with
            { Grants = [new(PvadLicenseProducts.Pdf, [])] }, default);
        var manualRow = await db.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.Id == manual.Id);
        await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => issuer.RenewFreeAsync(manualRow, harness.UserId, policy, default));
        Assert.Equal(2, await db.NativeLibraryLicenses.CountAsync());
        Assert.Empty(await db.AuditEvents.Where(row => row.Action == "native-licenses.renew").ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Free_renewal_concurrency_and_signing_failure_never_duplicate_or_partially_commit()
    {
        await using var harness = await Harness.CreateAsync(output);
        NativeLibraryLicense previous; PvadNativeLicenseAutomationOptions policy;
        await using (var db = new PvadDbContext(harness.Database))
            (previous, policy) = await PrepareFreeRenewalAsync(harness, db, SubscriptionAccountType.IndividualDeveloper);
        var clock = new FixedClock(previous.ValidUntil!.Value);
        var flaky = new FirstFailureSigner(harness.Signer);
        await using (var db = new PvadDbContext(harness.Database))
        {
            await Assert.ThrowsAsync<PvadNativeIssuanceException>(() => harness.Service(db, overrideSigner: flaky, issuerClock: clock)
                .RenewFreeAsync(previous, harness.UserId, policy, default));
            Assert.Single(await db.NativeLibraryLicenses.ToListAsync());
            Assert.Empty(await db.AuditEvents.Where(row => row.Action == "native-licenses.renew").ToListAsync());
        }
        async Task<int> Renew()
        {
            await using var db = new PvadDbContext(harness.Database);
            try { await harness.Service(db, issuerClock: clock).RenewFreeAsync(previous, harness.UserId, policy, default); return 200; }
            catch (PvadNativeIssuanceException error) { return error.StatusCode; }
        }
        var results = await Task.WhenAll(Renew(), Renew());
        Assert.Contains(200, results); Assert.All(results, status => Assert.Contains(status, new[] { 200, 409 }));
        await using var verify = new PvadDbContext(harness.Database);
        Assert.Equal(2, await verify.NativeLibraryLicenses.CountAsync());
        Assert.Single(await verify.AuditEvents.Where(row => row.Action == "native-licenses.renew").ToListAsync());
        var retained = await verify.NativeLibraryLicenses.AsNoTracking().SingleAsync(row => row.PreviousLicenseId == previous.Id);
        Assert.Equal(retained.Id, (await harness.Service(verify, issuerClock: clock).RenewFreeAsync(previous, harness.UserId, policy, default)).Id);
    }
}
