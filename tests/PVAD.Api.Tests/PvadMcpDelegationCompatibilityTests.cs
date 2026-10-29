using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadMcpDelegationCompatibilityTests
{
    private const string Secret = "synthetic-test-only-delegation-key-32-plus-bytes";

    [Theory]
    [InlineData("PVAD-MCP", "pvad-mcp-server", "pvad-api", "pvad.templates.read", true)]
    [InlineData("PXA-MCP", "pxa-mcp-server", "pxa-api", "pxa.templates.read", true)]
    [InlineData("PXA-MCP", "pvad-mcp-server", "pxa-api", "pxa.templates.read", true)]
    [InlineData("PVAD-MCP", "pxa-mcp-server", "pxa-api", "pxa.templates.read", false)]
    [InlineData("PXA-MCP", "pvad-mcp-server", "pvad-api", "pxa.templates.read", false)]
    [InlineData("PVAD-MCP", "pvad-mcp-server", "pvad-api", "pvad.templates.write", false)]
    [InlineData("PVAD-MCP", "pvad-mcp-server", "pvad-api", "pvad.unknown", false)]
    public async Task Signed_envelopes_accept_only_explicit_identity_generations_and_read_only_scopes(
        string prefix, string issuer, string audience, string scope, bool allowed)
    {
        using var host = await CreateHost();
        var payload = Payload(issuer, audience, scope);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Authorization", Sign(prefix, JsonSerializer.Serialize(payload)));
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("long-lived")]
    [InlineData("duplicate")]
    [InlineData("tampered")]
    [InlineData("oversized")]
    public async Task Invalid_delegations_fail_closed(string variant)
    {
        using var host = await CreateHost();
        var payload = Payload("pvad-mcp-server", "pvad-api", "pvad.templates.read");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (variant == "expired") payload["exp"] = now - 1;
        if (variant == "future") payload["iat"] = now + 30;
        if (variant == "long-lived") payload["exp"] = now + 120;
        var json = JsonSerializer.Serialize(payload);
        if (variant == "duplicate") json = json[..^1] + ",\"Org\":\"ambiguous\"}";
        var authorization = Sign("PVAD-MCP", json);
        if (variant == "tampered") authorization = authorization[..^3] + "invalid";
        if (variant == "oversized") authorization = "PVAD-MCP " + new string('a', 17_000);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Authorization", authorization);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Dictionary<string, object> Payload(string issuer, string audience, string scope)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new()
        {
            ["iss"] = issuer, ["aud"] = audience, ["sub"] = Guid.NewGuid().ToString(),
            ["org"] = Guid.NewGuid().ToString(), ["scope"] = new[] { scope },
            ["iat"] = now, ["exp"] = now + 30, ["jti"] = Guid.NewGuid().ToString(),
        };
    }

    private static string Sign(string prefix, string json)
    {
        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Encode(Encoding.UTF8.GetBytes(json));
        var signature = Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.ASCII.GetBytes(payload)));
        return $"{prefix} {payload}.{signature}";
    }

    private static async Task<IHost> CreateHost() => await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.Configure<PvadOAuthOptions>(options => options.McpDelegationSecret = Secret);
            services.AddAuthentication(PvadAuthenticationSchemes.McpDelegation)
                .AddScheme<AuthenticationSchemeOptions, PvadMcpDelegationAuthenticationHandler>(PvadAuthenticationSchemes.McpDelegation, _ => { });
        }).Configure(app =>
        {
            app.UseAuthentication();
            app.Run(context =>
            {
                context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true &&
                    context.User.HasClaim(claim => claim.Type == PvadClaimTypes.ActiveOrganization) &&
                    PvadOAuthPolicies.HasScope(context.User, PvadOAuthScopes.TemplatesRead) ? 204 : 401;
                return Task.CompletedTask;
            });
        })).StartAsync();
}
