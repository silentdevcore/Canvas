using System.Security.Claims;
using OpenIddict.Abstractions;

namespace PVAD.WebApi.Security;

public static class PvadOAuthPolicies
{
    public const string TemplatesRead = "oauth.templates.read";

    public static bool HasScope(ClaimsPrincipal principal, string requiredScope) =>
        principal.GetScopes().Any(scope => PvadScopeCompatibility.Equivalent(scope, requiredScope)) ||
        principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(scope => PvadScopeCompatibility.Equivalent(scope, requiredScope));
}
