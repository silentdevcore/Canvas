using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class DesignerAuthenticationControllerTests
{
    [PostgreSqlFact]
    public async Task Account_handoff_creates_a_separate_designer_session_and_rejects_replay()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        using var login = await CreateCsrfRequestAsync(
            accountClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            new
            {
                identifier = "designer@customer.test",
                password = "Pxa-Customer-Password-42!",
            });
        var loginResponse = await accountClient.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-PXA.Session=", StringComparison.Ordinal));

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var challenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var create = await CreateCsrfRequestAsync(
            accountClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/designer-handoff",
            new
            {
                designerOrigin = "http://localhost:5176",
                returnPath = "/pdf/create?mode=code#editor",
                codeChallenge = challenge,
                state,
            });
        var createResponse = await accountClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var redirectUrl = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("redirectUrl")
            .GetString()!;
        var redirect = new Uri(redirectUrl);
        Assert.Equal("http://localhost:5176/auth/callback", redirect.GetLeftPart(UriPartial.Path));
        var callback = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(redirect.Query);
        var code = callback["code"].ToString();
        Assert.Equal(state, callback["state"].ToString());

        using var designerClient = CreateClient(factory);
        using var exchange = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/designer-handoff/exchange",
            new
            {
                code,
                state,
                codeVerifier = verifier,
                designerOrigin = "http://localhost:5176",
            },
            designer: true);
        var exchangeResponse = await designerClient.SendAsync(exchange);
        Assert.Equal(HttpStatusCode.OK, exchangeResponse.StatusCode);
        Assert.Contains(
            exchangeResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-PXA.Designer.Session=", StringComparison.Ordinal));
        Assert.Equal(
            "/pdf/create?mode=code#editor",
            (await exchangeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("returnPath")
            .GetString());

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/pxa/v1/auth/me");
        me.Headers.Add("X-PXA-Application", "designer");
        var meResponse = await designerClient.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(
            "designer@customer.test",
            (await meResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString());

        using var mutationWithoutCsrf = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/migration/convert")
        {
            Content = JsonContent.Create(new { }),
        };
        mutationWithoutCsrf.Headers.Add("X-PXA-Application", "designer");
        var csrfFailure = await designerClient.SendAsync(mutationWithoutCsrf);
        Assert.Equal(HttpStatusCode.BadRequest, csrfFailure.StatusCode);
        Assert.Equal(
            "PXAAPI008",
            (await csrfFailure.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var anonymousClient = CreateClient(factory);
        using var anonymousApi = new HttpRequestMessage(HttpMethod.Get, "/api/migration/frameworks");
        anonymousApi.Headers.Add("X-PXA-Application", "designer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.SendAsync(anonymousApi)).StatusCode);

        using var replayClient = CreateClient(factory);
        using var replay = await CreateCsrfRequestAsync(
            replayClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/designer-handoff/exchange",
            new
            {
                code,
                state,
                codeVerifier = verifier,
                designerOrigin = "http://localhost:5176",
            },
            designer: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await replayClient.SendAsync(replay)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var handoff = await dbContext.DesignerAuthorizationCodes.SingleAsync();
        Assert.NotEqual(code, handoff.CodeHash);
        Assert.NotNull(handoff.ConsumedAt);
        Assert.Equal(2, await dbContext.UserSessions.CountAsync());

        var designerEntitlement = await dbContext.SubscriptionEntitlements.SingleAsync(
            value => value.Capability == "designer");
        designerEntitlement.Enabled = false;
        await dbContext.SaveChangesAsync();
        using var deniedMe = new HttpRequestMessage(HttpMethod.Get, "/api/pxa/v1/auth/me");
        deniedMe.Headers.Add("X-PXA-Application", "designer");
        var deniedMeResponse = await designerClient.SendAsync(deniedMe);
        Assert.Equal(HttpStatusCode.Forbidden, deniedMeResponse.StatusCode);
        Assert.Equal(
            "PXA_ENTITLEMENT_DENIED",
            (await deniedMeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code")
            .GetString());

        dbContext.ChangeTracker.Clear();
        Assert.True(await dbContext.AuditEvents.AnyAsync(value =>
            value.Action == "security.designer-entitlement.denied" &&
            value.Outcome == "rejected"));

        designerEntitlement = await dbContext.SubscriptionEntitlements.SingleAsync(
            value => value.Capability == "designer");
        designerEntitlement.Enabled = true;
        var sourceSessionId = handoff.SourceSessionId;
        var designerSession = await dbContext.UserSessions.SingleAsync(
            value => value.Id != sourceSessionId);
        designerSession.RevokedAt = DateTimeOffset.UtcNow;
        designerSession.RevocationReason = "test";
        await dbContext.SaveChangesAsync();

        using var revokedMe = new HttpRequestMessage(HttpMethod.Get, "/api/pxa/v1/auth/me");
        revokedMe.Headers.Add("X-PXA-Application", "designer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await designerClient.SendAsync(revokedMe)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Handoff_rejects_invalid_security_inputs_and_switches_only_between_member_organizations()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);

        var wrongPkceHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        using var wrongPkceClient = CreateClient(factory);
        var wrongVerifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        using var wrongPkce = await CreateExchangeRequestAsync(
            wrongPkceClient, wrongPkceHandoff, wrongVerifier);
        Assert.Equal(HttpStatusCode.BadRequest, (await wrongPkceClient.SendAsync(wrongPkce)).StatusCode);

        var wrongStateHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        using var wrongStateClient = CreateClient(factory);
        using var wrongState = await CreateExchangeRequestAsync(
            wrongStateClient,
            wrongStateHandoff with { State = wrongStateHandoff.State + "A" });
        Assert.Equal(HttpStatusCode.BadRequest, (await wrongStateClient.SendAsync(wrongState)).StatusCode);

        var wrongOriginHandoff = await CreateHandoffAsync(accountClient, "/pdf/template");
        using var wrongOriginClient = CreateClient(factory);
        using var wrongOrigin = await CreateExchangeRequestAsync(
            wrongOriginClient,
            wrongOriginHandoff,
            requestOrigin: "https://designer.invalid.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await wrongOriginClient.SendAsync(wrongOrigin)).StatusCode);

        var expiredHandoff = await CreateHandoffAsync(accountClient, "/pdf/viewer");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var codeEntity = await dbContext.DesignerAuthorizationCodes.SingleAsync(
                value => value.CodeHash == Hash(expiredHandoff.Code));
            codeEntity.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }
        using var expiredClient = CreateClient(factory);
        using var expired = await CreateExchangeRequestAsync(expiredClient, expiredHandoff);
        Assert.Equal(HttpStatusCode.BadRequest, (await expiredClient.SendAsync(expired)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var rejectedHandoffs = await dbContext.AuditEvents.CountAsync(value =>
                value.Action == "security.designer-handoff.exchanged" &&
                value.Outcome == "rejected");
            Assert.True(rejectedHandoffs >= 4);
            Assert.DoesNotContain(
                await dbContext.AuditEvents
                    .Where(value => value.Action == "security.designer-handoff.exchanged")
                    .Select(value => value.DetailsJson)
                    .ToListAsync(),
                details => details?.Contains(wrongPkceHandoff.Code, StringComparison.Ordinal) == true);
        }

        var concurrentHandoff = await CreateHandoffAsync(accountClient, "/spreadsheet/create");
        using var firstExchangeClient = CreateClient(factory);
        using var secondExchangeClient = CreateClient(factory);
        using var firstExchange = await CreateExchangeRequestAsync(firstExchangeClient, concurrentHandoff);
        using var secondExchange = await CreateExchangeRequestAsync(secondExchangeClient, concurrentHandoff);
        var exchangeResponses = await Task.WhenAll(
            firstExchangeClient.SendAsync(firstExchange),
            secondExchangeClient.SendAsync(secondExchange));
        Assert.Single(exchangeResponses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(exchangeResponses, response => response.StatusCode == HttpStatusCode.BadRequest);
        var designerClient = exchangeResponses[0].StatusCode == HttpStatusCode.OK
            ? firstExchangeClient
            : secondExchangeClient;

        var secondOrganizationId = await AddEntitledOrganizationAsync(
            factory.Services, seeded.UserId, "Second Workspace", "second-workspace");
        using var switchToSecond = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = secondOrganizationId },
            designer: true);
        var switchToSecondResponse = await designerClient.SendAsync(switchToSecond);
        Assert.Equal(HttpStatusCode.OK, switchToSecondResponse.StatusCode);
        Assert.Equal(
            secondOrganizationId,
            (await switchToSecondResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("user")
            .GetProperty("activeOrganizationId")
            .GetGuid());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var entitlement = await (
                from value in dbContext.SubscriptionEntitlements
                join subscription in dbContext.OrganizationSubscriptions
                    on value.SubscriptionId equals subscription.Id
                where subscription.OrganizationId == secondOrganizationId &&
                      value.Capability == "designer"
                select value)
                .SingleAsync();
            entitlement.Enabled = false;
            await dbContext.SaveChangesAsync();
        }

        // Switching remains available when the current workspace loses its entitlement.
        using var switchBack = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = seeded.OrganizationId },
            designer: true);
        Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(switchBack)).StatusCode);

        using var switchToUnentitled = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = secondOrganizationId },
            designer: true);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await designerClient.SendAsync(switchToUnentitled)).StatusCode);

        using var foreignSwitch = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = Guid.NewGuid() },
            designer: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await designerClient.SendAsync(foreignSwitch)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Handoff_rechecks_user_organization_and_entitlement_before_exchange()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);
        var unverifiedHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        var suspendedHandoff = await CreateHandoffAsync(accountClient, "/spreadsheet/create");
        var expiredEntitlementHandoff = await CreateHandoffAsync(accountClient, "/migrations");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var user = await dbContext.Users.SingleAsync(value => value.Id == seeded.UserId);
        user.EmailConfirmed = false;
        await dbContext.SaveChangesAsync();

        using (var client = CreateClient(factory))
        using (var exchange = await CreateExchangeRequestAsync(client, unverifiedHandoff))
        {
            var response = await client.SendAsync(exchange);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "PXA_DESIGNER_VERIFICATION_REQUIRED",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        user.EmailConfirmed = true;
        var organization = await dbContext.Organizations.SingleAsync(
            value => value.Id == seeded.OrganizationId);
        organization.Status = OrganizationStatus.Suspended;
        await dbContext.SaveChangesAsync();

        using (var client = CreateClient(factory))
        using (var exchange = await CreateExchangeRequestAsync(client, suspendedHandoff))
        {
            var response = await client.SendAsync(exchange);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "PXA_ORGANIZATION_INACTIVE",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        organization.Status = OrganizationStatus.Active;
        var entitlement = await dbContext.SubscriptionEntitlements.SingleAsync(
            value => value.Capability == "designer");
        entitlement.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        using (var client = CreateClient(factory))
        using (var exchange = await CreateExchangeRequestAsync(client, expiredEntitlementHandoff))
        {
            var response = await client.SendAsync(exchange);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "PXA_ENTITLEMENT_EXPIRED",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        dbContext.ChangeTracker.Clear();
        Assert.Equal(
            3,
            await dbContext.AuditEvents.CountAsync(value =>
                value.Action == "security.designer-handoff.exchanged" &&
                value.Outcome == "rejected"));
    }

    [PostgreSqlFact]
    public async Task Handoff_rejects_locked_inactive_expired_session_and_missing_entitlement()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);
        var existingSessionHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        var lockedHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        var inactiveHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        var missingEntitlementHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");
        var expiredSessionHandoff = await CreateHandoffAsync(accountClient, "/pdf/create");

        using var existingSessionClient = CreateClient(factory);
        using (var exchange = await CreateExchangeRequestAsync(
                   existingSessionClient, existingSessionHandoff))
        {
            Assert.Equal(HttpStatusCode.OK, (await existingSessionClient.SendAsync(exchange)).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var user = await dbContext.Users.SingleAsync(value => value.Id == seeded.UserId);

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        await dbContext.SaveChangesAsync();
        using (var me = DesignerGet("/api/pxa/v1/auth/me"))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await existingSessionClient.SendAsync(me)).StatusCode);
        }
        await AssertExchangeDeniedAsync(
            factory, lockedHandoff, "PXA_DESIGNER_ACCOUNT_LOCKED");

        user.LockoutEnd = null;
        user.IsActive = false;
        await dbContext.SaveChangesAsync();
        await AssertExchangeDeniedAsync(
            factory, inactiveHandoff, "PXA_DESIGNER_ACCOUNT_DISABLED");

        user.IsActive = true;
        var entitlement = await dbContext.SubscriptionEntitlements.SingleAsync(
            value => value.Capability == "designer");
        dbContext.SubscriptionEntitlements.Remove(entitlement);
        await dbContext.SaveChangesAsync();
        await AssertExchangeDeniedAsync(
            factory, missingEntitlementHandoff, "PXA_ENTITLEMENT_MISSING");

        var sourceSessionId = await dbContext.DesignerAuthorizationCodes
            .Where(value => value.CodeHash == Hash(expiredSessionHandoff.Code))
            .Select(value => value.SourceSessionId)
            .SingleAsync();
        var sourceSession = await dbContext.UserSessions.SingleAsync(
            value => value.Id == sourceSessionId);
        sourceSession.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
        await AssertExchangeDeniedAsync(
            factory, expiredSessionHandoff, "PXA_DESIGNER_SESSION_EXPIRED");
    }

    [PostgreSqlFact]
    public async Task Designer_templates_are_persistent_concurrent_versioned_and_tenant_isolated()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);
        var handoff = await CreateHandoffAsync(accountClient, "/pdf/template");
        using var designerClient = CreateClient(factory);
        using (var exchange = await CreateExchangeRequestAsync(designerClient, handoff))
        {
            Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(exchange)).StatusCode);
        }

        var initialDesign = new
        {
            template = new
            {
                id = "local-invoice",
                name = "Tenant Invoice",
                category = "invoice",
                description = "Persistent invoice",
                pages = new[] { new { id = "page-1", elements = Array.Empty<object>() } },
                sharedElements = Array.Empty<object>(),
                data = new { },
            },
            pageSettings = new { width = 595, height = 842 },
            jsonData = new { customer = "Ada" },
            documentMode = "pdf",
            currentPageIndex = 0,
        };
        using var create = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/designer/templates",
            new
            {
                name = "Tenant Invoice",
                description = "Persistent invoice",
                tags = new[] { "Invoice", " Customer " },
                designDocument = initialDesign,
                schemaVersion = "1.0",
                designerVersion = "1.0.0",
                organizationId = Guid.NewGuid(),
            },
            designer: true);
        var createResponse = await designerClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("\"1\"", createResponse.Headers.ETag?.Tag);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = created.GetProperty("id").GetGuid();
        Assert.Equal(1, created.GetProperty("revision").GetInt64());
        Assert.Equal(new[] { "invoice", "customer" }, created.GetProperty("tags").EnumerateArray()
            .Select(value => value.GetString()).ToArray());

        using (var list = new HttpRequestMessage(HttpMethod.Get, "/api/pxa/v1/designer/templates"))
        {
            list.Headers.Add("X-PXA-Application", "designer");
            var listResponse = await designerClient.SendAsync(list);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            Assert.Equal(1, (await listResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("total").GetInt32());
        }

        var firstChangedDesign = new
        {
            template = new { id = "local-invoice", name = "Tenant Invoice", pages = new[] { new { id = "page-1", elements = new[] { new { id = "a", type = "text" } } } } },
            pageSettings = new { width = 595, height = 842 },
            jsonData = new { customer = "Ada" },
            documentMode = "pdf",
            currentPageIndex = 0,
        };
        var secondChangedDesign = new
        {
            template = new { id = "local-invoice", name = "Tenant Invoice", pages = new[] { new { id = "page-1", elements = new[] { new { id = "b", type = "text" } } } } },
            pageSettings = new { width = 595, height = 842 },
            jsonData = new { customer = "Grace" },
            documentMode = "pdf",
            currentPageIndex = 0,
        };
        using var firstUpdate = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Put,
            $"/api/pxa/v1/designer/templates/{templateId}/draft",
            new { revision = 1, designDocument = firstChangedDesign },
            designer: true);
        using var secondUpdate = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Put,
            $"/api/pxa/v1/designer/templates/{templateId}/draft",
            new { revision = 1, designDocument = secondChangedDesign },
            designer: true);
        firstUpdate.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        secondUpdate.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var updateResponses = await Task.WhenAll(
            designerClient.SendAsync(firstUpdate),
            designerClient.SendAsync(secondUpdate));
        Assert.Single(updateResponses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(updateResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        var successfulUpdate = updateResponses.Single(response => response.StatusCode == HttpStatusCode.OK);
        var updated = await successfulUpdate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("revision").GetInt64());
        var winningDesign = updated.GetProperty("designDocument").Clone();
        var conflict = await updateResponses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, conflict.GetProperty("currentRevision").GetInt64());
        Assert.Equal("PXADESIGNER001", conflict.GetProperty("code").GetString());

        using (var compatibilityRead = DesignerGet($"/api/pxa/templates/{templateId}"))
        {
            var compatibilityResponse = await designerClient.SendAsync(compatibilityRead);
            Assert.Equal(HttpStatusCode.OK, compatibilityResponse.StatusCode);
            Assert.Single((await compatibilityResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("elements")
                .EnumerateArray());
        }

        using (var noOp = await CreateCsrfRequestAsync(
                   designerClient,
                   HttpMethod.Put,
                   $"/api/pxa/v1/designer/templates/{templateId}/draft",
                   new { revision = 2, designDocument = winningDesign },
                   designer: true))
        {
            noOp.Headers.TryAddWithoutValidation("If-Match", "\"2\"");
            var noOpResponse = await designerClient.SendAsync(noOp);
            Assert.Equal(HttpStatusCode.OK, noOpResponse.StatusCode);
            Assert.Equal(2, (await noOpResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("revision").GetInt64());
        }

        using var createVersion = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            $"/api/pxa/v1/designer/templates/{templateId}/versions",
            new { revision = 2, label = "Approved draft" },
            designer: true);
        var versionResponse = await designerClient.SendAsync(createVersion);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        Assert.True((await versionResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("created").GetBoolean());

        using (var duplicateVersion = await CreateCsrfRequestAsync(
                   designerClient,
                   HttpMethod.Post,
                   $"/api/pxa/v1/designer/templates/{templateId}/versions",
                   new { revision = 2, label = "Duplicate" },
                   designer: true))
        {
            var duplicateResponse = await designerClient.SendAsync(duplicateVersion);
            Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
            Assert.False((await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("created").GetBoolean());
        }

        using var publish = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            $"/api/pxa/v1/designer/templates/{templateId}/publish",
            new { revision = 2, versionNumber = 1 },
            designer: true);
        var published = await designerClient.SendAsync(publish);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(3, (await published.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("revision").GetInt64());

        using var archive = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            $"/api/pxa/v1/designer/templates/{templateId}/archive",
            new { revision = 3 },
            designer: true);
        var archived = await designerClient.SendAsync(archive);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal(4, (await archived.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("revision").GetInt64());

        using (var archivedList = new HttpRequestMessage(
                   HttpMethod.Get,
                   "/api/pxa/v1/designer/templates?archived=true"))
        {
            archivedList.Headers.Add("X-PXA-Application", "designer");
            Assert.Equal(1, (await (await designerClient.SendAsync(archivedList)).Content
                .ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        }

        using var restore = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            $"/api/pxa/v1/designer/templates/{templateId}/restore",
            new { revision = 4 },
            designer: true);
        Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(restore)).StatusCode);

        using var oversized = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/designer/templates",
            new
            {
                name = "Too large",
                designDocument = new { content = new string('x', 5000) },
            },
            designer: true);
        Assert.Equal(
            HttpStatusCode.RequestEntityTooLarge,
            (await designerClient.SendAsync(oversized)).StatusCode);

        var secondOrganizationId = await AddEntitledOrganizationAsync(
            factory.Services, seeded.UserId, "Other Customer", "other-customer");
        using var switchOrganization = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = secondOrganizationId },
            designer: true);
        Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(switchOrganization)).StatusCode);
        using var foreignRead = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/pxa/v1/designer/templates/{templateId}");
        foreignRead.Headers.Add("X-PXA-Application", "designer");
        Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignRead)).StatusCode);

        using (var foreignList = DesignerGet("/api/pxa/v1/designer/templates"))
        {
            var response = await designerClient.SendAsync(foreignList);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                0,
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());
        }

        using (var foreignUpdate = await CreateCsrfRequestAsync(
                   designerClient,
                   HttpMethod.Put,
                   $"/api/pxa/v1/designer/templates/{templateId}/draft",
                   new { revision = 5, designDocument = initialDesign },
                   designer: true))
        {
            foreignUpdate.Headers.TryAddWithoutValidation("If-Match", "\"5\"");
            Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignUpdate)).StatusCode);
        }

        using (var foreignVersions = DesignerGet(
                   $"/api/pxa/v1/designer/templates/{templateId}/versions"))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignVersions)).StatusCode);
        }

        using (var foreignArchive = await CreateCsrfRequestAsync(
                   designerClient,
                   HttpMethod.Post,
                   $"/api/pxa/v1/designer/templates/{templateId}/archive",
                   new { revision = 5 },
                   designer: true))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignArchive)).StatusCode);
        }

        using (var foreignRender = await CreateCsrfRequestAsync(
                   designerClient,
                   HttpMethod.Post,
                   $"/api/pxa/templates/render?templateId={templateId}",
                   new { customer = "Other tenant" },
                   designer: true))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignRender)).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Equal(seeded.OrganizationId, (await dbContext.DesignerTemplates.SingleAsync()).OrganizationId);
        Assert.Single(await dbContext.DesignerTemplateVersions.ToListAsync());
        Assert.True(await dbContext.AuditEvents.AnyAsync(value =>
            value.Action == "designer.templates.conflict" &&
            value.Outcome == "rejected" &&
            value.OrganizationId == seeded.OrganizationId));
    }

    [PostgreSqlFact]
    public async Task Template_listing_is_stably_paginated_and_tracks_access_revocation()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);
        var membershipHandoff = await CreateHandoffAsync(accountClient, "/pdf/templates");
        var revokedSessionHandoff = await CreateHandoffAsync(accountClient, "/pdf/templates");
        var expiredEntitlementHandoff = await CreateHandoffAsync(accountClient, "/pdf/templates");

        using var membershipClient = CreateClient(factory);
        using var revokedSessionClient = CreateClient(factory);
        using var expiredEntitlementClient = CreateClient(factory);
        await ExchangeSuccessfullyAsync(membershipClient, membershipHandoff);
        var revokedSessionId = await ExchangeAndFindSessionAsync(
            factory, revokedSessionClient, revokedSessionHandoff);
        await ExchangeSuccessfullyAsync(expiredEntitlementClient, expiredEntitlementHandoff);

        var commonUpdatedAt = new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
        var orderedIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            dbContext.DesignerTemplates.AddRange(
                CreateTemplate(orderedIds[1], seeded, "Alpha", commonUpdatedAt),
                CreateTemplate(orderedIds[2], seeded, "Bravo", commonUpdatedAt),
                CreateTemplate(orderedIds[3], seeded, "Charlie", commonUpdatedAt),
                CreateTemplate(orderedIds[0], seeded, "Newest", commonUpdatedAt.AddMinutes(1)));
            await dbContext.SaveChangesAsync();
        }

        var firstPage = await GetTemplatePageAsync(membershipClient, page: 1, pageSize: 2);
        var secondPage = await GetTemplatePageAsync(membershipClient, page: 2, pageSize: 2);
        Assert.Equal(4, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(orderedIds.Take(2), ReadTemplateIds(firstPage));
        Assert.Equal(orderedIds.Skip(2), ReadTemplateIds(secondPage));
        Assert.Empty(ReadTemplateIds(firstPage).Intersect(ReadTemplateIds(secondPage)));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var controller = new DesignerTemplatesController(
                scope.ServiceProvider.GetRequiredService<PxaDbContext>(),
                new TestTenantContext(seeded.UserId, seeded.OrganizationId),
                Options.Create(new PxaDesignerTemplateOptions()));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                controller.List(page: 1, pageSize: 2, cancellationToken: cancellation.Token));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var membership = await dbContext.OrganizationMemberships.SingleAsync(value =>
                value.OrganizationId == seeded.OrganizationId &&
                value.UserId == seeded.UserId);
            membership.Status = OrganizationMembershipStatus.Removed;
            await dbContext.SaveChangesAsync();
        }
        using (var list = DesignerGet("/api/pxa/v1/designer/templates"))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await membershipClient.SendAsync(list)).StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var membership = await dbContext.OrganizationMemberships.SingleAsync(value =>
                value.OrganizationId == seeded.OrganizationId &&
                value.UserId == seeded.UserId);
            membership.Status = OrganizationMembershipStatus.Active;
            var revokedSession = await dbContext.UserSessions.SingleAsync(
                value => value.Id == revokedSessionId);
            revokedSession.RevokedAt = DateTimeOffset.UtcNow;
            revokedSession.RevocationReason = "template-access-test";
            await dbContext.SaveChangesAsync();
        }
        using (var list = DesignerGet("/api/pxa/v1/designer/templates"))
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await revokedSessionClient.SendAsync(list)).StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var entitlement = await dbContext.SubscriptionEntitlements.SingleAsync(
                value => value.Capability == "designer");
            entitlement.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }
        using (var list = DesignerGet("/api/pxa/v1/designer/templates"))
        {
            var response = await expiredEntitlementClient.SendAsync(list);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "PXA_ENTITLEMENT_EXPIRED",
                (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
        }
    }

    [PostgreSqlFact]
    public async Task Legacy_template_api_uses_designer_persistence_and_enforces_revision_and_tenant()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        var seeded = await SeedCustomerAsync(factory.Services);

        using var accountClient = CreateClient(factory);
        await LoginAsync(accountClient);
        var handoff = await CreateHandoffAsync(accountClient, "/pdf/template");
        using var designerClient = CreateClient(factory);
        using (var exchange = await CreateExchangeRequestAsync(designerClient, handoff))
        {
            Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(exchange)).StatusCode);
        }

        using var create = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/templates",
            new
            {
                id = "legacy-invoice",
                name = "Legacy Invoice",
                description = "Created through the compatibility API",
                createdBy = "attacker-controlled",
                elements = new[]
                {
                    new
                    {
                        id = "title",
                        type = 0,
                        props = new Dictionary<string, object> { ["text"] = "Invoice" },
                        x = 20,
                        y = 30,
                        width = 200,
                        height = 30,
                    },
                },
            },
            designer: true);
        var createResponse = await designerClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("legacy-invoice", created.GetProperty("id").GetString());
        Assert.Equal(1, created.GetProperty("revision").GetInt64());
        Assert.Equal(
            seeded.UserId.ToString(),
            created.GetProperty("metadata").GetProperty("createdBy").GetString());

        using (var list = DesignerGet("/api/pxa/templates"))
        {
            var listResponse = await designerClient.SendAsync(list);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var items = await listResponse.Content.ReadFromJsonAsync<JsonElement[]>();
            Assert.Single(items!);
            Assert.Equal("legacy-invoice", items![0].GetProperty("id").GetString());
            Assert.DoesNotContain(items, value =>
                value.GetProperty("id").GetString() == "sample-invoice");
        }

        using var update = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Put,
            "/api/pxa/templates/legacy-invoice",
            new
            {
                revision = 1,
                name = "Updated Legacy Invoice",
                updatedBy = "attacker-controlled",
                createNewVersion = true,
                versionName = "before-update",
            },
            designer: true);
        var updateResponse = await designerClient.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("revision").GetInt64());
        Assert.Equal(
            seeded.UserId.ToString(),
            updated.GetProperty("metadata").GetProperty("updatedBy").GetString());

        using var noOpUpdate = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Put,
            "/api/pxa/templates/legacy-invoice",
            new { revision = 2 },
            designer: true);
        var noOpResponse = await designerClient.SendAsync(noOpUpdate);
        Assert.Equal(HttpStatusCode.OK, noOpResponse.StatusCode);
        Assert.Equal(
            2,
            (await noOpResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("revision")
            .GetInt64());

        using var staleUpdate = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Put,
            "/api/pxa/templates/legacy-invoice",
            new { revision = 1, name = "Stale overwrite" },
            designer: true);
        var staleResponse = await designerClient.SendAsync(staleUpdate);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.Equal(
            2,
            (await staleResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("currentRevision")
            .GetInt64());

        using (var versionRead = DesignerGet(
                   "/api/pxa/templates/legacy-invoice?version=before-update"))
        {
            var versionResponse = await designerClient.SendAsync(versionRead);
            Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
            Assert.Equal(
                "Legacy Invoice",
                (await versionResponse.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("name")
                .GetString());
        }

        var secondOrganizationId = await AddEntitledOrganizationAsync(
            factory.Services,
            seeded.UserId,
            "Legacy Other Customer",
            "legacy-other-customer");
        using var switchOrganization = await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/switch-organization",
            new { organizationId = secondOrganizationId },
            designer: true);
        Assert.Equal(HttpStatusCode.OK, (await designerClient.SendAsync(switchOrganization)).StatusCode);

        using (var foreignRead = DesignerGet("/api/pxa/templates/legacy-invoice"))
            Assert.Equal(HttpStatusCode.NotFound, (await designerClient.SendAsync(foreignRead)).StatusCode);
        using (var foreignList = DesignerGet("/api/pxa/templates"))
            Assert.Empty((await (await designerClient.SendAsync(foreignList)).Content
                .ReadFromJsonAsync<JsonElement[]>())!);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var persisted = await dbContext.DesignerTemplates.SingleAsync();
        Assert.Equal("legacy-invoice", persisted.ExternalId);
        Assert.Equal(seeded.OrganizationId, persisted.OrganizationId);
        Assert.Equal(2, persisted.Revision);
        Assert.Single(await dbContext.DesignerTemplateVersions.ToListAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PxaDatabase"] = connectionString,
                    ["Mail:Enabled"] = "false",
                    ["Mail:Transport"] = "Disabled",
                    ["DesignerAuthentication:AllowedOrigins:0"] = "http://localhost:5176",
                    ["DesignerTemplates:MaximumDesignJsonBytes"] = "4096",
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

    private static HttpRequestMessage DesignerGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-PXA-Application", "designer");
        return request;
    }

    private static async Task<SeededCustomer> SeedCustomerAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        await dbContext.Database.MigrateAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PxaIdentityRole>>();
        var role = new PxaIdentityRole
        {
            Name = PxaRoles.OrganizationAdministrator,
            IsSystemRole = true,
        };
        Assert.True((await roleManager.CreateAsync(role)).Succeeded);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PxaIdentityUser>>();
        var user = new PxaIdentityUser
        {
            UserName = "designer@customer.test",
            Email = "designer@customer.test",
            EmailConfirmed = true,
            DisplayName = "Designer Customer",
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, "Pxa-Customer-Password-42!")).Succeeded);
        var organization = new Organization { Name = "Designer Customer", Slug = "designer-customer" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
        };
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            Edition = SubscriptionEdition.Premium,
            AccountType = SubscriptionAccountType.Company,
            Status = SubscriptionStatus.Active,
            BillingPeriod = SubscriptionBillingPeriod.Monthly,
            DeploymentMode = SubscriptionDeploymentMode.Cloud,
        };
        dbContext.AddRange(organization, membership, subscription);
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = role.Id,
            AssignedByUserId = user.Id,
        });
        dbContext.SubscriptionEntitlements.Add(new SubscriptionEntitlement
        {
            SubscriptionId = subscription.Id,
            Capability = "designer",
            Enabled = true,
            Source = EntitlementSource.EditionDefault,
        });
        await dbContext.SaveChangesAsync();
        return new SeededCustomer(user.Id, organization.Id);
    }

    private static async Task LoginAsync(HttpClient accountClient)
    {
        using var login = await CreateCsrfRequestAsync(
            accountClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            new
            {
                identifier = "designer@customer.test",
                password = "Pxa-Customer-Password-42!",
            });
        Assert.Equal(HttpStatusCode.OK, (await accountClient.SendAsync(login)).StatusCode);
    }

    private static async Task<DesignerHandoff> CreateHandoffAsync(
        HttpClient accountClient,
        string returnPath)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var challenge = WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var create = await CreateCsrfRequestAsync(
            accountClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/designer-handoff",
            new
            {
                designerOrigin = "http://localhost:5176",
                returnPath,
                codeChallenge = challenge,
                state,
            });
        var response = await accountClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redirectUrl = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("redirectUrl")
            .GetString()!;
        var callback = QueryHelpers.ParseQuery(new Uri(redirectUrl).Query);
        return new DesignerHandoff(callback["code"].ToString(), state, verifier);
    }

    private static async Task<HttpRequestMessage> CreateExchangeRequestAsync(
        HttpClient designerClient,
        DesignerHandoff handoff,
        string? verifier = null,
        string requestOrigin = "http://localhost:5176") =>
        await CreateCsrfRequestAsync(
            designerClient,
            HttpMethod.Post,
            "/api/pxa/v1/auth/designer-handoff/exchange",
            new
            {
                code = handoff.Code,
                state = handoff.State,
                codeVerifier = verifier ?? handoff.Verifier,
                designerOrigin = "http://localhost:5176",
            },
            designer: true,
            designerOrigin: requestOrigin);

    private static async Task AssertExchangeDeniedAsync(
        WebApplicationFactory<Program> factory,
        DesignerHandoff handoff,
        string expectedCode)
    {
        using var client = CreateClient(factory);
        using var exchange = await CreateExchangeRequestAsync(client, handoff);
        var response = await client.SendAsync(exchange);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            expectedCode,
            (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code")
            .GetString());
    }

    private static async Task ExchangeSuccessfullyAsync(
        HttpClient client,
        DesignerHandoff handoff)
    {
        using var exchange = await CreateExchangeRequestAsync(client, handoff);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(exchange)).StatusCode);
    }

    private static async Task<Guid> ExchangeAndFindSessionAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        DesignerHandoff handoff)
    {
        HashSet<Guid> before;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            before = await scope.ServiceProvider.GetRequiredService<PxaDbContext>()
                .UserSessions
                .Select(value => value.Id)
                .ToHashSetAsync();
        }

        await ExchangeSuccessfullyAsync(client, handoff);

        await using var afterScope = factory.Services.CreateAsyncScope();
        var created = await afterScope.ServiceProvider.GetRequiredService<PxaDbContext>()
            .UserSessions
            .Where(value => !before.Contains(value.Id))
            .Select(value => value.Id)
            .ToArrayAsync();
        return Assert.Single(created);
    }

    private static DesignerTemplate CreateTemplate(
        Guid id,
        SeededCustomer customer,
        string name,
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            OrganizationId = customer.OrganizationId,
            CreatedByUserId = customer.UserId,
            UpdatedByUserId = customer.UserId,
            Name = name,
            DraftJson = "{}",
            DraftChecksum = Hash("{}"),
            SchemaVersion = "1.0",
            DesignerVersion = "1.0",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };

    private static async Task<JsonElement> GetTemplatePageAsync(
        HttpClient client,
        int page,
        int pageSize)
    {
        using var request = DesignerGet(
            $"/api/pxa/v1/designer/templates?page={page}&pageSize={pageSize}");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Guid[] ReadTemplateIds(JsonElement page) =>
        page.GetProperty("items")
            .EnumerateArray()
            .Select(value => value.GetProperty("id").GetGuid())
            .ToArray();

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;

    private static async Task<Guid> AddEntitledOrganizationAsync(
        IServiceProvider services,
        Guid userId,
        string name,
        string slug)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var organization = new Organization { Name = name, Slug = slug };
        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = userId,
        };
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            Edition = SubscriptionEdition.Premium,
            AccountType = SubscriptionAccountType.Company,
            Status = SubscriptionStatus.Active,
            BillingPeriod = SubscriptionBillingPeriod.Monthly,
            DeploymentMode = SubscriptionDeploymentMode.Cloud,
        };
        dbContext.AddRange(organization, membership, subscription);
        dbContext.SubscriptionEntitlements.Add(new SubscriptionEntitlement
        {
            SubscriptionId = subscription.Id,
            Capability = "designer",
            Enabled = true,
            Source = EntitlementSource.EditionDefault,
        });
        await dbContext.SaveChangesAsync();
        return organization.Id;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<HttpRequestMessage> CreateCsrfRequestAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object body,
        bool designer = false,
        string designerOrigin = "http://localhost:5176")
    {
        using var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/pxa/v1/auth/csrf");
        if (designer)
            csrfRequest.Headers.Add("X-PXA-Application", "designer");
        var csrfResponse = await client.SendAsync(csrfRequest);
        var csrf = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-PXA-CSRF", csrf);
        if (designer)
        {
            request.Headers.Add("X-PXA-Application", "designer");
            request.Headers.Add("Origin", designerOrigin);
        }
        return request;
    }

    private sealed record SeededCustomer(Guid UserId, Guid OrganizationId);
    private sealed record DesignerHandoff(string Code, string State, string Verifier);
}
