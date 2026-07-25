namespace PXA.Domain.Entities;

public sealed class UserConsentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string ConsentType { get; set; }
    public required string Decision { get; set; }
    public string? PolicyVersion { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
