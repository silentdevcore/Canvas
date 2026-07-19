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
    TrialActivationService trialActivation)
{
    private readonly PxaMailOptions mailOptions = mailOptions.Value;

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
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale,
            Country = request.Country,
        };
        var creation = await userManager.CreateAsync(user, request.Password);
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

        trialActivation.ActivateTrialForNewOrganization(organization, membership, validation.AccountType, now);

        var issued = await actionTokens.IssueAsync(
            user.Id,
            organization.Id,
            validation.Email,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            new { organizationId = organization.Id },
            TimeSpan.FromHours(24),
            cancellationToken);
        var actionUrl = $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(issued.RawToken)}";
        mailQueue.Enqueue(
            organization.Id,
            user.Id,
            validation.Email,
            "identity.registration-verification",
            new { displayName = validation.DisplayName, actionUrl },
            $"registration-verification:{issued.Entity.Id}");

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
                request.Country,
                request.Locale,
                TermsVersion = "draft-v1",
                PrivacyVersion = "draft-v1",
            }),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CustomerRegistrationOutcome.Accepted();
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
        user.EmailConfirmed = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return EmailVerificationOutcome.Invalid();
        actionToken.UsedAt = DateTimeOffset.UtcNow;
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
            $"account-welcome:{actionToken.Id}");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return EmailVerificationOutcome.Succeeded();
    }
}
