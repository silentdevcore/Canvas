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
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AccountRegistrationControllerTests
{
    [PostgreSqlFact]
    public async Task Company_registration_creates_verified_trial_workspace_and_customer_session()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        using var register = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            new
            {
                email = "owner@customer.test",
                displayName = "Customer Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "Customer Documents GmbH",
                organizationSlug = "customer-documents",
                country = "DE",
                locale = "de",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        var registrationResponse = await client.SendAsync(register);
        Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);
        Assert.DoesNotContain("userId", await registrationResponse.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        using var duplicate = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            new
            {
                email = "owner@customer.test",
                displayName = "Customer Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "Different Name",
                organizationSlug = "different-name",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(duplicate)).StatusCode);

        Guid userId;
        Guid organizationId;
        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var user = await dbContext.Users.SingleAsync();
            userId = user.Id;
            Assert.False(user.EmailConfirmed);
            Assert.Equal("de", user.Locale);
            Assert.Equal("DE", user.Country);
            var organization = await dbContext.Organizations.SingleAsync();
            organizationId = organization.Id;
            Assert.Equal("customer-documents", organization.Slug);
            var membership = await dbContext.OrganizationMemberships.SingleAsync();
            Assert.Equal(userId, membership.UserId);
            Assert.Equal(OrganizationMembershipStatus.Active, membership.Status);
            Assert.Single(await dbContext.OrganizationMembershipRoles.ToListAsync());
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync();
            Assert.Equal(SubscriptionEdition.Trial, subscription.Edition);
            Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
            Assert.Equal(SubscriptionAccountType.Company, subscription.AccountType);
            Assert.InRange(subscription.TrialEndsAt!.Value,
                DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
            Assert.Equal(8, await dbContext.SubscriptionEntitlements.CountAsync(value =>
                value.SubscriptionId == subscription.Id && value.Enabled));
            Assert.Single(await dbContext.SubscriptionSeatAssignments.ToListAsync());
            Assert.Contains("subscription.trial.started",
                await dbContext.SubscriptionLifecycleEvents.Select(value => value.Action).ToListAsync());
            Assert.Contains("account.registration.created",
                await dbContext.AuditEvents.Select(value => value.Action).ToListAsync());
            var outbox = await dbContext.MailOutboxMessages.SingleAsync();
            Assert.Equal("identity.registration-verification", outbox.TemplateKey);
            Assert.DoesNotContain("token=", outbox.ProtectedPayload, StringComparison.OrdinalIgnoreCase);
        }

        using (var unverifiedLoginResponse = await LoginAsync(
            client, "owner@customer.test", "Pxa-Customer-Password-42!", HttpStatusCode.Forbidden))
        {
            var problem = await unverifiedLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(PxaApiProblems.VerificationRequired, problem.GetProperty("code").GetString());
        }

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None);
        }
        var mail = Assert.Single(factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages);
        var verificationToken = GetToken(mail.TextBody);
        using var verify = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/verify-email",
            await GetCsrfAsync(client),
            new { token = verificationToken });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(verify)).StatusCode);

        using var reuse = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/verify-email",
            await GetCsrfAsync(client),
            new { token = verificationToken });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(reuse)).StatusCode);

        await LoginAsync(client, "owner@customer.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);
        var current = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/me");
        Assert.Equal(userId, current.GetProperty("id").GetGuid());
        Assert.Equal(organizationId, current.GetProperty("activeOrganizationId").GetGuid());
        Assert.Contains(current.GetProperty("roles").EnumerateArray(),
            role => role.GetString() == PxaRoles.OrganizationAdministrator);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Contains("account.registration.verified",
            await finalDbContext.AuditEvents.Select(value => value.Action).ToListAsync());
        // registration-verification + welcome + new-login (the final login above).
        Assert.Equal(3, await finalDbContext.MailOutboxMessages.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Resend_verification_is_enumeration_safe_and_reissues_a_usable_token()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        using var register = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            new
            {
                email = "resend@customer.test",
                displayName = "Resend Customer",
                password = "Pxa-Customer-Password-42!",
                accountType = "IndividualDeveloper",
                acceptTerms = true,
                acceptPrivacy = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        using var unknown = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/resend-verification",
            await GetCsrfAsync(client),
            new { email = "no-such-account@customer.test" });
        var unknownResponse = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        using var known = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/resend-verification",
            await GetCsrfAsync(client),
            new { email = "resend@customer.test" });
        var knownResponse = await client.SendAsync(known);
        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(
            await unknownResponse.Content.ReadAsStringAsync(),
            await knownResponse.Content.ReadAsStringAsync());

        await using (var mailScope = factory.Services.CreateAsyncScope())
        {
            await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None);
        }
        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        Assert.Equal(2, messages.Count);
        var resendToken = GetToken(messages.Last().TextBody);

        using var verify = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/verify-email",
            await GetCsrfAsync(client),
            new { token = resendToken });
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(verify)).StatusCode);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Contains("account.verification.resent",
            await finalDbContext.AuditEvents.Select(value => value.Action).ToListAsync());
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
