using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.WebApi.Security;

public sealed class PvadPermissionClaimsTransformation : IClaimsTransformation
{
    private const string ResolvedMarker = "pvad_permissions_resolved";

    private readonly UserManager<PvadIdentityUser> userManager;
    private readonly PvadDbContext dbContext;
    private readonly PvadSystemOperatorAccess systemOperatorAccess;

    public PvadPermissionClaimsTransformation(
        UserManager<PvadIdentityUser> userManager,
        PvadDbContext dbContext,
        PvadSystemOperatorAccess systemOperatorAccess)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.systemOperatorAccess = systemOperatorAccess;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (!PvadClaimCompatibility.TryNormalize(principal)) return new ClaimsPrincipal();
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated ||
            identity.HasClaim(claim => claim.Type == ResolvedMarker))
        {
            return principal;
        }

        // Permission expansion belongs to persistent browser sessions. API-key and
        // delegated principals already carry their own scoped authorization data.
        if (!PvadSessionService.TryGetSessionId(principal, out _))
            return principal;

        foreach (var claim in identity.FindAll(PvadClaimTypes.Permission).ToArray())
            identity.RemoveClaim(claim);

        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
            return principal;

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var globalRoles = await userManager.GetRolesAsync(user);
        foreach (var role in globalRoles)
        {
            if (string.Equals(role, PvadRoles.SystemAdministrator, StringComparison.Ordinal) &&
                !systemOperatorAccess.IsAuthorized(user))
            {
                continue;
            }

            permissions.UnionWith(PvadRoles.Permissions.GetValueOrDefault(role, []));
        }

        if (Guid.TryParse(identity.FindFirst(PvadClaimTypes.ActiveOrganization)?.Value, out var organizationId))
        {
            var membershipId = await dbContext.OrganizationMemberships.AsNoTracking()
                .Where(value =>
                    value.OrganizationId == organizationId &&
                    value.UserId == user.Id &&
                    value.Status == OrganizationMembershipStatus.Active)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync();
            if (membershipId is not null)
            {
                var snapshot = await PvadMembershipPermissionResolver.ResolveAsync(
                    dbContext,
                    membershipId.Value,
                    organizationId,
                    CancellationToken.None);
                permissions.UnionWith(snapshot.Permissions);
            }
        }

        foreach (var permission in permissions.Order(StringComparer.Ordinal))
            identity.AddClaim(new Claim(PvadClaimTypes.Permission, permission));
        identity.AddClaim(new Claim(ResolvedMarker, "true"));
        return principal;
    }
}
