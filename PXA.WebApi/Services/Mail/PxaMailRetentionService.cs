using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailRetentionService(
    PxaDbContext dbContext,
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

        var expiredIds = await dbContext.MailOutboxMessages
            .Where(message =>
                ((message.Status == MailDeliveryStatus.Delivered ||
                  message.Status == MailDeliveryStatus.Suppressed) &&
                 message.UpdatedAt < deliveredCutoff) ||
                (message.Status == MailDeliveryStatus.Cancelled &&
                 message.UpdatedAt < cancelledCutoff) ||
                (message.Status == MailDeliveryStatus.DeadLetter &&
                 message.UpdatedAt < deadLetterCutoff))
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
