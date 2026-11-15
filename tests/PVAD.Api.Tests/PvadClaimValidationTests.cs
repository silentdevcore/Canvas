using System.Security.Claims;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadClaimValidationTests
{
    [Theory]
    [InlineData("pvad_organization")]
    [InlineData("pvad_active_organization")]
    [InlineData("pvad_session")]
    [InlineData("pvad_service_account")]
    public async Task Repeated_single_valued_claims_are_rejected_before_permission_expansion(string type)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(type, "first"), new Claim(type, "second"),
        ], "synthetic"));
        Assert.False(PvadClaimValidation.HasUnambiguousContext(principal));
        var result = await new PvadPermissionClaimsTransformation(null!, null!, null!).TransformAsync(principal);
        Assert.False(result.Identity?.IsAuthenticated == true);
    }

    [Fact]
    public void Single_values_across_identities_are_checked_together_but_permission_sets_remain_supported()
    {
        var first = new ClaimsIdentity([new Claim(PvadClaimTypes.Session, "first")], "first");
        var second = new ClaimsIdentity([new Claim(PvadClaimTypes.Session, "second")], "second");
        Assert.False(PvadClaimValidation.HasUnambiguousContext(new ClaimsPrincipal([first, second])));
        var permissions = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(PvadClaimTypes.Permission, "write"), new Claim(PvadClaimTypes.Permission, "read"),
        ], "synthetic"));
        Assert.True(PvadClaimValidation.HasUnambiguousContext(permissions));
    }

    [Fact]
    public void Former_claim_types_are_not_canonical_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("pxa_organization", "org"), new Claim("pxa_permission", "templates.read"),
        ], "synthetic"));
        Assert.True(PvadClaimValidation.HasUnambiguousContext(principal));
        Assert.Null(principal.FindFirst(PvadClaimTypes.Organization));
        Assert.Null(principal.FindFirst(PvadClaimTypes.Permission));
    }
}
