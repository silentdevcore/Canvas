namespace PVAD.Domain.Entities;

public sealed class NativeLicenseRegistrationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OwnerKey { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid SourceUserId { get; set; }
    public Guid SourceOrganizationId { get; set; }
    public Guid SourceSubscriptionId { get; set; }
    public Guid VerificationId { get; set; }
    public SubscriptionAccountType AccountType { get; set; }
    public NativeLicenseRegistrationStatus Status { get; set; }
    public string? ContractJson { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset ScheduledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? LicenseId { get; set; }
    public string? FailureCode { get; set; }
}

public enum NativeLicenseRegistrationStatus { Pending, Processing, Completed, DeadLetter }
