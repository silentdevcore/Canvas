using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Security;

public static class PxaSecurityPolicies
{
    public const string RecentAuthentication = "pxa.security.recent-authentication";
    public const string OrganizationPdfSigning = "pxa.pdf.signatures.organization-sign";
    public const string AppliedPdfRedaction = "pxa.pdf.redactions.apply";
    public const string PdfTrustPolicyManagement = "pxa.pdf.trust-policies.manage";
    public const string PdfKeyProviderManagement = "pxa.pdf.key-providers.manage";
}

public sealed class PxaStepUpOptions
{
    public const string SectionName = "Security:StepUp";

    public int MaximumAuthenticationAgeMinutes { get; init; } = 15;
}

public sealed class PxaRecentAuthenticationRequirement : IAuthorizationRequirement;

public sealed class PxaRecentAuthenticationHandler(
    PxaDbContext dbContext,
    IOptions<PxaStepUpOptions> options,
    TimeProvider timeProvider) : AuthorizationHandler<PxaRecentAuthenticationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PxaRecentAuthenticationRequirement requirement)
    {
        if (!PxaSessionService.TryGetSessionId(context.User, out var sessionId) ||
            context.User.HasClaim(claim => claim.Type == PxaClaimTypes.ServiceAccount))
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
