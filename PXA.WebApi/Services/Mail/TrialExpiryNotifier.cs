using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;
using PXA.WebApi.Security;

namespace PXA.WebApi.Services.Mail;

/// <summary>
/// Notifies active Organization Administrators when their Trial is about to
/// end. Idempotency keys are bucketed per subscription and warning
/// threshold, so re-running the check does not enqueue duplicate mail for
/// the same subscription within the same threshold window - the unique
/// index on <see cref="MailOutboxMessage.IdempotencyKey"/> is the backstop,
/// but this checks first so a re-run is a cheap no-op rather than a caught
/// database exception.
/// </summary>
public sealed class TrialExpiryNotifier(
    PxaDbContext dbContext,
    IPxaMailQueue mailQueue,
    OrganizationNotificationService notifications)
{
    private static readonly int[] WarningDaysBeforeExpiry = [7, 3, 1];

    public async Task<int> NotifyExpiringTrialsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(WarningDaysBeforeExpiry.Max());
        var expiringSubscriptions = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(value =>
                value.Status == SubscriptionStatus.Trialing &&
                value.TrialEndsAt != null &&
                value.TrialEndsAt > now &&
                value.TrialEndsAt <= horizon)
            .ToListAsync(cancellationToken);

        var notified = 0;
        foreach (var subscription in expiringSubscriptions)
        {
            var trialEndsAt = subscription.TrialEndsAt!.Value;
            // The tightest (smallest) configured threshold that has been crossed, so the
            // warning tier escalates as the real remaining time shrinks (7 days out, then
            // separately at 3, then at 1) instead of getting stuck reporting the loosest
            // threshold every time.
            var daysRemaining = WarningDaysBeforeExpiry
                .Where(days => trialEndsAt <= now.AddDays(days))
                .DefaultIfEmpty(-1)
                .Min();
            if (daysRemaining < 0)
                continue;

            var idempotencyKey = $"trial-expiring:{subscription.Id}:{daysRemaining}";
            if (await dbContext.MailOutboxMessages.AsNoTracking()
                    .AnyAsync(value => value.IdempotencyKey == idempotencyKey, cancellationToken))
                continue;

            var recipients = await (
                    from membership in dbContext.OrganizationMemberships.AsNoTracking()
                    join membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                        on membership.Id equals membershipRole.OrganizationMembershipId
                    join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                    join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                    where membership.OrganizationId == subscription.OrganizationId &&
                          membership.Status == OrganizationMembershipStatus.Active &&
                          role.Name == PxaRoles.OrganizationAdministrator &&
                          user.IsActive
                    select new { user.Id, user.DisplayName, user.Email, user.Locale })
                .ToListAsync(cancellationToken);

            foreach (var recipient in recipients)
            {
                if (string.IsNullOrWhiteSpace(recipient.Email))
                    continue;
                mailQueue.Enqueue(
                    subscription.OrganizationId,
                    recipient.Id,
                    recipient.Email,
                    "identity.trial-expiring",
                    new { displayName = recipient.DisplayName, daysRemaining, trialEndsAt },
                    // Only the first recipient's enqueue uses the bucketed key (enforced by
                    // the unique index); additional recipients get their own per-user key so
                    // every active administrator is notified, not just the first one found.
                    recipient.Id == recipients[0].Id ? idempotencyKey : $"{idempotencyKey}:{recipient.Id}",
                    recipient.Locale);
                notified++;
            }
        }

        if (notified > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return notified;
    }

    public async Task<int> NotifyExpiringLicensesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now.AddDays(WarningDaysBeforeExpiry.Max());
        var licenses = await dbContext.OfflineLicenses.AsNoTracking()
            .Where(value =>
                value.Status == OfflineLicenseStatus.Active &&
                value.ValidUntil > now &&
                value.ValidUntil <= horizon)
            .ToListAsync(cancellationToken);

        var notified = 0;
        foreach (var license in licenses)
        {
            var daysRemaining = WarningDaysBeforeExpiry
                .Where(days => license.ValidUntil <= now.AddDays(days))
                .DefaultIfEmpty(-1)
                .Min();
            if (daysRemaining < 0)
                continue;

            var eventKey = $"license-expiring:{license.Id}:{daysRemaining}";
            if (await dbContext.MailOutboxMessages.AsNoTracking()
                    .AnyAsync(value => value.IdempotencyKey.StartsWith($"{eventKey}:"),
                        cancellationToken))
                continue;

            notified += await notifications.QueueAdministratorsAsync(
                license.OrganizationId,
                "license.changed",
                eventKey,
                new Dictionary<string, string>
                {
                    ["summary"] =
                        $"Offline license {license.LicenseNumber} expires on {license.ValidUntil:yyyy-MM-dd}.",
                },
                cancellationToken);
        }

        if (notified > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return notified;
    }
}

public sealed class TrialExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<TrialExpiryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<TrialExpiryNotifier>();
                await notifier.NotifyExpiringTrialsAsync(stoppingToken);
                await notifier.NotifyExpiringLicensesAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    PxaLogEvents.TrialExpiryCheckFailed,
                    exception,
                    "Trial-expiry notification check failed.");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
