using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class PxaSecurityContractsTests
{
    [Fact]
    public void Tenant_context_resolves_only_valid_authenticated_identifiers()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(PxaClaimTypes.ActiveOrganization, organizationId.ToString()),
                ], "test")),
            },
        };

        var context = new PxaTenantContext(accessor);

        Assert.Equal(userId, context.UserId);
        Assert.Equal(organizationId, context.OrganizationId);

        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "not-a-user-id"),
            new Claim(PxaClaimTypes.ActiveOrganization, "not-an-organization-id"),
        ], "test"));
        Assert.Null(context.UserId);
        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void Built_in_roles_use_only_registered_permissions_and_keep_commercial_access_separate()
    {
        var registeredPermissions = PxaPermissions.All
            .Concat(PxaAccountPermissions.All)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PxaRoles.Permissions.SelectMany(value => value.Value))
            Assert.Contains(permission, registeredPermissions);

        Assert.Equal(
            PxaPermissions.All.ToHashSet(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            PxaRoles.Permissions[PxaRoles.SystemAdministrator].Order(StringComparer.Ordinal));
        Assert.Contains(PxaPermissions.SubscriptionsRead,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);
        Assert.DoesNotContain(PxaPermissions.SubscriptionsManage,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);

        // Editor/Viewer are customer-facing member roles: no Admin-app permissions,
        // but they do get the self-scoped Account permissions every member needs.
        Assert.DoesNotContain(PxaRoles.Permissions[PxaRoles.Editor], PxaPermissions.All.Contains);
        Assert.DoesNotContain(PxaRoles.Permissions[PxaRoles.Viewer], PxaPermissions.All.Contains);
        Assert.Contains(PxaAccountPermissions.ProfileManage, PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.Contains(PxaAccountPermissions.ProfileManage, PxaRoles.Permissions[PxaRoles.Viewer]);
        Assert.DoesNotContain(PxaAccountPermissions.MembersInvite, PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.DoesNotContain(PxaAccountPermissions.MembersInvite, PxaRoles.Permissions[PxaRoles.Viewer]);
    }

    [Fact]
    public void Account_permissions_are_all_registered_and_mapped_to_at_least_one_role()
    {
        var mappedPermissions = PxaRoles.Permissions.SelectMany(value => value.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PxaAccountPermissions.All)
            Assert.Contains(permission, mappedPermissions);

        // The privileged System Administrator vocabulary and the customer Account
        // vocabulary must not overlap, so Admin authorization can never be satisfied
        // by an Account-only permission claim (checklist: keep customer authorization
        // separate from System Administrator access).
        Assert.Empty(PxaAccountPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PxaPermissions.All, StringComparer.Ordinal));
    }
}
