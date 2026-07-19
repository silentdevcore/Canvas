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
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AdminUsersControllerTests
{
    [PostgreSqlFact]
    public async Task User_administration_is_tenant_scoped_csrf_protected_and_audited()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedAsync(factory.Services);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        var anonymousCsrf = await GetCsrfAsync(client);
        using var login = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            anonymousCsrf,
            new { identifier = "system-admin@pxa.test", password = "Pxa-Admin-Integration-42!" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

        var ownSessions = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pxa/v1/admin/users/{seeded.AdministratorUserId}/sessions");
        Assert.True(ownSessions.EnumerateArray().Single().GetProperty("isCurrent").GetBoolean());

        Guid managedSessionId;
        await using (var sessionScope = factory.Services.CreateAsyncScope())
        {
            var sessionDbContext = sessionScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var managedSession = new UserSession
            {
                UserId = seeded.ManagedUserId,
                OrganizationId = seeded.OrganizationId,
                IpAddressHash = new string('a', 64),
                UserAgent = "PXA integration browser",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            };
            sessionDbContext.UserSessions.Add(managedSession);
            await sessionDbContext.SaveChangesAsync();
            managedSessionId = managedSession.Id;
        }

        var listResponse = await client.GetAsync("/api/pxa/v1/admin/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Contains(
            page.GetProperty("items").EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == seeded.ManagedUserId);
        Assert.DoesNotContain(
            page.GetProperty("items").EnumerateArray(),
            user => user.GetProperty("id").GetGuid() == seeded.ForeignUserId);

        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        var anonymousResponse = await anonymousClient.GetAsync("/api/pxa/v1/admin/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        await AssertProblemCodeAsync(anonymousResponse, PxaApiProblems.AuthenticationRequired);
        var roleCatalog = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/roles");
        Assert.Equal(4, roleCatalog.GetProperty("roles").GetArrayLength());
        var managerRole = roleCatalog.GetProperty("roles").EnumerateArray()
            .Single(role => role.GetProperty("key").GetString() == "manager");
        Assert.Equal(1, managerRole.GetProperty("memberCount").GetInt32());
        Assert.Contains(managerRole.GetProperty("permissions").EnumerateArray(),
            permission => permission.GetProperty("key").GetString() == PxaPermissions.AuditRead);
        var managerDetail = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/roles/manager");
        Assert.Equal(1, managerDetail.GetProperty("total").GetInt32());
        Assert.Equal(seeded.ManagedUserId,
            managerDetail.GetProperty("members").EnumerateArray().Single().GetProperty("userId").GetGuid());
        Assert.DoesNotContain(seeded.ForeignUserId,
            managerDetail.GetProperty("members").EnumerateArray().Select(value => value.GetProperty("userId").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/pxa/v1/admin/roles/system-administrator")).StatusCode);

        var foreignResponse = await client.GetAsync($"/api/pxa/v1/admin/users/{seeded.ForeignUserId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        await AssertProblemCodeAsync(foreignResponse, PxaApiProblems.ResourceNotFound);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/pxa/v1/admin/users/{seeded.ForeignUserId}/sessions")).StatusCode);

        var missingCsrf = await client.PatchAsJsonAsync(
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        await AssertProblemCodeAsync(missingCsrf, PxaApiProblems.InvalidCsrf);

        var authenticatedCsrf = await GetCsrfAsync(client);
        var managedSessions = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/sessions");
        Assert.Equal(managedSessionId, managedSessions.EnumerateArray().Single().GetProperty("id").GetGuid());
        using var revokeSession = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/sessions/{managedSessionId}/revoke",
            authenticatedCsrf,
            new { });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revokeSession)).StatusCode);

        await using (var additionalSessionScope = factory.Services.CreateAsyncScope())
        {
            var additionalSessionDbContext = additionalSessionScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            additionalSessionDbContext.UserSessions.Add(new UserSession
            {
                UserId = seeded.ManagedUserId,
                OrganizationId = seeded.OrganizationId,
                IpAddressHash = new string('b', 64),
                UserAgent = "PXA second integration browser",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            });
            await additionalSessionDbContext.SaveChangesAsync();
        }
        using var revokeAllSessions = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/sessions/revoke-all",
            authenticatedCsrf,
            new { });
        var revokeAllResponse = await client.SendAsync(revokeAllSessions);
        Assert.Equal(HttpStatusCode.OK, revokeAllResponse.StatusCode);
        Assert.Equal(1, (await revokeAllResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("revokedCount").GetInt32());

        using var updateProfile = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/profile",
            authenticatedCsrf,
            new { displayName = "Updated Managed User", email = "updated-member@pxa.test" });
        var profileResponse = await client.SendAsync(updateProfile);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var updatedProfile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Managed User", updatedProfile.GetProperty("displayName").GetString());
        Assert.Equal("updated-member@pxa.test", updatedProfile.GetProperty("pendingEmail").GetString());

        using var resetPassword = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/password-reset",
            authenticatedCsrf,
            new { });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(resetPassword)).StatusCode);

        using var softDelete = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/deletion",
            authenticatedCsrf,
            new { isDeleted = true });
        var deletedResponse = await client.SendAsync(softDelete);
        Assert.Equal(HttpStatusCode.OK, deletedResponse.StatusCode);
        Assert.Equal("Removed", (await deletedResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("membershipStatus").GetString());

        using var restore = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/deletion",
            authenticatedCsrf,
            new { isDeleted = false });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(restore)).StatusCode);

        using var bulkEnable = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/admin/users/bulk",
            authenticatedCsrf,
            new { userIds = new[] { seeded.ManagedUserId, seeded.AdministratorUserId }, action = "enable" });
        var bulkResponse = await client.SendAsync(bulkEnable);
        Assert.Equal(HttpStatusCode.OK, bulkResponse.StatusCode);
        Assert.Equal(2, (await bulkResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("succeededUserIds").GetArrayLength());

        var userAudit = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/audit");
        Assert.Contains(userAudit.EnumerateArray(), value => value.GetProperty("action").GetString() == "users.update");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/pxa/v1/admin/users/{seeded.ForeignUserId}/audit")).StatusCode);
        using var disable = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            authenticatedCsrf,
            new { isActive = false });
        var disableResponse = await client.SendAsync(disable);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        var disabledUser = await disableResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(disabledUser.GetProperty("isActive").GetBoolean());
        Assert.Equal("Suspended", disabledUser.GetProperty("membershipStatus").GetString());

        using var enable = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            authenticatedCsrf,
            new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(enable)).StatusCode);
        using var disableAgain = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            authenticatedCsrf,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(disableAgain)).StatusCode);

        using var assignRoles = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/roles",
            authenticatedCsrf,
            new { roles = new[] { PxaRoles.Viewer } });
        var rolesResponse = await client.SendAsync(assignRoles);
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var roleUser = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            roleUser.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == PxaRoles.Viewer);

        using var foreignRoleAssignment = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/roles/editor/members/{seeded.ForeignUserId}",
            authenticatedCsrf,
            new { });
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(foreignRoleAssignment)).StatusCode);
        using var selfRoleAssignment = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/roles/editor/members/{seeded.AdministratorUserId}",
            authenticatedCsrf,
            new { });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(selfRoleAssignment)).StatusCode);
        using var assignEditor = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/roles/editor/members/{seeded.ManagedUserId}",
            authenticatedCsrf,
            new { });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(assignEditor)).StatusCode);
        var editorDetail = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/roles/editor");
        Assert.Equal(1, editorDetail.GetProperty("total").GetInt32());
        using var revokeEditor = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/pxa/v1/admin/roles/editor/members/{seeded.ManagedUserId}");
        revokeEditor.Headers.Add("X-PXA-CSRF", authenticatedCsrf);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revokeEditor)).StatusCode);
        editorDetail = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/roles/editor");
        Assert.Equal(0, editorDetail.GetProperty("total").GetInt32());

        using var removeLastAdministrator = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/users/{seeded.AdministratorUserId}/roles",
            authenticatedCsrf,
            new { roles = new[] { PxaRoles.Viewer } });
        var removeLastAdministratorResponse = await client.SendAsync(removeLastAdministrator);
        Assert.Equal(HttpStatusCode.Conflict, removeLastAdministratorResponse.StatusCode);
        await AssertProblemCodeAsync(removeLastAdministratorResponse, PxaApiProblems.Conflict);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.False((await dbContext.Users.SingleAsync(user => user.Id == seeded.ManagedUserId)).IsActive);
        var auditActions = await dbContext.AuditEvents
            .Where(value => value.OrganizationId == seeded.OrganizationId)
            .Select(value => value.Action)
            .ToListAsync();
        Assert.Contains("users.disable", auditActions);
        Assert.Contains("roles.assign", auditActions);
        Assert.Contains("roles.member.assign", auditActions);
        Assert.Contains("roles.member.revoke", auditActions);
        Assert.Contains("sessions.revoke", auditActions);
        Assert.Contains("sessions.revoke-all", auditActions);
        Assert.Contains("users.enable", auditActions);
        Assert.Contains("users.update", auditActions);
        Assert.Contains("users.password-reset.request", auditActions);
        Assert.Contains("users.delete", auditActions);
        Assert.Contains("users.restore", auditActions);
        Assert.Contains("users.bulk.enable", auditActions);
        Assert.NotNull((await dbContext.UserSessions.SingleAsync(value => value.Id == managedSessionId)).RevokedAt);
        Assert.Equal("updated-member@pxa.test", (await dbContext.Users.SingleAsync(value => value.Id == seeded.ManagedUserId)).PendingEmail);
        Assert.Contains(await dbContext.MailOutboxMessages.ToListAsync(), value =>
            value.RecipientUserId == seeded.ManagedUserId && value.TemplateKey == "identity.email-verification");
        Assert.Contains(await dbContext.MailOutboxMessages.ToListAsync(), value =>
            value.RecipientUserId == seeded.ManagedUserId && value.TemplateKey == "identity.password-reset");
    }

    [PostgreSqlFact]
    public async Task Admin_permissions_reject_unauthorized_roles_and_suspended_users()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedAsync(factory.Services);

        using var anonymousClient = CreateClient(factory);
        var anonymousResponse = await anonymousClient.GetAsync("/api/pxa/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        await AssertProblemCodeAsync(anonymousResponse, PxaApiProblems.AuthenticationRequired);

        using var managerClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(
            managerClient,
            "member@pxa.test",
            "Pxa-Member-Integration-42!"));
        Assert.Equal(HttpStatusCode.OK,
            (await managerClient.GetAsync("/api/pxa/v1/admin/users")).StatusCode);
        var managerCsrf = await GetCsrfAsync(managerClient);
        using var managerMutation = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/admin/users/bulk",
            managerCsrf,
            new { userIds = new[] { seeded.ManagedUserId }, action = "disable" });
        var managerMutationResponse = await managerClient.SendAsync(managerMutation);
        Assert.Equal(HttpStatusCode.Forbidden, managerMutationResponse.StatusCode);
        await AssertProblemCodeAsync(managerMutationResponse, PxaApiProblems.PermissionDenied);

        using var administratorClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(
            administratorClient,
            "system-admin@pxa.test",
            "Pxa-Admin-Integration-42!"));
        var administratorCsrf = await GetCsrfAsync(administratorClient);

        await AssignRoleAsync(administratorClient, administratorCsrf, seeded.ManagedUserId, PxaRoles.Editor);
        using var editorClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(
            editorClient,
            "member@pxa.test",
            "Pxa-Member-Integration-42!"));
        var editorResponse = await editorClient.GetAsync("/api/pxa/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, editorResponse.StatusCode);
        await AssertProblemCodeAsync(editorResponse, PxaApiProblems.PermissionDenied);

        await AssignRoleAsync(administratorClient, administratorCsrf, seeded.ManagedUserId, PxaRoles.Viewer);
        using var viewerClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(
            viewerClient,
            "member@pxa.test",
            "Pxa-Member-Integration-42!"));
        var viewerResponse = await viewerClient.GetAsync("/api/pxa/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);
        await AssertProblemCodeAsync(viewerResponse, PxaApiProblems.PermissionDenied);

        using var disableUser = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            administratorCsrf,
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, (await administratorClient.SendAsync(disableUser)).StatusCode);

        using var suspendedClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(
            suspendedClient,
            "member@pxa.test",
            "Pxa-Member-Integration-42!"));
        var suspendedResponse = await suspendedClient.GetAsync("/api/pxa/v1/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, suspendedResponse.StatusCode);
        await AssertProblemCodeAsync(suspendedResponse, PxaApiProblems.AuthenticationRequired);

        using var unapprovedOperatorClient = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(
            unapprovedOperatorClient,
            "unapproved-operator@pxa.test",
            "Pxa-Unapproved-Operator-42!"));
        var unapprovedUser = await unapprovedOperatorClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/me");
        Assert.DoesNotContain(
            unapprovedUser.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == PxaRoles.SystemAdministrator);
        var unapprovedAdminResponse = await unapprovedOperatorClient.GetAsync("/api/pxa/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, unapprovedAdminResponse.StatusCode);
        await AssertProblemCodeAsync(unapprovedAdminResponse, PxaApiProblems.PermissionDenied);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:PxaDatabase"] = connectionString,
                        ["AdminSecurity:RequireExplicitSystemOperators"] = "true",
                        ["AdminSecurity:SystemOperatorEmails:0"] = "system-admin@pxa.test",
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PxaDbContext>>();
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

    private static async Task<HttpStatusCode> LoginAsync(
        HttpClient client,
        string identifier,
        string password)
    {
        var csrf = await GetCsrfAsync(client);
        using var login = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            csrf,
            new { identifier, password });
        return (await client.SendAsync(login)).StatusCode;
    }

    private static async Task AssignRoleAsync(
        HttpClient client,
        string csrf,
        Guid userId,
        string role)
    {
        using var request = CreateCsrfRequest(
            HttpMethod.Put,
            $"/api/pxa/v1/admin/users/{userId}/roles",
            csrf,
            new { roles = new[] { role } });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task AssertProblemCodeAsync(HttpResponseMessage response, string expectedCode)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    private static async Task<SeededData> SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        await dbContext.Database.MigrateAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PxaIdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PxaIdentityUser>>();

        foreach (var roleName in PxaRoles.Permissions.Keys)
        {
            var result = await roleManager.CreateAsync(new PxaIdentityRole
            {
                Name = roleName,
                IsSystemRole = true,
            });
            Assert.True(result.Succeeded, Describe(result));
        }

        var organization = new Organization { Name = "Tenant One", Slug = "tenant-one" };
        var foreignOrganization = new Organization { Name = "Tenant Two", Slug = "tenant-two" };
        dbContext.Organizations.AddRange(organization, foreignOrganization);
        await dbContext.SaveChangesAsync();

        var administrator = await CreateUserAsync(
            userManager,
            "system-admin@pxa.test",
            "System Administrator",
            "Pxa-Admin-Integration-42!");
        Assert.True((await userManager.AddToRoleAsync(administrator, PxaRoles.SystemAdministrator)).Succeeded);
        var unapprovedOperator = await CreateUserAsync(
            userManager,
            "unapproved-operator@pxa.test",
            "Unapproved Operator",
            "Pxa-Unapproved-Operator-42!");
        Assert.True((await userManager.AddToRoleAsync(unapprovedOperator, PxaRoles.SystemAdministrator)).Succeeded);
        var managedUser = await CreateUserAsync(userManager, "member@pxa.test", "Managed User");
        var foreignUser = await CreateUserAsync(userManager, "foreign@pxa.test", "Foreign User");

        var adminMembership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = administrator.Id,
        };
        var managedMembership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = managedUser.Id,
        };
        var foreignMembership = new OrganizationMembership
        {
            OrganizationId = foreignOrganization.Id,
            UserId = foreignUser.Id,
        };
        dbContext.OrganizationMemberships.AddRange(adminMembership, managedMembership, foreignMembership);
        await dbContext.SaveChangesAsync();

        var roleIds = await dbContext.Roles
            .Where(role => role.Name == PxaRoles.Manager || role.Name == PxaRoles.OrganizationAdministrator)
            .ToDictionaryAsync(role => role.Name!, role => role.Id);
        dbContext.OrganizationMembershipRoles.AddRange(
            new OrganizationMembershipRole
            {
                OrganizationMembershipId = adminMembership.Id,
                RoleId = roleIds[PxaRoles.OrganizationAdministrator],
                AssignedByUserId = administrator.Id,
            },
            new OrganizationMembershipRole
            {
                OrganizationMembershipId = managedMembership.Id,
                RoleId = roleIds[PxaRoles.Manager],
                AssignedByUserId = administrator.Id,
            },
            new OrganizationMembershipRole
            {
                OrganizationMembershipId = foreignMembership.Id,
                RoleId = roleIds[PxaRoles.Manager],
                AssignedByUserId = administrator.Id,
            });
        await dbContext.SaveChangesAsync();

        return new SeededData(organization.Id, administrator.Id, managedUser.Id, foreignUser.Id);
    }

    private static async Task<PxaIdentityUser> CreateUserAsync(
        UserManager<PxaIdentityUser> userManager,
        string email,
        string displayName,
        string password = "Pxa-Member-Integration-42!")
    {
        var user = new PxaIdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, Describe(result));
        return user;
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage CreateCsrfRequest(
        HttpMethod method,
        string path,
        string csrfToken,
        object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-PXA-CSRF", csrfToken);
        return request;
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    private sealed record SeededData(
        Guid OrganizationId,
        Guid AdministratorUserId,
        Guid ManagedUserId,
        Guid ForeignUserId);
}
