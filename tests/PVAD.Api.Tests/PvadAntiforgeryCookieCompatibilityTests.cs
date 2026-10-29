using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadAntiforgeryCookieCompatibilityTests
{
    [Theory]
    [InlineData(false, "X-PXA-CSRF")]
    [InlineData(false, "X-PVAD-CSRF")]
    [InlineData(true, "X-PXA-CSRF")]
    [InlineData(true, "X-PVAD-CSRF")]
    public async Task Legacy_cookie_and_both_headers_require_real_token_validation_and_migrate_safely(bool secure, string header)
    {
        var name = PvadCookieNames.Antiforgery(secure);
        var legacy = PvadCookieNames.LegacyAntiforgery(secure);
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddAntiforgery(options =>
                {
                    options.Cookie.Name = name;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = secure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                    options.HeaderName = "X-PVAD-CSRF";
                });
            }).Configure(app =>
            {
                app.UseMiddleware<PvadSecurityHeaderMiddleware>();
                app.UseMiddleware<PvadAntiforgeryCookieMiddleware>();
                app.Run(async context =>
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    if (context.Request.Method == "GET")
                    {
                        await context.Response.WriteAsJsonAsync(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
                        return;
                    }
                    try { await antiforgery.ValidateRequestAsync(context); context.Response.StatusCode = 204; }
                    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; }
                });
            })).StartAsync();
        using var client = host.GetTestClient();
        if (secure) client.BaseAddress = new Uri("https://localhost");
        using var issued = await client.GetAsync("/");
        var token = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var canonicalCookie = issued.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(name + "=", StringComparison.Ordinal)).Split(';')[0];
        var legacyCookie = legacy + canonicalCookie[name.Length..];
        foreach (var valid in new[] { true, false })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/");
            request.Headers.Add("Cookie", legacyCookie);
            request.Headers.Add(header, valid ? token : "invalid-token");
            using var result = await client.SendAsync(request);
            Assert.Equal(valid ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest, result.StatusCode);
            var cookies = result.Headers.GetValues("Set-Cookie").ToArray();
            Assert.Contains(cookies, value => value.StartsWith(name + "=", StringComparison.Ordinal));
            Assert.Contains(cookies, value => value.StartsWith(legacy + "=", StringComparison.Ordinal) && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
            Assert.All(cookies, value => Assert.DoesNotContain("domain=", value, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var input in new[] { canonicalCookie + "; " + legacy + "=different", name + "=; " + legacyCookie,
            canonicalCookie + "; " + canonicalCookie, legacyCookie + "; " + legacyCookie })
        {
            using var conflict = new HttpRequestMessage(HttpMethod.Post, "/");
            conflict.Headers.Add("Cookie", input);
            conflict.Headers.Add(header, token);
            using var rejected = await client.SendAsync(conflict);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Contains("PXAAPI008", await rejected.Content.ReadAsStringAsync());
            Assert.DoesNotContain("different", await rejected.Content.ReadAsStringAsync());
            Assert.All(rejected.Headers.GetValues("Set-Cookie"), value => Assert.Contains("expires=", value, StringComparison.OrdinalIgnoreCase));
        }
        using var matching = new HttpRequestMessage(HttpMethod.Post, "/");
        matching.Headers.Add("Cookie", canonicalCookie + "; " + legacyCookie);
        matching.Headers.Add(header, token);
        using var accepted = await client.SendAsync(matching);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using var missing = await client.PostAsync("/", null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }
}
