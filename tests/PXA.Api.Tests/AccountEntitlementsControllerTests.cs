using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

/// <summary>
/// Proves the "Account roles cannot grant products not enabled by subscription
/// entitlements" acceptance criterion in checklists/PXA.Account.md: role-based
/// authorization ([Authorize(Policy = PxaAccountPermissions.X)]) and entitlement
/// evaluation (PxaEntitlementService, driven purely by OrganizationSubscription/
/// SubscriptionEntitlement rows) are two entirely separate code paths that never
/// cross-reference each other. AccountEntitlementsController.Evaluate itself has
/// no PxaAccountPermissions policy at all - only [Authorize] - so these tests
/// exercise the org's subscription state, not the caller's role.
/// </summary>
public sealed class AccountEntitlementsControllerTests
{
    [PostgreSqlFact]
    public async Task A_fully_privileged_owner_is_allowed_while_the_trial_is_healthy_but_denied_once_it_expires()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@entitlements.test", "Owner");
        await LoginAsync(client, "owner@entitlements.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var allowed = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/entitlements/generator");
        Assert.True(allowed.GetProperty("allowed").GetBoolean());
        Assert.Equal("PXA_ENTITLEMENT_ALLOWED", allowed.GetProperty("code").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync();
            subscription.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();
        }

        // Same fully-privileged owner, same role, same session - only the
        // subscription's own state changed. A maximal role does not override it.
        var denied = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/entitlements/generator");
        Assert.False(denied.GetProperty("allowed").GetBoolean());
        Assert.Equal("PXA_TRIAL_EXPIRED", denied.GetProperty("code").GetString());
    }

    [PostgreSqlFact]
    public async Task A_viewer_role_member_still_gets_a_true_entitlement_result_with_a_healthy_trial()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@viewerentitlements.test", "Owner");
        await LoginAsync(ownerClient, "owner@viewerentitlements.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var invite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new { email = "viewer@viewerentitlements.test", displayName = "Viewer Teammate", roles = new[] { PxaRoles.Viewer } });
        Assert.Equal(HttpStatusCode.Accepted, (await ownerClient.SendAsync(invite)).StatusCode);

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var invitationMail = Assert.Single(messages, message => message.RecipientEmail == "viewer@viewerentitlements.test");
        var token = GetToken(invitationMail.TextBody);

        using var viewerClient = CreateClient(factory);
        using var accept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(viewerClient), new { token, password = "Pxa-Viewer-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await viewerClient.SendAsync(accept)).StatusCode);
        await LoginAsync(viewerClient, "viewer@viewerentitlements.test", "Pxa-Viewer-Password-42!", HttpStatusCode.OK);

        // The Viewer role grants none of the PxaAccountPermissions.* policies used
        // to gate other controllers, and AccountEntitlementsController requires no
        // policy at all beyond [Authorize] - the entitlement outcome is decided
        // purely by the organization's subscription/entitlement rows.
        var response = await viewerClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/entitlements/generator");
        Assert.True(response.GetProperty("allowed").GetBoolean());
        Assert.Equal("PXA_ENTITLEMENT_ALLOWED", response.GetProperty("code").GetString());
    }

    private static async Task RegisterVerifiedCompanyAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string email,
        string displayName)
    {
        var slug = email.Split('@')[0].Replace('.', '-');
        using var register = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            new
            {
                email,
                displayName,
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = $"{displayName} GmbH",
                organizationSlug = slug,
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        await using var mailScope = factory.Services.CreateAsyncScope();
        await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
            .ProcessPendingAsync(CancellationToken.None);
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var verificationMail = Assert.Single(messages, message => message.RecipientEmail == email);
        var token = GetToken(verificationMail.TextBody);

        using var verify = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/verify-email",
            await GetCsrfAsync(client),
            new { token });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(verify)).StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PxaDatabase"] = connectionString,
                    ["Mail:Enabled"] = "true",
                    ["Mail:Transport"] = "Development",
                    ["Mail:AccountBaseUrl"] = "https://account.pxa.test",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PxaDbContext>>();
                services.RemoveAll<IHostedService>();
                services.AddDbContext<PxaDbContext>(options => options.UseNpgsql(connectionString));
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        await dbContext.Database.MigrateAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PxaIdentityRole>>();
        foreach (var roleName in new[]
                 {
                     PxaRoles.OrganizationAdministrator,
                     PxaRoles.Viewer,
                 })
        {
            Assert.True((await roleManager.CreateAsync(new PxaIdentityRole
            {
                Name = roleName,
                IsSystemRole = true,
            })).Succeeded);
        }
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        HttpStatusCode expected)
    {
        using var login = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            await GetCsrfAsync(client),
            new { identifier = email, password });
        var response = await client.SendAsync(login);
        Assert.Equal(expected, response.StatusCode);
        return response;
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage CreateCsrfRequest(
        HttpMethod method,
        string path,
        string token,
        object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-PXA-CSRF", token);
        return request;
    }

    private static string GetToken(string body)
    {
        const string marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return Uri.UnescapeDataString(body[(start + marker.Length)..].Trim());
    }
}
