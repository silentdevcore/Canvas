using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
public sealed class OAuthClientRegistrationController(
    IServiceProvider services,
    IOptions<PvadOAuthOptions> options) : ControllerBase
{
    private readonly PvadOAuthOptions settings = options.Value;

    [HttpPost("~/connect/register")]
    [EnableRateLimiting("oauth-client-registration")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> Register(
        [FromBody] DynamicClientRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!settings.Enabled || !settings.DynamicClientRegistrationEnabled)
            return NotFound();

        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();

        var validationError = Validate(request);
        if (validationError is not null)
            return BadRequest(new DynamicClientRegistrationError("invalid_client_metadata", validationError));

        var redirectUris = request.RedirectUris!;
        var clientId = "pvad-mcp-dcr-" + Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = request.ClientName!.Trim(),
        };
        foreach (var redirectUri in redirectUris)
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            .. PvadScopeCompatibility.ReadOnlyScopes.Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope),
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        await applicationManager.CreateAsync(descriptor, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new DynamicClientRegistrationResponse(
            clientId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            request.ClientName.Trim(),
            redirectUris,
            "none",
            ["authorization_code"],
            ["code"]));
    }

    private string? Validate(DynamicClientRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName) || request.ClientName.Trim().Length > 100)
            return "client_name must contain between 1 and 100 characters.";
        if (request.RedirectUris is not { Count: > 0 } ||
            request.RedirectUris.Count > settings.DynamicClientMaximumRedirectUris ||
            request.RedirectUris.Distinct(StringComparer.Ordinal).Count() != request.RedirectUris.Count)
            return $"redirect_uris must contain between 1 and {settings.DynamicClientMaximumRedirectUris} unique values.";
        if (request.TokenEndpointAuthMethod is not null and not "none")
            return "Only public clients using token_endpoint_auth_method 'none' are supported.";
        if (request.GrantTypes is not null &&
            (request.GrantTypes.Count != 1 || request.GrantTypes[0] != "authorization_code"))
            return "Only the authorization_code grant is supported.";
        if (request.ResponseTypes is not null &&
            (request.ResponseTypes.Count != 1 || request.ResponseTypes[0] != "code"))
            return "Only the code response type is supported.";
        return request.RedirectUris.FirstOrDefault(value => !IsAllowedRedirectUri(value)) is { } invalid
            ? $"Redirect URI '{invalid}' must use HTTPS or loopback HTTP and contain no credentials or fragment."
            : null;
    }

    private static bool IsAllowedRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            return false;
        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;
        return uri.Scheme == Uri.UriSchemeHttp &&
               (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record DynamicClientRegistrationRequest(
    [property: JsonPropertyName("client_name")] string? ClientName,
    [property: JsonPropertyName("redirect_uris")] List<string>? RedirectUris,
    [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod,
    [property: JsonPropertyName("grant_types")] List<string>? GrantTypes,
    [property: JsonPropertyName("response_types")] List<string>? ResponseTypes);

public sealed record DynamicClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_id_issued_at")] long ClientIdIssuedAt,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string> GrantTypes,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string> ResponseTypes);

public sealed record DynamicClientRegistrationError(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
