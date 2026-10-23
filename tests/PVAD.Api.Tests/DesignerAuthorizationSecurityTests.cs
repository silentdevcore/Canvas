using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using PVAD.WebApi.Application.Identity;

namespace PVAD.Api.Tests;

public sealed class DesignerAuthorizationSecurityTests
{
    [Theory]
    [InlineData("https://designer.pvade.com", "https://designer.pvade.com")]
    [InlineData("https://designer.powerdoxautomation.com", "https://designer.powerdoxautomation.com")]
    [InlineData("http://localhost:5176", "http://localhost:5176")]
    [InlineData("HTTP://LOCALHOST:5176", "http://localhost:5176")]
    public void NormalizeOrigin_accepts_only_plain_http_origins(string value, string expected)
    {
        Assert.Equal(expected, DesignerAuthorizationSecurity.NormalizeOrigin(value));
    }

    [Theory]
    [InlineData("https://designer.pvade.com/path")]
    [InlineData("https://user@designer.pvade.com")]
    [InlineData("https://designer.pvade.com?next=evil")]
    [InlineData("https://designer.pvade.com#fragment")]
    [InlineData("https://designer.powerdoxautomation.com/path")]
    [InlineData("https://user@designer.powerdoxautomation.com")]
    [InlineData("https://designer.powerdoxautomation.com?next=evil")]
    [InlineData("https://designer.powerdoxautomation.com#fragment")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//designer.powerdoxautomation.com")]
    public void NormalizeOrigin_rejects_values_that_are_not_origins(string value)
    {
        Assert.Null(DesignerAuthorizationSecurity.NormalizeOrigin(value));
    }

    [Fact]
    public void Repository_defaults_bind_explicit_product_origins_without_legacy_or_administrator_trust()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(directory.FullName, "PVAD.WebApi", "appsettings.json"))
            .Build();
        var registration = configuration.GetSection(PvadRegistrationOptions.SectionName)
            .Get<PvadRegistrationOptions>()!;
        var designer = configuration.GetSection(PvadDesignerAuthenticationOptions.SectionName)
            .Get<PvadDesignerAuthenticationOptions>()!;

        Assert.Equal(new[]
        {
            "http://localhost:5173", "http://localhost:5174", "http://localhost:5175",
            "http://localhost:5176", "http://localhost:5178",
            "https://pvade.com", "https://docs.pvade.com", "https://demos.pvade.com",
            "https://designer.pvade.com", "https://account.pvade.com",
        }, registration.AllowedReturnOrigins);
        Assert.Equal(new[]
        {
            "http://localhost:5173", "http://localhost:5176", "https://designer.pvade.com",
        }, designer.AllowedOrigins);
        foreach (var origin in registration.AllowedReturnOrigins.Concat(designer.AllowedOrigins))
            Assert.Equal(origin, DesignerAuthorizationSecurity.NormalizeOrigin(origin));

        foreach (var untrusted in new[]
        {
            "https://admin.pvade.com", "https://operator.pvade.com",
            "https://designer.powerdoxautomation.com", "https://designer.pvade.com.evil.test",
            "https://pvade.com.evil.test", "http://designer.pvade.com",
            "https://designer.pvade.com:444", "https://*.pvade.com",
        })
        {
            Assert.DoesNotContain(untrusted, registration.AllowedReturnOrigins);
            Assert.DoesNotContain(untrusted, designer.AllowedOrigins);
        }
    }

    [Theory]
    [InlineData("/pdf/create?mode=code#editor", "/pdf/create?mode=code#editor")]
    [InlineData(" /spreadsheet/create ", "/spreadsheet/create")]
    public void ReturnPath_accepts_and_normalizes_local_destinations(string value, string expected)
    {
        Assert.True(DesignerAuthorizationSecurity.TryValidateReturnPath(value, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://external.example/path")]
    [InlineData("//external.example/path")]
    [InlineData("/pdf/create\r\nLocation: https://external.example")]
    public void ReturnPath_rejects_external_or_malformed_destinations(string value)
    {
        Assert.False(DesignerAuthorizationSecurity.TryValidateReturnPath(value, out _));
    }

    [Fact]
    public void Pkce_state_and_hash_contracts_are_stable()
    {
        const string verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var challenge = DesignerAuthorizationSecurity.CreatePkceChallenge(verifier);
        var expectedChallenge = WebEncoders.Base64UrlEncode(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.ASCII.GetBytes(verifier)));

        Assert.Equal(expectedChallenge, challenge);
        Assert.True(DesignerAuthorizationSecurity.IsValidVerifier(verifier));
        Assert.True(DesignerAuthorizationSecurity.IsValidPkceChallenge(challenge));
        Assert.False(DesignerAuthorizationSecurity.IsValidVerifier("too-short"));
        Assert.False(DesignerAuthorizationSecurity.IsValidPkceChallenge(new string('a', 129)));
        Assert.True(DesignerAuthorizationSecurity.IsValidState(new string('s', 32)));
        Assert.False(DesignerAuthorizationSecurity.IsValidState(new string('s', 31)));
        Assert.False(DesignerAuthorizationSecurity.IsValidState($"{new string('s', 31)}+"));
        Assert.Equal(
            DesignerAuthorizationSecurity.Hash("same-value"),
            DesignerAuthorizationSecurity.Hash("same-value"));
        Assert.NotEqual(
            DesignerAuthorizationSecurity.Hash("same-value"),
            DesignerAuthorizationSecurity.Hash("different-value"));
        Assert.DoesNotContain("same-value", DesignerAuthorizationSecurity.Hash("same-value"));
    }
}
