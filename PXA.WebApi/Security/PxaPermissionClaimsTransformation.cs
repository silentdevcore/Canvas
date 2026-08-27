using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.WebApi.Security;

public sealed class PxaPermissionClaimsTransformation : IClaimsTransformation
{
    private const string ResolvedMarker = "pxa_permissions_resolved";

    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly PxaDbContext dbContext;
    private readonly PxaSystemOperatorAccess systemOperatorAccess;

    public PxaPermissionClaimsTransformation(
        UserManager<PxaIdentityUser> userManager,
        PxaDbContext dbContext,
        PxaSystemOperatorAccess systemOperatorAccess)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
        this.systemOperatorAccess = systemOperatorAccess;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated ||
            identity.HasClaim(claim => claim.Type == ResolvedMarker))
        {
            return principal;
        }

        // Permission expansion belongs to persistent browser sessions. API-key and
        // delegated principals already carry their own scoped authorization data.
        if (!PxaSessionService.TryGetSessionId(principal, out _))
            return principal;

        foreach (var claim in identity.FindAll(PxaClaimTypes.Permission).ToArray())
            identity.RemoveClaim(claim);

        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
            return principal;

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        var globalRoles = await userManager.GetRolesAsync(user);
        foreach (var role in globalRoles)
        {
            if (string.Equals(role, PxaRoles.SystemAdministrator, StringComparison.Ordinal) &&
                !systemOperatorAccess.IsAuthorized(user))
            {
                continue;
            }

            permissions.UnionWith(PxaRoles.Permissions.GetValueOrDefault(role, []));
        }

        if (Guid.TryParse(identity.FindFirst(PxaClaimTypes.ActiveOrganization)?.Value, out var organizationId))
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
                var snapshot = await PxaMembershipPermissionResolver.ResolveAsync(
                    dbContext,
                    membershipId.Value,
                    organizationId,
                    CancellationToken.None);
                permissions.UnionWith(snapshot.Permissions);
            }
        }

        foreach (var permission in permissions.Order(StringComparer.Ordinal))
            identity.AddClaim(new Claim(PxaClaimTypes.Permission, permission));
        identity.AddClaim(new Claim(ResolvedMarker, "true"));
        return principal;
    }
}
