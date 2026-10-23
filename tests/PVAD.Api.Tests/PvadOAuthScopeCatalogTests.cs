using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadOAuthScopeCatalogTests
{
    [Fact]
    public void Supported_scopes_add_only_known_equivalent_aliases()
    {
        var scopes = PvadScopeCompatibility.SupportedScopes;
        Assert.Equal(PvadOAuthScopes.All.Length * 2, scopes.Length);
        Assert.Equal(scopes.Length, scopes.Distinct(StringComparer.Ordinal).Count());
        foreach (var legacy in PvadOAuthScopes.All)
        {
            Assert.Contains(legacy, scopes);
            Assert.Contains(PvadScopeCompatibility.Canonical(legacy), scopes);
        }
        Assert.DoesNotContain("pvad.unknown", scopes);
    }

    [Fact]
    public void Registration_aliases_do_not_grant_write_or_unknown_scopes()
    {
        Assert.Equal(6, PvadScopeCompatibility.ReadOnlyScopes.Length);
        Assert.All(PvadScopeCompatibility.ReadOnlyScopes, scope => Assert.True(PvadScopeCompatibility.IsReadOnly(scope)));
        Assert.DoesNotContain("pvad.templates.write", PvadScopeCompatibility.ReadOnlyScopes);
        Assert.False(PvadScopeCompatibility.IsReadOnly("pvad.unknown"));
        Assert.False(PvadScopeCompatibility.IsReadOnly("PVAD.templates.read"));
    }

    [Theory]
    [InlineData("pvad.templates.read")]
    [InlineData("pxa.templates.read")]
    public void Both_generations_require_the_same_template_entitlement(string scope)
    {
        Assert.True(PvadScopeCompatibility.Contains([scope], PvadOAuthScopes.TemplatesRead));
        Assert.False(PvadScopeCompatibility.Contains(["pvad.docs.read"], PvadOAuthScopes.TemplatesRead));
    }
}
