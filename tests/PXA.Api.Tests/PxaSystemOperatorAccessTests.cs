using Microsoft.Extensions.Options;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class PxaSystemOperatorAccessTests
{
    [Fact]
    public void Explicit_operator_allowlist_is_case_insensitive_and_deny_by_default()
    {
        var access = CreateAccess(true, "operator@powerdoxautomation.com");

        Assert.True(access.IsAuthorized(CreateUser("OPERATOR@powerdoxautomation.com")));
        Assert.False(access.IsAuthorized(CreateUser("administrator@customer.test")));
        Assert.False(access.IsAuthorized(CreateUser(null)));
    }

    [Fact]
    public void Explicit_operator_enforcement_can_be_disabled_for_development_and_tests()
    {
        var access = CreateAccess(false);

        Assert.True(access.IsAuthorized(CreateUser("local-admin@pxa.test")));
    }

    private static PxaSystemOperatorAccess CreateAccess(bool required, params string[] emails) =>
        new(Options.Create(new PxaAdminSecurityOptions
        {
            RequireExplicitSystemOperators = required,
            SystemOperatorEmails = [.. emails],
        }));

    private static PxaIdentityUser CreateUser(string? email) => new()
    {
        DisplayName = "Test Operator",
        Email = email,
        UserName = email,
    };
}
