namespace PXA.Domain.Entities;

public sealed class PxaBackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public required string Type { get; set; }
    public required string PayloadJson { get; set; }
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public PxaBackgroundJobStatus Status { get; set; } = PxaBackgroundJobStatus.Pending;
    public int Attempts { get; set; }
    public int MaximumAttempts { get; set; } = 3;
    public DateTimeOffset ScheduledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public Guid? InputObjectId { get; set; }
    public Guid? ResultObjectId { get; set; }
    public int ProgressPercent { get; set; }
    public string? DiagnosticsJson { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum PxaBackgroundJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled,
    DeadLetter,
    Expired,
}
