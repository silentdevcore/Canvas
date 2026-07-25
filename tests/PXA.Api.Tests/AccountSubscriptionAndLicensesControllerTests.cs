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
using PXA.WebApi.Services.Licensing;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class AccountSubscriptionAndLicensesControllerTests
{
    [PostgreSqlFact]
    public async Task Subscription_endpoints_expose_the_callers_own_trial_and_seat()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, client, "owner@subscription.test", "Owner", "Subscription GmbH", "subscription-co");
        await LoginAsync(client, "owner@subscription.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        var subscription = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/subscription");
        Assert.Equal("Trial", subscription.GetProperty("edition").GetString());
        Assert.Equal("Trialing", subscription.GetProperty("status").GetString());
        Assert.Equal(1, subscription.GetProperty("assignedSeats").GetInt32());
        Assert.True(subscription.GetProperty("entitlements").GetArrayLength() > 0);

        var seats = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/subscription/seats");
        Assert.Equal(1, seats.GetArrayLength());
        Assert.True(seats[0].GetProperty("assigned").GetBoolean());

        var history = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/subscription/history");
        Assert.Contains(history.EnumerateArray(), entry => entry.GetProperty("action").GetString() == "subscription.trial.started");

        var usage = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/subscription/usage");
        Assert.Equal(0, usage.GetProperty("totalQuantity").GetInt64());
    }

    [PostgreSqlFact]
    public async Task Subscription_endpoints_require_authentication()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pxa/v1/account/subscription")).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Licenses_are_scoped_to_the_callers_own_organization_and_validate_and_download()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var ownerClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, ownerClient, "owner@license.test", "Owner", "License GmbH", "license-co");
        await LoginAsync(ownerClient, "owner@license.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        using var otherClient = CreateClient(factory);
        await RegisterVerifiedCompanyAsync(factory, otherClient, "owner@otherlicense.test", "Other Owner", "Other GmbH", "other-license-co");
        await LoginAsync(otherClient, "owner@otherlicense.test", "Pxa-Customer-Password-42!", HttpStatusCode.OK);

        Guid licenseId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var signing = scope.ServiceProvider.GetRequiredService<IPxaLicenseSigningService>();
            var organization = await dbContext.Organizations.SingleAsync(value => value.Slug == "license-co");
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync(value => value.OrganizationId == organization.Id);
            var owner = await dbContext.Users.SingleAsync(value => value.Email == "owner@license.test");
            subscription.Edition = SubscriptionEdition.Enterprise;
            subscription.Status = SubscriptionStatus.Active;
            subscription.BillingPeriod = SubscriptionBillingPeriod.Annual;
            subscription.DeploymentMode = SubscriptionDeploymentMode.Hybrid;
            subscription.TrialEndsAt = null;
            subscription.CurrentPeriodEndsAt = DateTimeOffset.UtcNow.AddYears(1);

            var license = new OfflineLicense
            {
                OrganizationId = organization.Id,
                SubscriptionId = subscription.Id,
                LicenseNumber = "PXA-TEST-0001",
                EnvelopeJson = string.Empty,
                Signature = string.Empty,
                KeyId = string.Empty,
                Algorithm = string.Empty,
                ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
                ValidUntil = DateTimeOffset.UtcNow.AddYears(1),
                InstanceLimit = 5,
                IssuedByUserId = owner.Id,
            };
            var envelope = new PxaOfflineLicenseEnvelope(
                2, license.Id, license.LicenseNumber, organization.Id, organization.Name,
                subscription.Edition.ToString(), subscription.AccountType.ToString(), subscription.DeploymentMode.ToString(),
                license.ValidFrom, license.ValidUntil, license.InstanceLimit,
                "1.0.0", "license-co-prod", [], license.IssuedAt);
            var artifact = signing.Sign(envelope);
            license.EnvelopeJson = artifact.EnvelopeJson;
            license.Signature = artifact.Signature;
            license.KeyId = artifact.KeyId;
            license.Algorithm = artifact.Algorithm;
            dbContext.OfflineLicenses.Add(license);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            licenseId = license.Id;
        }

        var licenses = await ownerClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/licenses");
        Assert.Equal(1, licenses.GetArrayLength());
        Assert.Equal("PXA-TEST-0001", licenses[0].GetProperty("licenseNumber").GetString());

        var validation = await ownerClient.GetFromJsonAsync<JsonElement>($"/api/pxa/v1/account/licenses/{licenseId}/validate");
        Assert.True(validation.GetProperty("valid").GetBoolean());
        Assert.Equal("PXA_LICENSE_VALID", validation.GetProperty("code").GetString());

        var download = await ownerClient.GetAsync($"/api/pxa/v1/account/licenses/{licenseId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/vnd.pxa.license+json", download.Content.Headers.ContentType?.MediaType);

        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/pxa/v1/account/licenses/{licenseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/pxa/v1/account/licenses/{licenseId}/validate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/pxa/v1/account/licenses/{licenseId}/download")).StatusCode);
        var otherLicenses = await otherClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/account/licenses");
        Assert.Equal(0, otherLicenses.GetArrayLength());
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
