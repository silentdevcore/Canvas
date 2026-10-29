using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadCorsConfigurationTests
{
    [Fact]
    public void Missing_configuration_preserves_development_defaults()
    {
        var origins = PvadCorsConfiguration.ResolveOrigins(new ConfigurationBuilder().Build());
        Assert.Equal(8, origins.Length);
        Assert.Contains("http://localhost:5178", origins);
    }

    [Fact]
    public void Explicit_origins_replace_defaults_and_normalize_trailing_slashes()
    {
        var origins = PvadCorsConfiguration.ResolveOrigins(Configuration(
            "https://account.pvade.example/", "https://account.pvade.example", "https://designer.pvade.example"));
        Assert.Equal(new[] { "https://account.pvade.example", "https://designer.pvade.example" }, origins);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.test")]
    [InlineData("null")]
    [InlineData("file:///tmp/project")]
    [InlineData("https://user:private@example.test")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test/?private=value")]
    [InlineData("https://example.test/#fragment")]
    [InlineData(" https://example.test")]
    [InlineData("https://example.test\n")]
    [InlineData("https://example.test\\evil")]
    public void Invalid_origins_fail_without_echoing_values(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PvadCorsConfiguration.ResolveOrigins(Configuration(value)));
        Assert.Equal("Invalid Cors:AllowedOrigins entry.", error.Message);
    }

    [Fact]
    public void Empty_or_unbounded_configurations_are_rejected()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "",
        }).Build();
        Assert.Throws<InvalidOperationException>(() => PvadCorsConfiguration.ResolveOrigins(empty));
        Assert.Throws<InvalidOperationException>(() => PvadCorsConfiguration.ResolveOrigins(
            Configuration(Enumerable.Range(0, 65).Select(index => $"https://host{index}.example.test").ToArray())));
    }

    [Theory]
    [InlineData("/api/pvad/v1/version")]
    [InlineData("/api/pxa/v1/version")]
    public async Task Actual_api_accepts_only_the_configured_origin_for_both_route_generations(string path)
    {
        using var factory = new PvadApiTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://designer.pvade.example" })));
        using var client = factory.CreateClient();
        foreach (var origin in new[] { "https://designer.pvade.example", "http://localhost:5173", "https://designer.pvade.example.evil.test", "null" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, path);
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            if (origin == "https://designer.pvade.example")
            {
                Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
                Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            }
            else Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    private static IConfiguration Configuration(params string[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.Select((value, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", value))).Build();
}
