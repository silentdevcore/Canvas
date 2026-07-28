namespace PXA.Domain.Entities;

public sealed class DesignerReleaseRead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.UtcNow;
}
