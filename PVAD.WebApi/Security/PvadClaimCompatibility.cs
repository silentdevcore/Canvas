using System.Security.Claims;

namespace PVAD.WebApi.Security;

public static class PvadClaimCompatibility
{
    private static readonly string[] CanonicalTypes =
    [
        PvadClaimTypes.Permission, PvadClaimTypes.Organization, PvadClaimTypes.ActiveOrganization,
        PvadClaimTypes.ServiceAccount, PvadClaimTypes.Session,
    ];

    public static bool HasLegacyClaims(ClaimsPrincipal principal) => principal.Claims.Any(claim =>
        CanonicalTypes.Any(type => claim.Type == Legacy(type)));

    public static bool TryNormalize(ClaimsPrincipal principal)
    {
        // Validate the entire principal before changing any identity or claim.
        foreach (var type in CanonicalTypes)
        {
            var current = principal.FindAll(type).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
            var old = principal.FindAll(Legacy(type)).Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
            if (current.Count > 0 && old.Count > 0 && !current.SetEquals(old)) return false;
            if (type != PvadClaimTypes.Permission && (current.Count > 1 || old.Count > 1)) return false;
        }
        foreach (var identity in principal.Identities)
        {
            foreach (var type in CanonicalTypes)
            {
                foreach (var claim in identity.FindAll(Legacy(type)).ToArray())
                {
                    var replacement = new Claim(type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer, identity);
                    foreach (var property in claim.Properties) replacement.Properties.Add(property);
                    identity.RemoveClaim(claim);
                    identity.AddClaim(replacement);
                }
            }
        }
        return true;
    }

    private static string Legacy(string canonical) => "pxa_" + canonical["pvad_".Length..];
}
