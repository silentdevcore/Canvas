using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Services.Mail;

namespace PVAD.Api.Tests;

public sealed class PvadDocumentHeaderCompatibilityTests
{
    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://localhost:5177", true)]
    [InlineData("http://localhost:5178", true)]
    [InlineData("https://untrusted.example.invalid", false)]
    [InlineData("http://localhost:5177.attacker.example.invalid", false)]
    [InlineData("null", false)]
    public async Task ActualCorsPolicy_PreservesOriginBoundariesOnBothRouteGenerations(string origin, bool allowed)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        foreach (var prefix in new[] { "pvad", "pxa" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, $"/api/{prefix}/v1/version");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            using var response = await client.SendAsync(request);
            Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
            if (allowed)
            {
                Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
                Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
            }
        }
    }

    [Fact]
    public async Task ActualCorsPolicy_ExposesCanonicalAndLegacyDocumentHeaders()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/pvad/v1/version");
        request.Headers.Add("Origin", "http://localhost:5176");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var exposed = response.Headers.GetValues("Access-Control-Expose-Headers")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries)).ToArray();
        Assert.All(PvadResponseHeaders.ExposedHeaders, header =>
            Assert.Contains(header, exposed, StringComparer.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> DocumentHeaders =>
        PvadResponseHeaders.DocumentSuffixes.Select(suffix => new object[] { suffix });

    [Theory]
    [MemberData(nameof(DocumentHeaders))]
    public void DocumentHeaders_WriteEqualAliasesAndExposeBothToBrowsers(string suffix)
    {
        var context = new DefaultHttpContext();
        PvadResponseHeaders.SetDocument(context.Response, suffix, "synthetic-value");
        Assert.Equal("synthetic-value", context.Response.Headers[$"PVAD-{suffix}"].ToString());
        Assert.Equal("synthetic-value", context.Response.Headers[$"PXA-{suffix}"].ToString());
        Assert.Contains($"PVAD-{suffix}", PvadResponseHeaders.ExposedHeaders);
        Assert.Contains($"PXA-{suffix}", PvadResponseHeaders.ExposedHeaders);
    }

    [Fact]
    public void ExposedHeaders_AreUniqueAndDoNotExposeCredentialHeaders()
    {
        var headers = PvadResponseHeaders.ExposedHeaders;
        Assert.Equal(headers.Length, headers.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain("X-PVAD-API-Key", headers);
        Assert.DoesNotContain("X-PVAD-CSRF", headers);
        Assert.DoesNotContain("Set-Cookie", headers);
    }

    [Fact]
    public void SmtpMessage_RetainsLegacyOutboxHeaderAndAddsCanonicalAlias()
    {
        var id = Guid.NewGuid();
        var transport = new SmtpMailTransport(Options.Create(new PvadMailOptions
        {
            SenderAddress = "sender@example.invalid", SenderName = "PVADE",
        }));
        using var mail = transport.CreateMessage(new RenderedMail(id, "recipient@example.invalid",
            "Synthetic subject", "<p>Content</p>", "Content"));
        Assert.Equal(id.ToString(), mail.Headers["X-PVAD-Outbox-Id"]);
        Assert.Equal(mail.Headers["X-PVAD-Outbox-Id"], mail.Headers["X-PXA-Outbox-Id"]);
        Assert.Equal("Synthetic subject", mail.Subject);
        Assert.Single(mail.AlternateViews);
        Assert.Equal("recipient@example.invalid", Assert.Single(mail.To).Address);
    }
}
