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

public sealed class AccountProfileControllerTests
{
    [PostgreSqlFact]
    public async Task Get_profile_returns_the_callers_own_identity_and_active_organization_role()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@profile.test", "Profile Owner");
        await LoginAsync(client, "owner@profile.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/profile");
        Assert.Equal("Profile Owner", profile.GetProperty("displayName").GetString());
        Assert.Equal("owner@profile.test", profile.GetProperty("email").GetString());
        Assert.Equal("en", profile.GetProperty("locale").GetString());
        Assert.Contains(profile.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == PxaRoles.OrganizationAdministrator);
    }

    [PostgreSqlFact]
    public async Task Get_profile_requires_authentication()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/api/pxa/v1/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Update_display_name_persists_validates_length_and_is_audited()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@displayname.test", "Original Name");
        await LoginAsync(client, "owner@displayname.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var tooShort = CreateCsrfRequest(
            HttpMethod.Patch, "/api/pxa/v1/account/profile/display-name",
            await GetCsrfAsync(client), new { displayName = "A" });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(tooShort)).StatusCode);

        using var update = CreateCsrfRequest(
            HttpMethod.Patch, "/api/pxa/v1/account/profile/display-name",
            await GetCsrfAsync(client), new { displayName = "Updated Name" });
        var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", body.GetProperty("displayName").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Contains("account.profile.display-name-updated",
            await dbContext.AuditEvents.Select(value => value.Action).ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Update_locale_persists()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@locale.test", "Locale Owner");
        await LoginAsync(client, "owner@locale.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var update = CreateCsrfRequest(
            HttpMethod.Patch, "/api/pxa/v1/account/profile/locale",
            await GetCsrfAsync(client), new { locale = "de" });
        var response = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("de", body.GetProperty("locale").GetString());
    }

    [PostgreSqlFact]
    public async Task Request_email_change_is_enumeration_safe_and_the_issued_token_confirms()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@emailchange.test", "Email Owner");
        await RegisterVerifiedCompanyAsync(factory, client, "taken@emailchange.test", "Someone Else");
        await LoginAsync(client, "owner@emailchange.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var takenRequest = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/profile/email-change/request",
            await GetCsrfAsync(client), new { newEmail = "taken@emailchange.test" });
        var takenResponse = await client.SendAsync(takenRequest);
        Assert.Equal(HttpStatusCode.Accepted, takenResponse.StatusCode);

        using var freeRequest = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/profile/email-change/request",
            await GetCsrfAsync(client), new { newEmail = "new-address@emailchange.test" });
        var freeResponse = await client.SendAsync(freeRequest);
        Assert.Equal(HttpStatusCode.Accepted, freeResponse.StatusCode);
        Assert.Equal(
            await takenResponse.Content.ReadAsStringAsync(),
            await freeResponse.Content.ReadAsStringAsync());

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        var emailChangeMail = Assert.Single(messages, message => message.RecipientEmail == "new-address@emailchange.test");
        Assert.DoesNotContain(messages, message => message.RecipientEmail == "taken@emailchange.test" &&
            message.Subject.Contains("new Power Dox Automation email address", StringComparison.OrdinalIgnoreCase));
        var confirmationToken = GetToken(emailChangeMail.TextBody);

        using var confirm = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/email-change/confirm",
            await GetCsrfAsync(client), new { token = confirmationToken });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirm)).StatusCode);

        // Confirming an email change revokes active sessions, same as password reset.
        await LoginAsync(client, "new-address@emailchange.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/profile");
        Assert.Equal("new-address@emailchange.test", profile.GetProperty("email").GetString());
    }

    [PostgreSqlFact]
    public async Task Change_password_requires_current_password_and_revokes_other_sessions()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var primaryClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, primaryClient, "owner@password.test", "Password Owner");
        await LoginAsync(primaryClient, "owner@password.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var secondaryClient = CreateClient(factory);
        await LoginAsync(secondaryClient, "owner@password.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, (await secondaryClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);

        using var wrongCurrent = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/profile/password-change",
            await GetCsrfAsync(primaryClient),
            new { currentPassword = "not-the-password", newPassword = "Pxa-Customer-Password-99!" });
        Assert.Equal(HttpStatusCode.BadRequest, (await primaryClient.SendAsync(wrongCurrent)).StatusCode);

        using var change = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/account/profile/password-change",
            await GetCsrfAsync(primaryClient),
            new { currentPassword = "Pxa-Customer-Password-42!", newPassword = "Pxa-Customer-Password-99!" });
        Assert.Equal(HttpStatusCode.NoContent, (await primaryClient.SendAsync(change)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await secondaryClient.GetAsync("/api/pxa/v1/auth/me")).StatusCode);
        await LoginAsync(secondaryClient, "owner@password.test", "Pxa-Customer-Password-42!", HttpStatusCode.Unauthorized);
        await LoginAsync(secondaryClient, "owner@password.test", "Pxa-Customer-Password-99!", HttpStatusCode.OK);
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
        Assert.True((await roleManager.CreateAsync(new PxaIdentityRole
        {
            Name = PxaRoles.OrganizationAdministrator,
            IsSystemRole = true,
        })).Succeeded);
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
