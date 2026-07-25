using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.WebApi.Security;

public sealed class PxaCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly PxaDbContext dbContext;
    private readonly PxaSystemOperatorAccess systemOperatorAccess;

    public PxaCookieAuthenticationEvents(
        UserManager<PxaIdentityUser> userManager,
        PxaDbContext dbContext,
        PxaSystemOperatorAccess systemOperatorAccess)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.systemOperatorAccess = systemOperatorAccess;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = await userManager.GetUserAsync(context.Principal!);
        var principalStamp = context.Principal?.FindFirstValue(
            userManager.Options.ClaimsIdentity.SecurityStampClaimType);
        var currentStamp = user is null ? null : await userManager.GetSecurityStampAsync(user);

        var hasSession = PxaSessionService.TryGetSessionId(context.Principal!, out var sessionId);
        var session = !hasSession || user is null
            ? null
            : await dbContext.UserSessions.SingleOrDefaultAsync(value =>
                value.Id == sessionId && value.UserId == user.Id);
        var now = DateTimeOffset.UtcNow;

        var isSystemAdministrator = user is not null &&
            context.Principal?.IsInRole(PxaRoles.SystemAdministrator) == true;
        var isAuthorizedSystemOperator = user is not null &&
            isSystemAdministrator &&
            systemOperatorAccess.IsAuthorized(user);
        var hasUnauthorizedSystemRole = isSystemAdministrator && !isAuthorizedSystemOperator;
        var organizationAccessIsValid = user is not null &&
            session is not null &&
            await HasValidOrganizationAccessAsync(
                context.Principal!, user.Id, session.OrganizationId, isAuthorizedSystemOperator,
                context.HttpContext.RequestAborted);

        if (user is not { IsActive: true } ||
            string.IsNullOrEmpty(principalStamp) ||
            !string.Equals(principalStamp, currentStamp, StringComparison.Ordinal) ||
            session is null || session.RevokedAt is not null || session.ExpiresAt <= now ||
            hasUnauthorizedSystemRole || !organizationAccessIsValid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        if (session.LastSeenAt <= now.AddMinutes(-5))
        {
            session.LastSeenAt = now;
            await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
    }

    private async Task<bool> HasValidOrganizationAccessAsync(
        ClaimsPrincipal principal,
        Guid userId,
        Guid? sessionOrganizationId,
        bool isAuthorizedSystemOperator,
        CancellationToken cancellationToken)
    {
        var organizationClaim = principal.FindFirstValue(PxaClaimTypes.ActiveOrganization);
        if (organizationClaim is null)
            return sessionOrganizationId is null;
        if (!Guid.TryParse(organizationClaim, out var organizationId) ||
            sessionOrganizationId != organizationId)
        {
            return false;
        }

        var organization = await dbContext.Organizations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == organizationId, cancellationToken);
        if (organization is null)
            return false;
        if (isAuthorizedSystemOperator)
            return true;

        var hasActiveMembership = await dbContext.OrganizationMemberships.AsNoTracking()
            .AnyAsync(value =>
                value.OrganizationId == organizationId &&
                value.UserId == userId &&
                value.Status == PXA.Domain.Entities.OrganizationMembershipStatus.Active,
                cancellationToken);
        if (!hasActiveMembership)
            return false;
        if (organization.Status != PXA.Domain.Entities.OrganizationStatus.Closed)
            return true;

        return await dbContext.AccountClosureRequests.AsNoTracking()
            .AnyAsync(value =>
                value.OrganizationId == organizationId &&
                value.TargetType == PXA.Domain.Entities.AccountClosureTargetType.Organization &&
                value.Status == PXA.Domain.Entities.AccountClosureStatus.Pending &&
                value.RequestedByUserId == userId,
                cancellationToken);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
