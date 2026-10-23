using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Configuration;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Mail;

namespace PVAD.WebApi.Application.Retention;

public sealed class PvadRetentionGovernanceService(
    PvadDbContext dbContext,
    PvadRetentionPolicyCatalog catalog,
    PvadRetentionLegalHoldService legalHolds,
    IOptions<PvadJobOptions> jobOptions,
    IOptions<PvadMailOptions> mailOptions,
    IConfiguration configuration)
{
    public async Task<PvadRetentionStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var holds = await legalHolds.Query(includeReleased: false).LongCountAsync(cancellationToken);
        var policies = catalog.Policies.Select(value => new PvadRetentionPolicyResponse(
            value.Id,
            value.Name,
            value.Status,
            value.ApprovalStatus,
            value.Rule,
            EffectiveConfiguration(value.Id, value.Configuration)))
            .ToArray();
        return new PvadRetentionStatusResponse(
            catalog.IsProductionReady,
            catalog.ProductionApproved,
            catalog.ReviewedAt,
            policies.Count(value => value.ApprovalStatus != "approved"),
            holds,
            policies);
    }

    public async Task<PvadRetentionDryRunResponse> DryRunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var jobScope = await legalHolds.GetActiveScopeAsync("background-document-jobs", cancellationToken);
        var jobs = await dbContext.BackgroundJobs.AsNoTracking()
            .Where(value => value.ExpiresAt <= now &&
                            (value.Status == PvadBackgroundJobStatus.Completed ||
                             value.Status == PvadBackgroundJobStatus.Cancelled ||
                             value.Status == PvadBackgroundJobStatus.DeadLetter))
            .Select(value => value.OrganizationId)
            .ToArrayAsync(cancellationToken);

        var mailScope = await legalHolds.GetActiveScopeAsync("transactional-mail", cancellationToken);
        var mail = await EligibleMail(now)
            .Select(value => value.OrganizationId)
            .ToArrayAsync(cancellationToken);

        var closureScope = await legalHolds.GetActiveScopeAsync("account-closure", cancellationToken);
        var closures = await dbContext.AccountClosureRequests.AsNoTracking()
            .Where(value => value.Status == AccountClosureStatus.Pending && value.ScheduledPurgeAt <= now)
            .Select(value => value.OrganizationId)
            .ToArrayAsync(cancellationToken);

        var decisions = catalog.Policies.Select(policy => policy.Id switch
        {
            "background-document-jobs" => Decision(
                policy, "delete-content", jobs.LongLength,
                jobs.LongCount(value => jobScope.Holds(value)),
                "Expired input and result objects; job metadata remains retained."),
            "transactional-mail" => Decision(
                policy, "delete", mail.LongLength,
                mail.LongCount(value => mailScope.Holds(value)),
                "Expired terminal mail-outbox records."),
            "account-closure" => Decision(
                policy, "retain", closures.LongLength,
                closures.LongCount(value => closureScope.Holds(value)),
                "Closure is due, but downstream erasure and pseudonymization are not approved."),
            _ => Decision(
                policy,
                policy.Status == "transient" ? "release-after-operation" : "retain",
                null,
                0,
                policy.ApprovalStatus == "approved"
                    ? "The approved runtime policy applies automatically."
                    : "No destructive action is permitted before legal approval."),
        }).ToArray();

        return new PvadRetentionDryRunResponse(now, false, decisions);
    }

    private IQueryable<MailOutboxMessage> EligibleMail(DateTimeOffset now)
    {
        var settings = mailOptions.Value;
        var deliveredCutoff = now.AddDays(-settings.DeliveredRetentionDays);
        var cancelledCutoff = now.AddDays(-settings.CancelledRetentionDays);
        var deadLetterCutoff = now.AddDays(-settings.DeadLetterRetentionDays);
        return dbContext.MailOutboxMessages.AsNoTracking().Where(message =>
            ((message.Status == MailDeliveryStatus.Delivered ||
              message.Status == MailDeliveryStatus.Suppressed) && message.UpdatedAt < deliveredCutoff) ||
            (message.Status == MailDeliveryStatus.Cancelled && message.UpdatedAt < cancelledCutoff) ||
            (message.Status == MailDeliveryStatus.DeadLetter && message.UpdatedAt < deadLetterCutoff));
    }

    private IReadOnlyList<PvadRetentionConfigurationValue> EffectiveConfiguration(
        string policyId,
        IReadOnlyList<string> keys) => keys.Select(key => new PvadRetentionConfigurationValue(
            key,
            policyId switch
            {
                "background-document-jobs" when key == "Jobs:ResultRetentionDays" => $"{jobOptions.Value.ResultRetentionDays} days",
                "background-document-jobs" when key == "Jobs:CleanupIntervalMinutes" => $"{jobOptions.Value.CleanupIntervalMinutes} minutes",
                "transactional-mail" when key == "Mail:DeliveredRetentionDays" => $"{mailOptions.Value.DeliveredRetentionDays} days",
                "transactional-mail" when key == "Mail:CancelledRetentionDays" => $"{mailOptions.Value.CancelledRetentionDays} days",
                "transactional-mail" when key == "Mail:DeadLetterRetentionDays" => $"{mailOptions.Value.DeadLetterRetentionDays} days",
                "account-closure" when key == "AccountClosure:RetentionPeriod" => configuration[key] ?? "30 days",
                "product-observability" when key == "PVAD_METRIC_RETENTION" => ReadRetention(key) ?? "90d",
                "product-observability" when key == "PVAD_LOG_RETENTION" => ReadRetention(key) ?? "720h",
                "product-observability" when key == "PVAD_TRACE_RETENTION" => ReadRetention(key) ?? "336h",
                "product-observability" when key == "PVAD_ALERT_RETENTION" => ReadRetention(key) ?? "120h",
                _ => configuration[key] ?? "Configured by deployment",
            })).ToArray();

    private string? ReadRetention(string key) => PvadEnvironment.Read(key, name => configuration[name]);

    private static PvadRetentionDryRunDecision Decision(
        PvadRetentionPolicyDefinition policy,
        string action,
        long? candidates,
        long held,
        string explanation) => new(
            policy.Id,
            policy.Name,
            action,
            candidates,
            held,
            candidates is null ? null : Math.Max(0, candidates.Value - held),
            policy.ApprovalStatus,
            explanation);
}

public sealed record PvadRetentionStatusResponse(
    bool ProductionReady,
    bool ProductionApproved,
    DateOnly ReviewedAt,
    int PendingApprovalCount,
    long ActiveLegalHoldCount,
    IReadOnlyList<PvadRetentionPolicyResponse> Policies);

public sealed record PvadRetentionPolicyResponse(
    string Id,
    string Name,
    string Status,
    string ApprovalStatus,
    string Rule,
    IReadOnlyList<PvadRetentionConfigurationValue> EffectiveConfiguration);

public sealed record PvadRetentionConfigurationValue(string Key, string Value);

public sealed record PvadRetentionDryRunResponse(
    DateTimeOffset EvaluatedAt,
    bool Executed,
    IReadOnlyList<PvadRetentionDryRunDecision> Decisions);

public sealed record PvadRetentionDryRunDecision(
    string Category,
    string Name,
    string Action,
    long? CandidateCount,
    long HeldCount,
    long? ActionableCount,
    string ApprovalStatus,
    string Explanation);
