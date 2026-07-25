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
    public string? TermsAcceptedVersion { get; set; }
    public DateTimeOffset? TermsAcceptedAt { get; set; }
    public string? PrivacyAcknowledgedVersion { get; set; }
    public DateTimeOffset? PrivacyAcknowledgedAt { get; set; }
    public DateTimeOffset? MarketingConsentGrantedAt { get; set; }
    public DateTimeOffset? MarketingConsentWithdrawnAt { get; set; }
    public string? MarketingConsentSource { get; set; }
}
