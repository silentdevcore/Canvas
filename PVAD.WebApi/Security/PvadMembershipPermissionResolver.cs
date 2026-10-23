using Microsoft.EntityFrameworkCore;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

internal static class PvadMembershipPermissionResolver
{
    public static async Task<MembershipPermissionSnapshot> ResolveAsync(
        PvadDbContext dbContext,
        Guid membershipId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var roles = await (from assignment in dbContext.OrganizationMembershipRoles.AsNoTracking()
                           join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                           where assignment.OrganizationMembershipId == membershipId &&
                                 (role.IsSystemRole ||
                                  (role.OrganizationId == organizationId && !role.IsArchived))
                           select new
                           {
                               role.Id,
                               role.Name,
                               role.DisplayName,
                               role.IsSystemRole,
                           })
            .ToArrayAsync(cancellationToken);

        var roleIds = roles.Select(value => value.Id).ToArray();
        var claimedPermissions = roleIds.Length == 0
            ? []
            : await dbContext.RoleClaims.AsNoTracking()
                .Where(value => roleIds.Contains(value.RoleId) &&
                                value.ClaimType == PvadClaimTypes.Permission &&
                                value.ClaimValue != null)
                .Select(value => value.ClaimValue!)
                .ToArrayAsync(cancellationToken);
        var builtInPermissions = roles.Where(value => value.IsSystemRole)
            .SelectMany(value => PvadRoles.Permissions.GetValueOrDefault(value.Name!, []));

        return new MembershipPermissionSnapshot(
            roles.Select(value => value.DisplayName ?? value.Name ?? string.Empty)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            builtInPermissions.Concat(claimedPermissions)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}

internal sealed record MembershipPermissionSnapshot(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
