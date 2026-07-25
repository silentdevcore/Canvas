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
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class IdentityMailFlowTests
{
    [PostgreSqlFact]
    public async Task Invitation_and_password_reset_are_queued_secure_single_use_flows()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());
        await SeedAsync(factory.Services);
        using var adminClient = CreateClient(factory);
        await LoginAsync(adminClient, "admin@pxa.test", "Pxa-Admin-Integration-42!", HttpStatusCode.OK);

        var adminCsrf = await GetCsrfAsync(adminClient);
        using var invitation = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/admin/invitations",
            adminCsrf,
            new
            {
                email = "invited@pxa.test",
                displayName = "Invited User",
                roles = new[] { PxaRoles.Editor },
            });
        var invitationResponse = await adminClient.SendAsync(invitation);
        Assert.Equal(HttpStatusCode.Accepted, invitationResponse.StatusCode);
        var invitedUserId = (await invitationResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("userId").GetGuid();

        var transport = factory.Services.GetRequiredService<DevelopmentMailTransport>();
        await ProcessMailAsync(factory.Services);
        var invitationMail = await WaitForMessageAsync(transport, "identity.invitation");
        var invitationToken = GetToken(invitationMail.TextBody);

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var storedToken = await dbContext.IdentityActionTokens.SingleAsync(value =>
                value.Purpose == IdentityActionTokenService.InvitationPurpose);
            Assert.DoesNotContain(invitationToken, storedToken.TokenHash, StringComparison.Ordinal);
            var outbox = await dbContext.MailOutboxMessages.SingleAsync(value =>
                value.TemplateKey == "identity.invitation");
            Assert.DoesNotContain(invitationToken, outbox.ProtectedPayload, StringComparison.Ordinal);
        }

        using var invitedClient = CreateClient(factory);
        var invitationCsrf = await GetCsrfAsync(invitedClient);
        using var acceptInvitation = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/accept-invitation",
            invitationCsrf,
            new { token = invitationToken, password = "Pxa-Invited-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await invitedClient.SendAsync(acceptInvitation)).StatusCode);

        invitationCsrf = await GetCsrfAsync(invitedClient);
        using var reuseInvitation = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/accept-invitation",
            invitationCsrf,
            new { token = invitationToken, password = "Pxa-Another-Password-42!" });
        Assert.Equal(HttpStatusCode.BadRequest, (await invitedClient.SendAsync(reuseInvitation)).StatusCode);
        await LoginAsync(invitedClient, "invited@pxa.test", "Pxa-Invited-Password-42!", HttpStatusCode.OK);

        using var resetClient = CreateClient(factory);
        var resetCsrf = await GetCsrfAsync(resetClient);
        using var unknownReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/request",
            resetCsrf,
            new { email = "missing@pxa.test" });
        Assert.Equal(HttpStatusCode.Accepted, (await resetClient.SendAsync(unknownReset)).StatusCode);

        resetCsrf = await GetCsrfAsync(resetClient);
        using var requestReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/request",
            resetCsrf,
            new { email = "invited@pxa.test" });
        Assert.Equal(HttpStatusCode.Accepted, (await resetClient.SendAsync(requestReset)).StatusCode);
        await ProcessMailAsync(factory.Services);
        var resetMail = await WaitForMessageAsync(transport, "identity.password-reset");
        Assert.Contains("https://account.pxa.test/reset-password", resetMail.TextBody, StringComparison.Ordinal);
        var resetToken = GetToken(resetMail.TextBody);

        resetCsrf = await GetCsrfAsync(resetClient);
        using var confirmReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/confirm",
            resetCsrf,
            new { token = resetToken, newPassword = "Pxa-Reset-Password-42!" });
        Assert.Equal(HttpStatusCode.NoContent, (await resetClient.SendAsync(confirmReset)).StatusCode);
        await ProcessMailAsync(factory.Services);
        await LoginAsync(CreateClient(factory), "invited@pxa.test", "Pxa-Reset-Password-42!", HttpStatusCode.OK);

        resetCsrf = await GetCsrfAsync(resetClient);
        using var reuseReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/confirm",
            resetCsrf,
            new { token = resetToken, newPassword = "Pxa-Reused-Password-42!" });
        Assert.Equal(HttpStatusCode.BadRequest, (await resetClient.SendAsync(reuseReset)).StatusCode);

        resetCsrf = await GetCsrfAsync(resetClient);
        using var requestAdminReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/admin/password-reset/request",
            resetCsrf,
            new { email = "invited@pxa.test" });
        Assert.Equal(HttpStatusCode.Accepted, (await resetClient.SendAsync(requestAdminReset)).StatusCode);
        await ProcessMailAsync(factory.Services);
        var adminResetMail = transport.Messages.Last(value =>
            value.Subject.Contains("Reset", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("https://admin.pxa.test/reset-password", adminResetMail.TextBody, StringComparison.Ordinal);

        string expiredResetToken;
        await using (var expiredTokenScope = factory.Services.CreateAsyncScope())
        {
            var tokens = expiredTokenScope.ServiceProvider.GetRequiredService<IdentityActionTokenService>();
            var issued = await tokens.IssueAsync(
                invitedUserId,
                null,
                "invited@pxa.test",
                IdentityActionTokenService.PasswordResetPurpose,
                new { },
                TimeSpan.FromMinutes(-1),
                CancellationToken.None);
            expiredResetToken = issued.RawToken;
            await expiredTokenScope.ServiceProvider.GetRequiredService<PxaDbContext>().SaveChangesAsync();
        }
        resetCsrf = await GetCsrfAsync(resetClient);
        using var expiredReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/confirm",
            resetCsrf,
            new { token = expiredResetToken, newPassword = "Pxa-Expired-Password-42!" });
        Assert.Equal(HttpStatusCode.BadRequest, (await resetClient.SendAsync(expiredReset)).StatusCode);

        var mailPage = await adminClient.GetAsync("/api/pxa/v1/admin/mail?status=Delivered");
        Assert.Equal(HttpStatusCode.OK, mailPage.StatusCode);
        var mailJson = await mailPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("protectedPayload", mailJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invitationToken, mailJson, StringComparison.Ordinal);
        Assert.DoesNotContain(resetToken, mailJson, StringComparison.Ordinal);
        Assert.DoesNotContain("invited@pxa.test", mailJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("i***@p***.test", mailJson, StringComparison.Ordinal);

        Guid retryMessageId;
        await using (var retryScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = retryScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var failedMessage = await dbContext.MailOutboxMessages.FirstAsync();
            failedMessage.Status = MailDeliveryStatus.DeadLetter;
            failedMessage.FailureReason = "Integration-test failure";
            failedMessage.Attempts = 5;
            retryMessageId = failedMessage.Id;
            await dbContext.SaveChangesAsync();
        }

        using var retryRequest = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/mail/{retryMessageId}/retry",
            await GetCsrfAsync(adminClient),
            new { });
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(retryRequest)).StatusCode);

        resetCsrf = await GetCsrfAsync(resetClient);
        using var cancellableReset = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/password-reset/request",
            resetCsrf,
            new { email = "invited@pxa.test" });
        Assert.Equal(HttpStatusCode.Accepted, (await resetClient.SendAsync(cancellableReset)).StatusCode);

        Guid cancelMessageId;
        await using (var cancelScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = cancelScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            cancelMessageId = await dbContext.MailOutboxMessages
                .Where(message => message.Status == MailDeliveryStatus.Pending && message.Id != retryMessageId)
                .OrderByDescending(message => message.CreatedAt)
                .Select(message => message.Id)
                .FirstAsync();
        }
        using var cancelRequest = CreateCsrfRequest(
            HttpMethod.Post,
            $"/api/pxa/v1/admin/mail/{cancelMessageId}/cancel",
            await GetCsrfAsync(adminClient),
            new { });
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(cancelRequest)).StatusCode);

        await using (var actionScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = actionScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            Assert.Equal(MailDeliveryStatus.Pending,
                (await dbContext.MailOutboxMessages.FindAsync(retryMessageId))!.Status);
            Assert.Equal(MailDeliveryStatus.Cancelled,
                (await dbContext.MailOutboxMessages.FindAsync(cancelMessageId))!.Status);
            Assert.Contains(await dbContext.AuditEvents.ToListAsync(), audit =>
                audit.Action == "mail.retry" && audit.TargetId == retryMessageId.ToString());
            Assert.Contains(await dbContext.AuditEvents.ToListAsync(), audit =>
                audit.Action == "mail.cancel" && audit.TargetId == cancelMessageId.ToString());
            Assert.Contains(await dbContext.AuditEvents.ToListAsync(), audit =>
                audit.Action == "invitations.create" && audit.TargetId == invitedUserId.ToString());
        }

        var statusResponse = await adminClient.GetFromJsonAsync<JsonElement>("/api/pxa/v1/admin/mail/status");
        Assert.Equal("Development", statusResponse.GetProperty("transport").GetString());
        Assert.True(statusResponse.GetProperty("deliveryEnabled").GetBoolean());
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
                    ["Mail:AdminBaseUrl"] = "https://admin.pxa.test",
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

    private static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        await dbContext.Database.MigrateAsync();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<PxaIdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PxaIdentityUser>>();
        foreach (var roleName in PxaRoles.Permissions.Keys)
        {
            var roleResult = await roleManager.CreateAsync(new PxaIdentityRole { Name = roleName, IsSystemRole = true });
            Assert.True(roleResult.Succeeded);
        }

        var organization = new Organization { Name = "Mail Tenant", Slug = "mail-tenant" };
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();
        var admin = new PxaIdentityUser
        {
            UserName = "admin@pxa.test",
            Email = "admin@pxa.test",
            EmailConfirmed = true,
            DisplayName = "Mail Administrator",
        };
        Assert.True((await userManager.CreateAsync(admin, "Pxa-Admin-Integration-42!")).Succeeded);
        var membership = new OrganizationMembership { OrganizationId = organization.Id, UserId = admin.Id };
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();
        var roleId = await dbContext.Roles.Where(role => role.Name == PxaRoles.OrganizationAdministrator)
            .Select(role => role.Id).SingleAsync();
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = roleId,
            AssignedByUserId = admin.Id,
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<RenderedMail> WaitForMessageAsync(
        DevelopmentMailTransport transport,
        string templateMarker)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var message = transport.Messages.LastOrDefault(value =>
                templateMarker == "identity.invitation"
                    ? value.Subject.Contains("invitation", StringComparison.OrdinalIgnoreCase)
                    : value.Subject.Contains("Reset", StringComparison.OrdinalIgnoreCase));
            if (message is not null)
                return message;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Mail {templateMarker} was not delivered.");
    }

    private static async Task ProcessMailAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
            .ProcessPendingAsync(CancellationToken.None);
    }

    private static string GetToken(string textBody)
    {
        var marker = "token=";
        var start = textBody.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        return Uri.UnescapeDataString(textBody[(start + marker.Length)..].Trim());
    }

    private static async Task LoginAsync(
        HttpClient client,
        string email,
        string password,
        HttpStatusCode expected)
    {
        using var request = CreateCsrfRequest(
            HttpMethod.Post,
            "/api/pxa/v1/auth/login",
            await GetCsrfAsync(client),
            new { identifier = email, password });
        Assert.Equal(expected, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<JsonElement>("/api/pxa/v1/auth/csrf");
        return response.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage CreateCsrfRequest(
        HttpMethod method,
        string path,
        string csrfToken,
        object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-PXA-CSRF", csrfToken);
        return request;
    }
}
