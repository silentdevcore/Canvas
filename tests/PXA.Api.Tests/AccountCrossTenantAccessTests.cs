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
/// Closes the two remaining Acceptance Criteria/Tests gaps flagged in the Phase 12
/// section of checklists/PXA.Account.Portal-Implementation.md: a table-driven sweep
/// proving an authenticated customer session in org A is rejected against org B's
/// organization-member endpoints, and proving the same session is rejected against
/// live PXA Admin routes. Per-resource cross-tenant coverage for licenses and
/// service accounts already exists in AccountSubscriptionAndLicensesControllerTests
/// and AccountServiceAccountsAndSecurityControllerTests; this file does not repeat it.
/// </summary>
public sealed class AccountCrossTenantAccessTests
{
    [PostgreSqlFact]
    public async Task Organization_member_mutations_reject_a_userId_that_belongs_to_a_different_organization()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);

        using var clientA = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, clientA, "owner-a@crosstenant.test", "Owner A");
        await LoginAsync(clientA, "owner-a@crosstenant.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var clientB = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, clientB, "owner-b@crosstenant.test", "Owner B");
        await LoginAsync(clientB, "owner-b@crosstenant.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var ownerBUserId = await dbContext.Users.Where(value => value.Email == "owner-b@crosstenant.test")
            .Select(value => value.Id).SingleAsync();

        using var foreignRoleUpdate = CreateCsrfRequest(
            HttpMethod.Put, $"/api/pxa/v1/account/organization/members/{ownerBUserId}/roles",
            await GetCsrfAsync(clientA), new { roles = new[] { PxaRoles.Viewer } });
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.SendAsync(foreignRoleUpdate)).StatusCode);

        using var foreignRemoval = CreateCsrfRequest(
            HttpMethod.Delete, $"/api/pxa/v1/account/organization/members/{ownerBUserId}", await GetCsrfAsync(clientA), new { });
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.SendAsync(foreignRemoval)).StatusCode);

        // Owner B's own membership must be untouched by the rejected cross-tenant attempts.
        var members = await clientB.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/organization/members");
        Assert.Single(members.EnumerateArray());
    }

    /// <summary>
    /// PXA Admin already let a Company's own "Organization Administrator" (and, more
    /// narrowly, "Manager") self-administer their own tenant through the Admin API
    /// before PXA.Account existed - PxaRoles.Permissions intentionally grants both
    /// PxaPermissions.* and PxaAccountPermissions.* to that one shared role, so an
    /// owner legitimately gets 200 from GET /admin/users today. That is pre-existing
    /// behavior this pass does not change. The real, checkable boundaries are: (1) a
    /// lower-privileged organization member (Viewer, holding zero PxaPermissions.*
    /// claims) gets 403 everywhere under /admin, and (2) even the owner never reaches
    /// a true System-Administrator-only action.
    /// </summary>
    [PostgreSqlFact]
    public async Task A_non_administrator_organization_member_is_rejected_against_admin_routes()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);

        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@customerdenied.test", "Customer Owner");
        await LoginAsync(ownerClient, "owner@customerdenied.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var invite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new { email = "viewer@customerdenied.test", displayName = "Viewer Teammate", roles = new[] { PxaRoles.Viewer } });
        Assert.Equal(HttpStatusCode.Accepted, (await ownerClient.SendAsync(invite)).StatusCode);

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var invitationMail = Assert.Single(messages, message => message.RecipientEmail == "viewer@customerdenied.test");
        var token = GetToken(invitationMail.TextBody);

        using var viewerClient = CreateClient(factory);
        using var accept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(viewerClient), new { token, password = "Pxa-Viewer-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await viewerClient.SendAsync(accept)).StatusCode);
        await LoginAsync(viewerClient, "viewer@customerdenied.test", "Pxa-Viewer-Password-42!", HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/organizations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/subscriptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/licenses")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/service-accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.GetAsync("/api/pxa/v1/admin/roles")).StatusCode);

        using var userMutation = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/admin/users/bulk", await GetCsrfAsync(viewerClient),
            new { userIds = Array.Empty<Guid>(), action = "disable" });
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.SendAsync(userMutation)).StatusCode);

        using var invitationMutation = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/admin/invitations", await GetCsrfAsync(viewerClient),
            new { email = "nobody@customerdenied.test", displayName = "Nobody", roles = new[] { PxaRoles.Viewer } });
        Assert.Equal(HttpStatusCode.Forbidden, (await viewerClient.SendAsync(invitationMutation)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Even_an_organization_administrator_cannot_reach_a_system_administrator_only_action()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);

        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@systemadminboundary.test", "Boundary Owner");
        await LoginAsync(client, "owner@systemadminboundary.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var createOrganization = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/admin/organizations", await GetCsrfAsync(client),
            new { name = "Should Not Be Created", slug = "should-not-be-created" });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(createOrganization)).StatusCode);
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
