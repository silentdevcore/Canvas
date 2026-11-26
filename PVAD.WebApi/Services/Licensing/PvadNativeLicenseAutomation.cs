using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadNativeLicenseAutomationOptions
{
    public const string SectionName = "NativeLicenseAutomation";
    public bool Enabled { get; set; }
    public Guid IssuerUserId { get; set; }
    public string? PolicyVersion { get; set; }
    public PvadLicenseModel? Model { get; set; }
    public int? ValidityDays { get; set; }
    public int? ValidityMonths { get; set; }
    public DateTimeOffset? UpdatesUntil { get; set; }
    public bool IsValid() => !Enabled || IssuerUserId != Guid.Empty &&
        PolicyVersion is not null && Regex.IsMatch(PolicyVersion, "^[a-z0-9][a-z0-9.-]{0,63}$") &&
        (Model == PvadLicenseModel.Subscription && ((ValidityDays is >= 1 and <= 3650 && ValidityMonths is null) || (ValidityMonths is >= 1 and <= 120 && ValidityDays is null)) && UpdatesUntil is null ||
         Model == PvadLicenseModel.Trial && ValidityDays == 30 && ValidityMonths is null && UpdatesUntil is null ||
         Model == PvadLicenseModel.Perpetual && ValidityDays is null && ValidityMonths is null && UpdatesUntil is { Offset: var offset } && offset == TimeSpan.Zero);
    internal DateTimeOffset? EndFrom(DateTimeOffset start) => ValidityMonths is { } months ? start.AddMonths(months) :
        ValidityDays is { } days ? start.AddDays(days) : null;
}

internal sealed record PvadNativeAutomaticContract(string PolicyVersion, Guid IssuerUserId, PvadLicenseModel Model,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, DateTimeOffset? UpdatesUntil, int? GracePeriodDays = null);

