using System.Security.Claims;
using OpenIddict.Abstractions;

namespace PXA.WebApi.Security;

public static class PxaOAuthPolicies
{
    public const string TemplatesRead = "oauth.templates.read";

    public static bool HasScope(ClaimsPrincipal principal, string requiredScope) =>
        principal.HasScope(requiredScope) ||
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
}
