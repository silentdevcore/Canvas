using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Legal;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Entitlements;

namespace PXA.WebApi.Application.Identity;

public sealed class DesignerAuthorizationCodeService(
    PxaDbContext dbContext,
    UserManager<PxaIdentityUser> userManager,
    IUserClaimsPrincipalFactory<PxaIdentityUser> principalFactory,
    PxaSessionService sessionService,
    IPxaEntitlementService entitlementService,
    AccountLegalObligationService legalObligations,
    IOptions<PxaDesignerAuthenticationOptions> options)
{
    private readonly HashSet<string> allowedOrigins = options.Value.AllowedOrigins
        .Select(DesignerAuthorizationSecurity.NormalizeOrigin)
        .Where(value => value is not null)
        .Select(value => value!)
        .ToHashSet(StringComparer.Ordinal);

    public async Task<DesignerHandoffResult> CreateAsync(
        ClaimsPrincipal principal,
        CreateDesignerHandoffRequest request,
        CancellationToken cancellationToken)
    {
        var hasSession = PxaSessionService.TryGetSessionId(principal, out var sourceSessionId);
        var hasUser = Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var hasOrganization = Guid.TryParse(
            principal.FindFirstValue(PxaClaimTypes.ActiveOrganization), out var organizationId);
        if (!TryValidateOrigin(request.DesignerOrigin, out var designerOrigin) ||
            !DesignerAuthorizationSecurity.TryValidateReturnPath(request.ReturnPath, out var returnPath) ||
            !DesignerAuthorizationSecurity.IsValidPkceChallenge(request.CodeChallenge) ||
            !DesignerAuthorizationSecurity.IsValidState(request.State) ||
            !hasSession ||
            !hasUser ||
            !hasOrganization)
        {
            if (hasUser && hasOrganization)
            {
                AddAudit(
                    organizationId,
                    userId,
                    "security.designer-handoff.created",
                    hasSession ? sourceSessionId : Guid.Empty,
                    "rejected",
                    "invalid-request");
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return DesignerHandoffResult.Invalid();
        }

        var now = DateTimeOffset.UtcNow;
        var user = await userManager.FindByIdAsync(userId.ToString());
        var userIsLocked = user is not null && await userManager.IsLockedOutAsync(user);
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
        var legalReviewRequired = false;
        if (user is not null)
        {
            var obligations = await legalObligations.ResolveAsync(
                user, organizationId, cancellationToken);
            legalReviewRequired =
                !obligations.Available ||
                obligations.Terms?.ActionRequired == true ||
                obligations.Privacy?.ActionRequired == true;
        }
        if (user is not { IsActive: true, EmailConfirmed: true } ||
            userIsLocked ||
            sourceSession is null ||
            !membershipIsActive ||
            !entitlement.Allowed ||
            legalReviewRequired)
        {
            if (legalReviewRequired)
            {
                AddAudit(
                    organizationId,
                    userId,
                    "security.designer-handoff.created",
                    sourceSessionId,
                    "rejected",
                    "legal-review-required");
                await dbContext.SaveChangesAsync(cancellationToken);
                return DesignerHandoffResult.Denied(
                    "PXAAPI017",
                    "Review the current legal documents in PXA Account before opening Designer.");
            }
            var denial = ResolveAccessDenial(
                user, userIsLocked, sourceSession is not null, membershipIsActive, true, entitlement);
            AddAudit(
                organizationId,
                userId,
                "security.designer-handoff.created",
                sourceSessionId,
                "rejected",
                denial.Code);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DesignerHandoffResult.Denied(denial.Code, denial.Reason);
        }

        var rawCode = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entity = new DesignerAuthorizationCode
        {
            UserId = userId,
            OrganizationId = organizationId,
            SourceSessionId = sourceSessionId,
            CodeHash = DesignerAuthorizationSecurity.Hash(rawCode),
            StateHash = DesignerAuthorizationSecurity.Hash(request.State),
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
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 256)
            return DesignerExchangeResult.Invalid();

        var now = DateTimeOffset.UtcNow;
        var codeHash = DesignerAuthorizationSecurity.Hash(request.Code);
        var entity = await dbContext.DesignerAuthorizationCodes.AsNoTracking().SingleOrDefaultAsync(
            value => value.CodeHash == codeHash,
            cancellationToken);
        if (entity is null)
            return DesignerExchangeResult.Invalid();

        if (!TryValidateOrigin(request.DesignerOrigin, out var designerOrigin) ||
            !string.Equals(
                DesignerAuthorizationSecurity.NormalizeOrigin(requestOrigin),
                designerOrigin,
                StringComparison.Ordinal) ||
            !DesignerAuthorizationSecurity.IsValidState(request.State) ||
            !DesignerAuthorizationSecurity.IsValidVerifier(request.CodeVerifier) ||
            !string.Equals(entity.DesignerOrigin, designerOrigin, StringComparison.Ordinal) ||
            entity.ConsumedAt is not null ||
            entity.ExpiresAt <= now)
        {
            await AddRejectedExchangeAuditAsync(entity, "invalid-or-expired", cancellationToken);
            return DesignerExchangeResult.Invalid();
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(entity.StateHash),
                Convert.FromHexString(DesignerAuthorizationSecurity.Hash(request.State))) ||
            !string.Equals(
                DesignerAuthorizationSecurity.CreatePkceChallenge(request.CodeVerifier),
                entity.PkceChallenge,
                StringComparison.Ordinal))
        {
            await AddRejectedExchangeAuditAsync(entity, "state-or-pkce-invalid", cancellationToken);
            return DesignerExchangeResult.Invalid();
        }

        var user = await userManager.FindByIdAsync(entity.UserId.ToString());
        var userIsLocked = user is not null && await userManager.IsLockedOutAsync(user);
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
            userIsLocked ||
            !sourceSessionIsActive ||
            membership is null ||
            !organizationIsActive ||
            !entitlement.Allowed)
        {
            var denial = ResolveAccessDenial(
                user,
                userIsLocked,
                sourceSessionIsActive,
                membership is not null,
                organizationIsActive,
                entitlement);
            AddAudit(
                entity.OrganizationId,
                entity.UserId,
                "security.designer-handoff.exchanged",
                entity.Id,
                "rejected",
                denial.Code);
            await dbContext.SaveChangesAsync(cancellationToken);
            return DesignerExchangeResult.Denied(denial.Code, denial.Reason);
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
            entity.Id, "succeeded");
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
        normalized = DesignerAuthorizationSecurity.NormalizeOrigin(value) ?? string.Empty;
        return allowedOrigins.Contains(normalized);
    }

    private async Task AddRejectedExchangeAuditAsync(
        DesignerAuthorizationCode entity,
        string reason,
        CancellationToken cancellationToken)
    {
        AddAudit(
            entity.OrganizationId,
            entity.UserId,
            "security.designer-handoff.exchanged",
            entity.Id,
            "rejected",
            reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (string Code, string Reason) ResolveAccessDenial(
        PxaIdentityUser? user,
        bool userIsLocked,
        bool sessionIsActive,
        bool membershipIsActive,
        bool organizationIsActive,
        PxaEntitlementDecision entitlement)
    {
        if (user is null or { IsActive: false })
            return ("PXA_DESIGNER_ACCOUNT_DISABLED", "The account is disabled.");
        if (userIsLocked)
            return ("PXA_DESIGNER_ACCOUNT_LOCKED", "The account is temporarily locked.");
        if (!user.EmailConfirmed)
            return ("PXA_DESIGNER_VERIFICATION_REQUIRED", "Verify the account email before using PXA Designer.");
        if (!sessionIsActive)
            return ("PXA_DESIGNER_SESSION_EXPIRED", "The Account session is expired or revoked.");
        if (!membershipIsActive)
            return ("PXA_DESIGNER_MEMBERSHIP_INACTIVE", "The organization membership is not active.");
        if (!organizationIsActive)
            return ("PXA_ORGANIZATION_INACTIVE", "The organization is not active.");
        return (entitlement.Code, entitlement.Reason);
    }

    private void AddAudit(
        Guid organizationId,
        Guid userId,
        string action,
        Guid targetId,
        string outcome,
        string? reason = null)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = userId,
            Action = action,
            TargetType = "designer_handoff",
            TargetId = targetId.ToString(),
            Outcome = outcome,
            DetailsJson = JsonSerializer.Serialize(new { Reason = reason }),
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
