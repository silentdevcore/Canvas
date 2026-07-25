using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Application.Identity;

/// <summary>
/// Orchestrates registration and email verification as a single use case:
/// validation, duplicate/slug checks, user + organization + Trial creation,
/// token issuance, mail enqueue, and audit — owning the transaction so
/// <c>AccountRegistrationController</c> stays a thin request/response mapper.
/// </summary>
public sealed class CustomerRegistrationService(
    PxaDbContext dbContext,
    UserManager<PxaIdentityUser> userManager,
    IdentityActionTokenService actionTokens,
    IPxaMailQueue mailQueue,
    IOptions<PxaMailOptions> mailOptions,
    IOptions<PxaRegistrationOptions> registrationOptions,
    TrialActivationService trialActivation)
{
    private readonly PxaMailOptions mailOptions = mailOptions.Value;
    private readonly PxaRegistrationOptions registrationOptions = registrationOptions.Value;

    public async Task<CustomerRegistrationOutcome> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var validation = RegistrationValidation.Validate(request);
        if (!validation.IsValid)
            return CustomerRegistrationOutcome.Invalid(validation.Error!);

        if (await userManager.FindByEmailAsync(validation.Email) is not null)
            return CustomerRegistrationOutcome.Accepted();

        if (validation.RequestedSlug is not null && await dbContext.Organizations.AnyAsync(
                value => value.Slug == validation.RequestedSlug, cancellationToken))
        {
            return CustomerRegistrationOutcome.SlugConflict();
        }

        var administratorRole = await dbContext.Roles.SingleOrDefaultAsync(
            value => value.Name == PxaRoles.OrganizationAdministrator,
            cancellationToken);
        if (administratorRole is null)
            return CustomerRegistrationOutcome.Unavailable();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var user = new PxaIdentityUser
        {
            UserName = validation.Email,
            Email = validation.Email,
            EmailConfirmed = false,
            DisplayName = validation.DisplayName,
            IsActive = true,
            Locale = validation.Locale,
            Country = validation.Country,
            TermsAcceptedVersion = registrationOptions.TermsVersion,
            TermsAcceptedAt = now,
            PrivacyAcknowledgedVersion = registrationOptions.PrivacyVersion,
            PrivacyAcknowledgedAt = now,
            MarketingConsentGrantedAt = request.SubscribeToNewsletter == true ? now : null,
            MarketingConsentSource = request.SubscribeToNewsletter == true ? "registration" : null,
        };
        IdentityResult creation;
        try
        {
            creation = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException)
        {
            // A concurrent request for the same email won the race between our
            // FindByEmailAsync pre-check above and this insert - Identity's EF store
            // does not always convert the resulting unique-constraint violation into
            // a graceful "Duplicate" IdentityError, so the exception itself is the
            // signal. Treated the same as the pre-check duplicate path: enumeration-safe.
            return CustomerRegistrationOutcome.Accepted();
        }
        if (!creation.Succeeded)
        {
            if (creation.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
                return CustomerRegistrationOutcome.Accepted();
            return CustomerRegistrationOutcome.InvalidIdentity(creation);
        }

        var organization = new Organization
        {
            Name = validation.AccountType == SubscriptionAccountType.Company
                ? validation.CompanyName!
                : $"{validation.DisplayName}'s workspace",
            Slug = validation.RequestedSlug ?? $"developer-{user.Id:N}"[..26],
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
        };
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = administratorRole.Id,
            AssignedByUserId = user.Id,
        });

        trialActivation.CreatePendingTrialForNewOrganization(organization, validation.AccountType, now);

        var issued = await actionTokens.IssueAsync(
            user.Id,
            organization.Id,
            validation.Email,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            new { organizationId = organization.Id },
            TimeSpan.FromHours(24),
            cancellationToken);
        var actionUrl = BuildVerificationUrl(issued.RawToken, request.ReturnUrl);
        mailQueue.Enqueue(
            organization.Id,
            user.Id,
            validation.Email,
            "identity.registration-verification",
            new { displayName = validation.DisplayName, actionUrl },
            $"registration-verification:{issued.Entity.Id}",
            user.Locale);

        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organization.Id,
            ActorUserId = user.Id,
            Action = "account.registration.created",
            TargetType = "user",
            TargetId = user.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new
            {
                AccountType = validation.AccountType,
                validation.Country,
                validation.Locale,
                TermsVersion = registrationOptions.TermsVersion,
                TermsAcceptedAt = now,
                PrivacyVersion = registrationOptions.PrivacyVersion,
                PrivacyAcknowledgedAt = now,
                NewsletterConsent = request.SubscribeToNewsletter ?? false,
                NewsletterConsentSource = request.SubscribeToNewsletter == true ? "registration" : null,
                CampaignContext = CampaignAttribution.Sanitize(request.CampaignContext),
            }),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CustomerRegistrationOutcome.Accepted();
    }

    public async Task ResendVerificationAsync(
        string email,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is not { IsActive: true, EmailConfirmed: false })
            return;

        var organizationId = await dbContext.OrganizationMemberships.AsNoTracking()
            .Where(membership =>
                membership.UserId == user.Id &&
                membership.Status == OrganizationMembershipStatus.Active)
            .OrderBy(membership => membership.CreatedAt)
            .Select(membership => (Guid?)membership.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

        var issued = await actionTokens.IssueAsync(
            user.Id,
            organizationId,
            user.Email!,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            new { organizationId },
            TimeSpan.FromHours(24),
            cancellationToken);
        var actionUrl = BuildVerificationUrl(issued.RawToken, returnUrl);
        mailQueue.Enqueue(
            organizationId,
            user.Id,
            user.Email!,
            "identity.registration-verification",
            new { displayName = user.DisplayName, actionUrl },
            $"registration-verification-resend:{issued.Entity.Id}",
            user.Locale);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = user.Id,
            Action = "account.verification.resent",
            TargetType = "identity_action_token",
            TargetId = issued.Entity.Id.ToString(),
            Outcome = "succeeded",
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailVerificationOutcome> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        var actionToken = await actionTokens.FindValidAsync(
            token,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            cancellationToken);
        if (actionToken is null)
            return EmailVerificationOutcome.Invalid();
        var user = await userManager.FindByIdAsync(actionToken.UserId.ToString());
        if (user is null || !user.IsActive || user.EmailConfirmed)
            return EmailVerificationOutcome.Invalid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var subscription = actionToken.OrganizationId is { } organizationId
            ? await dbContext.OrganizationSubscriptions.SingleOrDefaultAsync(
                value => value.OrganizationId == organizationId,
                cancellationToken)
            : null;
        var membership = actionToken.OrganizationId is { } membershipOrganizationId
            ? await dbContext.OrganizationMemberships.SingleOrDefaultAsync(
                value => value.OrganizationId == membershipOrganizationId &&
                         value.UserId == user.Id &&
                         value.Status == OrganizationMembershipStatus.Active,
                cancellationToken)
            : null;
        if (subscription is null || membership is null || subscription.Status != SubscriptionStatus.Pending)
            return EmailVerificationOutcome.Invalid();

        var now = DateTimeOffset.UtcNow;
        user.EmailConfirmed = true;
        user.UpdatedAt = now;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return EmailVerificationOutcome.Invalid();
        trialActivation.ActivatePendingTrial(subscription, membership, now);
        actionToken.UsedAt = now;
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = actionToken.OrganizationId,
            ActorUserId = user.Id,
            Action = "account.registration.verified",
            TargetType = "user",
            TargetId = user.Id.ToString(),
            Outcome = "succeeded",
        });
        mailQueue.Enqueue(
            actionToken.OrganizationId,
            user.Id,
            user.Email!,
            "identity.welcome",
            new { displayName = user.DisplayName, actionUrl = $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/login" },
            $"account-welcome:{actionToken.Id}",
            user.Locale);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return EmailVerificationOutcome.Succeeded();
    }

    private string BuildVerificationUrl(string rawToken, string? requestedReturnUrl)
    {
        var actionUrl =
            $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        var safeReturnUrl = SanitizeReturnUrl(requestedReturnUrl);
        return safeReturnUrl is null
            ? actionUrl
            : $"{actionUrl}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
    }

    private string? SanitizeReturnUrl(string? rawValue)
    {
        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var value) ||
            (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return registrationOptions.AllowedReturnOrigins.Any(rawOrigin =>
                Uri.TryCreate(rawOrigin, UriKind.Absolute, out var origin) &&
                string.Equals(value.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
                value.Port == origin.Port)
            ? value.AbsoluteUri
            : null;
    }
}
