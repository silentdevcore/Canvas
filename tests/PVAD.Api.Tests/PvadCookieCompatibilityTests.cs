using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadCookieCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_chunks_are_read_and_both_generations_are_deleted_on_signout(bool designer)
    {
        var name = designer ? PvadCookieNames.Designer(true) : PvadCookieNames.Session(true);
        var legacy = designer ? PvadCookieNames.LegacyDesigner(true) : PvadCookieNames.LegacySession(true);
        var manager = new PvadMigratingCookieManager(legacy);
        var options = new CookieOptions { Path = "/", HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict };
        var issue = new DefaultHttpContext();
        var payload = new string('s', 9000);
        new ChunkingCookieManager().AppendResponseCookie(issue, legacy, payload, options);
        var cookie = string.Join("; ", issue.Response.Headers.SetCookie.Select(value => value!.Split(';')[0]));
        var request = new DefaultHttpContext();
        request.Request.Headers.Cookie = cookie;
        Assert.Equal(payload, manager.GetRequestCookie(request, name));
        Assert.True(PvadMigratingCookieManager.WasLegacyRead(request, name));
        manager.DeleteCookie(request, name, options);
        var deleted = request.Response.Headers.SetCookie.ToArray();
        Assert.Contains(deleted, value => value!.StartsWith(name + "=", StringComparison.Ordinal));
        Assert.Contains(deleted, value => value!.StartsWith(legacy + "C1=", StringComparison.Ordinal));
        Assert.All(deleted, value => Assert.Contains("expires=", value!, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("canonical", "legacy", null)]
    [InlineData("", "legacy", null)]
    [InlineData("same", "same", "same")]
    public void Canonical_presence_never_falls_back_to_a_different_legacy_ticket(string current, string legacy, string? expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"PVAD.Session.Development={current}; PXA.Session.Development={legacy}";
        Assert.Equal(expected, new PvadMigratingCookieManager("PXA.Session.Development")
            .GetRequestCookie(context, "PVAD.Session.Development"));
        Assert.False(PvadMigratingCookieManager.WasLegacyRead(context, "PVAD.Session.Development"));
    }

    [Fact]
    public void Designer_does_not_read_account_cookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "__Host-PXA.Session=account-ticket; __Host-PVAD.Session=account-ticket";
        Assert.Null(new PvadMigratingCookieManager(PvadCookieNames.LegacyDesigner(true))
            .GetRequestCookie(context, PvadCookieNames.Designer(true)));
    }

    [Theory]
    [InlineData("PVAD.Session.Development=same; PVAD.Session.Development=same; PXA.Session.Development=same")]
    [InlineData("PXA.Session.Development=first; PXA.Session.Development=second")]
    public void Duplicate_session_cookie_names_are_rejected(string cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookie;
        Assert.Null(new PvadMigratingCookieManager("PXA.Session.Development")
            .GetRequestCookie(context, "PVAD.Session.Development"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_legacy_ticket_is_validated_then_renewed_under_the_new_cookie_name(bool designer)
    {
        const string scheme = "retained-synthetic-scheme";
        var name = designer ? PvadCookieNames.Designer(true) : PvadCookieNames.Session(true);
        var legacy = designer ? PvadCookieNames.LegacyDesigner(true) : PvadCookieNames.LegacySession(true);
        var validations = 0;
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddAuthentication(scheme).AddCookie(scheme, options =>
                {
                    options.Cookie.Name = name;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.CookieManager = new PvadMigratingCookieManager(legacy);
                    options.Events.OnValidatePrincipal = context =>
                    {
                        validations++;
                        if (PvadMigratingCookieManager.WasLegacyRead(context.HttpContext, name)) context.ShouldRenew = true;
                        return Task.CompletedTask;
                    };
                });
            }).Configure(app =>
            {
                app.UseAuthentication();
                app.Run(async context =>
                {
                    if (context.Request.Path == "/legacy")
                    {
                        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme);
                        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(ClaimTypes.NameIdentifier, "synthetic-user")], scheme)),
                            new AuthenticationProperties
                            {
                                IssuedUtc = context.Request.Query.ContainsKey("expired") ? DateTimeOffset.UtcNow.AddHours(-2) : DateTimeOffset.UtcNow,
                                ExpiresUtc = context.Request.Query.ContainsKey("expired") ? DateTimeOffset.UtcNow.AddHours(-1) : DateTimeOffset.UtcNow.AddHours(1),
                            }, scheme);
                        new ChunkingCookieManager().AppendResponseCookie(context, legacy,
                            options.TicketDataFormat.Protect(ticket), options.Cookie.Build(context));
                        return;
                    }
                    context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true ? 204 : 401;
                    await Task.CompletedTask;
                });
            })).StartAsync();
        using var client = host.GetTestClient();
        using var issued = await client.GetAsync("/legacy");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", string.Join("; ", issued.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0])));
        using var result = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Equal(1, validations);
        var cookies = result.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, value => value.StartsWith(name + "=", StringComparison.Ordinal) && !value.Contains("expires=Thu, 01 Jan 1970"));
        Assert.All(cookies, value =>
        {
            Assert.Contains("secure", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase);
        });
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/protected");
        invalid.Headers.Add("Cookie", name + "=invalid; " + legacy + "=invalid");
        using var denied = await client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var expired = await client.GetAsync("/legacy?expired=true");
        using var stale = new HttpRequestMessage(HttpMethod.Get, "/protected");
        stale.Headers.Add("Cookie", string.Join("; ", expired.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0])));
        using var expiredResult = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResult.StatusCode);
        Assert.Equal(1, validations);
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
            Assert.IsType<PvadMigratingCookieManager>(value.CookieManager);
            Assert.Equal(typeof(PvadCookieAuthenticationEvents), value.EventsType);
        }
        var provider = services.GetRequiredService<IDataProtectionProvider>();
        var oldFormat = new TicketDataFormat(provider.CreateProtector(
            "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware",
            IdentityConstants.ApplicationScheme, "v2"));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "synthetic")], "synthetic")), IdentityConstants.ApplicationScheme);
        var protectedTicket = oldFormat.Protect(ticket);
        Assert.NotNull(account.TicketDataFormat.Unprotect(protectedTicket));
        Assert.Null(designer.TicketDataFormat.Unprotect(protectedTicket));
    }
}
