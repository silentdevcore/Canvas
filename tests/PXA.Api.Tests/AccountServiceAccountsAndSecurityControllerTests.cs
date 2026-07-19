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

public sealed class AccountServiceAccountsAndSecurityControllerTests
{
    [PostgreSqlFact]
    public async Task Service_account_and_key_lifecycle_reveals_the_secret_exactly_once()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@keys.test", "Owner", "Keys GmbH", "keys-co");
        await LoginAsync(client, "owner@keys.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var create = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/service-accounts", await GetCsrfAsync(client), new { name = "CI pipeline" });
        var createResponse = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var account = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = account.GetProperty("id").GetGuid();

        using var createKey = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/service-accounts/{accountId}/keys",
            await GetCsrfAsync(client), new { name = "primary" });
        var keyResponse = await client.SendAsync(createKey);
        Assert.Equal(HttpStatusCode.Created, keyResponse.StatusCode);
        var key = await keyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secret = key.GetProperty("secret").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secret));
        var keyId = key.GetProperty("id").GetGuid();

        var accounts = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/service-accounts");
        var listedKey = accounts[0].GetProperty("keys")[0];
        Assert.False(listedKey.TryGetProperty("secret", out _));

        using var revokeKey = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/service-accounts/{accountId}/keys/{keyId}/revoke",
            await GetCsrfAsync(client), new { });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revokeKey)).StatusCode);

        using var revokeAccount = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/service-accounts/{accountId}/revoke",
            await GetCsrfAsync(client), new { });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revokeAccount)).StatusCode);

        var afterRevoke = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/service-accounts");
        Assert.False(afterRevoke[0].GetProperty("isActive").GetBoolean());
    }

    [PostgreSqlFact]
    public async Task Service_accounts_are_scoped_to_the_callers_own_organization()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);

        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@svcscope.test", "Owner", "Scope GmbH", "svc-scope-co");
        await LoginAsync(ownerClient, "owner@svcscope.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);
        using var create = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/service-accounts", await GetCsrfAsync(ownerClient), new { name = "Shared tooling" });
        var account = await (await ownerClient.SendAsync(create)).Content.ReadFromJsonAsync<JsonElement>();
        var accountId = account.GetProperty("id").GetGuid();

        using var otherClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, otherClient, "owner@svcother.test", "Other Owner", "Other Scope GmbH", "svc-other-co");
        await LoginAsync(otherClient, "owner@svcother.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var otherAccounts = await otherClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/service-accounts");
        Assert.Equal(0, otherAccounts.GetArrayLength());

        using var revokeForeign = CreateCsrfRequest(
            HttpMethod.Post, $"/api/pxa/v1/account/service-accounts/{accountId}/revoke",
            await GetCsrfAsync(otherClient), new { });
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.SendAsync(revokeForeign)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Sessions_list_marks_current_session_and_revoke_all_preserves_it()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var primaryClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, primaryClient, "owner@sessions.test", "Owner", "Sessions GmbH", "sessions-co");
        await LoginAsync(primaryClient, "owner@sessions.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var secondaryClient = CreateClient(factory);
        await LoginAsync(secondaryClient, "owner@sessions.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var sessions = await primaryClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/security/sessions");
        Assert.Equal(2, sessions.GetArrayLength());
        Assert.Single(sessions.EnumerateArray(), session => session.GetProperty("isCurrent").GetBoolean());

        using var revokeAll = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/security/sessions/revoke-all", await GetCsrfAsync(primaryClient), new { });
        var revokeAllResponse = await primaryClient.SendAsync(revokeAll);
        Assert.Equal(HttpStatusCode.OK, revokeAllResponse.StatusCode);
        var revoked = await revokeAllResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, revoked.GetProperty("revokedCount").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await primaryClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await secondaryClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Sessions_endpoints_require_authentication()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/account/security/sessions")).StatusCode);
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
