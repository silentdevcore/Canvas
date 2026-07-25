using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Entitlements;

namespace PXA.WebApi.Application.Identity;

public sealed class DesignerAuthorizationCodeService(
    PxaDbContext dbContext,
    UserManager<PxaIdentityUser> userManager,
    IUserClaimsPrincipalFactory<PxaIdentityUser> principalFactory,
    PxaSessionService sessionService,
    IPxaEntitlementService entitlementService,
    IOptions<PxaDesignerAuthenticationOptions> options)
{
    private readonly HashSet<string> allowedOrigins = options.Value.AllowedOrigins
        .Select(NormalizeOrigin)
        .Where(value => value is not null)
        .Select(value => value!)
        .ToHashSet(StringComparer.Ordinal);

    public async Task<DesignerHandoffResult> CreateAsync(
        ClaimsPrincipal principal,
        CreateDesignerHandoffRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryValidateOrigin(request.DesignerOrigin, out var designerOrigin) ||
            !TryValidateReturnPath(request.ReturnPath, out var returnPath) ||
            !IsValidPkceChallenge(request.CodeChallenge) ||
            !IsValidState(request.State) ||
            !PxaSessionService.TryGetSessionId(principal, out var sourceSessionId) ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue(PxaClaimTypes.ActiveOrganization), out var organizationId))
        {
            return DesignerHandoffResult.Invalid();
        }

        var now = DateTimeOffset.UtcNow;
        var user = await userManager.FindByIdAsync(userId.ToString());
        var sourceSession = await dbContext.UserSessions.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == sourceSessionId &&
                     value.UserId == userId &&
                     value.OrganizationId == organizationId &&
                     value.RevokedAt == null &&
                     value.ExpiresAt > now,
            cancellationToken);
        var membershipIsActive = await dbContext.OrganizationMemberships.AsNoTracking().AnyAsync(
            value => value.UserId == userId &&
                     value.OrganizationId == organizationId &&
                     value.Status == OrganizationMembershipStatus.Active,
            cancellationToken);
        var entitlement = await entitlementService.EvaluateAsync(
            organizationId, "designer", cancellationToken: cancellationToken);
        if (user is not { IsActive: true, EmailConfirmed: true } ||
            sourceSession is null ||
            !membershipIsActive ||
            !entitlement.Allowed)
        {
            return DesignerHandoffResult.Denied(entitlement.Code, entitlement.Reason);
        }

        var rawCode = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entity = new DesignerAuthorizationCode
        {
            UserId = userId,
            OrganizationId = organizationId,
            SourceSessionId = sourceSessionId,
            CodeHash = Hash(rawCode),
            StateHash = Hash(request.State),
            PkceChallenge = request.CodeChallenge,
            DesignerOrigin = designerOrigin,
            ReturnPath = returnPath,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(2),
        };
        dbContext.DesignerAuthorizationCodes.Add(entity);
        AddAudit(organizationId, userId, "security.designer-handoff.created", entity.Id, "succeeded");
        await dbContext.SaveChangesAsync(cancellationToken);

        var callback = new UriBuilder($"{designerOrigin}/auth/callback")
        {
            Query = QueryString.Create(
            [
                new KeyValuePair<string, string?>("code", rawCode),
                new KeyValuePair<string, string?>("state", request.State),
            ]).Value?.TrimStart('?'),
        };
        return DesignerHandoffResult.Created(callback.Uri.ToString());
    }

    public async Task<DesignerExchangeResult> ExchangeAsync(
        ExchangeDesignerHandoffRequest request,
        string? requestOrigin,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryValidateOrigin(request.DesignerOrigin, out var designerOrigin) ||
            !string.Equals(NormalizeOrigin(requestOrigin), designerOrigin, StringComparison.Ordinal) ||
            !IsValidState(request.State) ||
            !IsValidVerifier(request.CodeVerifier) ||
            string.IsNullOrWhiteSpace(request.Code))
        {
            return DesignerExchangeResult.Invalid();
        }

        var now = DateTimeOffset.UtcNow;
        var codeHash = Hash(request.Code);
        var entity = await dbContext.DesignerAuthorizationCodes.AsNoTracking().SingleOrDefaultAsync(
            value => value.CodeHash == codeHash &&
                     value.DesignerOrigin == designerOrigin &&
                     value.ConsumedAt == null &&
                     value.ExpiresAt > now,
            cancellationToken);
        if (entity is null ||
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(entity.StateHash),
                Convert.FromHexString(Hash(request.State))) ||
            !string.Equals(CreatePkceChallenge(request.CodeVerifier), entity.PkceChallenge, StringComparison.Ordinal))
        {
            return DesignerExchangeResult.Invalid();
        }

        var user = await userManager.FindByIdAsync(entity.UserId.ToString());
        var sourceSessionIsActive = await dbContext.UserSessions.AsNoTracking().AnyAsync(
            value => value.Id == entity.SourceSessionId &&
                     value.UserId == entity.UserId &&
                     value.OrganizationId == entity.OrganizationId &&
                     value.RevokedAt == null &&
                     value.ExpiresAt > now,
            cancellationToken);
        var membership = await dbContext.OrganizationMemberships.AsNoTracking().SingleOrDefaultAsync(
            value => value.UserId == entity.UserId &&
                     value.OrganizationId == entity.OrganizationId &&
                     value.Status == OrganizationMembershipStatus.Active,
            cancellationToken);
        var organizationIsActive = await dbContext.Organizations.AsNoTracking().AnyAsync(
            value => value.Id == entity.OrganizationId && value.Status == OrganizationStatus.Active,
            cancellationToken);
        var entitlement = await entitlementService.EvaluateAsync(
            entity.OrganizationId, "designer", cancellationToken: cancellationToken);
        if (user is not { IsActive: true, EmailConfirmed: true } ||
            !sourceSessionIsActive ||
            membership is null ||
            !organizationIsActive ||
            !entitlement.Allowed)
        {
            return DesignerExchangeResult.Denied(entitlement.Code, entitlement.Reason);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var consumed = await dbContext.DesignerAuthorizationCodes
            .Where(value => value.Id == entity.Id &&
                            value.ConsumedAt == null &&
                            value.ExpiresAt > now)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(value => value.ConsumedAt, now),
                cancellationToken);
        if (consumed != 1)
            return DesignerExchangeResult.Invalid();

        var expiresAt = now.AddHours(8);
        var designerSession = await sessionService.CreateAsync(
            user.Id, entity.OrganizationId, expiresAt, httpContext, cancellationToken);
        var principal = await CreateDesignerPrincipalAsync(
            user, membership.Id, entity.OrganizationId, designerSession.Id, cancellationToken);
        AddAudit(entity.OrganizationId, user.Id, "security.designer-handoff.exchanged",
            designerSession.Id, "succeeded");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DesignerExchangeResult.Exchanged(principal, expiresAt, entity.ReturnPath);
    }

    private async Task<ClaimsPrincipal> CreateDesignerPrincipalAsync(
        PxaIdentityUser user,
        Guid membershipId,
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var principal = await principalFactory.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var systemRole in identity.FindAll(identity.RoleClaimType)
                     .Where(value => value.Value == PxaRoles.SystemAdministrator)
                     .ToArray())
        {
            identity.RemoveClaim(systemRole);
        }

        identity.AddClaim(new Claim(PxaClaimTypes.Organization, organizationId.ToString()));
        identity.AddClaim(new Claim(PxaClaimTypes.ActiveOrganization, organizationId.ToString()));
        identity.AddClaim(new Claim(PxaClaimTypes.Session, sessionId.ToString()));
        var roles = await (from membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                           join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                           where membershipRole.OrganizationMembershipId == membershipId
                           select role.Name!)
            .ToListAsync(cancellationToken);
        foreach (var permission in roles
                     .SelectMany(role => PxaRoles.Permissions.GetValueOrDefault(role, []))
                     .Distinct(StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(PxaClaimTypes.Permission, permission));
        }
        return principal;
    }

    private bool TryValidateOrigin(string value, out string normalized)
    {
        normalized = NormalizeOrigin(value) ?? string.Empty;
        return allowedOrigins.Contains(normalized);
    }

    private static string? NormalizeOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return null;
        }
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static bool TryValidateReturnPath(string value, out string normalized)
    {
        normalized = value.Trim();
        return normalized.Length is > 0 and <= 2048 &&
               normalized.StartsWith('/') &&
               !normalized.StartsWith("//", StringComparison.Ordinal) &&
               !normalized.Contains('\r') &&
               !normalized.Contains('\n');
    }

    private static bool IsValidPkceChallenge(string value) =>
        value.Length is >= 43 and <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '~');

    private static bool IsValidVerifier(string value) => IsValidPkceChallenge(value);

    private static bool IsValidState(string value) =>
        value.Length is >= 32 and <= 256 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string CreatePkceChallenge(string verifier) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void AddAudit(
        Guid organizationId,
        Guid userId,
        string action,
        Guid targetId,
        string outcome)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = userId,
            Action = action,
            TargetType = "designer_session",
            TargetId = targetId.ToString(),
            Outcome = outcome,
            DetailsJson = JsonSerializer.Serialize(new { }),
        });
    }
}

