using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Identity;
using PXA.WebApi.Application.Legal;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PxaAccountPermissions.ProfileManage)]
[Route("api/pxa/v1/account/profile")]
public sealed class AccountProfileController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPxaMailQueue mailQueue;
    private readonly PxaMailOptions mailOptions;
    private readonly PxaRegistrationOptions registrationOptions;
    private readonly AccountLegalObligationService legalObligations;

    public AccountProfileController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        UserManager<PxaIdentityUser> userManager,
        IdentityActionTokenService actionTokens,
        IPxaMailQueue mailQueue,
        IOptions<PxaMailOptions> mailOptions,
        IOptions<PxaRegistrationOptions> registrationOptions,
        AccountLegalObligationService legalObligations)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.userManager = userManager;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
        this.registrationOptions = registrationOptions.Value;
        this.legalObligations = legalObligations;
    }

    [HttpGet]
    public async Task<ActionResult<AccountProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        var roles = await GetActiveOrganizationRolesAsync(userId.Value, cancellationToken);
        return Ok(await ToResponseAsync(user, roles, cancellationToken));
    }

    [HttpPatch("display-name")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.profile.display-name")]
    public async Task<ActionResult<AccountProfileResponse>> UpdateDisplayName(
        UpdateDisplayNameRequest request,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var displayName = request.DisplayName.Trim();
        if (displayName.Length is < 2 or > 200)
            return ValidationProblem("Display name must contain between 2 and 200 characters.");

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        user.DisplayName = displayName;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(NewAuditEvent(user.Id, "account.profile.display-name-updated"));
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetActiveOrganizationRolesAsync(userId.Value, cancellationToken);
        return Ok(await ToResponseAsync(user, roles, cancellationToken));
    }

    [HttpPatch("locale")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.profile.locale")]
    public async Task<ActionResult<AccountProfileResponse>> UpdateLocale(
        UpdateLocaleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var locale = request.Locale.Trim();
        if (locale.Length is < 2 or > 16)
            return ValidationProblem("Locale must contain between 2 and 16 characters.");

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        user.Locale = locale;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(NewAuditEvent(user.Id, "account.profile.locale-updated"));
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetActiveOrganizationRolesAsync(userId.Value, cancellationToken);
        return Ok(await ToResponseAsync(user, roles, cancellationToken));
    }

    [HttpPost("email-change/request")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.profile.email-change-request")]
    [EnableRateLimiting("identity-action")]
    public async Task<IActionResult> RequestEmailChange(
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var newEmail = request.NewEmail.Trim();
        if (!new EmailAddressAttribute().IsValid(newEmail))
            return ValidationProblem("Provide a valid email address.");

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        // Enumeration-safe: an authenticated customer could otherwise probe whether an
        // arbitrary address is already registered by watching for a distinct conflict
        // response, so every outcome (self, taken, free) returns the same generic reply.
        if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase) &&
            await userManager.FindByEmailAsync(newEmail) is null)
        {
            user.PendingEmail = newEmail;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            var issued = await actionTokens.IssueAsync(
                user.Id,
                tenantContext.OrganizationId,
                newEmail,
                IdentityActionTokenService.EmailChangePurpose,
                new { },
                TimeSpan.FromHours(24),
                cancellationToken);
            var actionUrl = $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/confirm-email?token={Uri.EscapeDataString(issued.RawToken)}";
            mailQueue.Enqueue(
                tenantContext.OrganizationId,
                user.Id,
                newEmail,
                "identity.email-verification",
                new { displayName = user.DisplayName, actionUrl },
                $"account-email-change:{issued.Entity.Id}");
            dbContext.AuditEvents.Add(NewAuditEvent(user.Id, "account.profile.email-change-requested"));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Accepted(new AccountProfileActionAccepted(
            "If the address is available, a confirmation message will be sent shortly."));
    }

    [HttpPost("password-change")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.profile.password-change")]
    [EnableRateLimiting("identity-action")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
        {
            return ValidationProblem(string.Join(" ", changeResult.Errors.Select(error => error.Description)));
        }

        var sessions = await dbContext.UserSessions
            .Where(value => value.UserId == user.Id && value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            session.RevokedByUserId = user.Id;
            session.RevocationReason = "password-change";
        }
        user.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(NewAuditEvent(user.Id, "account.profile.password-changed", new
        {
            RevokedSessions = sessions.Count,
        }));
        mailQueue.Enqueue(
            tenantContext.OrganizationId,
            user.Id,
            user.Email!,
            "identity.password-changed",
            new { displayName = user.DisplayName },
            $"account-password-changed:{Guid.NewGuid()}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("consent")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.profile.consent")]
    public async Task<ActionResult<AccountProfileResponse>> UpdateConsent(
        UpdateAccountConsentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM identity.users WHERE \"Id\" = {userId.Value} FOR UPDATE",
                cancellationToken);
        }
        await using var transactionScope = transaction;

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        var obligations = await legalObligations.ResolveAsync(
            user, tenantContext.OrganizationId, cancellationToken);
        if (!obligations.Available || obligations.Terms is null || obligations.Privacy is null)
            return LegalPolicyUnavailable();
        if (obligations.Terms.ActionRequired &&
            (request.AcceptTerms != true || request.TermsVersionId != obligations.Terms.Id))
        {
            return StaleLegalPolicy(
                "The exact current Terms version must be accepted.");
        }
        if (obligations.Privacy.ActionRequired &&
            (request.AcceptPrivacy != true || request.PrivacyVersionId != obligations.Privacy.Id))
        {
            return StaleLegalPolicy(
                "The exact current Privacy notice must be acknowledged.");
        }

        var now = DateTimeOffset.UtcNow;
        if (obligations.Terms.ActionRequired && request.AcceptTerms == true)
        {
            user.TermsAcceptedVersion = obligations.Terms.Version;
            user.TermsAcceptedAt = now;
            AddLegalAcceptance(
                user.Id, obligations.Terms, "TermsAndConditions", "accepted", now);
            dbContext.UserConsentEvents.Add(NewConsentEvent(
                user.Id, "terms", "accepted", obligations.Terms.Version, now));
        }
        if (obligations.Privacy.ActionRequired && request.AcceptPrivacy == true)
        {
            user.PrivacyAcknowledgedVersion = obligations.Privacy.Version;
            user.PrivacyAcknowledgedAt = now;
            AddLegalAcceptance(
                user.Id, obligations.Privacy, "PrivacyNotice", "acknowledged", now);
            dbContext.UserConsentEvents.Add(NewConsentEvent(
                user.Id, "privacy", "acknowledged", obligations.Privacy.Version, now));
        }

        var marketingGranted = user.MarketingConsentGrantedAt is not null &&
                               user.MarketingConsentWithdrawnAt is null;
        if (request.MarketingConsent != marketingGranted)
        {
            if (request.MarketingConsent)
            {
                user.MarketingConsentGrantedAt = now;
                user.MarketingConsentWithdrawnAt = null;
            }
            else
            {
                user.MarketingConsentWithdrawnAt = now;
            }
            user.MarketingConsentSource = "account-profile";
            dbContext.UserConsentEvents.Add(NewConsentEvent(
                user.Id, "marketing", request.MarketingConsent ? "granted" : "withdrawn", null, now));
        }

        user.UpdatedAt = now;
        dbContext.AuditEvents.Add(NewAuditEvent(user.Id, "account.profile.consent-updated", new
        {
            TermsVersion = user.TermsAcceptedVersion,
            PrivacyVersion = user.PrivacyAcknowledgedVersion,
            request.MarketingConsent,
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        var roles = await GetActiveOrganizationRolesAsync(user.Id, cancellationToken);
        return Ok(await ToResponseAsync(user, roles, cancellationToken));
    }

    private async Task<IReadOnlyList<string>> GetActiveOrganizationRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return [];

        var membershipId = await dbContext.OrganizationMemberships.AsNoTracking()
            .Where(value => value.UserId == userId && value.OrganizationId == organizationId)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (membershipId is null)
            return [];

        return await (from membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                      join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                      where membershipRole.OrganizationMembershipId == membershipId
                      select role.Name!)
            .ToListAsync(cancellationToken);
    }

    private AuditEvent NewAuditEvent(Guid userId, string action, object? details = null) => new()
    {
        OrganizationId = tenantContext.OrganizationId,
        ActorUserId = userId,
        Action = action,
        TargetType = "user",
        TargetId = userId.ToString(),
        Outcome = "succeeded",
        DetailsJson = details is null ? null : System.Text.Json.JsonSerializer.Serialize(details),
    };

    private UserConsentEvent NewConsentEvent(
        Guid userId,
        string consentType,
        string decision,
        string? policyVersion,
        DateTimeOffset createdAt) =>
        new()
        {
            UserId = userId,
            ConsentType = consentType,
            Decision = decision,
            PolicyVersion = policyVersion,
            Source = "account-profile",
            CreatedAt = createdAt,
        };

    private async Task<AccountProfileResponse> ToResponseAsync(
        PxaIdentityUser user,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        var obligations = await legalObligations.ResolveAsync(
            user, tenantContext.OrganizationId, cancellationToken);
        return new AccountProfileResponse(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.PendingEmail,
            user.Locale,
            user.Country,
            roles,
            user.TermsAcceptedVersion,
            obligations.Terms?.Id,
            obligations.Terms?.Version ?? registrationOptions.TermsVersion,
            obligations.Terms?.ChangeSummary,
            obligations.Terms?.PreviousVersion,
            obligations.Terms?.ActionRequired ?? true,
            user.PrivacyAcknowledgedVersion,
            obligations.Privacy?.Id,
            obligations.Privacy?.Version ?? registrationOptions.PrivacyVersion,
            obligations.Privacy?.ChangeSummary,
            obligations.Privacy?.PreviousVersion,
            obligations.Privacy?.ActionRequired ?? true,
            obligations.Available,
            user.MarketingConsentGrantedAt is not null &&
            user.MarketingConsentWithdrawnAt is null);
    }

    private void AddLegalAcceptance(
        Guid userId,
        AccountLegalObligationDocument document,
        string documentType,
        string decision,
        DateTimeOffset createdAt)
    {
        if (document.Id is null || document.ContentHash is null)
            return;
        dbContext.LegalAcceptanceEvents.Add(new LegalAcceptanceEvent
        {
            UserId = userId,
            OrganizationId = tenantContext.OrganizationId,
            LegalDocumentVersionId = document.Id.Value,
            DocumentType = documentType,
            Decision = decision,
            ContentHash = document.ContentHash,
            Locale = document.Locale,
            Source = "account-legal-review",
            CreatedAt = createdAt,
        });
    }

    private ObjectResult StaleLegalPolicy(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Legal document version changed",
        detail: detail,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "PXAAPI017",
        });

    private ObjectResult LegalPolicyUnavailable() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Legal documents are temporarily unavailable",
        detail: "The current legal document versions could not be verified.");

    private ActionResult ValidationProblem(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid profile update",
        detail: detail);
}

