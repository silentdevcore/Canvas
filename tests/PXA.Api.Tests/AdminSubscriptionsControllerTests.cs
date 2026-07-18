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

public sealed class AdminSubscriptionsControllerTests
{
    [PostgreSqlFact]
    public async Task Subscription_lifecycle_entitlements_seats_and_tenant_access_are_enforced()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedAsync(factory.Services);
        using var systemClient = CreateClient(factory);
        await LoginAsync(systemClient, "system@pxa.test", "Pxa-System-Subscription-42!");

        using var create = CreateCsrfRequest(HttpMethod.Post, "/api/pxa/v1/admin/subscriptions",
            await GetCsrfAsync(systemClient), new
            {
                organizationId = seeded.OrganizationId,
                edition = "Trial",
                accountType = "Company",
                status = "Trialing",
                billingPeriod = "None",
                deploymentMode = "Cloud",
                seatLimit = 1,
                entitlements = new object[]
                {
                    new { capability = "generator", enabled = true },
                    new { capability = "api", enabled = true, limit = 1000, unit = "operations" },
                },
            });
        var createResponse = await systemClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var subscription = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var subscriptionId = subscription.GetProperty("id").GetGuid();
        Assert.Equal("Trialing", subscription.GetProperty("status").GetString());
        Assert.Equal(2, subscription.GetProperty("entitlements").GetArrayLength());
        var trialEndsAt = subscription.GetProperty("trialEndsAt").GetDateTimeOffset();
        Assert.InRange(trialEndsAt, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));

        using var duplicate = CreateCsrfRequest(HttpMethod.Post, "/api/pxa/v1/admin/subscriptions",
            await GetCsrfAsync(systemClient), new
            {
                organizationId = seeded.OrganizationId, edition = "Free", accountType = "Company",
                status = "Active", billingPeriod = "None", deploymentMode = "Cloud",
                entitlements = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.Conflict, (await systemClient.SendAsync(duplicate)).StatusCode);

        using var updateEntitlements = CreateCsrfRequest(HttpMethod.Patch,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}",
            await GetCsrfAsync(systemClient), new
            {
                entitlements = new object[]
                {
                    new { capability = "generator", enabled = true },
                    new { capability = "api", enabled = true, limit = 2000, unit = "operations" },
                    new { capability = "preview.experimental", enabled = false, source = "TemporaryGrant" },
                },
            });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(updateEntitlements)).StatusCode);

        using var extendTrial = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/trial/extend",
            await GetCsrfAsync(systemClient), new { days = 7 });
        var extendedResponse = await systemClient.SendAsync(extendTrial);
        Assert.Equal(HttpStatusCode.OK, extendedResponse.StatusCode);
        var extended = await extendedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(extended.GetProperty("trialEndsAt").GetDateTimeOffset() > trialEndsAt);

        using var assignSeat = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/seats/{seeded.OrganizationAdminMembershipId}",
            await GetCsrfAsync(systemClient), new { });
        Assert.Equal(HttpStatusCode.NoContent, (await systemClient.SendAsync(assignSeat)).StatusCode);
        using var exceedSeats = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/seats/{seeded.SystemMembershipId}",
            await GetCsrfAsync(systemClient), new { });
        Assert.Equal(HttpStatusCode.Conflict, (await systemClient.SendAsync(exceedSeats)).StatusCode);

        var seats = await systemClient.GetFromJsonAsync<JsonElement>(
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/seats");
        Assert.Equal(2, seats.GetArrayLength());
        Assert.Single(seats.EnumerateArray(), value => value.GetProperty("assigned").GetBoolean());

        using var organizationClient = CreateClient(factory);
        await LoginAsync(organizationClient, "organization@pxa.test", "Pxa-Organization-Subscription-42!");
        var allowed = await organizationClient.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/account/entitlements/api?quantity=1999");
        Assert.True(allowed.GetProperty("allowed").GetBoolean());
        var denied = await organizationClient.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/account/entitlements/api?quantity=2001");
        Assert.False(denied.GetProperty("allowed").GetBoolean());
        Assert.Equal("PXA_ENTITLEMENT_LIMIT_EXCEEDED", denied.GetProperty("code").GetString());

        using var activate = CreateCsrfRequest(HttpMethod.Patch,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}",
            await GetCsrfAsync(systemClient), new { edition = "Premium", status = "Active", billingPeriod = "Annual" });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(activate)).StatusCode);
        var renewalEnd = DateTimeOffset.UtcNow.AddYears(1);
        using var renew = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/renew",
            await GetCsrfAsync(systemClient), new { periodEndsAt = renewalEnd });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(renew)).StatusCode);
        using var pastDue = CreateCsrfRequest(HttpMethod.Patch,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}",
            await GetCsrfAsync(systemClient), new { status = "PastDue" });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(pastDue)).StatusCode);
        using var gracePeriod = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/grace-period",
            await GetCsrfAsync(systemClient), new { endsAt = DateTimeOffset.UtcNow.AddDays(14) });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(gracePeriod)).StatusCode);
        using var scheduleCancellation = CreateCsrfRequest(HttpMethod.Post,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/cancel",
            await GetCsrfAsync(systemClient), new { effectiveAt = DateTimeOffset.UtcNow.AddDays(10) });
        Assert.Equal(HttpStatusCode.OK, (await systemClient.SendAsync(scheduleCancellation)).StatusCode);
        using var invalidTransition = CreateCsrfRequest(HttpMethod.Patch,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}",
            await GetCsrfAsync(systemClient), new { status = "Trialing" });
        Assert.Equal(HttpStatusCode.Conflict, (await systemClient.SendAsync(invalidTransition)).StatusCode);

        var tenantPage = await organizationClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/subscriptions");
        Assert.Equal(1, tenantPage.GetProperty("total").GetInt32());
        using var forbiddenUpdate = CreateCsrfRequest(HttpMethod.Patch,
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}",
            await GetCsrfAsync(organizationClient), new { status = "Active" });
        Assert.Equal(HttpStatusCode.Forbidden, (await organizationClient.SendAsync(forbiddenUpdate)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Equal(SubscriptionStatus.GracePeriod,
            (await dbContext.OrganizationSubscriptions.FindAsync(subscriptionId))!.Status);
        Assert.Equal(8, await dbContext.SubscriptionLifecycleEvents.CountAsync());
        var history = await systemClient.GetFromJsonAsync<JsonElement>(
            $"/api/pxa/v1/admin/subscriptions/{subscriptionId}/history");
        Assert.Equal(8, history.GetArrayLength());
        Assert.Contains(await dbContext.AuditEvents.ToListAsync(), value => value.Action == "subscriptions.create");
        Assert.Contains(await dbContext.AuditEvents.ToListAsync(), value => value.Action == "subscriptions.update");
        Assert.Contains(await dbContext.AuditEvents.ToListAsync(), value => value.Action == "subscriptions.seat.assign");
        Assert.Contains(await dbContext.AuditEvents.ToListAsync(), value => value.Action == "subscription.renewed");
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:PxaDatabase"] = connectionString }));
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
            Assert.True((await roleManager.CreateAsync(new PxaIdentityRole { Name = roleName, IsSystemRole = true })).Succeeded);

        var organization = new Organization { Name = "Subscription Tenant", Slug = "subscription-tenant" };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();
        var system = await CreateUser(userManager, "system@pxa.test", "System", "Pxa-System-Subscription-42!");
        var organizationAdmin = await CreateUser(userManager, "organization@pxa.test", "Organization", "Pxa-Organization-Subscription-42!");
        Assert.True((await userManager.AddToRoleAsync(system, PxaRoles.SystemAdministrator)).Succeeded);
        var systemMembership = new OrganizationMembership { OrganizationId = organization.Id, UserId = system.Id };
        var adminMembership = new OrganizationMembership { OrganizationId = organization.Id, UserId = organizationAdmin.Id };
        dbContext.OrganizationMemberships.AddRange(systemMembership, adminMembership);
        await dbContext.SaveChangesAsync();
        var roleId = await dbContext.Roles.Where(value => value.Name == PxaRoles.OrganizationAdministrator)
            .Select(value => value.Id).SingleAsync();
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = adminMembership.Id,
            RoleId = roleId,
            AssignedByUserId = system.Id,
        });
        await dbContext.SaveChangesAsync();
        return new SeededData(organization.Id, systemMembership.Id, adminMembership.Id);
    }

    private static async Task<PxaIdentityUser> CreateUser(
        UserManager<PxaIdentityUser> manager, string email, string name, string password)
    {
        var user = new PxaIdentityUser
        {
            UserName = email, Email = email, EmailConfirmed = true, DisplayName = name,
        };
        Assert.True((await manager.CreateAsync(user, password)).Succeeded);
        return user;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        using var request = CreateCsrfRequest(HttpMethod.Post, "/api/pxa/v1/auth/login",
            await GetCsrfAsync(client), new { identifier = email, password });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf")).GetProperty("token").GetString()!;

    private static HttpRequestMessage CreateCsrfRequest(
        HttpMethod method, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-PXA-CSRF", csrf);
        return request;
    }

    private sealed record SeededData(Guid OrganizationId, Guid SystemMembershipId, Guid OrganizationAdminMembershipId);
}
