using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadApiKeyHeaderIntegrationTests
{
    public static IEnumerable<object[]> KeyCases()
    {
        foreach (var header in new[] { "X-PVAD-API-Key", "X-PXA-API-Key", "Authorization" })
        foreach (var legacy in new[] { false, true })
        foreach (var state in new[] { "active", "revoked", "expired", "disabled-service", "suspended-organization", "closed-organization" })
            yield return [header, legacy, state];
    }

    [Theory]
    [MemberData(nameof(KeyCases))]
    public async Task Both_header_names_use_the_same_real_key_hash_revocation_and_tenant_checks(string header, bool legacy, string state)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddAuthentication(PvadAuthenticationSchemes.ApiKey)
                    .AddScheme<AuthenticationSchemeOptions, PvadApiKeyAuthenticationHandler>(PvadAuthenticationSchemes.ApiKey, _ => { });
            })
            .Configure(app =>
            {
                app.UseMiddleware<PvadSecurityHeaderMiddleware>();
                app.UseAuthentication();
                app.Run(async context =>
                {
                    if (context.User.Identity?.IsAuthenticated != true) { context.Response.StatusCode = 401; return; }
                    await context.Response.WriteAsync(context.User.FindFirst(PvadClaimTypes.ActiveOrganization)!.Value);
                });
            })).StartAsync();

        var organization = new Organization { Name = "Synthetic", Slug = "synthetic" };
        var account = new ServiceAccount { Name = "Synthetic service", OrganizationId = organization.Id };
        var generated = PvadApiKeySecret.Create();
        Assert.StartsWith("pvad_", generated.Secret);
        var value = legacy ? generated.Secret.Replace("pvad_", "pxa_", StringComparison.Ordinal) : generated.Secret;
        var secret = (Secret: value, Prefix: legacy ? generated.Prefix.Replace("pvad_", "pxa_", StringComparison.Ordinal) : generated.Prefix,
            Hash: PvadApiKeySecret.Hash(value));
        var key = new ApiKey { Name = "Synthetic key", OrganizationId = organization.Id,
            ServiceAccountId = account.Id, Prefix = secret.Prefix, SecretHash = secret.Hash };
        if (state == "revoked") key.RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        if (state == "expired") key.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        if (state == "disabled-service") account.IsActive = false;
        if (state == "suspended-organization") organization.Status = OrganizationStatus.Suspended;
        if (state == "closed-organization") organization.Status = OrganizationStatus.Closed;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            db.AddRange(organization, account, key);
            await db.SaveChangesAsync();
        }
        using var client = host.GetTestClient();
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/"))
        {
            request.Headers.Add(header, header == "Authorization" ? $"Bearer {secret.Secret}" : secret.Secret);
            using var result = await client.SendAsync(request);
            Assert.Equal(state == "active" ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, result.StatusCode);
            if (state == "active")
                Assert.Equal(organization.Id.ToString(), await result.Content.ReadAsStringAsync());
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var saved = await db.ApiKeys.SingleAsync();
            if (state == "active") Assert.NotNull(saved.LastUsedAt);
            else Assert.Null(saved.LastUsedAt);
            saved.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/"))
        {
            request.Headers.Add(header, header == "Authorization" ? $"Bearer {secret.Secret}" : secret.Secret);
            using var result = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/"))
        {
            request.Headers.Add(header, header == "Authorization" ? "Bearer pvad_synthetic.invalid" : "pvad_synthetic.invalid");
            using var result = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        }
    }
}

public sealed class PvadApiKeySchemeCompatibilityTests(PvadApiTestFactory factory)
    : IClassFixture<PvadApiTestFactory>
{
    [Theory]
    [InlineData("PVAD-MCP synthetic.invalid")]
    [InlineData("PXA-MCP synthetic.invalid")]
    public void Combined_authentication_selects_the_delegation_handler_for_both_envelope_generations(string authorization)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = authorization;
        var options = factory.Services.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(PvadAuthenticationSchemes.Combined);
        Assert.Equal(PvadAuthenticationSchemes.McpDelegation, options.ForwardDefaultSelector!(context));
    }

    [Theory]
    [InlineData("Bearer pvad_synthetic.invalid")]
    [InlineData("Bearer pxa_synthetic.invalid")]
    public void Combined_authentication_routes_both_key_generations_to_the_key_handler(string authorization)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = authorization;
        var options = factory.Services.GetRequiredService<IOptionsMonitor<PolicySchemeOptions>>()
            .Get(PvadAuthenticationSchemes.Combined);
        Assert.Equal(PvadAuthenticationSchemes.ApiKey, options.ForwardDefaultSelector!(context));
    }
}
