namespace PVAD.Domain.Entities;

/// <summary>Immutable manufacturer-issued artifact; not an organization subscription.</summary>
public sealed class NativeLibraryLicense
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid IssuedByUserId { get; set; }
    public Guid AuditEventId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? PreviousLicenseId { get; set; }
    public required string RequestSha256 { get; set; }
    public required string ArtifactJson { get; set; }
    public required string ArtifactSha256 { get; set; }
    public required string LicenseModel { get; set; }
    public required string CustomerReference { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public DateTimeOffset? UpdatesUntil { get; set; }
}
