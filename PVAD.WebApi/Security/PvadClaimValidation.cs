using System.Security.Claims;

namespace PVAD.WebApi.Security;

public static class PvadClaimValidation
{
    private static readonly string[] SingleValuedTypes =
    [
        PvadClaimTypes.Organization, PvadClaimTypes.ActiveOrganization,
        PvadClaimTypes.ServiceAccount, PvadClaimTypes.Session,
    ];

    // Ambiguous tenant or session context must never be resolved by picking one value.
    public static bool HasUnambiguousContext(ClaimsPrincipal principal) =>
        SingleValuedTypes.All(type => principal.FindAll(type).Take(2).Count() < 2);
}
