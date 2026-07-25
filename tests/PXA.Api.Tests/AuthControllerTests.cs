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

public sealed class AuthControllerTests
{
    [PostgreSqlFact]
    public async Task Cookie_authentication_round_trips_against_persistent_identity()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        using var factory = CreateFactory(postgres.GetConnectionString());

        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        await SeedIdentityAsync(factory.Services, userId, organizationId);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        var missingCsrfResponse = await client.PostAsJsonAsync(
            "/api/pxa/v1/auth/login",
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrfResponse.StatusCode);

        var csrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrf.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var invalidLogin = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            token!,
            new { identifier = "admin@pxa.test", password = "wrong-password" });
        var invalidResponse = await client.SendAsync(invalidLogin);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        using var login = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            token!,
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var sessionCookie = Assert.Single(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-PXA.Session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var user = loginBody.GetProperty("user");
        Assert.Equal(userId, user.GetProperty("id").GetGuid());
        Assert.Contains(
            user.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == PxaRoles.OrganizationAdministrator);
        var permissions = user.GetProperty("permissions").EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains(PxaAccountPermissions.OrganizationManage, permissions);
        Assert.Contains(PxaAccountPermissions.MembersInvite, permissions);
        Assert.Contains(PxaAccountPermissions.ServiceAccountsManage, permissions);
        Assert.Equal(permissions.Order(StringComparer.Ordinal), permissions);
        Assert.Equal(organizationId, user.GetProperty("activeOrganizationId").GetGuid());

        var currentResponse = await client.GetAsync("/api/pxa/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        var currentUser = await currentResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            permissions,
            currentUser.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()).ToArray());
        await using (var sessionScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = sessionScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var session = await dbContext.UserSessions.SingleAsync(value => value.UserId == userId);
            Assert.Equal(organizationId, session.OrganizationId);
            Assert.Null(session.RevokedAt);
            Assert.Contains("security.login", await dbContext.AuditEvents.Select(value => value.Action).ToListAsync());
        }

        var authenticatedCsrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        var authenticatedCsrf = await authenticatedCsrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var authenticatedToken = authenticatedCsrf.GetProperty("token").GetString();

        using var logout = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/logout",
            authenticatedToken!);
        var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        await using (var sessionScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = sessionScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var session = await dbContext.UserSessions.SingleAsync(value => value.UserId == userId);
            Assert.NotNull(session.RevokedAt);
            Assert.Equal("logout", session.RevocationReason);
        }

        var signedOutResponse = await client.GetAsync("/api/pxa/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOutResponse.StatusCode);

        var signedOutCsrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        var signedOutCsrf = await signedOutCsrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var secondLogin = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            signedOutCsrf.GetProperty("token").GetString()!,
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        var secondLoginResponse = await client.SendAsync(secondLogin);
        Assert.Equal(HttpStatusCode.OK, secondLoginResponse.StatusCode);

        var emailChangeToken = await IssueEmailChangeAsync(
            factory.Services,
            userId,
            organizationId,
            "new-admin@pxa.test");
        var emailChangeCsrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        var emailChangeCsrf = await emailChangeCsrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var confirmEmail = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/email-change/confirm",
            emailChangeCsrf.GetProperty("token").GetString()!,
            new { token = emailChangeToken });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/auth/me")).StatusCode);

        var thirdCsrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        var thirdCsrf = await thirdCsrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var thirdLogin = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            thirdCsrf.GetProperty("token").GetString()!,
            new { identifier = "new-admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(thirdLogin)).StatusCode);

        await ExpireSessionsAsync(factory.Services, userId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/auth/me")).StatusCode);

        var fourthCsrfResponse = await client.GetAsync("/api/pxa/v1/auth/csrf");
        var fourthCsrf = await fourthCsrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var fourthLogin = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            fourthCsrf.GetProperty("token").GetString()!,
            new { identifier = "new-admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(fourthLogin)).StatusCode);

        await RevokeSessionsAsync(factory.Services, userId);
        var revokedSessionResponse = await client.GetAsync("/api/pxa/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSessionResponse.StatusCode);
    }

    [PostgreSqlFact]
    public async Task Correct_credentials_receive_explicit_disabled_and_suspended_statuses()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        await SeedIdentityAsync(factory.Services, userId, organizationId);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            (await dbContext.Users.SingleAsync()).IsActive = false;
            await dbContext.SaveChangesAsync();
        }
        using var wrongDisabled = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
            new { identifier = "admin@pxa.test", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrongDisabled)).StatusCode);

        using var disabled = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        var disabledResponse = await client.SendAsync(disabled);
        Assert.Equal(HttpStatusCode.Forbidden, disabledResponse.StatusCode);
        Assert.Equal(PxaApiProblems.AccountDisabled,
            (await disabledResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            (await dbContext.Users.SingleAsync()).IsActive = true;
            (await dbContext.Organizations.SingleAsync()).Status = OrganizationStatus.Suspended;
            await dbContext.SaveChangesAsync();
        }
        using var suspended = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        var suspendedResponse = await client.SendAsync(suspended);
        Assert.Equal(HttpStatusCode.Forbidden, suspendedResponse.StatusCode);
        Assert.Equal(PxaApiProblems.OrganizationSuspended,
            (await suspendedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [PostgreSqlFact]
    public async Task Repeated_invalid_passwords_lock_the_account_and_create_security_audit_events()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var userId = Guid.NewGuid();
        await SeedIdentityAsync(factory.Services, userId, Guid.NewGuid());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
            using var invalidLogin = CreateCsrfRequest(
                HttpMethod.Post,
                "/api/pxa/v1/auth/login",
                csrf.GetProperty("token").GetString()!,
                new { identifier = "admin@pxa.test", password = $"Invalid-password-{attempt}!" });
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalidLogin)).StatusCode);
        }

        var lockedCsrf = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        using var lockedLogin = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            lockedCsrf.GetProperty("token").GetString()!,
            new { identifier = "admin@pxa.test", password = "Pxa-Integration-Password-42!" });
        Assert.Equal(HttpStatusCode.Locked, (await client.SendAsync(lockedLogin)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var user = await dbContext.Users.SingleAsync(value => value.Id == userId);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.Contains("security.login.lockout",
            await dbContext.AuditEvents.Select(value => value.Action).ToListAsync());
        Assert.Contains("security.login.locked",
            await dbContext.AuditEvents.Select(value => value.Action).ToListAsync());
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

    private static async Task SeedIdentityAsync(
        IServiceProvider services,
        Guid userId,
        Guid organizationId)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        await dbContext.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PxaIdentityRole>>();
        var roleResult = await roleManager.CreateAsync(new PxaIdentityRole
        {
            Name = PxaRoles.OrganizationAdministrator,
            Description = "Manages users and settings for one organization.",
            IsSystemRole = true,
        });
        Assert.True(roleResult.Succeeded, Describe(roleResult));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PxaIdentityUser>>();
        var identityUser = new PxaIdentityUser
        {
            Id = userId,
            UserName = "admin@pxa.test",
            Email = "admin@pxa.test",
            EmailConfirmed = true,
            DisplayName = "PXA Test Administrator",
        };
        var userResult = await userManager.CreateAsync(identityUser, "Pxa-Integration-Password-42!");
        Assert.True(userResult.Succeeded, Describe(userResult));

        var assignmentResult = await userManager.AddToRoleAsync(
            identityUser,
            PxaRoles.OrganizationAdministrator);
        Assert.True(assignmentResult.Succeeded, Describe(assignmentResult));

        dbContext.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "PXA Integration Test",
            Slug = "pxa-integration-test",
        });
        dbContext.OrganizationMemberships.Add(new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = userId,
        });
        await dbContext.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateCsrfRequest(
        HttpMethod method,
        string path,
        string csrfToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-PXA-CSRF", csrfToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return request;
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    private static async Task RevokeSessionsAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PxaIdentityUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var result = await userManager.UpdateSecurityStampAsync(user!);
        Assert.True(result.Succeeded, Describe(result));
    }

    private static async Task ExpireSessionsAsync(IServiceProvider services, Guid userId)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var sessions = await dbContext.UserSessions
            .Where(value => value.UserId == userId && value.RevokedAt == null)
            .ToListAsync();
        Assert.NotEmpty(sessions);
        foreach (var session in sessions)
            session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> IssueEmailChangeAsync(
        IServiceProvider services,
        Guid userId,
        Guid organizationId,
        string email)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var user = await dbContext.Users.SingleAsync(value => value.Id == userId);
        user.PendingEmail = email;
        var tokens = scope.ServiceProvider.GetRequiredService<IdentityActionTokenService>();
        var issued = await tokens.IssueAsync(
            userId,
            organizationId,
            email,
            IdentityActionTokenService.EmailChangePurpose,
            new { },
            TimeSpan.FromHours(1),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        return issued.RawToken;
    }

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
