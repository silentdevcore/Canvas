using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Services.Mail;

internal static class PvadMailTemplatePolicy
{
    private static readonly HashSet<string> TransactionalTemplates = new(StringComparer.Ordinal)
    {
        "identity.invitation",
        "identity.password-reset",
        "identity.password-changed",
        "identity.email-verification",
        "identity.email-changed",
        "identity.registration-verification",
        "identity.welcome",
        "identity.new-login",
        "identity.lockout",
        "identity.trial-expiring",
        "subscription.changed",
        "license.changed",
        "security.organization-changed",
    };

    public static bool IsTransactional(string templateKey) =>
        !string.IsNullOrWhiteSpace(templateKey) && TransactionalTemplates.Contains(templateKey);
}

public interface IPvadMailQueue
{
    MailOutboxMessage Enqueue(
        Guid? organizationId,
        Guid? recipientUserId,
        string recipientEmail,
        string templateKey,
        object payload,
        string idempotencyKey,
        string locale = "en");
}

public sealed class PvadMailQueue : IPvadMailQueue
{
    private readonly PvadDbContext dbContext;
    private readonly IDataProtector protector;

    public PvadMailQueue(PvadDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        this.dbContext = dbContext;
        protector = PvadDataProtection.MailPayload(dataProtectionProvider);
    }

    public MailOutboxMessage Enqueue(
        Guid? organizationId,
        Guid? recipientUserId,
        string recipientEmail,
        string templateKey,
        object payload,
        string idempotencyKey,
        string locale = "en")
    {
        if (!PvadMailTemplatePolicy.IsTransactional(templateKey))
        {
            throw new ArgumentException(
                "The transactional mail queue accepts approved transactional templates only. Marketing messages require a separate consent and suppression workflow.",
                nameof(templateKey));
        }

        using var activity = PvadTelemetry.StartMailEnqueue();
        var traceContext = PvadTelemetry.CaptureTraceContext();
        var message = new MailOutboxMessage
        {
            OrganizationId = organizationId,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            TemplateKey = templateKey,
            Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale,
            ProtectedPayload = protector.Protect(JsonSerializer.Serialize(payload)),
            IdempotencyKey = idempotencyKey,
            TraceParent = traceContext.TraceParent,
            TraceState = traceContext.TraceState,
        };
        dbContext.MailOutboxMessages.Add(message);
        PvadTelemetry.CompleteMailOperation(activity, "queued");
        return message;
    }
}
