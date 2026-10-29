using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PVAD.WebApi.Security;

public sealed class PvadMcpDelegationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PvadOAuthOptions> oauthOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    public static bool HasDelegationPrefix(string value) =>
        value.StartsWith("PVAD-MCP ", StringComparison.Ordinal) || value.StartsWith("PXA-MCP ", StringComparison.Ordinal);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!HasDelegationPrefix(authorization))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (authorization.Length > 16_384)
            return Task.FromResult(AuthenticateResult.Fail("MCP delegation exceeds its size limit."));
        var canonical = authorization.StartsWith("PVAD-MCP ", StringComparison.Ordinal);

        var secret = oauthOptions.Value.McpDelegationSecret;
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            return Task.FromResult(AuthenticateResult.Fail("MCP delegation is not configured."));

        var compact = authorization[(canonical ? "PVAD-MCP ".Length : "PXA-MCP ".Length)..];
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

            var payloadBytes = Base64UrlDecode(payloadPart);
            using var json = JsonDocument.Parse(payloadBytes);
            if (json.RootElement.ValueKind != JsonValueKind.Object || json.RootElement.EnumerateObject()
                .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                return Task.FromResult(AuthenticateResult.Fail("Ambiguous MCP delegation."));
            var payload = JsonSerializer.Deserialize<DelegationPayload>(
                payloadBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validIdentity = payload is not null && (canonical
                ? payload.Iss == "pvad-mcp-server" && payload.Aud == "pvad-api"
                : payload.Aud == "pxa-api" && payload.Iss is "pxa-mcp-server" or "pvad-mcp-server");
            if (payload is null || !validIdentity ||
                payload.Iat > now + 10 || payload.Iat < now - 60 ||
                payload.Exp <= now || payload.Exp > now + 60 || payload.Exp <= payload.Iat || payload.Exp - payload.Iat > 60 ||
                !Guid.TryParse(payload.Sub, out _) || !Guid.TryParse(payload.Org, out _) ||
                payload.Scope is null || payload.Scope.Length is 0 or > 16 ||
                payload.Scope.Any(scope => !PvadScopeCompatibility.IsReadOnly(scope)) ||
                string.IsNullOrWhiteSpace(payload.Jti) || payload.Jti.Length > 200)
            {
                return Task.FromResult(AuthenticateResult.Fail("Expired or invalid MCP delegation."));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, payload.Sub),
                new(PvadClaimTypes.Organization, payload.Org),
                new(PvadClaimTypes.ActiveOrganization, payload.Org),
            };
            claims.AddRange(payload.Scope.Select(PvadScopeCompatibility.Canonical).Distinct(StringComparer.Ordinal)
                .Select(scope => new Claim("scope", scope)));
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
