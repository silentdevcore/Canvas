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
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AccountMailNotificationsTests
{
    [PostgreSqlFact]
    public async Task Successful_login_enqueues_a_new_login_notification()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@newlogin.test", "Owner", "NewLogin GmbH", "new-login-co", locale: null);

        using var login = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
            new { identifier = "owner@newlogin.test", password = "Pxa-Customer-Password-42!" });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Contains(await dbContext.MailOutboxMessages.Select(value => value.TemplateKey).ToListAsync(),
            key => key == "identity.new-login");
    }

    [PostgreSqlFact]
    public async Task Repeated_failed_logins_enqueue_a_single_lockout_notification()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@lockout.test", "Owner", "Lockout GmbH", "lockout-co", locale: null);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var badLogin = CreateCsrfRequest(
                HttpMethod.Post, "/api/pxa/v1/auth/login", await GetCsrfAsync(client),
                new { identifier = "owner@lockout.test", password = "wrong-password" });
            await client.SendAsync(badLogin);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var lockoutMailCount = await dbContext.MailOutboxMessages.CountAsync(value => value.TemplateKey == "identity.lockout");
        Assert.Equal(1, lockoutMailCount);
    }

    [PostgreSqlFact]
    public async Task German_locale_registration_uses_localized_mail_templates()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@german.test", "Eigentümer", "German GmbH", "german-co", locale: "de");

        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        Assert.Contains(messages, message => message.Subject.Contains("Bestätigen", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Subject.Contains("Willkommen", StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task Newsletter_consent_is_recorded_without_affecting_registration()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        using var register = CreateCsrfRequest(
            HttpMethod.Post, "/api/pxa/v1/auth/register", await GetCsrfAsync(client),
            new
            {
                email = "owner@newsletter.test",
                displayName = "Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "Newsletter GmbH",
                organizationSlug = "newsletter-co",
                acceptTerms = true,
                acceptPrivacy = true,
                subscribeToNewsletter = true,
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var audit = await dbContext.AuditEvents.SingleAsync(value => value.Action == "account.registration.created");
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        Assert.True(details.RootElement.GetProperty("NewsletterConsent").GetBoolean());
    }

    [PostgreSqlFact]
    public async Task Password_reset_is_delivered_without_marketing_consent()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        const string email = "owner@transactional.test";
        await RegisterVerifiedCompanyAsync(
            factory,
            client,
            email,
            "Owner",
            "Transactional GmbH",
            "transactional-co",
            locale: null);

        await using (var consentScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = consentScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var user = await dbContext.Users.SingleAsync(value => value.Email == email);
            Assert.Null(user.MarketingConsentGrantedAt);
            Assert.Null(user.MarketingConsentWithdrawnAt);
        }

        using var reset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/request",
            await GetCsrfAsync(client),
            new { email });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(reset)).StatusCode);

        await using (var deliveryScope = factory.Services.CreateAsyncScope())
        {
            await deliveryScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None);
        }

        var messages = factory.Services.GetRequiredService<DevelopmentMailTransport>().Messages;
        Assert.Contains(messages, message =>
            message.RecipientEmail == email &&
            message.Subject == "Reset your Power Dox Automation password");

        await using var assertScope = factory.Services.CreateAsyncScope();
        var finalDbContext = assertScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var templates = await finalDbContext.MailOutboxMessages
            .Where(value => value.RecipientEmail == email)
            .Select(value => value.TemplateKey)
            .ToListAsync();
        Assert.Contains("identity.password-reset", templates);
        Assert.All(templates, template => Assert.StartsWith("identity.", template, StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task Trial_expiry_notifier_notifies_administrators_once_per_threshold()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@trialexpiry.test", "Owner", "TrialExpiry GmbH", "trial-expiry-co", locale: null);

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync(
                value => value.OrganizationId == dbContext.Organizations.Single(o => o.Slug == "trial-expiry-co").Id);
            subscription.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(2);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        int firstRunCount;
        await using (var runScope = factory.Services.CreateAsyncScope())
        {
            firstRunCount = await runScope.ServiceProvider.GetRequiredService<TrialExpiryNotifier>()
                .NotifyExpiringTrialsAsync(CancellationToken.None);
        }
        Assert.Equal(1, firstRunCount);

        int secondRunCount;
        await using (var rerunScope = factory.Services.CreateAsyncScope())
        {
            secondRunCount = await rerunScope.ServiceProvider.GetRequiredService<TrialExpiryNotifier>()
                .NotifyExpiringTrialsAsync(CancellationToken.None);
        }
        Assert.Equal(0, secondRunCount);

        await using var assertScope = factory.Services.CreateAsyncScope();
        var finalDbContext = assertScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var trialMailCount = await finalDbContext.MailOutboxMessages.CountAsync(value => value.TemplateKey == "identity.trial-expiring");
        Assert.Equal(1, trialMailCount);
    }

    private static async Task RegisterVerifiedCompanyAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string email,
        string displayName,
        string companyName,
        string slug,
        string? locale)
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
                locale,
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

        await mailScope.ServiceProvider.GetRequiredService<PxaMailProcessor>().ProcessPendingAsync(CancellationToken.None);
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
