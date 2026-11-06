using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadCookieTicketMinimizationTests(PvadApiTestFactory factory) : IClassFixture<PvadApiTestFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initial_tickets_keep_identity_session_roles_and_tenant_but_not_derived_claims(bool designer)
    {
        var scheme = designer ? PvadAuthenticationSchemes.DesignerCookie : IdentityConstants.ApplicationScheme;
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
        var retained = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, PvadRoles.SystemAdministrator),
            new Claim(PvadClaimTypes.Session, Guid.NewGuid().ToString()),
            new Claim(PvadClaimTypes.ActiveOrganization, Guid.NewGuid().ToString()),
            new Claim("AspNet.Identity.SecurityStamp", "synthetic-stamp"),
            new Claim("custom-preserved", "value"),
        };
        var identity = new ClaimsIdentity(retained, scheme);
        foreach (var index in Enumerable.Range(0, 1000))
        {
            identity.AddClaim(new Claim(PvadClaimTypes.Permission, $"synthetic.permission.{index}"));
            identity.AddClaim(new Claim(PvadClaimTypes.Organization, Guid.NewGuid().ToString()));
        }
        identity.AddClaim(new Claim("pvad_permissions_resolved", "true"));
        var secondary = new ClaimsIdentity([new Claim(PvadClaimTypes.Permission, "synthetic.secondary")]);
        var principal = new ClaimsPrincipal([identity, secondary]);
        var context = new DefaultHttpContext();
        var properties = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
        var signing = new CookieSigningInContext(context, new AuthenticationScheme(scheme, null, typeof(CookieAuthenticationHandler)), options, principal, properties, options.Cookie.Build(context));
        await scope.ServiceProvider.GetRequiredService<PvadCookieAuthenticationEvents>().SigningIn(signing);

        Assert.Equal(retained.Select(value => (value.Type, value.Value)), principal.Claims.Select(value => (value.Type, value.Value)));
        Assert.True(principal.Identity!.IsAuthenticated);
        var encoded = options.TicketDataFormat.Protect(new AuthenticationTicket(principal, properties, scheme));
        options.CookieManager.AppendResponseCookie(context, options.Cookie.Name!, encoded, options.Cookie.Build(context));
        var cookie = Assert.Single(context.Response.Headers.SetCookie, value => value!.StartsWith(options.Cookie.Name + "=", StringComparison.Ordinal));
        Assert.True(cookie!.Length < 4096);
        Assert.True(context.Response.Headers.SetCookie.Sum(value => value!.Length) < 4096);
        var decoded = Assert.IsType<AuthenticationTicket>(options.TicketDataFormat.Unprotect(encoded));
        Assert.Equal(retained.Select(value => (value.Type, value.Value)), decoded.Principal.Claims.Select(value => (value.Type, value.Value)));
    }
}
