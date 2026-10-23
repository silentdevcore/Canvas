using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Controllers;

[ApiController]
public sealed class OAuthController(
    PvadDbContext dbContext,
    UserManager<PvadIdentityUser> userManager,
    IPvadEntitlementService entitlementService,
    IOptions<PvadOAuthOptions> options) : ControllerBase
{
    private readonly PvadOAuthOptions settings = options.Value;

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth authorization request is unavailable.");
        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            var returnUrl = new Uri(
                new Uri(settings.Issuer.TrimEnd('/') + "/"),
                $"{Request.PathBase}{Request.Path}{Request.QueryString}");
            return Redirect($"{settings.Issuer.TrimEnd('/')}/login?returnUrl={Uri.EscapeDataString(returnUrl.ToString())}");
        }

        var access = await ResolveInteractiveAccessAsync(authentication.Principal, request.GetScopes(), cancellationToken);
        if (!access.Allowed)
            return OAuthForbid(access.Code!, access.Reason!);

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, access.UserId!.Value.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, access.DisplayName!);
        identity.AddClaim(PvadClaimTypes.Organization, access.OrganizationId!.Value.ToString());
        identity.AddClaim(PvadClaimTypes.ActiveOrganization, access.OrganizationId.Value.ToString());
        identity.AddClaim(PvadClaimTypes.Session, access.SessionId!.Value.ToString());
        identity.SetDestinations(_ => [OpenIddictConstants.Destinations.AccessToken]);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(settings.McpResource);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OAuth token request is unavailable.");
        if (request.IsClientCredentialsGrantType())
            return await ExchangeServiceAccountAsync(request, cancellationToken);
        if (!request.IsAuthorizationCodeGrantType())
            return OAuthForbid(
                OpenIddictConstants.Errors.UnsupportedGrantType,
                "Only authorization_code and client_credentials grants are enabled.");

        var authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
            return OAuthForbid(OpenIddictConstants.Errors.InvalidGrant, "The authorization code is invalid.");

        var principal = authentication.Principal;
        if (!PvadClaimCompatibility.TryNormalize(principal))
            return OAuthForbid(OpenIddictConstants.Errors.InvalidGrant, "The authorization context is inconsistent.");
        if (!Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(PvadClaimTypes.Organization), out var organizationId) ||
            !Guid.TryParse(principal.FindFirstValue(PvadClaimTypes.Session), out var sessionId))
        {
            return OAuthForbid(OpenIddictConstants.Errors.InvalidGrant, "The authorization is incomplete.");
        }

        var access = await ValidateUserContextAsync(
            userId, organizationId, sessionId, principal.GetScopes(), cancellationToken);
        if (!access.Allowed)
            return OAuthForbid(OpenIddictConstants.Errors.InvalidGrant, access.Reason!);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> ExchangeServiceAccountAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseServiceAccountClientId(request.ClientId, out var apiKeyId))
            return OAuthForbid(OpenIddictConstants.Errors.InvalidClient, "The OAuth client is invalid.");

        var now = DateTimeOffset.UtcNow;
        var access = await (
            from key in dbContext.ApiKeys
            join account in dbContext.ServiceAccounts on key.ServiceAccountId equals account.Id
            join organization in dbContext.Organizations on key.OrganizationId equals organization.Id
            where key.Id == apiKeyId && key.OAuthClientId == request.ClientId && key.RevokedAt == null &&
                  (key.ExpiresAt == null || key.ExpiresAt > now) &&
                  account.IsActive && account.RevokedAt == null &&
                  organization.Status == OrganizationStatus.Active
            select new
            {
                Key = key,
                AccountId = account.Id,
                AccountName = account.Name,
                OrganizationId = organization.Id,
            }).SingleOrDefaultAsync(cancellationToken);
        if (access is null)
            return OAuthForbid(OpenIddictConstants.Errors.InvalidGrant, "The service account is unavailable.");

        var scopes = request.GetScopes();
        if (!scopes.Any() || scopes.Any(scope => !PvadScopeCompatibility.IsReadOnly(scope)))
            return OAuthForbid(
                OpenIddictConstants.Errors.InvalidScope,
                "At least one supported read-only PVAD scope is required.");

        if (PvadScopeCompatibility.Contains(scopes, PvadOAuthScopes.TemplatesRead))
        {
            var entitlement = await entitlementService.EvaluateAsync(
                access.OrganizationId, "designer", cancellationToken: cancellationToken);
            if (!entitlement.Allowed)
                return OAuthForbid(OpenIddictConstants.Errors.AccessDenied, entitlement.Reason);
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, access.AccountId.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, access.AccountName);
        identity.AddClaim(PvadClaimTypes.Organization, access.OrganizationId.ToString());
        identity.AddClaim(PvadClaimTypes.ActiveOrganization, access.OrganizationId.ToString());
        identity.AddClaim(PvadClaimTypes.ServiceAccount, access.AccountId.ToString());
        identity.SetDestinations(_ => [OpenIddictConstants.Destinations.AccessToken]);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(settings.McpResource);
        access.Key.LastUsedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static bool TryParseServiceAccountClientId(string? clientId, out Guid apiKeyId)
    {
        const string prefix = "pxa-sa-";
        apiKeyId = Guid.Empty;
        return clientId?.StartsWith(prefix, StringComparison.Ordinal) == true &&
               Guid.TryParseExact(clientId[prefix.Length..], "N", out apiKeyId);
    }

    private async Task<InteractiveAccess> ResolveInteractiveAccessAsync(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(PvadClaimTypes.ActiveOrganization), out var organizationId) ||
            !PvadSessionService.TryGetSessionId(principal, out var sessionId))
        {
            return InteractiveAccess.Denied(OpenIddictConstants.Errors.AccessDenied, "The Account session is incomplete.");
        }
        return await ValidateUserContextAsync(userId, organizationId, sessionId, scopes, cancellationToken);
    }

    private async Task<InteractiveAccess> ValidateUserContextAsync(
        Guid userId,
        Guid organizationId,
        Guid sessionId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not { IsActive: true, EmailConfirmed: true } || await userManager.IsLockedOutAsync(user))
            return InteractiveAccess.Denied(OpenIddictConstants.Errors.AccessDenied, "The account is unavailable.");

        var sessionActive = await dbContext.UserSessions.AsNoTracking().AnyAsync(value =>
            value.Id == sessionId && value.UserId == userId && value.OrganizationId == organizationId &&
            value.RevokedAt == null && value.ExpiresAt > now, cancellationToken);
        var membershipActive = await dbContext.OrganizationMemberships.AsNoTracking().AnyAsync(value =>
            value.UserId == userId && value.OrganizationId == organizationId &&
            value.Status == OrganizationMembershipStatus.Active, cancellationToken);
        var organizationActive = await dbContext.Organizations.AsNoTracking().AnyAsync(value =>
            value.Id == organizationId && value.Status == OrganizationStatus.Active, cancellationToken);
        if (!sessionActive || !membershipActive || !organizationActive)
            return InteractiveAccess.Denied(OpenIddictConstants.Errors.AccessDenied,
                "The Account session or organization is unavailable.");

        if (PvadScopeCompatibility.Contains(scopes, PvadOAuthScopes.TemplatesRead))
        {
            var entitlement = await entitlementService.EvaluateAsync(
                organizationId, "designer", cancellationToken: cancellationToken);
            if (!entitlement.Allowed)
                return InteractiveAccess.Denied(OpenIddictConstants.Errors.AccessDenied, entitlement.Reason);
        }
        return InteractiveAccess.Granted(user.Id, organizationId, sessionId, user.DisplayName);
    }

    private ForbidResult OAuthForbid(string error, string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }),
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private sealed record InteractiveAccess(
        bool Allowed,
        Guid? UserId,
        Guid? OrganizationId,
        Guid? SessionId,
        string? DisplayName,
        string? Code,
        string? Reason)
    {
        public static InteractiveAccess Granted(
            Guid userId, Guid organizationId, Guid sessionId, string displayName) =>
            new(true, userId, organizationId, sessionId, displayName, null, null);

        public static InteractiveAccess Denied(string code, string reason) =>
            new(false, null, null, null, null, code, reason);
    }
}
