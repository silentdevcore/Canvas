using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Mail;

public interface IPxaMailQueue
{
    MailOutboxMessage Enqueue(
        Guid? organizationId,
        Guid? recipientUserId,
        string recipientEmail,
        string templateKey,
        object payload,
        string idempotencyKey);
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
        string idempotencyKey)
    {
        var message = new MailOutboxMessage
        {
            OrganizationId = organizationId,
            RecipientUserId = recipientUserId,
            RecipientEmail = recipientEmail,
            TemplateKey = templateKey,
            ProtectedPayload = protector.Protect(JsonSerializer.Serialize(payload)),
            IdempotencyKey = idempotencyKey,
        };
        dbContext.MailOutboxMessages.Add(message);
        return message;
    }
}
