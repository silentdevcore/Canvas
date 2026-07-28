using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AdminSystemControllerTests
{
    [Fact]
    public async Task Anonymous_users_cannot_access_system_health()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/pxa/v1/admin/system/health")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/pxa/v1/admin/operator/access")).StatusCode);
    }

    [Fact]
    public void Controller_requires_system_administrator_and_disables_caching()
    {
        foreach (var controllerType in new[]
                 {
                     typeof(AdminSystemController),
                     typeof(AdminOperatorAccessController),
                 })
        {
            var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
            var responseCache = controllerType.GetCustomAttribute<ResponseCacheAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal(PxaRoles.SystemAdministrator, authorize.Roles);
            Assert.NotNull(responseCache);
            Assert.True(responseCache.NoStore);
            Assert.Equal(ResponseCacheLocation.None, responseCache.Location);
        }
    }

    [PostgreSqlFact]
    public async Task Only_system_administrators_receive_sanitized_system_health()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedAsync(factory.Services);

        using var organizationClient = CreateClient(factory);
        await LoginAsync(
            organizationClient,
            "organization-admin@pxa.test",
            "Pxa-OrgAdmin-Integration-42!");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await organizationClient.GetAsync("/api/pxa/v1/admin/system/health")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await organizationClient.GetAsync("/api/pxa/v1/admin/operator/access")).StatusCode);

        using var systemClient = CreateClient(factory);
        await LoginAsync(
            systemClient,
            "system-admin@pxa.test",
            "Pxa-SystemAdmin-Integration-42!");
        using var response = await systemClient.GetAsync("/api/pxa/v1/admin/system/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<JsonElement>(body);
        var componentKeys = health.GetProperty("components")
            .EnumerateArray()
            .Select(value => value.GetProperty("key").GetString()!)
            .ToArray();
        Assert.Equal(
            ["webapi", "database", "jobs", "ocr", "mail", "telemetry"],
            componentKeys);
        var jobs = health.GetProperty("components")
            .EnumerateArray()
            .Single(value => value.GetProperty("key").GetString() == "jobs");
        Assert.Equal("Disabled", jobs.GetProperty("status").GetString());
        Assert.Equal(1, jobs.GetProperty("pendingJobs").GetInt64());
        Assert.Equal(1, jobs.GetProperty("deadLetterJobs").GetInt64());
        Assert.True(jobs.GetProperty("oldestPendingSeconds").GetInt64() >= 0);
        Assert.DoesNotContain("system-admin@pxa.test", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("organization-admin@pxa.test", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);

        using var operatorAccess = await systemClient.GetAsync(
            "/api/pxa/v1/admin/operator/access");
        Assert.Equal(HttpStatusCode.NoContent, operatorAccess.StatusCode);
        var operatorName = Assert.Single(operatorAccess.Headers.GetValues("X-PXA-Operator"));
        Assert.Matches("^pxa-[a-f0-9]{24}$", operatorName);
        Assert.DoesNotContain("system-admin", operatorName, StringComparison.OrdinalIgnoreCase);
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
                        ["AdminSecurity:RequireExplicitSystemOperators"] = "true",
                        ["AdminSecurity:SystemOperatorEmails:0"] = "system-admin@pxa.test",
                        ["Mail:Transport"] = "Disabled",
                    }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<PxaDbContext>>();
                    services.AddDbContext<PxaDbContext>(
                        options => options.UseNpgsql(connectionString));
                });
            });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    private static async Task SeedAsync(IServiceProvider services)
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

        var organization = new Organization
        {
            Name = "System Health Test",
            Slug = "system-health-test",
        };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var systemAdministrator = await CreateUserAsync(
            userManager,
            "system-admin@pxa.test",
            "System Administrator",
            "Pxa-SystemAdmin-Integration-42!");
        Assert.True((await userManager.AddToRoleAsync(
            systemAdministrator,
            PxaRoles.SystemAdministrator)).Succeeded);
        var organizationAdministrator = await CreateUserAsync(
            userManager,
            "organization-admin@pxa.test",
            "Organization Administrator",
            "Pxa-OrgAdmin-Integration-42!");

        var systemMembership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = systemAdministrator.Id,
        };
        var organizationMembership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = organizationAdministrator.Id,
        };
        dbContext.OrganizationMemberships.AddRange(systemMembership, organizationMembership);
        dbContext.BackgroundJobs.AddRange(
            new PxaBackgroundJob
            {
                OrganizationId = organization.Id,
                CreatedByUserId = systemAdministrator.Id,
                Type = "document.import",
                PayloadJson = "{}",
                Status = PxaBackgroundJobStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            },
            new PxaBackgroundJob
            {
                OrganizationId = organization.Id,
                CreatedByUserId = systemAdministrator.Id,
                Type = "document.export",
                PayloadJson = "{}",
                Status = PxaBackgroundJobStatus.DeadLetter,
            });
        await dbContext.SaveChangesAsync();

        var roleId = await dbContext.Roles
            .Where(role => role.Name == PxaRoles.OrganizationAdministrator)
            .Select(role => role.Id)
            .SingleAsync();
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = organizationMembership.Id,
            RoleId = roleId,
            AssignedByUserId = systemAdministrator.Id,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<PxaIdentityUser> CreateUserAsync(
        UserManager<PxaIdentityUser> userManager,
        string email,
        string displayName,
        string password)
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
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login")
        {
            Content = JsonContent.Create(new { identifier, password }),
        };
        request.Headers.Add("X-PXA-CSRF", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
