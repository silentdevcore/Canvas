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
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Organizations;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AccountOrganizationControllerTests
{
    [PostgreSqlFact]
    public async Task Get_and_update_organization_profile_validates_name_length()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@orgprofile.test", "Owner", "Orgprofile GmbH", "orgprofile");
        await LoginAsync(client, "owner@orgprofile.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/organization");
        Assert.Equal("Orgprofile GmbH", profile.GetProperty("name").GetString());
        Assert.Equal("orgprofile", profile.GetProperty("slug").GetString());

        using var tooShort = CreateCsrfRequest(
            HttpMethod.Patch, "/api/pxa/v1/account/organization", await GetCsrfAsync(client), new { name = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(tooShort)).StatusCode);

        using var update = CreateCsrfRequest(
            HttpMethod.Patch, "/api/pxa/v1/account/organization", await GetCsrfAsync(client), new { name = "Renamed GmbH" });
        var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed GmbH", body.GetProperty("name").GetString());
    }

    [PostgreSqlFact]
    public async Task Organization_endpoints_require_authentication()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/account/organization")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/account/organization/members")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Invite_member_creates_an_invited_membership_that_accepts_and_appears_active_with_roles()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@invite.test", "Owner", "Invite GmbH", "invite-co");
        await LoginAsync(ownerClient, "owner@invite.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var invite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new { email = "teammate@invite.test", displayName = "Teammate", roles = new[] { PxaRoles.Manager } });
        var inviteResponse = await ownerClient.SendAsync(invite);
        Assert.Equal(HttpStatusCode.Accepted, inviteResponse.StatusCode);
        var invited = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invited", invited.GetProperty("membershipStatus").GetString());

        // Inviting the same address again is rejected while the account already exists.
        using var duplicateInvite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new { email = "teammate@invite.test", displayName = "Teammate", roles = new[] { PxaRoles.Manager } });
        Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.SendAsync(duplicateInvite)).StatusCode);

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var invitationMail = Assert.Single(messages, message => message.RecipientEmail == "teammate@invite.test");
        var token = GetToken(invitationMail.TextBody);

        using var teammateClient = CreateClient(factory);
        using var accept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(teammateClient),
            new { token, password = "Pxa-Teammate-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await teammateClient.SendAsync(accept)).StatusCode);

        var members = await ownerClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/organization/members");
        var teammate = members.EnumerateArray().Single(member => member.GetProperty("email").GetString() == "teammate@invite.test");
        Assert.Equal("Active", teammate.GetProperty("membershipStatus").GetString());
        Assert.Contains(teammate.GetProperty("roles").EnumerateArray(), role => role.GetString() == PxaRoles.Manager);
    }

    [PostgreSqlFact]
    public async Task Existing_user_must_sign_in_to_accept_without_creating_another_workspace_or_trial()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var ownerClient = CreateClient(factory);
        using var existingClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(
            factory, ownerClient, "owner@existing-invite.test", "Owner", "Inviting GmbH", "inviting-co");
        await RegisterVerifiedCompanyAsync(
            factory, existingClient, "existing@existing-invite.test", "Existing", "Existing GmbH", "existing-co");
        await LoginAsync(ownerClient, "owner@existing-invite.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var invite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new
            {
                email = "existing@existing-invite.test",
                displayName = "Existing",
                roles = new[] { PxaRoles.Editor },
            });
        Assert.Equal(HttpStatusCode.Accepted, (await ownerClient.SendAsync(invite)).StatusCode);

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None);
        }
        var invitationMail = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages.Last(message =>
            message.RecipientEmail == "existing@existing-invite.test" &&
            message.TextBody.Contains("/accept-invitation?", StringComparison.Ordinal));
        var token = GetToken(invitationMail.TextBody);

        using var anonymousClient = CreateClient(factory);
        using var anonymousAccept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(anonymousClient), new { token });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.SendAsync(anonymousAccept)).StatusCode);

        await LoginAsync(
            existingClient, "existing@existing-invite.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);
        using var authenticatedAccept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(existingClient), new { token });
        Assert.Equal(HttpStatusCode.NoContent, (await existingClient.SendAsync(authenticatedAccept)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var existingUserId = await dbContext.Users
            .Where(user => user.Email == "existing@existing-invite.test")
            .Select(user => user.Id)
            .SingleAsync();
        Assert.Equal(2, await dbContext.OrganizationMemberships.CountAsync(
            membership => membership.UserId == existingUserId &&
                          membership.Status == OrganizationMembershipStatus.Active));
        Assert.Equal(2, await dbContext.Organizations.CountAsync());
        Assert.Equal(2, await dbContext.OrganizationSubscriptions.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Update_member_roles_protects_the_last_active_administrator()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@roles.test", "Owner", "Roles GmbH", "roles-co");
        await LoginAsync(client, "owner@roles.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var ownerUserId = await dbContext.Users.Select(u => u.Id).SingleAsync();

        using var demoteSelf = CreateCsrfRequest(
            HttpMethod.Put, $"/api/pxa/v1/account/organization/members/{ownerUserId}/roles",
            await GetCsrfAsync(client), new { roles = new[] { PxaRoles.Editor } });
        var demoteResponse = await client.SendAsync(demoteSelf);
        Assert.Equal(HttpStatusCode.Conflict, demoteResponse.StatusCode);
        Assert.Equal(
            "PXAAPI013",
            (await demoteResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var promoteSelf = CreateCsrfRequest(
            HttpMethod.Put, $"/api/pxa/v1/account/organization/members/{ownerUserId}/roles",
            await GetCsrfAsync(client), new { roles = new[] { PxaRoles.OrganizationAdministrator, PxaRoles.Manager } });
        var response = await client.SendAsync(promoteSelf);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("roles").GetArrayLength());
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Remove_member_blocks_self_removal_but_allows_removing_another_active_administrator()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@remove.test", "Owner", "Remove GmbH", "remove-co");
        await LoginAsync(ownerClient, "owner@remove.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var invite = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/organization/members",
            await GetCsrfAsync(ownerClient),
            new { email = "co-owner@remove.test", displayName = "Co Owner", roles = new[] { PxaRoles.OrganizationAdministrator } });
        var inviteResponse = await ownerClient.SendAsync(invite);
        Assert.Equal(HttpStatusCode.Accepted, inviteResponse.StatusCode);

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var invitationMail = Assert.Single(messages, message => message.RecipientEmail == "co-owner@remove.test");
        var token = GetToken(invitationMail.TextBody);
        using var coOwnerClient = CreateClient(factory);
        using var accept = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/accept-invitation",
            await GetCsrfAsync(coOwnerClient), new { token, password = "Pxa-CoOwner-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await coOwnerClient.SendAsync(accept)).StatusCode);
        await LoginAsync(coOwnerClient, "co-owner@remove.test", "Pxa-CoOwner-Password-42!", HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var ownerUserId = await dbContext.Users
            .Where(u => u.Email == "owner@remove.test")
            .Select(u => u.Id)
            .SingleAsync();
        var coOwnerUserId = await dbContext.Users
            .Where(u => u.Email == "co-owner@remove.test")
            .Select(u => u.Id)
            .SingleAsync();

        using var selfRemoval = CreateCsrfRequest(
            HttpMethod.Delete, $"/api/pxa/v1/account/organization/members/{ownerUserId}",
            await GetCsrfAsync(ownerClient), new { });
        Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.SendAsync(selfRemoval)).StatusCode);

        using var removeCoOwner = CreateCsrfRequest(
            HttpMethod.Delete, $"/api/pxa/v1/account/organization/members/{coOwnerUserId}",
            await GetCsrfAsync(ownerClient), new { });
        Assert.Equal(HttpStatusCode.NoContent, (await ownerClient.SendAsync(removeCoOwner)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await coOwnerClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
    }

    private static async Task RegisterVerifiedCompanyAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string email,
        string displayName,
        string companyName,
        string slug)
    {
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
                companyName,
                organizationSlug = slug,
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        await using var mailScope = factory.Services.CreateAsyncScope();
        await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var verificationMail = Assert.Single(messages, message => message.RecipientEmail == email);
        var token = GetToken(verificationMail.TextBody);

        using var verify = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/verify-email", await GetCsrfAsync(client), new { token });
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
        foreach (var roleName in OrganizationMembershipService.OrganizationRoles)
        {
            Assert.True((await roleManager.CreateAsync(new PxaIdentityRole
            {
                Name = roleName,
                IsSystemRole = true,
            })).Succeeded);
        }
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string email, string password, HttpStatusCode expected)
    {
        using var login = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
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

    private static HttpRequestMessage CreateCsrfRequest(HttpMethod method, string path, string token, object body)
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
