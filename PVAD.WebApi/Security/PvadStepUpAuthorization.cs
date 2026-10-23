using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

public static class PvadSecurityPolicies
{
    public const string RecentAuthentication = "pxa.security.recent-authentication";
    public const string OrganizationPdfSigning = "pxa.pdf.signatures.organization-sign";
    public const string AppliedPdfRedaction = "pxa.pdf.redactions.apply";
    public const string PdfTrustPolicyManagement = "pxa.pdf.trust-policies.manage";
    public const string PdfKeyProviderManagement = "pxa.pdf.key-providers.manage";
}

public sealed class PvadStepUpOptions
{
    public const string SectionName = "Security:StepUp";

    public int MaximumAuthenticationAgeMinutes { get; init; } = 15;
}

public sealed class PvadRecentAuthenticationRequirement : IAuthorizationRequirement;

public sealed class PvadRecentAuthenticationHandler(
    PvadDbContext dbContext,
    IOptions<PvadStepUpOptions> options,
    TimeProvider timeProvider) : AuthorizationHandler<PvadRecentAuthenticationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PvadRecentAuthenticationRequirement requirement)
    {
        if (!PvadSessionService.TryGetSessionId(context.User, out var sessionId) ||
            context.User.HasClaim(claim => claim.Type == PvadClaimTypes.ServiceAccount))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var earliestAccepted = now.AddMinutes(-options.Value.MaximumAuthenticationAgeMinutes);
        var isRecent = await dbContext.UserSessions.AsNoTracking().AnyAsync(session =>
            session.Id == sessionId &&
            session.RevokedAt == null &&
            session.ExpiresAt > now &&
            session.CreatedAt >= earliestAccepted);
        if (isRecent)
        {
            context.Succeed(requirement);
        }
    }
}
