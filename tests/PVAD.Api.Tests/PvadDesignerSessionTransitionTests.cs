using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadDesignerSessionTransitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_persistent_legacy_sessions_renew_validate_csrf_and_cannot_be_replayed_after_signout(bool canonicalCookieName)
    {
        using var host = await StartAsync();
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        var (legacyCookie, sessionId) = await IssueLegacyAsync(host.Services, canonicalCookieName);
        using var first = await SendAsync(client, "/me", legacyCookie);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        var renewed = Cookie(first, PvadCookieNames.Designer(true));
        Assert.All(first.Headers.GetValues("Set-Cookie"), value =>
        {
            Assert.Contains("httponly", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(first.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(PvadCookieNames.LegacyDesigner(true) + "=", StringComparison.Ordinal) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        using var csrf = await SendAsync(client, "/csrf", renewed);
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var cookies = renewed + "; " + Cookie(csrf, PvadCookieNames.Antiforgery(true));
        using var rejected = await SendAsync(client, "/logout", cookies, "invalid-token");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var stillActive = await SendAsync(client, "/me", renewed);
        Assert.Equal(HttpStatusCode.NoContent, stillActive.StatusCode);
        using var signedOut = await SendAsync(client, canonicalCookieName ? "/legacy-logout" : "/logout", cookies, token);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        foreach (var name in new[] { PvadCookieNames.Designer(true), PvadCookieNames.LegacyDesigner(true) })
            Assert.Contains(signedOut.Headers.GetValues("Set-Cookie"), value =>
                value.StartsWith(name + "=", StringComparison.Ordinal) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        using var replay = await SendAsync(client, "/me", legacyCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        Assert.NotNull((await db.UserSessions.SingleAsync(value => value.Id == sessionId)).RevokedAt);
        Assert.Single(await db.UserSessions.ToListAsync());
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("session-expired")]
    [InlineData("ticket-expired")]
    [InlineData("disabled")]
    [InlineData("unverified")]
    [InlineData("locked")]
    [InlineData("stamp-changed")]
    [InlineData("membership-removed")]
    [InlineData("organization-mismatch")]
    public async Task Invalid_persistent_sessions_are_rejected_before_migration_renewal(string state)
    {
        using var host = await StartAsync();
        using var client = host.GetTestClient();
        var (cookie, sessionId) = await IssueLegacyAsync(host.Services, false, state == "ticket-expired");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var user = await db.Users.SingleAsync();
            var session = await db.UserSessions.SingleAsync(value => value.Id == sessionId);
            if (state == "revoked") session.RevokedAt = DateTimeOffset.UtcNow;
            if (state == "session-expired") session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            if (state == "disabled") user.IsActive = false;
            if (state == "unverified") user.EmailConfirmed = false;
            if (state == "locked") { user.LockoutEnabled = true; user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1); }
            if (state == "stamp-changed") user.SecurityStamp = Guid.NewGuid().ToString();
            if (state == "membership-removed") db.OrganizationMemberships.Remove(await db.OrganizationMemberships.SingleAsync());
            if (state == "organization-mismatch") session.OrganizationId = Guid.NewGuid();
            await db.SaveChangesAsync();
        }
        using var result = await SendAsync(client, "/me", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.DoesNotContain(result.Headers.TryGetValues("Set-Cookie", out var values) ? values : [], value =>
            value.StartsWith(PvadCookieNames.Designer(true) + "=", StringComparison.Ordinal) && !value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Fresh_signins_write_only_current_tickets_and_the_account_cookie_cannot_open_the_designer()
    {
        using var host = await StartAsync();
        using var client = host.GetTestClient();
        var (legacyCookie, _) = await IssueLegacyAsync(host.Services, false);
        using var migrated = await SendAsync(client, "/me", legacyCookie);
        var currentCookie = Cookie(migrated, PvadCookieNames.Designer(true));
        using var signedIn = await SendAsync(client, "/signin", currentCookie);
        var issued = Cookie(signedIn, PvadCookieNames.Designer(true));
        var provider = host.Services.GetRequiredService<IDataProtectionProvider>();
        var raw = issued[(issued.IndexOf('=') + 1)..];
        var format = PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.DesignerCookie);
        Assert.NotNull(format.Unprotect(Uri.UnescapeDataString(raw)));
        Assert.Null(PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.LegacyDesignerCookie).Unprotect(Uri.UnescapeDataString(raw)));
        using var aliasSignIn = await SendAsync(client, "/legacy-signin", issued);
        var aliasCookie = Cookie(aliasSignIn, PvadCookieNames.Designer(true));
        var aliasRaw = Uri.UnescapeDataString(aliasCookie[(aliasCookie.IndexOf('=') + 1)..]);
        Assert.NotNull(format.Unprotect(aliasRaw));
        Assert.Null(PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.LegacyDesignerCookie).Unprotect(aliasRaw));
        var accountCookie = PvadCookieNames.Session(true) + issued[issued.IndexOf('=')..];
        using var wrongHost = await SendAsync(client, "/me", accountCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongHost.StatusCode);
    }

    private static async Task<IHost> StartAsync()
    {
        var database = Guid.NewGuid().ToString();
        return await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddIdentityCore<PvadIdentityUser>().AddRoles<PvadIdentityRole>().AddEntityFrameworkStores<PvadDbContext>();
            services.AddOptions<PvadAdminSecurityOptions>();
            services.AddSingleton<PvadSystemOperatorAccess>();
            services.AddScoped<PvadSessionService>();
            services.AddScoped<PvadCookieAuthenticationEvents>();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = PvadCookieNames.Antiforgery(true);
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.HeaderName = "X-PVAD-CSRF";
            });
            services.AddAuthentication(PvadAuthenticationSchemes.DesignerCookie)
                .AddCookie(PvadAuthenticationSchemes.DesignerCookie, options =>
                {
                    options.Cookie.Name = PvadCookieNames.Designer(true);
                    options.Cookie.Path = "/";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CookieManager = new PvadMigratingCookieManager(PvadCookieNames.LegacyDesigner(true));
                    options.EventsType = typeof(PvadCookieAuthenticationEvents);
                }).AddPvadAuthenticationCompatibility();
        }).Configure(app =>
        {
            app.UseAuthentication();
            app.Run(async context =>
            {
                if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                if (context.Request.Path == "/csrf")
                {
                    await context.Response.WriteAsJsonAsync(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
                    return;
                }
                var path = context.Request.Path;
                if (path == "/logout" || path == "/legacy-logout")
                {
                    try { await antiforgery.ValidateRequestAsync(context); }
                    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; return; }
                    await context.RequestServices.GetRequiredService<PvadSessionService>().RevokeCurrentAsync(context.User, null, "logout", default);
                    await context.SignOutAsync(path == "/legacy-logout"
                        ? PvadAuthenticationSchemes.LegacyDesignerCookie : PvadAuthenticationSchemes.DesignerCookie);
                }
                if (context.Request.Path == "/signin")
                    await context.SignInAsync(PvadAuthenticationSchemes.DesignerCookie, context.User);
                if (context.Request.Path == "/legacy-signin")
                    await context.SignInAsync(PvadAuthenticationSchemes.LegacyDesignerCookie, context.User);
                context.Response.StatusCode = 204;
            });
        })).StartAsync();
    }

    // Synthetic issuance models a prior server after login/handoff, never a production route.
    private static async Task<(string Cookie, Guid SessionId)> IssueLegacyAsync(IServiceProvider services, bool canonicalCookieName, bool expired = false)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PvadIdentityUser>>();
        var user = new PvadIdentityUser { UserName = "synthetic@session.test", Email = "synthetic@session.test", DisplayName = "Synthetic session", EmailConfirmed = true, IsActive = true };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        var organization = new Organization { Name = "Synthetic workspace", Slug = "synthetic-workspace" };
        db.Organizations.Add(organization);
        db.OrganizationMemberships.Add(new OrganizationMembership { OrganizationId = organization.Id, UserId = user.Id, Status = OrganizationMembershipStatus.Active });
        await db.SaveChangesAsync();
        var session = await scope.ServiceProvider.GetRequiredService<PvadSessionService>().CreateAsync(user.Id, organization.Id,
            DateTimeOffset.UtcNow.AddHours(1), new DefaultHttpContext(), default);
        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<PvadIdentityUser>>().CreateAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaims([
            new Claim(PvadClaimTypes.Session, session.Id.ToString()),
            new Claim(PvadClaimTypes.ActiveOrganization, organization.Id.ToString()),
        ]);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresUtc = expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1),
        }, PvadAuthenticationSchemes.LegacyDesignerCookie);
        var encoded = PvadAuthenticationSchemeTransitionTests.Format(services.GetRequiredService<IDataProtectionProvider>(), PvadAuthenticationSchemes.LegacyDesignerCookie).Protect(ticket);
        return ((canonicalCookieName ? PvadCookieNames.Designer(true) : PvadCookieNames.LegacyDesigner(true)) + "=" + encoded, session.Id);
    }

    private static string Cookie(HttpResponseMessage response, string name) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(name + "=", StringComparison.Ordinal) && !value.Contains("expires=", StringComparison.OrdinalIgnoreCase)).Split(';')[0];

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string cookies, string? csrf = null)
    {
        using var request = new HttpRequestMessage(csrf is null ? HttpMethod.Get : HttpMethod.Post, path);
        request.Headers.Add("Cookie", cookies);
        if (csrf is not null) request.Headers.Add("X-PVAD-CSRF", csrf);
        return await client.SendAsync(request);
    }
}
