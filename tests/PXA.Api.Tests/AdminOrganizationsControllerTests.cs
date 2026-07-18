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

public sealed class AdminOrganizationsControllerTests
{
    [PostgreSqlFact]
    public async Task Organization_administration_is_tenant_scoped_switchable_and_audited()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedAsync(factory.Services);

        using var systemClient = CreateClient(factory);
        await LoginAsync(systemClient, "system-admin@pxa.test", "Pxa-Admin-Integration-42!");

        var organizations = await systemClient.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/admin/organizations?page=1&pageSize=20");
        Assert.Equal(2, organizations.GetProperty("total").GetInt32());

        using var createOrganization = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/admin/organizations",
            await GetCsrfAsync(systemClient),
            new { name = "Tenant Three", slug = "tenant-three" });
        var createOrganizationResponse = await systemClient.SendAsync(createOrganization);
        Assert.Equal(HttpStatusCode.Created, createOrganizationResponse.StatusCode);
        var createdOrganizationId = (await createOrganizationResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var csrf = await GetCsrfAsync(systemClient);
        using var switchOrganization = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            csrf,
            new { organizationId = seeded.SecondOrganizationId });
        var switchResponse = await systemClient.SendAsync(switchOrganization);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);
        var switched = await switchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            seeded.SecondOrganizationId,
            switched.GetProperty("user").GetProperty("activeOrganizationId").GetGuid());

        var tenantUsers = await systemClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/users");
        Assert.Equal(1, tenantUsers.GetProperty("total").GetInt32());
        Assert.Equal(
            seeded.SecondOrganizationUserId,
            tenantUsers.GetProperty("items")[0].GetProperty("id").GetGuid());

        csrf = await GetCsrfAsync(systemClient);
        using var addMember = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/organizations/{seeded.SecondOrganizationId}/members",
            csrf,
            new { email = "candidate@pxa.test", roles = new[] { PxaRoles.Viewer } });
        var addMemberResponse = await systemClient.SendAsync(addMember);
        Assert.Equal(HttpStatusCode.OK, addMemberResponse.StatusCode);
        var member = await addMemberResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(seeded.CandidateUserId, member.GetProperty("userId").GetGuid());

        using var updateOrganization = CreateCsrfRequest(
            HttpMethod.Patch,
            $"/api/pxa/v1/admin/organizations/{seeded.SecondOrganizationId}",
            csrf,
            new { name = "Tenant Two Updated" });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(updateOrganization)).StatusCode);

        using var removeMember = CreateCsrfRequest(
            HttpMethod.Delete,
            $"/api/pxa/v1/admin/organizations/{seeded.SecondOrganizationId}/members/{seeded.CandidateUserId}",
            csrf,
            new { });
        Assert.Equal(HttpStatusCode.NoContent, (await systemClient.SendAsync(removeMember)).StatusCode);

        using var removeLastAdministrator = CreateCsrfRequest(
            HttpMethod.Delete,
            $"/api/pxa/v1/admin/organizations/{seeded.FirstOrganizationId}/members/{seeded.OrganizationAdministratorUserId}",
            csrf,
            new { });
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await systemClient.SendAsync(removeLastAdministrator)).StatusCode);

        using var organizationClient = CreateClient(factory);
        await LoginAsync(organizationClient, "organization-admin@pxa.test", "Pxa-OrgAdmin-Integration-42!");
        var tenantOrganizations = await organizationClient.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/admin/organizations");
        Assert.Equal(1, tenantOrganizations.GetProperty("total").GetInt32());
        Assert.Equal(
            seeded.FirstOrganizationId,
            tenantOrganizations.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await organizationClient.GetAsync(
                $"/api/pxa/v1/admin/organizations/{seeded.SecondOrganizationId}")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var auditActions = await dbContext.AuditEvents
            .Where(value => value.OrganizationId == seeded.SecondOrganizationId)
            .Select(value => value.Action)
            .ToListAsync();
        Assert.Contains("memberships.add", auditActions);
        Assert.Contains("memberships.remove", auditActions);
        Assert.Contains("organizations.update", auditActions);
        Assert.True(await dbContext.AuditEvents.AnyAsync(value =>
            value.OrganizationId == createdOrganizationId && value.Action == "organizations.create"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:PxaDatabase"] = connectionString,
                    }));
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

        var firstOrganization = new Organization { Name = "Tenant One", Slug = "tenant-one" };
        var secondOrganization = new Organization { Name = "Tenant Two", Slug = "tenant-two" };
        dbContext.Organizations.AddRange(firstOrganization, secondOrganization);
        await dbContext.SaveChangesAsync();

        var systemAdmin = await CreateUserAsync(
            userManager,
            "system-admin@pxa.test",
            "System Administrator",
            "Pxa-Admin-Integration-42!");
        Assert.True((await userManager.AddToRoleAsync(systemAdmin, PxaRoles.SystemAdministrator)).Succeeded);
        var organizationAdmin = await CreateUserAsync(
            userManager,
            "organization-admin@pxa.test",
            "Organization Administrator",
            "Pxa-OrgAdmin-Integration-42!");
        var secondOrganizationUser = await CreateUserAsync(
            userManager,
            "second-user@pxa.test",
            "Second Tenant User");
        var candidate = await CreateUserAsync(userManager, "candidate@pxa.test", "Candidate User");

        var systemMembership = new OrganizationMembership
        {
            OrganizationId = firstOrganization.Id,
            UserId = systemAdmin.Id,
        };
        var organizationAdminMembership = new OrganizationMembership
        {
            OrganizationId = firstOrganization.Id,
            UserId = organizationAdmin.Id,
        };
        var secondMembership = new OrganizationMembership
        {
            OrganizationId = secondOrganization.Id,
            UserId = secondOrganizationUser.Id,
        };
        dbContext.OrganizationMemberships.AddRange(
            systemMembership,
            organizationAdminMembership,
            secondMembership);
        await dbContext.SaveChangesAsync();

        var administratorRoleId = await dbContext.Roles
            .Where(role => role.Name == PxaRoles.OrganizationAdministrator)
            .Select(role => role.Id)
            .SingleAsync();
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = organizationAdminMembership.Id,
            RoleId = administratorRoleId,
            AssignedByUserId = systemAdmin.Id,
        });
        await dbContext.SaveChangesAsync();

        return new SeededData(
            firstOrganization.Id,
            secondOrganization.Id,
            organizationAdmin.Id,
            secondOrganizationUser.Id,
            candidate.Id);
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

    private static async Task LoginAsync(HttpClient client, string identifier, string password)
    {
        using var request = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            await GetCsrfAsync(client),
            new { identifier, password });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
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
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-PXA-CSRF", csrfToken);
        return request;
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));

    private sealed record SeededData(
        Guid FirstOrganizationId,
        Guid SecondOrganizationId,
        Guid OrganizationAdministratorUserId,
        Guid SecondOrganizationUserId,
        Guid CandidateUserId);
}
