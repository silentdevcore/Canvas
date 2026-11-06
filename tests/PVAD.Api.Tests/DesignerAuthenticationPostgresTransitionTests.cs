using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed partial class DesignerAuthenticationControllerTests
{
    [PostgreSqlFact]
    public Task Legacy_designer_cookie_renews_after_real_api_restart_and_cannot_replay_after_signout() =>
        VerifyPostgresTicketTransitionAsync(canonicalCookieName: false);

    [PostgreSqlFact]
    public Task Legacy_ticket_in_current_cookie_renews_after_real_api_restart_and_cannot_replay_after_signout() =>
        VerifyPostgresTicketTransitionAsync(canonicalCookieName: true);

    private static async Task VerifyPostgresTicketTransitionAsync(bool canonicalCookieName)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var keyring = Directory.CreateTempSubdirectory("pvad-postgres-auth-");
        try
        {
            string legacyCookie;
            Guid sessionId;
            SeededCustomer customer;
            using (var issuer = CreateFactory(postgres.GetConnectionString(), keyring.FullName))
            {
                customer = await SeedCustomerAsync(issuer.Services);
                using var account = CreateClient(issuer);
                await LoginAsync(account);
                var handoff = await CreateHandoffAsync(account, "/pdf/create");
                using var designer = CreateClient(issuer);
                using var exchange = await CreateExchangeRequestAsync(designer, handoff);
                using var response = await designer.SendAsync(exchange);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var raw = ReadSessionCookie(response)[(PvadCookieNames.Designer(true).Length + 1)..];
                var provider = issuer.Services.GetRequiredService<IDataProtectionProvider>();
                var current = PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.DesignerCookie);
                var ticket = Assert.IsType<AuthenticationTicket>(current.Unprotect(Uri.UnescapeDataString(raw)));
                Assert.True(PvadSessionService.TryGetSessionId(ticket.Principal, out sessionId));

                // Model the earlier server's protection purpose, not a bypass of login or persistent validation.
                var legacy = PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.LegacyDesignerCookie)
                    .Protect(new AuthenticationTicket(ticket.Principal, ticket.Properties, PvadAuthenticationSchemes.LegacyDesignerCookie));
                legacyCookie = (canonicalCookieName ? PvadCookieNames.Designer(true) : PvadCookieNames.LegacyDesigner(true)) + "=" + legacy;
                using var scope = issuer.Services.CreateScope();
                Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<PvadDbContext>().UserSessions.CountAsync());
            }
            Assert.NotEmpty(keyring.GetFiles("*.xml"));

            string renewedCookie;
            using (var restarted = CreateFactory(postgres.GetConnectionString(), keyring.FullName))
            {
                using var browser = restarted.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false,
                });
                using var migrated = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/me", legacyCookie);
                Assert.Equal(HttpStatusCode.OK, migrated.StatusCode);
                renewedCookie = ReadSessionCookie(migrated);
                var header = migrated.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(renewedCookie, StringComparison.Ordinal));
                foreach (var attribute in new[] { "httponly", "secure", "samesite=strict", "path=/" })
                    Assert.Contains(attribute, header, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("domain=", header, StringComparison.OrdinalIgnoreCase);

                var provider = restarted.Services.GetRequiredService<IDataProtectionProvider>();
                var raw = Uri.UnescapeDataString(renewedCookie[(renewedCookie.IndexOf('=') + 1)..]);
                var current = PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.DesignerCookie);
                var ticket = Assert.IsType<AuthenticationTicket>(current.Unprotect(raw));
                Assert.Null(PvadAuthenticationSchemeTransitionTests.Format(provider, PvadAuthenticationSchemes.LegacyDesignerCookie).Unprotect(raw));
                Assert.False(PvadMigratingTicketDataFormat.ConsumeLegacyMarker(ticket.Properties));
                Assert.True(PvadSessionService.TryGetSessionId(ticket.Principal, out var renewedId));
                Assert.Equal(sessionId, renewedId);

                using var stable = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/me", renewedCookie);
                Assert.Equal(HttpStatusCode.OK, stable.StatusCode);
                Assert.DoesNotContain(stable.Headers.TryGetValues("Set-Cookie", out var values) ? values : [], value =>
                    value.StartsWith(PvadCookieNames.Designer(true) + "=", StringComparison.Ordinal));
                using var wrongCookie = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/me",
                    PvadCookieNames.Session(true) + renewedCookie[renewedCookie.IndexOf('=')..]);
                Assert.Equal(HttpStatusCode.Unauthorized, wrongCookie.StatusCode);

                using var csrfResponse = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/csrf", renewedCookie);
                Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
                var csrf = (await csrfResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("token").GetString()!;
                var csrfCookie = csrfResponse.Headers.GetValues("Set-Cookie").Single(value =>
                    value.StartsWith(PvadCookieNames.Antiforgery(true) + "=", StringComparison.Ordinal)).Split(';')[0];
                using var rejected = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/logout", renewedCookie + "; " + csrfCookie, "invalid-token");
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                using var stillActive = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/me", renewedCookie);
                Assert.Equal(HttpStatusCode.OK, stillActive.StatusCode);
                using var logout = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/logout", renewedCookie + "; " + csrfCookie, csrf);
                Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
                foreach (var name in new[] { PvadCookieNames.Designer(true), PvadCookieNames.LegacyDesigner(true) })
                    Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.StartsWith(name + "=", StringComparison.Ordinal)
                        && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
            }

            using (var afterLogout = CreateFactory(postgres.GetConnectionString(), keyring.FullName))
            {
                using var browser = CreateClient(afterLogout);
                foreach (var cookie in new[] { legacyCookie, renewedCookie })
                {
                    using var replay = await SendSessionRequestAsync(browser, "/api/pvad/v1/auth/me", cookie);
                    Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
                }
                await using var scope = afterLogout.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                var session = await db.UserSessions.SingleAsync(value => value.Id == sessionId);
                Assert.Equal(customer.UserId, session.UserId);
                Assert.Equal(customer.OrganizationId, session.OrganizationId);
                Assert.NotNull(session.RevokedAt);
                Assert.Equal("logout", session.RevocationReason);
                Assert.Equal(2, await db.UserSessions.CountAsync());
                Assert.Contains(await db.AuditEvents.ToListAsync(), value => value.Action == "security.logout" && value.OrganizationId == customer.OrganizationId);
            }
        }
        finally { keyring.Delete(recursive: true); }
    }

    private static string ReadSessionCookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith(PvadCookieNames.Designer(true) + "=", StringComparison.Ordinal)
            && !value.Contains("expires=", StringComparison.OrdinalIgnoreCase)).Split(';')[0];

    private static async Task<HttpResponseMessage> SendSessionRequestAsync(HttpClient client, string path, string cookies, string? csrf = null)
    {
        using var request = new HttpRequestMessage(csrf is null ? HttpMethod.Get : HttpMethod.Post, path);
        request.Headers.Add("X-PVAD-Application", "designer");
        request.Headers.Add("Cookie", cookies);
        if (csrf is not null) request.Headers.Add("X-PVAD-CSRF", csrf);
        return await client.SendAsync(request);
    }
}
