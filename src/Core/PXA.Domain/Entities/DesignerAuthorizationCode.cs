namespace PXA.Domain.Entities;

public sealed class DesignerAuthorizationCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceSessionId { get; set; }
    public required string CodeHash { get; set; }
    public required string StateHash { get; set; }
    public required string PkceChallenge { get; set; }
    public required string DesignerOrigin { get; set; }
    public required string ReturnPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
