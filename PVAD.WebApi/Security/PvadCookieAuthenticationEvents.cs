using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.WebApi.Security;

public sealed class PvadCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly UserManager<PvadIdentityUser> userManager;
    private readonly PvadDbContext dbContext;
    private readonly PvadSystemOperatorAccess systemOperatorAccess;

    public PvadCookieAuthenticationEvents(
        UserManager<PvadIdentityUser> userManager,
        PvadDbContext dbContext,
        PvadSystemOperatorAccess systemOperatorAccess)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.systemOperatorAccess = systemOperatorAccess;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var legacyClaims = PvadClaimCompatibility.HasLegacyClaims(context.Principal!);
        if (!PvadClaimCompatibility.TryNormalize(context.Principal!))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }
        var user = await userManager.GetUserAsync(context.Principal!);
        var principalStamp = context.Principal?.FindFirstValue(
            userManager.Options.ClaimsIdentity.SecurityStampClaimType);
        var currentStamp = user is null ? null : await userManager.GetSecurityStampAsync(user);
        var userIsLocked = user is not null && await userManager.IsLockedOutAsync(user);

        var hasSession = PvadSessionService.TryGetSessionId(context.Principal!, out var sessionId);
        var session = !hasSession || user is null
            ? null
            : await dbContext.UserSessions.SingleOrDefaultAsync(value =>
                value.Id == sessionId && value.UserId == user.Id);
        var now = DateTimeOffset.UtcNow;

        var isSystemAdministrator = user is not null &&
            context.Principal?.IsInRole(PvadRoles.SystemAdministrator) == true;
        var isAuthorizedSystemOperator = user is not null &&
            isSystemAdministrator &&
            systemOperatorAccess.IsAuthorized(user);
        var hasUnauthorizedSystemRole = isSystemAdministrator && !isAuthorizedSystemOperator;
        var organizationAccessIsValid = user is not null &&
            session is not null &&
            await HasValidOrganizationAccessAsync(
                context.Principal!, user.Id, session.OrganizationId, isAuthorizedSystemOperator,
                context.HttpContext.RequestAborted);

        if (user is not { IsActive: true, EmailConfirmed: true } ||
            userIsLocked ||
            string.IsNullOrEmpty(principalStamp) ||
            !string.Equals(principalStamp, currentStamp, StringComparison.Ordinal) ||
            session is null || session.RevokedAt is not null || session.ExpiresAt <= now ||
            hasUnauthorizedSystemRole || !organizationAccessIsValid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        if (session.LastSeenAt <= now.AddMinutes(-5))
        {
            session.LastSeenAt = now;
            await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
        }

        if (legacyClaims || context.Options.Cookie.Name is { } cookieName &&
            PvadMigratingCookieManager.WasLegacyRead(context.HttpContext, cookieName))
            context.ShouldRenew = true;

        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            var storedPermissions = identity.FindAll(PvadClaimTypes.Permission).ToArray();
            var storedOrganizations = identity.FindAll(PvadClaimTypes.Organization).ToArray();
            if (storedPermissions.Length > 0 || storedOrganizations.Length > 0)
            {
                foreach (var claim in storedPermissions.Concat(storedOrganizations))
                    identity.RemoveClaim(claim);
                context.ShouldRenew = true;
            }
        }
    }

    private async Task<bool> HasValidOrganizationAccessAsync(
        ClaimsPrincipal principal,
        Guid userId,
        Guid? sessionOrganizationId,
        bool isAuthorizedSystemOperator,
        CancellationToken cancellationToken)
    {
        var organizationClaim = principal.FindFirstValue(PvadClaimTypes.ActiveOrganization);
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
                value.Status == PVAD.Domain.Entities.OrganizationMembershipStatus.Active,
                cancellationToken);
        if (!hasActiveMembership)
            return false;
        if (organization.Status != PVAD.Domain.Entities.OrganizationStatus.Closed)
            return true;

        return await dbContext.AccountClosureRequests.AsNoTracking()
            .AnyAsync(value =>
                value.OrganizationId == organizationId &&
                value.TargetType == PVAD.Domain.Entities.AccountClosureTargetType.Organization &&
                value.Status == PVAD.Domain.Entities.AccountClosureStatus.Pending &&
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
