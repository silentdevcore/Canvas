namespace PXA.Domain.Entities;

public sealed class AccountClosureRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AccountClosureTargetType TargetType { get; set; }
    public required Guid TargetId { get; set; }
    public Guid? OrganizationId { get; set; }
    public required Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public AccountClosureStatus Status { get; set; } = AccountClosureStatus.Pending;
    public required DateTimeOffset ScheduledPurgeAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public enum AccountClosureTargetType
{
    User,
    Organization,
}

public enum AccountClosureStatus
{
    Pending,
    Cancelled,
    Completed,
}
