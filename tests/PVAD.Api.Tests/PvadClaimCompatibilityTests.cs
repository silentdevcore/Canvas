using System.Security.Claims;
using OpenIddict.Abstractions;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadClaimCompatibilityTests
{
    [Fact]
    public void Legacy_claims_become_canonical_without_changing_values_issuers_destinations_or_identity()
    {
        var identity = new ClaimsIdentity("synthetic");
        var claim = new Claim("pxa_organization", "synthetic-org", ClaimValueTypes.String, "synthetic-issuer", "synthetic-original");
        claim.Properties[".destinations"] = "retained-metadata";
        identity.AddClaim(claim);
        identity.AddClaim(new Claim("pxa_permission", "templates.read"));
        var principal = new ClaimsPrincipal(identity);
        Assert.True(PvadClaimCompatibility.TryNormalize(principal));
        Assert.Same(identity, principal.Identity);
        var canonical = principal.FindFirst(PvadClaimTypes.Organization)!;
        Assert.Equal("synthetic-org", canonical.Value);
        Assert.Equal("synthetic-issuer", canonical.Issuer);
        Assert.Equal("synthetic-original", canonical.OriginalIssuer);
        Assert.Equal("retained-metadata", canonical.Properties[".destinations"]);
        Assert.False(PvadClaimCompatibility.HasLegacyClaims(principal));
        Assert.True(PvadClaimCompatibility.TryNormalize(principal));
    }

    [Theory]
    [InlineData("pvad_active_organization", "pxa_active_organization")]
    [InlineData("pvad_session", "pxa_session")]
    [InlineData("pvad_service_account", "pxa_service_account")]
    [InlineData("pvad_permission", "pxa_permission")]
    public async Task Conflicting_aliases_fail_before_claim_expansion_and_without_partial_mutation(string canonical, string legacy)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("pxa_organization", "unchanged"), new Claim(canonical, "first"), new Claim(legacy, "second"),
        ], "synthetic"));
        Assert.False(PvadClaimCompatibility.TryNormalize(principal));
        Assert.NotNull(principal.FindFirst("pxa_organization"));
        var result = await new PvadPermissionClaimsTransformation(null!, null!, null!).TransformAsync(principal);
        Assert.False(result.Identity?.IsAuthenticated == true);
    }

    [Fact]
    public void Multiple_singleton_values_across_identities_are_rejected_but_permission_sets_remain_supported()
    {
        var first = new ClaimsIdentity([new Claim(PvadClaimTypes.Session, "first")], "first");
        var second = new ClaimsIdentity([new Claim(PvadClaimTypes.Session, "second")], "second");
        Assert.False(PvadClaimCompatibility.TryNormalize(new ClaimsPrincipal([first, second])));
        var permissions = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("pxa_permission", "read"), new Claim("pxa_permission", "write"),
            new Claim(PvadClaimTypes.Permission, "write"), new Claim(PvadClaimTypes.Permission, "read"),
        ], "synthetic"));
        Assert.True(PvadClaimCompatibility.TryNormalize(permissions));
    }

    [Theory]
    [InlineData("pxa.templates.read")]
    [InlineData("pvad.templates.read")]
    public void Known_scope_aliases_work_in_public_and_OpenIddict_scope_claims(string scope)
    {
        var publicClaims = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", scope)], "synthetic"));
        Assert.True(PvadOAuthPolicies.HasScope(publicClaims, PvadOAuthScopes.TemplatesRead));
        var privateClaims = new ClaimsPrincipal(new ClaimsIdentity("synthetic"));
        privateClaims.SetScopes(scope);
        Assert.True(PvadOAuthPolicies.HasScope(privateClaims, PvadOAuthScopes.TemplatesRead));
        Assert.False(PvadScopeCompatibility.IsReadOnly("pvad.templates.write"));
        Assert.False(PvadScopeCompatibility.Equivalent("pxa.unknown", "pvad.unknown"));
    }
}