public sealed record CreateDesignerHandoffRequest(
    string DesignerOrigin,
    string ReturnPath,
    string CodeChallenge,
    string State);

public sealed record ExchangeDesignerHandoffRequest(
    string Code,
    string State,
    string CodeVerifier,
    string DesignerOrigin);

public sealed record DesignerHandoffResult(
    bool Success,
    bool Forbidden,
    string? RedirectUrl,
    string? Code,
    string? Reason)
{
    public static DesignerHandoffResult Created(string redirectUrl) =>
        new(true, false, redirectUrl, null, null);
    public static DesignerHandoffResult Invalid() =>
        new(false, false, null, "PXA_DESIGNER_HANDOFF_INVALID", "The Designer handoff request is invalid.");
    public static DesignerHandoffResult Denied(string code, string reason) =>
        new(false, true, null, code, reason);
}

public sealed record DesignerExchangeResult(
    bool Success,
    bool Forbidden,
    ClaimsPrincipal? Principal,
    DateTimeOffset? ExpiresAt,
    string? ReturnPath,
    string? Code,
    string? Reason)
{
    public static DesignerExchangeResult Exchanged(
        ClaimsPrincipal principal,
        DateTimeOffset expiresAt,
        string returnPath) =>
        new(true, false, principal, expiresAt, returnPath, null, null);
    public static DesignerExchangeResult Invalid() =>
        new(false, false, null, null, null, "PXA_DESIGNER_HANDOFF_INVALID",
            "The Designer handoff is invalid or expired.");
    public static DesignerExchangeResult Denied(string code, string reason) =>
        new(false, true, null, null, null, code, reason);
}
