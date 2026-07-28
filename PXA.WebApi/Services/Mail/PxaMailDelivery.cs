using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Services.Mail;

public sealed record RenderedMail(
    Guid OutboxId,
    string RecipientEmail,
    string Subject,
    string HtmlBody,
    string TextBody);

internal sealed class PxaPermanentMailException(string message) : Exception(message);

public interface IPxaMailTransport
{
    Task<string> SendAsync(RenderedMail message, CancellationToken cancellationToken);
}

public sealed class DevelopmentMailTransport : IPxaMailTransport
{
    private readonly ConcurrentQueue<RenderedMail> messages = new();

    public IReadOnlyCollection<RenderedMail> Messages => messages.ToArray();

    public Task<string> SendAsync(RenderedMail message, CancellationToken cancellationToken)
    {
        messages.Enqueue(message);
        return Task.FromResult($"development:{message.OutboxId}");
    }
}

public sealed class PxaMailProcessor
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaMailTransport transport;
    private readonly IDataProtector protector;
    private readonly PxaMailOptions options;
    private readonly PxaMailTemplateRenderer templateRenderer;

    public PxaMailProcessor(
        PxaDbContext dbContext,
        IPxaMailTransport transport,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PxaMailOptions> options,
        PxaMailTemplateRenderer templateRenderer)
    {
        this.dbContext = dbContext;
        this.transport = transport;
        protector = dataProtectionProvider.CreateProtector("PXA.Mail.Outbox.Payload.v1");
        this.options = options.Value;
        this.templateRenderer = templateRenderer;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var queueState = await dbContext.MailOutboxMessages
            .Where(message =>
                message.Status == MailDeliveryStatus.Pending ||
                message.Status == MailDeliveryStatus.Failed)
            .GroupBy(_ => 1)
            .Select(messages => new
            {
                Count = messages.LongCount(),
                OldestCreatedAt = messages.Min(message => message.CreatedAt),
            })
            .SingleOrDefaultAsync(cancellationToken);
        PxaTelemetry.RecordMailQueue(
            queueState?.Count ?? 0,
            queueState is null ? TimeSpan.Zero : now - queueState.OldestCreatedAt);

        if (!options.IsDeliveryEnabled)
            return 0;

        var messages = await dbContext.MailOutboxMessages
            .Where(message =>
                (message.Status == MailDeliveryStatus.Pending || message.Status == MailDeliveryStatus.Failed) &&
                message.ScheduledAt <= now &&
                message.Attempts < 5)
            .OrderBy(message => message.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            using var activity = PxaTelemetry.StartMailDelivery(
                message.Attempts + 1,
                message.TraceParent,
                message.TraceState);
            var deliveryStopwatch = Stopwatch.StartNew();
            message.Status = MailDeliveryStatus.Sending;
            message.Attempts++;
            message.LastAttemptAt = now;
            message.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    protector.Unprotect(message.ProtectedPayload)) ?? [];
                var rendered = templateRenderer.Render(message, payload);
                message.ProviderMessageId = await transport.SendAsync(rendered, cancellationToken);
                message.Status = MailDeliveryStatus.Delivered;
                message.DeliveredAt = DateTimeOffset.UtcNow;
                message.FailureReason = null;
            }
            catch (PxaPermanentMailException exception)
            {
                message.Status = MailDeliveryStatus.DeadLetter;
                message.FailureReason = $"{exception.GetType().Name}: Mail delivery cannot be retried.";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Status = message.Attempts >= 5
                    ? MailDeliveryStatus.DeadLetter
                    : MailDeliveryStatus.Failed;
                message.FailureReason = $"{exception.GetType().Name}: Mail delivery failed.";
                message.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, message.Attempts));
            }

            message.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            deliveryStopwatch.Stop();
            PxaTelemetry.RecordMailDelivery(
                message.Status switch
                {
                    MailDeliveryStatus.Delivered => "delivered",
                    MailDeliveryStatus.DeadLetter => "dead_letter",
                    _ => "retry",
                },
                deliveryStopwatch.Elapsed,
                now - message.CreatedAt);
            PxaTelemetry.CompleteMailOperation(
                activity,
                message.Status switch
                {
                    MailDeliveryStatus.Delivered => "delivered",
                    MailDeliveryStatus.DeadLetter => "dead_letter",
                    _ => "retry",
                });
        }

        return messages.Count;
    }

}

public sealed class PxaMailWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<PxaMailWorker> logger;
    private readonly PxaMailOptions options;
    private DateTimeOffset nextRetentionCleanupAt = DateTimeOffset.MinValue;

    public PxaMailWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PxaMailWorker> logger,
        IOptions<PxaMailOptions> options)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                    .ProcessPendingAsync(stoppingToken);
                var now = DateTimeOffset.UtcNow;
                if (now >= nextRetentionCleanupAt)
                {
                    await scope.ServiceProvider.GetRequiredService<PxaMailRetentionService>()
                        .DeleteExpiredAsync(now, stoppingToken);
                    nextRetentionCleanupAt = now.AddMinutes(options.RetentionCleanupIntervalMinutes);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    PxaLogEvents.MailProcessingFailed,
                    exception,
                    "PXA mail processing is temporarily unavailable.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
