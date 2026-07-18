using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.WebApi.Security;

public sealed class PxaCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly UserManager<PxaIdentityUser> userManager;

    public PxaCookieAuthenticationEvents(UserManager<PxaIdentityUser> userManager)
    {
        this.userManager = userManager;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = await userManager.GetUserAsync(context.Principal!);
        var principalStamp = context.Principal?.FindFirstValue(
            userManager.Options.ClaimsIdentity.SecurityStampClaimType);
        var currentStamp = user is null ? null : await userManager.GetSecurityStampAsync(user);

        if (user is not { IsActive: true } ||
            string.IsNullOrEmpty(principalStamp) ||
            !string.Equals(principalStamp, currentStamp, StringComparison.Ordinal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
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
