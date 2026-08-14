namespace PXA.Domain.Entities;

public sealed class DesignerNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public DesignerNotificationCategory Category { get; set; }
    public DesignerNotificationSeverity Severity { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }
    public bool Dismissible { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public enum DesignerNotificationCategory
{
    System,
    Security,
    Subscription,
    ActionRequired,
    Legal,
}

public enum DesignerNotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
}
