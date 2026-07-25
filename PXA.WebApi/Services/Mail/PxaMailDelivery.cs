using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

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

    public PxaMailProcessor(
        PxaDbContext dbContext,
        IPxaMailTransport transport,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PxaMailOptions> options)
    {
        this.dbContext = dbContext;
        this.transport = transport;
        protector = dataProtectionProvider.CreateProtector("PXA.Mail.Outbox.Payload.v1");
        this.options = options.Value;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        if (!options.IsDeliveryEnabled)
            return 0;

        var now = DateTimeOffset.UtcNow;
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
            message.Status = MailDeliveryStatus.Sending;
            message.Attempts++;
            message.LastAttemptAt = now;
            message.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    protector.Unprotect(message.ProtectedPayload)) ?? [];
                var rendered = Render(message, payload);
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
        }

        return messages.Count;
    }

    private static RenderedMail Render(MailOutboxMessage message, IReadOnlyDictionary<string, string> payload)
    {
        var displayName = WebUtility.HtmlEncode(payload.GetValueOrDefault("displayName", "PXA user"));
        var actionUrl = WebUtility.HtmlEncode(payload.GetValueOrDefault("actionUrl", string.Empty));
        var rawActionUrl = payload.GetValueOrDefault("actionUrl", string.Empty);
        var isGerman = string.Equals(message.Locale, "de", StringComparison.OrdinalIgnoreCase);
        return message.TemplateKey switch
        {
            "identity.invitation" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Your Power Dox Automation invitation",
                $"<p>Hello {displayName},</p><p>You have been invited to Power Dox Automation.</p><p><a href=\"{actionUrl}\">Accept invitation</a></p>",
                $"Hello {payload.GetValueOrDefault("displayName", "PXA user")}, accept your invitation: {payload.GetValueOrDefault("actionUrl", string.Empty)}"),
            "identity.password-reset" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Reset your Power Dox Automation password",
                $"<p>Hello {displayName},</p><p><a href=\"{actionUrl}\">Reset password</a></p>",
                $"Reset your Power Dox Automation password: {payload.GetValueOrDefault("actionUrl", string.Empty)}"),
            "identity.password-changed" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Your Power Dox Automation password changed",
                $"<p>Hello {displayName},</p><p>Your password was changed. Contact support immediately if this was not you.</p>",
                "Your Power Dox Automation password was changed. Contact support immediately if this was not you."),
            "identity.email-verification" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Verify your new Power Dox Automation email address",
                $"<p>Hello {displayName},</p><p><a href=\"{actionUrl}\">Verify email address</a></p>",
                $"Verify your new Power Dox Automation email address: {payload.GetValueOrDefault("actionUrl", string.Empty)}"),
            "identity.email-changed" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Your Power Dox Automation email address changed",
                $"<p>Hello {displayName},</p><p>Your account email address was changed. Contact support immediately if this was not expected.</p>",
                "Your Power Dox Automation account email address was changed. Contact support immediately if this was not expected."),
            "identity.registration-verification" when isGerman => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Bestätigen Sie Ihr Power Dox Automation-Konto",
                $"<p>Hallo {displayName},</p><p><a href=\"{actionUrl}\">Konto bestätigen</a></p>",
                $"Bestätigen Sie Ihr Power Dox Automation-Konto: {rawActionUrl}"),
            "identity.registration-verification" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Verify your Power Dox Automation account",
                $"<p>Hello {displayName},</p><p><a href=\"{actionUrl}\">Verify your account</a></p>",
                $"Verify your Power Dox Automation account: {rawActionUrl}"),
            "identity.welcome" when isGerman => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Willkommen bei Power Dox Automation",
                $"<p>Hallo {displayName},</p><p>Ihr Power Dox Automation-Konto und Ihre Testphase sind bereit.</p><p><a href=\"{actionUrl}\">Konto öffnen</a></p>",
                $"Ihr Power Dox Automation-Konto und Ihre Testphase sind bereit: {rawActionUrl}"),
            "identity.welcome" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Welcome to Power Dox Automation",
                $"<p>Hello {displayName},</p><p>Your Power Dox Automation account and Trial are ready.</p><p><a href=\"{actionUrl}\">Open your account</a></p>",
                $"Your Power Dox Automation account and Trial are ready: {rawActionUrl}"),
            "identity.new-login" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "New sign-in to your Power Dox Automation account",
                $"<p>Hello {displayName},</p><p>Your account was just signed in to{(payload.TryGetValue("client", out var loginClient) && loginClient.Length > 0 ? $" from {WebUtility.HtmlEncode(loginClient)}" : string.Empty)}. Contact support immediately if this was not you.</p>",
                $"Your Power Dox Automation account was just signed in to{(payload.TryGetValue("client", out var loginClientText) && loginClientText.Length > 0 ? $" from {loginClientText}" : string.Empty)}. Contact support immediately if this was not you."),
            "identity.lockout" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Your Power Dox Automation account was locked",
                $"<p>Hello {displayName},</p><p>Your account was temporarily locked after too many unsuccessful sign-in attempts. Contact support if this was not you.</p>",
                "Your Power Dox Automation account was temporarily locked after too many unsuccessful sign-in attempts. Contact support if this was not you."),
            "identity.trial-expiring" => new RenderedMail(
                message.Id,
                message.RecipientEmail,
                "Your Power Dox Automation Trial is ending soon",
                $"<p>Hello {displayName},</p><p>Your Trial ends on {WebUtility.HtmlEncode(payload.GetValueOrDefault("trialEndsAt", string.Empty))}. Upgrade to keep access to your workspace.</p>",
                $"Your Power Dox Automation Trial ends on {payload.GetValueOrDefault("trialEndsAt", string.Empty)}. Upgrade to keep access to your workspace."),
            "subscription.changed" => RenderChange(
                message,
                payload,
                displayName,
                "Your Power Dox Automation subscription changed",
                "Subscription"),
            "license.changed" => RenderChange(
                message,
                payload,
                displayName,
                "Your Power Dox Automation license changed",
                "License"),
            "security.organization-changed" => RenderChange(
                message,
                payload,
                displayName,
                "Security change in your Power Dox Automation organization",
                "Security"),
            _ => throw new PxaPermanentMailException("Unknown transactional mail template."),
        };
    }

    private static RenderedMail RenderChange(
        MailOutboxMessage message,
        IReadOnlyDictionary<string, string> payload,
        string displayName,
        string subject,
        string category)
    {
        var summary = payload.GetValueOrDefault("summary", $"{category} settings changed.");
        var safeSummary = WebUtility.HtmlEncode(summary);
        var actionUrl = payload.GetValueOrDefault("actionUrl", string.Empty);
        var safeActionUrl = WebUtility.HtmlEncode(actionUrl);
        return new RenderedMail(
            message.Id,
            message.RecipientEmail,
            subject,
            $"<p>Hello {displayName},</p><p>{safeSummary}</p><p><a href=\"{safeActionUrl}\">Open your account</a></p>",
            $"Hello {payload.GetValueOrDefault("displayName", "PXA user")}, {summary} Open your account: {actionUrl}");
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
                logger.LogWarning(exception, "PXA mail processing is temporarily unavailable.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
