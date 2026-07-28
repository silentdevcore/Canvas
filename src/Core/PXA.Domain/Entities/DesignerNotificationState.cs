namespace PXA.Domain.Entities;

public sealed class DesignerNotificationState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
