namespace PXA.Domain.Entities;

public sealed class MailOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public required string RecipientEmail { get; set; }
    public required string TemplateKey { get; set; }
    public int TemplateVersion { get; set; } = 1;
    public string Locale { get; set; } = "en";
    public required string ProtectedPayload { get; set; }
    public MailDeliveryStatus Status { get; set; } = MailDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset ScheduledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? FailureReason { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum MailDeliveryStatus
{
    Pending,
    Scheduled,
    Sending,
    Delivered,
    Failed,
    Suppressed,
    Cancelled,
    DeadLetter,
}
