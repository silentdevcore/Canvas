using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Mail;

internal static class PxaMailTemplatePolicy
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
    };

    public static bool IsTransactional(string templateKey) =>
        !string.IsNullOrWhiteSpace(templateKey) && TransactionalTemplates.Contains(templateKey);
}

public interface IPxaMailQueue
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

public sealed class PxaMailQueue : IPxaMailQueue
{
    private readonly PxaDbContext dbContext;
    private readonly IDataProtector protector;

    public PxaMailQueue(PxaDbContext dbContext, IDataProtectionProvider dataProtectionProvider)
    {
        this.dbContext = dbContext;
        protector = dataProtectionProvider.CreateProtector("PXA.Mail.Outbox.Payload.v1");
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
        if (!PxaMailTemplatePolicy.IsTransactional(templateKey))
        {
            throw new ArgumentException(
                "The transactional mail queue accepts identity templates only. Marketing messages require a separate consent and suppression workflow.",
                nameof(templateKey));
        }

        var message = new MailOutboxMessage
        {
            OrganizationId = organizationId,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            TemplateKey = templateKey,
            Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale,
            ProtectedPayload = protector.Protect(JsonSerializer.Serialize(payload)),
            IdempotencyKey = idempotencyKey,
        };
        dbContext.MailOutboxMessages.Add(message);
        return message;
    }
}
