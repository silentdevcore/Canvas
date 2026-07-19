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
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AccountClosureControllerTests
{
    [PostgreSqlFact]
    public async Task Organization_closure_can_be_requested_and_cancelled()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@closure.test", "Owner", "Closure GmbH", "closure-co");
        await LoginAsync(client, "owner@closure.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var request = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/closure/organization", await GetCsrfAsync(client), new { reason = "No longer needed" });
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closure = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", closure.GetProperty("status").GetString());
        var closureId = closure.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var organization = await dbContext.Organizations.SingleAsync(value => value.Slug == "closure-co");
            Assert.Equal("Closed", organization.Status.ToString());
        }

        using var duplicate = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/closure/organization", await GetCsrfAsync(client), new { reason = "again" });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(duplicate)).StatusCode);

        using var cancel = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/closure/{closureId}/cancel", await GetCsrfAsync(client), new { });
        var cancelResponse = await client.SendAsync(cancel);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var organization = await dbContext.Organizations.SingleAsync(value => value.Slug == "closure-co");
            Assert.Equal("Active", organization.Status.ToString());
        }

        using var recancel = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/closure/{closureId}/cancel", await GetCsrfAsync(client), new { });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(recancel)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Account_closure_revokes_sessions_and_is_scoped_to_the_requesting_user()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var primaryClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, primaryClient, "owner@selfclosure.test", "Owner", "SelfClosure GmbH", "self-closure-co");
        await LoginAsync(primaryClient, "owner@selfclosure.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var secondaryClient = CreateClient(factory);
        await LoginAsync(secondaryClient, "owner@selfclosure.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var request = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/closure/account", await GetCsrfAsync(primaryClient), new { reason = null as string });
        var response = await primaryClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await secondaryClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Closure_endpoints_require_authentication()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/account/closure")).StatusCode);
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
        Assert.True((await roleManager.CreateAsync(new PxaIdentityRole
        {
            Name = "Organization Administrator",
            IsSystemRole = true,
        })).Succeeded);
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
