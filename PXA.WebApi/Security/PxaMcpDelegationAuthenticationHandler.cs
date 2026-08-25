using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PXA.WebApi.Security;

public sealed class PxaMcpDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PxaOAuthOptions> oauthOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("PXA-MCP ", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var secret = oauthOptions.Value.McpDelegationSecret;
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            return Task.FromResult(AuthenticateResult.Fail("MCP delegation is not configured."));

        var compact = authorization["PXA-MCP ".Length..];
        var separator = compact.LastIndexOf('.');
        if (separator <= 0 || separator == compact.Length - 1)
            return Task.FromResult(AuthenticateResult.Fail("Invalid MCP delegation."));

        try
        {
            var payloadPart = compact[..separator];
            var suppliedSignature = Base64UrlDecode(compact[(separator + 1)..]);
            var expectedSignature = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(payloadPart));
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return Task.FromResult(AuthenticateResult.Fail("Invalid MCP delegation signature."));

            var payload = JsonSerializer.Deserialize<DelegationPayload>(
                Base64UrlDecode(payloadPart),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload is null || payload.Iss != "pxa-mcp-server" || payload.Aud != "pxa-api" ||
                payload.Iat > now + 10 || payload.Iat < now - 60 ||
                payload.Exp < now || payload.Exp > now + 60 ||
                !Guid.TryParse(payload.Sub, out _) || !Guid.TryParse(payload.Org, out _) ||
                payload.Scope is null || payload.Scope.Length == 0 ||
                payload.Scope.Except(PxaOAuthScopes.ReadOnly, StringComparer.Ordinal).Any())
            {
                return Task.FromResult(AuthenticateResult.Fail("Expired or invalid MCP delegation."));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, payload.Sub),
                new(PxaClaimTypes.Organization, payload.Org),
                new(PxaClaimTypes.ActiveOrganization, payload.Org),
            };
            claims.AddRange(payload.Scope.Select(scope => new Claim("scope", scope)));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or CryptographicException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid MCP delegation."));
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private sealed record DelegationPayload(
        string Iss,
        string Aud,
        string Sub,
        string Org,
        string[] Scope,
        long Iat,
        long Exp,
        string Jti);
}
