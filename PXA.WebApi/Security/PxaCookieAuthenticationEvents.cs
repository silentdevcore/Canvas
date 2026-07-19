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

        var hasUnauthorizedSystemRole = user is not null &&
            context.Principal?.IsInRole(PxaRoles.SystemAdministrator) == true &&
            !systemOperatorAccess.IsAuthorized(user);

        if (user is not { IsActive: true } ||
            string.IsNullOrEmpty(principalStamp) ||
            !string.Equals(principalStamp, currentStamp, StringComparison.Ordinal) ||
            session is null || session.RevokedAt is not null || session.ExpiresAt <= now ||
            hasUnauthorizedSystemRole)
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