public sealed record AccountProfileResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string? PendingEmail,
    string Locale,
    string? Country,
    IReadOnlyList<string> Roles,
    string? TermsAcceptedVersion,
    Guid? CurrentTermsVersionId,
    string CurrentTermsVersion,
    string? CurrentTermsChangeSummary,
    string? PreviousTermsVersion,
    bool RequiresTermsAcceptance,
    string? PrivacyAcknowledgedVersion,
    Guid? CurrentPrivacyVersionId,
    string CurrentPrivacyVersion,
    string? CurrentPrivacyChangeSummary,
    string? PreviousPrivacyVersion,
    bool RequiresPrivacyAcknowledgement,
    bool LegalPolicyAvailable,
    bool MarketingConsent);

public sealed record UpdateDisplayNameRequest([Required] string DisplayName);

public sealed record UpdateLocaleRequest([Required] string Locale);

public sealed record RequestEmailChangeRequest([Required, EmailAddress] string NewEmail);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required] string NewPassword);

public sealed record UpdateAccountConsentRequest(
    bool? AcceptTerms,
    bool? AcceptPrivacy,
    bool MarketingConsent,
    Guid? TermsVersionId = null,
    Guid? PrivacyVersionId = null);

public sealed record AccountProfileActionAccepted(string Message);
