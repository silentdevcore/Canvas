using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadOAuthScopeCatalogTests
{
    [Fact]
    public void Supported_scopes_are_canonical_and_unique()
    {
        Assert.Equal(PvadOAuthScopes.All.Length, PvadOAuthScopes.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PvadOAuthScopes.All, scope => Assert.StartsWith("pvad.", scope));
    }

    [Fact]
    public void Read_only_scopes_do_not_grant_write_former_or_unknown_scopes()
    {
        Assert.All(PvadOAuthScopes.ReadOnly, scope => Assert.True(PvadOAuthScopes.IsReadOnly(scope)));
        Assert.False(PvadOAuthScopes.IsReadOnly("pvad.templates.write"));
        Assert.False(PvadOAuthScopes.IsReadOnly("pvad.unknown"));
        Assert.False(PvadOAuthScopes.IsReadOnly("PVAD.templates.read"));
        Assert.False(PvadOAuthScopes.IsReadOnly("pxa.templates.read"));
    }
}
