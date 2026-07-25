using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Mail;

internal static class PxaMailTemplatePolicy
{
    private const string TransactionalPrefix = "identity.";

    public static bool IsTransactional(string templateKey) =>
        !string.IsNullOrWhiteSpace(templateKey) &&
        templateKey.StartsWith(TransactionalPrefix, StringComparison.Ordinal) &&
        templateKey.Length > TransactionalPrefix.Length;
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