public sealed class PvadNativeLicenseRegistrationQueue(PvadDbContext db)
{
    // Called inside registration's verification transaction. No signing, secrets,
    // caller-selected owner or grants; a retained owner is never enqueued twice.
    public Task EnqueueAsync(Guid userId, OrganizationSubscription subscription, Guid verificationId, CancellationToken cancellationToken)
    {
        if (subscription.AccountType is not (SubscriptionAccountType.Company or SubscriptionAccountType.IndividualDeveloper))
            throw new InvalidOperationException("Unknown registration account type.");
        Guid? organizationId = subscription.AccountType == SubscriptionAccountType.Company ? subscription.OrganizationId : null;
        Guid? ownerUserId = organizationId is null ? userId : null;
        var ownerKey = organizationId is { } organization ? $"organization:{organization:D}" : $"user:{userId:D}";
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var type = subscription.AccountType.ToString();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO administration.native_license_registration_jobs
                ("Id", "OwnerKey", "OrganizationId", "OwnerUserId", "SourceUserId", "SourceOrganizationId", "SourceSubscriptionId", "VerificationId", "AccountType", "Status", "Attempts", "ScheduledAt", "CreatedAt")
            VALUES ({id}, {ownerKey}, {organizationId}, {ownerUserId}, {userId}, {subscription.OrganizationId}, {subscription.Id}, {verificationId}, {type}, 'Pending', 0, {now}, {now})
            ON CONFLICT ("OwnerKey") DO NOTHING
            """, cancellationToken);
    }
}

public sealed class PvadNativeLicenseAutomationProcessor(PvadDbContext db, PvadNativeLicenseIssuanceService issuer,
    IOptions<PvadNativeLicenseAutomationOptions> options, IOptions<PvadNativeIssuerOptions> nativeOptions, TimeProvider clock)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var policy = options.Value;
        if (!policy.Enabled || !nativeOptions.Value.Enabled) return false;
        if (!policy.IsValid()) throw new InvalidOperationException("Native automation policy is not configured.");
        var now = clock.GetUtcNow();
        NativeLicenseRegistrationJob? job;
        await using (var claim = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            job = await db.NativeLicenseRegistrationJobs.FromSqlInterpolated($"""
                SELECT * FROM administration.native_license_registration_jobs
                WHERE ("Status" = 'Pending' AND "ScheduledAt" <= {now})
                   OR ("Status" = 'Processing' AND "LeaseUntil" <= {now})
                ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).FirstOrDefaultAsync(cancellationToken);
            if (job is null) return false;
            job.Status = NativeLicenseRegistrationStatus.Processing;
            job.LeaseId = Guid.NewGuid(); job.LeaseUntil = now.AddMinutes(5); job.Attempts++;
            job.ContractJson ??= JsonSerializer.Serialize(new PvadNativeAutomaticContract(policy.PolicyVersion!, policy.IssuerUserId,
                policy.Model!.Value, PvadLicenseTimestamps.Normalize(now), policy.EndFrom(PvadLicenseTimestamps.Normalize(now)),
                policy.UpdatesUntil is { } cutoff ? PvadLicenseTimestamps.Normalize(cutoff) : null, policy.Model == PvadLicenseModel.Subscription ? 0 : null), PvadLicenseJson.Options);
            await db.SaveChangesAsync(cancellationToken);
            await claim.CommitAsync(cancellationToken);
        }
        var leaseId = job.LeaseId;
        Guid? licenseId = null;
        string? failure = null;
        var permanent = false;
        try
        {
            var contract = JsonSerializer.Deserialize<PvadNativeAutomaticContract>(job.ContractJson!, PvadLicenseJson.Options)
                ?? throw new InvalidOperationException("Missing contract.");
            if (contract.PolicyVersion != policy.PolicyVersion || contract.IssuerUserId != policy.IssuerUserId ||
                contract.Model != policy.Model || contract.UpdatesUntil != (policy.UpdatesUntil is { } cutoff ? PvadLicenseTimestamps.Normalize(cutoff) : null) ||
                contract.ValidUntil != policy.EndFrom(contract.ValidFrom) ||
                contract.GracePeriodDays != (policy.Model == PvadLicenseModel.Subscription ? 0 : (int?)null))
                throw new PvadNativeIssuanceException("PVAD_NATIVE_AUTOMATION_POLICY_CHANGED", 409);
            var subscription = await db.OrganizationSubscriptions.AsNoTracking().SingleOrDefaultAsync(value =>
                value.Id == job.SourceSubscriptionId && value.OrganizationId == job.SourceOrganizationId, cancellationToken);
            if (subscription is null || subscription.AccountType != job.AccountType ||
                (job.AccountType == SubscriptionAccountType.Company ? job.OrganizationId != job.SourceOrganizationId || job.OwnerUserId != null
                    : job.OwnerUserId != job.SourceUserId || job.OrganizationId != null) ||
                !await db.Users.AnyAsync(value => value.Id == job.SourceUserId && value.IsActive && value.EmailConfirmed &&
                    (value.LockoutEnd == null || value.LockoutEnd <= now), cancellationToken) ||
                job.AccountType == SubscriptionAccountType.Company &&
                    (!await db.Organizations.AnyAsync(value => value.Id == job.SourceOrganizationId && value.Status == OrganizationStatus.Active, cancellationToken) ||
                     await PvadAccountPolicyService.ReadAsync(db, job.SourceOrganizationId, cancellationToken, requireActive: true) is null ||
                     !await db.OrganizationMemberships.AnyAsync(value => value.UserId == job.SourceUserId && value.OrganizationId == job.SourceOrganizationId &&
                         value.Status == OrganizationMembershipStatus.Active, cancellationToken)))
                throw new PvadNativeIssuanceException("PVAD_NATIVE_AUTOMATION_ACCOUNT_UNAVAILABLE", 409);
            licenseId = (await issuer.IssueAutomaticAsync(job, contract, policy, cancellationToken)).Id;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (PvadNativeIssuanceException error) { failure = error.Code; permanent = error.StatusCode is 400 or 403 or 404 || error.Code is "PVAD_NATIVE_AUTOMATION_POLICY_CHANGED" or "PVAD_NATIVE_AUTOMATION_ACCOUNT_UNAVAILABLE" or "PVAD_NATIVE_OWNER_UNAVAILABLE" or "PVAD_NATIVE_ORGANIZATION_UNAVAILABLE"; }
        catch (Exception) { failure = "PVAD_NATIVE_AUTOMATION_FAILED"; }
        var status = licenseId is not null ? NativeLicenseRegistrationStatus.Completed :
            permanent || job.Attempts >= 5 ? NativeLicenseRegistrationStatus.DeadLetter : NativeLicenseRegistrationStatus.Pending;
        // A worker whose lease expired cannot overwrite a newer worker's result.
        await db.NativeLicenseRegistrationJobs.Where(value => value.Id == job.Id && value.LeaseId == leaseId && value.Status == NativeLicenseRegistrationStatus.Processing)
            .ExecuteUpdateAsync(set => set.SetProperty(value => value.Status, status)
                .SetProperty(value => value.LicenseId, licenseId).SetProperty(value => value.FailureCode, failure)
                .SetProperty(value => value.CompletedAt, status == NativeLicenseRegistrationStatus.Completed || status == NativeLicenseRegistrationStatus.DeadLetter ? (DateTimeOffset?)clock.GetUtcNow() : null)
                .SetProperty(value => value.ScheduledAt, clock.GetUtcNow().AddSeconds(Math.Min(300, 10 * job.Attempts)))
                .SetProperty(value => value.LeaseId, (Guid?)null).SetProperty(value => value.LeaseUntil, (DateTimeOffset?)null), cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }
}

public sealed class PvadNativeLicenseAutomationWorker(IServiceScopeFactory scopes, ILogger<PvadNativeLicenseAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<PvadNativeLicenseAutomationProcessor>().ProcessNextAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Native license automation could not complete its queue check."); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
