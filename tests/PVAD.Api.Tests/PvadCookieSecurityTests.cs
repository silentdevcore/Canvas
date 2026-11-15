using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadCookieSecurityTests
{
    [Fact]
    public void Designer_does_not_read_account_cookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Host-PVAD.Session=account-ticket";
        Assert.Null(new PvadStrictCookieManager().GetRequestCookie(context, PvadCookieNames.Designer(true)));
    }

    [Theory]
    [InlineData("PVAD.Session.Development=same; PVAD.Session.Development=same")]
    [InlineData("PVAD.Session.Development=first; PVAD.Session.Development=second")]
    [InlineData("PVAD.Session.DevelopmentC1=first; PVAD.Session.DevelopmentC1=second")]
    public void Duplicate_session_cookie_names_are_rejected(string cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookie;
        Assert.Null(new PvadStrictCookieManager().GetRequestCookie(context, "PVAD.Session.Development"));
    }

    [Fact]
    public void Former_session_cookie_names_are_ignored()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Host-PXA.Session=retained; __Host-PXA.Designer.Session=retained";
        var manager = new PvadStrictCookieManager();
        Assert.Null(manager.GetRequestCookie(context, PvadCookieNames.Session(true)));
        Assert.Null(manager.GetRequestCookie(context, PvadCookieNames.Designer(true)));
    }

    [Fact]
    public void Single_canonical_cookie_is_read()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Host-PVAD.Session=ticket; unrelated=value";
        Assert.Equal("ticket", new PvadStrictCookieManager().GetRequestCookie(context, PvadCookieNames.Session(true)));
    }
}

public sealed class PvadCookieConfigurationTests(PvadApiTestFactory factory)
    : IClassFixture<PvadApiTestFactory>
{
    [Fact]
    public void Actual_cookie_options_remain_host_only_and_account_tickets_do_not_authenticate_the_designer()
    {
        var services = factory.Services;
        var secure = !services.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        var account = options.Get(IdentityConstants.ApplicationScheme);
        var designer = options.Get(PvadAuthenticationSchemes.DesignerCookie);
        Assert.Equal(PvadCookieNames.Session(secure), account.Cookie.Name);
        Assert.Equal(PvadCookieNames.Designer(secure), designer.Cookie.Name);
        foreach (var value in new[] { account, designer })
        {
            Assert.Null(value.Cookie.Domain);
            Assert.Equal("/", value.Cookie.Path);
            Assert.True(value.Cookie.HttpOnly);
            Assert.Equal(SameSiteMode.Strict, value.Cookie.SameSite);
            Assert.IsType<PvadStrictCookieManager>(value.CookieManager);
            Assert.Equal(typeof(PvadCookieAuthenticationEvents), value.EventsType);
        }
        var provider = services.GetRequiredService<IDataProtectionProvider>();
        var accountFormat = new TicketDataFormat(provider.CreateProtector(
            "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware",
            IdentityConstants.ApplicationScheme, "v2"));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "synthetic")], "synthetic")), IdentityConstants.ApplicationScheme);
        var protectedTicket = accountFormat.Protect(ticket);
        Assert.NotNull(account.TicketDataFormat.Unprotect(protectedTicket));
        Assert.Null(designer.TicketDataFormat.Unprotect(protectedTicket));
    }

    [Fact]
    public async Task Only_canonical_authentication_schemes_are_registered()
    {
        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        foreach (var current in new[]
        {
            PvadAuthenticationSchemes.Combined, PvadAuthenticationSchemes.ApiKey,
            PvadAuthenticationSchemes.McpDelegation, PvadAuthenticationSchemes.DesignerCookie,
        })
            Assert.NotNull(await schemes.GetSchemeAsync(current));
        Assert.DoesNotContain(await schemes.GetAllSchemesAsync(),
            scheme => scheme.Name.StartsWith("PXA.", StringComparison.Ordinal));
        var defaults = factory.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(PvadAuthenticationSchemes.Combined, defaults.DefaultAuthenticateScheme);
        Assert.Equal(IdentityConstants.ApplicationScheme, defaults.DefaultSignInScheme);
    }
}
