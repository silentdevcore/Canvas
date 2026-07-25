using Microsoft.AspNetCore.WebUtilities;
using PXA.WebApi.Application.Identity;

namespace PXA.Api.Tests;

public sealed class DesignerAuthorizationSecurityTests
{
    [Theory]
    [InlineData("https://designer.powerdoxautomation.com", "https://designer.powerdoxautomation.com")]
    [InlineData("http://localhost:5176", "http://localhost:5176")]
    [InlineData("HTTP://LOCALHOST:5176", "http://localhost:5176")]
    public void NormalizeOrigin_accepts_only_plain_http_origins(string value, string expected)
    {
        Assert.Equal(expected, DesignerAuthorizationSecurity.NormalizeOrigin(value));
    }

    [Theory]
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
