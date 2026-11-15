using System.Security.Claims;
using OpenIddict.Abstractions;

namespace PVAD.WebApi.Security;

public static class PvadOAuthPolicies
{
    public const string TemplatesRead = "oauth.templates.read";

    public static bool HasScope(ClaimsPrincipal principal, string requiredScope) =>
        principal.GetScopes().Any(scope => string.Equals(scope, requiredScope, StringComparison.Ordinal)) ||
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.Ordinal));
}
