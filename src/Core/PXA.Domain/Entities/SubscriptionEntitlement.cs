namespace PXA.Domain.Entities;

public sealed class SubscriptionEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public required string Capability { get; set; }
    public bool Enabled { get; set; }
    public long? Limit { get; set; }
    public string? Unit { get; set; }
    public EntitlementSource Source { get; set; } = EntitlementSource.EditionDefault;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum EntitlementSource { EditionDefault, NegotiatedOverride, TemporaryGrant }
