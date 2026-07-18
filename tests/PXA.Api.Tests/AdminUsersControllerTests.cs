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
using PXA.WebApi.Security;
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
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymousClient.GetAsync("/api/pxa/v1/admin/roles")).StatusCode);
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

        var missingCsrf = await client.PatchAsJsonAsync(
            $"/api/pxa/v1/admin/users/{seeded.ManagedUserId}/status",
            new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);

        var authenticatedCsrf = await GetCsrfAsync(client);
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
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.SendAsync(removeLastAdministrator)).StatusCode);

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
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PxaDbContext>>();
                    services.AddDbContext<PxaDbContext>(options => options.UseNpgsql(connectionString));
                });
            });

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
