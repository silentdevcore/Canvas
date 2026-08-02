using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Services.Jobs;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Application.Retention;

public sealed class PxaRetentionGovernanceService(
    PxaDbContext dbContext,
    PxaRetentionPolicyCatalog catalog,
    PxaRetentionLegalHoldService legalHolds,
    IOptions<PxaJobOptions> jobOptions,
    IOptions<PxaMailOptions> mailOptions,
    IConfiguration configuration)
{
    public async Task<PxaRetentionStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var holds = await legalHolds.Query(includeReleased: false).LongCountAsync(cancellationToken);
        var policies = catalog.Policies.Select(value => new PxaRetentionPolicyResponse(
            value.Id,
            value.Name,
            value.Status,
            value.ApprovalStatus,
            value.Rule,
            EffectiveConfiguration(value.Id, value.Configuration)))
            .ToArray();
        return new PxaRetentionStatusResponse(
            catalog.IsProductionReady,
            catalog.ProductionApproved,
            catalog.ReviewedAt,
            policies.Count(value => value.ApprovalStatus != "approved"),
            holds,
            policies);
    }

    public async Task<PxaRetentionDryRunResponse> DryRunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var jobScope = await legalHolds.GetActiveScopeAsync("background-document-jobs", cancellationToken);
        var jobs = await dbContext.BackgroundJobs.AsNoTracking()
            .Where(value => value.ExpiresAt <= now &&
                            (value.Status == PxaBackgroundJobStatus.Completed ||
                             value.Status == PxaBackgroundJobStatus.Cancelled ||
                             value.Status == PxaBackgroundJobStatus.DeadLetter))
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

        return new PxaRetentionDryRunResponse(now, false, decisions);
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

    private IReadOnlyList<PxaRetentionConfigurationValue> EffectiveConfiguration(
        string policyId,
        IReadOnlyList<string> keys) => keys.Select(key => new PxaRetentionConfigurationValue(
            key,
            policyId switch
            {
                "background-document-jobs" when key == "Jobs:ResultRetentionDays" => $"{jobOptions.Value.ResultRetentionDays} days",
                "background-document-jobs" when key == "Jobs:CleanupIntervalMinutes" => $"{jobOptions.Value.CleanupIntervalMinutes} minutes",
                "transactional-mail" when key == "Mail:DeliveredRetentionDays" => $"{mailOptions.Value.DeliveredRetentionDays} days",
                "transactional-mail" when key == "Mail:CancelledRetentionDays" => $"{mailOptions.Value.CancelledRetentionDays} days",
                "transactional-mail" when key == "Mail:DeadLetterRetentionDays" => $"{mailOptions.Value.DeadLetterRetentionDays} days",
                "account-closure" when key == "AccountClosure:RetentionPeriod" => configuration[key] ?? "30 days",
                "product-observability" when key == "PXA_METRIC_RETENTION" => configuration[key] ?? "90d",
                "product-observability" when key == "PXA_LOG_RETENTION" => configuration[key] ?? "720h",
                "product-observability" when key == "PXA_TRACE_RETENTION" => configuration[key] ?? "336h",
                "product-observability" when key == "PXA_ALERT_RETENTION" => configuration[key] ?? "120h",
                _ => configuration[key] ?? "Configured by deployment",
            })).ToArray();

    private static PxaRetentionDryRunDecision Decision(
        PxaRetentionPolicyDefinition policy,
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

public sealed record PxaRetentionStatusResponse(
    bool ProductionReady,
    bool ProductionApproved,
    DateOnly ReviewedAt,
    int PendingApprovalCount,
    long ActiveLegalHoldCount,
    IReadOnlyList<PxaRetentionPolicyResponse> Policies);

public sealed record PxaRetentionPolicyResponse(
    string Id,
    string Name,
    string Status,
    string ApprovalStatus,
    string Rule,
    IReadOnlyList<PxaRetentionConfigurationValue> EffectiveConfiguration);

public sealed record PxaRetentionConfigurationValue(string Key, string Value);

public sealed record PxaRetentionDryRunResponse(
    DateTimeOffset EvaluatedAt,
    bool Executed,
    IReadOnlyList<PxaRetentionDryRunDecision> Decisions);

public sealed record PxaRetentionDryRunDecision(
    string Category,
    string Name,
    string Action,
    long? CandidateCount,
    long HeldCount,
    long? ActionableCount,
    string ApprovalStatus,
    string Explanation);
