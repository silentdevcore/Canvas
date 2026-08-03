using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Retention;

namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailRetentionService(
    PxaDbContext dbContext,
    PxaRetentionLegalHoldService legalHolds,
    IOptions<PxaMailOptions> options)
{
    private readonly PxaMailOptions options = options.Value;

    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deliveredCutoff = now.AddDays(-options.DeliveredRetentionDays);
        var cancelledCutoff = now.AddDays(-options.CancelledRetentionDays);
        var deadLetterCutoff = now.AddDays(-options.DeadLetterRetentionDays);
        var holdScope = await legalHolds.GetActiveScopeAsync(
            "transactional-mail",
            cancellationToken);
        if (holdScope.Global)
            return 0;

        var heldOrganizationIds = holdScope.OrganizationIds.ToArray();
        var expiredIds = await dbContext.MailOutboxMessages
            .Where(message =>
                (message.OrganizationId == null ||
                 !heldOrganizationIds.Contains(message.OrganizationId.Value)) &&
                (((message.Status == MailDeliveryStatus.Delivered ||
                   message.Status == MailDeliveryStatus.Suppressed) &&
                  message.UpdatedAt < deliveredCutoff) ||
                 (message.Status == MailDeliveryStatus.Cancelled &&
                  message.UpdatedAt < cancelledCutoff) ||
                 (message.Status == MailDeliveryStatus.DeadLetter &&
                  message.UpdatedAt < deadLetterCutoff)))
            .OrderBy(message => message.UpdatedAt)
            .Select(message => message.Id)
            .Take(options.RetentionBatchSize)
            .ToListAsync(cancellationToken);

        if (expiredIds.Count == 0)
            return 0;

        return await dbContext.MailOutboxMessages
            .Where(message => expiredIds.Contains(message.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
