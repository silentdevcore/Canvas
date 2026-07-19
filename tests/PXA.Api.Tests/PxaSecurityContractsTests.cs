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
        var registeredPermissions = PxaPermissions.All.ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PxaRoles.Permissions.SelectMany(value => value.Value))
            Assert.Contains(permission, registeredPermissions);

        Assert.Equal(
            registeredPermissions.Order(StringComparer.Ordinal),
            PxaRoles.Permissions[PxaRoles.SystemAdministrator].Order(StringComparer.Ordinal));
        Assert.Contains(PxaPermissions.SubscriptionsRead,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);
        Assert.DoesNotContain(PxaPermissions.SubscriptionsManage,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);
        Assert.Empty(PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.Empty(PxaRoles.Permissions[PxaRoles.Viewer]);
    }
}
