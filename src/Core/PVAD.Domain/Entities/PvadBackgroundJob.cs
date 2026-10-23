namespace PVAD.Domain.Entities;

public sealed class PvadBackgroundJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public required string Type { get; set; }
    public required string PayloadJson { get; set; }
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
    public PvadBackgroundJobStatus Status { get; set; } = PvadBackgroundJobStatus.Pending;
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
    public PvadJobRetentionMode RetentionMode { get; set; } = PvadJobRetentionMode.Transient;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
    public DateTimeOffset MetadataExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);
    public DateTimeOffset? ResultDownloadedAt { get; set; }
    public DateTimeOffset? ContentPurgedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum PvadJobRetentionMode
{
    Transient,
    Retained,
}

public enum PvadBackgroundJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled,
    DeadLetter,
    Expired,
}
