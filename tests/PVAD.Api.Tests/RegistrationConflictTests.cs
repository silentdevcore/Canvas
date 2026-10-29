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
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Infrastructure;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

/// <summary>
/// Closes remaining gaps from the Tests/Acceptance-Criteria sections of
/// checklists/PVAD.Account.md that the per-phase tests did not already cover:
/// the Individual Developer registration path (every other test in this
/// suite registers as Company), duplicate organization slug conflicts, and
/// a concurrent same-email registration race.
/// </summary>
public sealed class RegistrationConflictTests
{
    [PostgreSqlFact]
    public async Task Individual_developer_registration_creates_a_single_seat_personal_workspace()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        using var register = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pvad/v1/auth/register",
            await GetCsrfAsync(client),
            new
            {
                email = "developer@individual.test",
                displayName = "Solo Developer",
                password = "Pxa-Customer-Password-42!",
                accountType = "IndividualDeveloper",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var user = await dbContext.Users.SingleAsync(value => value.Email == "developer@individual.test");
            var organization = await dbContext.Organizations.SingleAsync();
            Assert.Equal("Solo Developer's workspace", organization.Name);
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync(
                value => value.OrganizationId == organization.Id);
            Assert.Equal(1, subscription.SeatLimit);
            Assert.Equal(SubscriptionAccountType.IndividualDeveloper, subscription.AccountType);
            Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
            var membership = await dbContext.OrganizationMemberships.SingleAsync(value => value.UserId == user.Id);
            Assert.Single(await dbContext.OrganizationMembershipRoles
                .Where(value => value.OrganizationMembershipId == membership.Id)
                .ToListAsync());
        }

        await using var mailScope = factory.Services.CreateAsyncScope();
        await mailScope.ServiceProvider.GetRequiredService<PVAD.WebApi.Services.Mail.PvadMailProcessor>()
            .ProcessPendingAsync(CancellationToken.None);
        var messages = factory.Services.GetRequiredService<PVAD.WebApi.Services.Mail.DevelopmentMailTransport>().Messages;
        var verificationMail = Assert.Single(messages, message => message.RecipientEmail == "developer@individual.test");
        var token = GetToken(verificationMail.TextBody);

        using var verify = CreateCsrfRequest(
            HttpMethod.Post, "/api/pvad/v1/auth/verify-email", await GetCsrfAsync(client), new { token });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(verify)).StatusCode);

        await using (var trialScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = trialScope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync();
            Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
            Assert.InRange(subscription.TrialEndsAt!.Value,
                DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
        }

        using var login = CreateCsrfRequest(
            HttpMethod.Post, "/api/pvad/v1/auth/login", await GetCsrfAsync(client),
            new { identifier = "developer@individual.test", password = "Pxa-Customer-Password-42!" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/me");
        Assert.Single(me.GetProperty("organizations").EnumerateArray());
    }

    [PostgreSqlFact]
    public async Task Duplicate_organization_slug_is_rejected_with_a_conflict()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var firstClient = CreateClient(factory);

        using var firstRegister = CreateCsrfRequest(
            HttpMethod.Post, "/api/pvad/v1/auth/register", await GetCsrfAsync(firstClient),
            new
            {
                email = "first@slugconflict.test",
                displayName = "First Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "First Co",
                organizationSlug = "shared-slug",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await firstClient.SendAsync(firstRegister)).StatusCode);

        using var secondClient = CreateClient(factory);
        using var secondRegister = CreateCsrfRequest(
            HttpMethod.Post, "/api/pvad/v1/auth/register", await GetCsrfAsync(secondClient),
            new
            {
                email = "second@slugconflict.test",
                displayName = "Second Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "Second Co",
                organizationSlug = "shared-slug",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        var conflictResponse = await secondClient.SendAsync(secondRegister);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal(
            PvadApiProblems.OrganizationSlugUnavailable,
            (await conflictResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        Assert.Equal(1, await dbContext.Organizations.CountAsync(value => value.Slug == "shared-slug"));
    }

    [PostgreSqlFact]
    public async Task Concurrent_registration_with_the_same_email_creates_exactly_one_user()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);

        using var clientA = CreateClient(factory);
        using var clientB = CreateClient(factory);
        var csrfA = await GetCsrfAsync(clientA);
        var csrfB = await GetCsrfAsync(clientB);

        object Payload(string companyName, string slug) => new
        {
            email = "race@concurrent.test",
            displayName = "Race Owner",
            password = "Pxa-Customer-Password-42!",
            accountType = "Company",
            companyName,
            organizationSlug = slug,
            acceptTerms = true,
            acceptPrivacy = true,
        };

        using var requestA = CreateCsrfRequest(HttpMethod.Post, "/api/pvad/v1/auth/register", csrfA, Payload("Race Co A", "race-co-a"));
        using var requestB = CreateCsrfRequest(HttpMethod.Post, "/api/pvad/v1/auth/register", csrfB, Payload("Race Co B", "race-co-b"));

        var responses = await Task.WhenAll(clientA.SendAsync(requestA), clientB.SendAsync(requestB));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync(value => value.Email == "race@concurrent.test"));
        Assert.Equal(1, await dbContext.Organizations.CountAsync());
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
                services.RemoveAll<DbContextOptions<PvadDbContext>>();
                services.RemoveAll<IHostedService>();
                services.AddDbContext<PvadDbContext>(options => options.UseNpgsql(connectionString));
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
        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        await dbContext.Database.MigrateAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PvadIdentityRole>>();
        Assert.True((await roleManager.CreateAsync(new PvadIdentityRole
        {
            Name = "Organization Administrator",
            IsSystemRole = true,
        })).Succeeded);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/csrf");
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
