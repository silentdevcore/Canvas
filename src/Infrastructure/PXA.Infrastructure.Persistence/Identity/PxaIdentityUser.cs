using Microsoft.AspNetCore.Identity;

namespace PXA.Infrastructure.Persistence.Identity;

public sealed class PxaIdentityUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public string? PendingEmail { get; set; }
    public string Locale { get; set; } = "en";
    public string? Country { get; set; }
}
