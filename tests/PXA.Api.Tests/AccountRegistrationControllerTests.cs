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
    public async Task Registration_requires_current_legal_versions_and_records_exact_acceptances()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString(), requireDatabaseLegalDocuments: true);
        await SeedRolesAsync(factory.Services);
        var policyIds = await SeedPublishedRegistrationPolicyAsync(factory.Services);
        using var client = CreateClient(factory);

        var policy = await client.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/auth/registration-policy?locale=de");
        Assert.True(policy.GetProperty("databaseBacked").GetBoolean());
        Assert.Equal(policyIds.Terms, policy.GetProperty("terms").GetProperty("id").GetGuid());
        Assert.Equal(policyIds.Privacy, policy.GetProperty("privacy").GetProperty("id").GetGuid());

        var snapshot = await client.GetFromJsonAsync<JsonElement>(
            "/api/pxa/v1/legal/snapshot?locale=de&audience=All");
        Assert.Equal(1, snapshot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            ["privacy", "terms"],
            snapshot.GetProperty("documents").EnumerateArray()
                .Select(value => value.GetProperty("key").GetString()!)
                .ToArray());

        object Payload(
            Guid? termsVersionId,
            Guid? privacyVersionId,
            string email = "legal-registration@customer.test") => new
        {
            email,
            displayName = "Legal Registration",
            password = "Pxa-Customer-Password-42!",
            accountType = "IndividualDeveloper",
            locale = "de",
            acceptTerms = true,
            acceptPrivacy = true,
            termsVersionId,
            privacyVersionId,
        };

        using (var stale = CreateCsrfRequest(
                   HttpMethod.Post,
                   "/api/pxa/v1/auth/register",
                   await GetCsrfAsync(client),
                   Payload(null, null)))
        {
            var response = await client.SendAsync(stale);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(
                PxaApiProblems.PolicyAcceptanceRequired,
                problem.GetProperty("code").GetString());
        }

        using var current = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            Payload(policyIds.Terms, policyIds.Privacy));
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(current)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var registeredUser = await dbContext.Users.SingleAsync(
            value => value.Email == "legal-registration@customer.test");
        var acceptances = await dbContext.LegalAcceptanceEvents
            .Where(value => value.UserId == registeredUser.Id)
            .OrderBy(value => value.DocumentType)
            .ToListAsync();
        Assert.Equal(2, acceptances.Count);
        Assert.Contains(acceptances, value =>
            value.DocumentType == "terms" &&
            value.LegalDocumentVersionId == policyIds.Terms &&
            value.Decision == "accepted");
        Assert.Contains(acceptances, value =>
            value.DocumentType == "privacy" &&
            value.LegalDocumentVersionId == policyIds.Privacy &&
            value.Decision == "acknowledged");
        Assert.All(acceptances, value =>
        {
            Assert.Equal("registration", value.Source);
            Assert.Equal("de", value.Locale);
            Assert.Equal(64, value.ContentHash.Length);
            Assert.NotNull(value.OrganizationId);
        });

        Assert.Contains(
            await dbContext.Database.GetAppliedMigrationsAsync(),
            migration => migration.EndsWith("_AddLegalDocumentGovernance", StringComparison.Ordinal));

        foreach (var version in await dbContext.LegalDocumentVersions.ToListAsync())
        {
            version.Status = LegalDocumentStatus.Retired;
            version.RetiredAt = DateTimeOffset.UtcNow;
        }
        await dbContext.SaveChangesAsync();

        var unavailablePolicy = await client.GetAsync(
            "/api/pxa/v1/auth/registration-policy?locale=de");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailablePolicy.StatusCode);

        const string unavailableEmail = "legal-unavailable@customer.test";
        using var unavailableRegistration = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/register",
            await GetCsrfAsync(client),
            Payload(policyIds.Terms, policyIds.Privacy, unavailableEmail));
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (await client.SendAsync(unavailableRegistration)).StatusCode);
        Assert.False(await dbContext.Users.AnyAsync(value => value.Email == unavailableEmail));
    }

    [PostgreSqlFact]
    public async Task Company_registration_creates_verified_trial_workspace_and_customer_session()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedRolesAsync(factory.Services);
        using var client = CreateClient(factory);

        using (var breachedPassword = CreateCsrfRequest(
                   HttpMethod.Post,
                   "/api/pxa/v1/auth/register",
                   await GetCsrfAsync(client),
                   new
                   {
                       email = "breached@customer.test",
                       displayName = "Breached Password",
                       password = "Password123!",
                       accountType = "Company",
                       companyName = "Rejected Registration",
                       organizationSlug = "rejected-registration",
                       acceptTerms = true,
                       acceptPrivacy = true,
                   }))
        {
            var response = await client.SendAsync(breachedPassword);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(
                "known compromised-password list",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

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
                subscribeToNewsletter = true,
                returnUrl = "https://designer.powerdoxautomation.com/auth/callback?document=42",
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
            Assert.Equal("terms-test-v2", user.TermsAcceptedVersion);
            Assert.NotNull(user.TermsAcceptedAt);
            Assert.Equal("privacy-test-v3", user.PrivacyAcknowledgedVersion);
            Assert.NotNull(user.PrivacyAcknowledgedAt);
            Assert.NotNull(user.MarketingConsentGrantedAt);
            Assert.Null(user.MarketingConsentWithdrawnAt);
            Assert.Equal("registration", user.MarketingConsentSource);
            var consentEvents = await dbContext.UserConsentEvents
                .Where(value => value.UserId == user.Id)
                .OrderBy(value => value.ConsentType)
                .ToListAsync();
            Assert.Equal(3, consentEvents.Count);
            Assert.Contains(consentEvents, value =>
                value.ConsentType == "terms" && value.Decision == "accepted" &&
                value.PolicyVersion == "terms-test-v2");
            Assert.Contains(consentEvents, value =>
                value.ConsentType == "privacy" && value.Decision == "acknowledged" &&
                value.PolicyVersion == "privacy-test-v3");
            Assert.Contains(consentEvents, value =>
                value.ConsentType == "marketing" && value.Decision == "granted");
            var organization = await dbContext.Organizations.SingleAsync();
            organizationId = organization.Id;
            Assert.Equal("customer-documents", organization.Slug);
            var membership = await dbContext.OrganizationMemberships.SingleAsync();
            Assert.Equal(userId, membership.UserId);
            Assert.Equal(OrganizationMembershipStatus.Active, membership.Status);
            Assert.Single(await dbContext.OrganizationMembershipRoles.ToListAsync());
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync();
            Assert.Equal(SubscriptionEdition.Trial, subscription.Edition);
            Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
            Assert.Equal(SubscriptionAccountType.Company, subscription.AccountType);
            Assert.Null(subscription.TrialEndsAt);
            Assert.Empty(await dbContext.SubscriptionEntitlements.ToListAsync());
            Assert.Empty(await dbContext.SubscriptionSeatAssignments.ToListAsync());
            Assert.Empty(await dbContext.SubscriptionLifecycleEvents.ToListAsync());
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
        Assert.Contains(
            "returnUrl=https%3A%2F%2Fdesigner.powerdoxautomation.com%2Fauth%2Fcallback%3Fdocument%3D42",
            mail.TextBody,
            StringComparison.Ordinal);
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
        var activatedSubscription = await finalDbContext.OrganizationSubscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatus.Trialing, activatedSubscription.Status);
        Assert.InRange(activatedSubscription.TrialEndsAt!.Value,
            DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
        Assert.Equal(8, await finalDbContext.SubscriptionEntitlements.CountAsync(value =>
            value.SubscriptionId == activatedSubscription.Id && value.Enabled));
        Assert.Single(await finalDbContext.SubscriptionSeatAssignments.ToListAsync());
        Assert.Contains("subscription.trial.started",
            await finalDbContext.SubscriptionLifecycleEvents.Select(value => value.Action).ToListAsync());
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
            new { email = "resend@customer.test", returnUrl = "https://evil.example/phishing" });
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
        Assert.DoesNotContain("evil.example", messages.Last().TextBody, StringComparison.Ordinal);
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

    [PostgreSqlFact]
    public async Task Registration_drops_non_allowlisted_campaign_context_keys_before_auditing()
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
                email = "owner@campaign.test",
                displayName = "Campaign Owner",
                password = "Pxa-Customer-Password-42!",
                accountType = "Company",
                companyName = "Campaign GmbH",
                organizationSlug = "campaign-co",
                acceptTerms = true,
                acceptPrivacy = true,
                campaignContext = new Dictionary<string, string>
                {
                    ["utm_source"] = "newsletter",
                    ["utm_campaign"] = "spring-launch",
                    ["password"] = "smuggled-value",
                    ["email"] = "smuggled@evil.test",
                },
            });
        Assert.Equal(HttpStatusCode.Accepted, (await client.SendAsync(register)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var audit = await dbContext.AuditEvents.SingleAsync(value => value.Action == "account.registration.created");
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        var campaignContext = details.RootElement.GetProperty("CampaignContext");
        var keys = campaignContext.EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Equal(2, keys.Length);
        Assert.Contains("utm_source", keys);
        Assert.Contains("utm_campaign", keys);
        Assert.DoesNotContain("password", keys);
        Assert.DoesNotContain("email", keys);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        bool requireDatabaseLegalDocuments = false) =>
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
                    ["Registration:TermsVersion"] = "terms-test-v2",
                    ["Registration:PrivacyVersion"] = "privacy-test-v3",
                    ["Registration:RequireDatabaseLegalDocuments"] =
                        requireDatabaseLegalDocuments.ToString(),
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PxaDbContext>>();
                services.RemoveAll<IHostedService>();
                services.AddDbContext<PxaDbContext>(options => options.UseNpgsql(connectionString));
            });
        });

    private static async Task<(Guid Terms, Guid Privacy)> SeedPublishedRegistrationPolicyAsync(
        IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var author = new PxaIdentityUser
        {
            UserName = "legal-author@pxa.test",
            NormalizedUserName = "LEGAL-AUTHOR@PXA.TEST",
            Email = "legal-author@pxa.test",
            NormalizedEmail = "LEGAL-AUTHOR@PXA.TEST",
            EmailConfirmed = true,
            DisplayName = "Legal Author",
        };
        dbContext.Users.Add(author);
        var termsDocument = new LegalDocument
        {
            Type = LegalDocumentType.TermsAndConditions,
            Key = "terms",
            DisplayName = "Terms and Conditions",
            CreatedByUserId = author.Id,
        };
        var privacyDocument = new LegalDocument
        {
            Type = LegalDocumentType.PrivacyNotice,
            Key = "privacy",
            DisplayName = "Privacy Notice",
            CreatedByUserId = author.Id,
        };
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        LegalDocumentVersion Version(LegalDocument document, string version, string source) => new()
        {
            LegalDocumentId = document.Id,
            Version = version,
            Locale = "de",
            Audience = LegalDocumentAudience.All,
            Status = LegalDocumentStatus.Published,
            SourceMarkdown = source,
            RenderedHtml = PXA.WebApi.Application.Legal.PxaLegalDocumentService.RenderSafeHtml(source),
            ContentHash = PXA.WebApi.Application.Legal.PxaLegalDocumentService.ComputeHash(source),
            RequiresAcceptance = true,
            IsAuthoritative = true,
            CreatedByUserId = author.Id,
            PublishedByUserId = author.Id,
            PublishedAt = now,
            EffectiveAt = now,
        };
        var terms = Version(termsDocument, "terms-2026-07", "# Terms");
        var privacy = Version(privacyDocument, "privacy-2026-07", "# Privacy");
        dbContext.AddRange(termsDocument, privacyDocument, terms, privacy);
        await dbContext.SaveChangesAsync();
        return (terms.Id, privacy.Id);
    }

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
        var encodedToken = body[(start + marker.Length)..].Trim();
        var separator = encodedToken.IndexOf('&');
        if (separator >= 0)
            encodedToken = encodedToken[..separator];
        return Uri.UnescapeDataString(encodedToken);
    }
}
