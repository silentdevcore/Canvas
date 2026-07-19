using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pxa/v1/auth")]
public sealed partial class AccountRegistrationController : ControllerBase
{
    private static readonly string[] TrialCapabilities =
    [
        "generator", "designer", "migration", "importer",
        "pdf-viewer", "spreadsheet", "api", "sdk",
    ];

    private readonly PxaDbContext dbContext;
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPxaMailQueue mailQueue;
    private readonly PxaMailOptions mailOptions;

    public AccountRegistrationController(
        PxaDbContext dbContext,
        UserManager<PxaIdentityUser> userManager,
        IdentityActionTokenService actionTokens,
        IPxaMailQueue mailQueue,
        IOptions<PxaMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
    }

    [HttpPost("register")]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("registration")]
    public async Task<ActionResult<RegistrationAcceptedResponse>> Register(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (!request.AcceptTerms || !request.AcceptPrivacy)
            return RegistrationValidation("Terms and Privacy acceptance are required.");
        if (displayName.Length is < 2 or > 200)
            return RegistrationValidation("Display name must contain between 2 and 200 characters.");
        if (!Enum.TryParse<SubscriptionAccountType>(request.AccountType, true, out var accountType))
            return RegistrationValidation("Account type must be IndividualDeveloper or Company.");
        if (await userManager.FindByEmailAsync(email) is not null)
            return Accepted(AcceptedResponse());

        var companyName = request.CompanyName?.Trim();
        if (accountType == SubscriptionAccountType.Company &&
            (string.IsNullOrWhiteSpace(companyName) || companyName.Length is < 2 or > 200))
        {
            return RegistrationValidation("Company registrations require a company name.");
        }

        var requestedSlug = accountType == SubscriptionAccountType.Company
            ? Slugify(request.OrganizationSlug ?? companyName!)
            : null;
        if (accountType == SubscriptionAccountType.Company && requestedSlug is { Length: < 3 })
            return RegistrationValidation("Organization slug must contain at least three letters or numbers.");
        if (requestedSlug is not null && await dbContext.Organizations.AnyAsync(
                value => value.Slug == requestedSlug, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Organization unavailable",
                Detail = "Choose another organization identifier.",
            });
        }

        var administratorRole = await dbContext.Roles.SingleOrDefaultAsync(
            value => value.Name == PxaRoles.OrganizationAdministrator,
            cancellationToken);
        if (administratorRole is null)
            return Problem(statusCode: 503, title: "Account registration is temporarily unavailable.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var user = new PxaIdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            DisplayName = displayName,
            IsActive = true,
        };
        var creation = await userManager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            if (creation.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
                return Accepted(AcceptedResponse());
            return IdentityFailure(creation);
        }

        var organization = new Organization
        {
            Name = accountType == SubscriptionAccountType.Company
                ? companyName!
                : $"{displayName}'s workspace",
            Slug = requestedSlug ?? $"developer-{user.Id:N}"[..26],
        };
        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
        };
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            Edition = SubscriptionEdition.Trial,
            AccountType = accountType,
            Status = SubscriptionStatus.Trialing,
            BillingPeriod = SubscriptionBillingPeriod.None,
            DeploymentMode = SubscriptionDeploymentMode.Cloud,
            SeatLimit = accountType == SubscriptionAccountType.IndividualDeveloper ? 1 : null,
            StartsAt = now,
            CurrentPeriodStartsAt = now,
            TrialEndsAt = now.AddDays(30),
        };
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);
        dbContext.OrganizationMembershipRoles.Add(new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = administratorRole.Id,
            AssignedByUserId = user.Id,
        });
        dbContext.OrganizationSubscriptions.Add(subscription);
        dbContext.SubscriptionEntitlements.AddRange(TrialCapabilities.Select(capability =>
            new SubscriptionEntitlement
            {
                SubscriptionId = subscription.Id,
                Capability = capability,
                Enabled = true,
                Source = EntitlementSource.EditionDefault,
                ExpiresAt = subscription.TrialEndsAt,
            }));
        dbContext.SubscriptionSeatAssignments.Add(new SubscriptionSeatAssignment
        {
            SubscriptionId = subscription.Id,
            OrganizationMembershipId = membership.Id,
            AssignedByUserId = user.Id,
        });
        dbContext.SubscriptionLifecycleEvents.Add(new SubscriptionLifecycleEvent
        {
            SubscriptionId = subscription.Id,
            OrganizationId = organization.Id,
            ActorUserId = user.Id,
            Action = "subscription.trial.started",
            CurrentStatus = SubscriptionStatus.Trialing,
            DetailsJson = JsonSerializer.Serialize(new { AccountType = accountType, TrialDays = 30 }),
        });
        var issued = await actionTokens.IssueAsync(
            user.Id,
            organization.Id,
            email,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            new { organizationId = organization.Id },
            TimeSpan.FromHours(24),
            cancellationToken);
        var actionUrl = $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(issued.RawToken)}";
        mailQueue.Enqueue(
            organization.Id,
            user.Id,
            email,
            "identity.registration-verification",
            new { displayName, actionUrl },
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
                AccountType = accountType,
                request.Country,
                request.Locale,
                TermsVersion = "draft-v1",
                PrivacyVersion = "draft-v1",
            }),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Accepted(AcceptedResponse());
    }

    [HttpPost("verify-email")]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("identity-action")]
    public async Task<IActionResult> VerifyEmail(
        VerifyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var actionToken = await actionTokens.FindValidAsync(
            request.Token,
            IdentityActionTokenService.RegistrationVerificationPurpose,
            cancellationToken);
        if (actionToken is null)
            return InvalidVerification();
        var user = await userManager.FindByIdAsync(actionToken.UserId.ToString());
        if (user is null || !user.IsActive || user.EmailConfirmed)
            return InvalidVerification();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        user.EmailConfirmed = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return IdentityFailure(update);
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
        return NoContent();
    }

    private static RegistrationAcceptedResponse AcceptedResponse() => new(
        "If the registration can be accepted, a verification message will be sent shortly.");

    private ObjectResult RegistrationValidation(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid registration",
        detail: detail);

    private ObjectResult IdentityFailure(IdentityResult result) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid account details",
        detail: string.Join(" ", result.Errors.Select(error => error.Description)));

    private ObjectResult InvalidVerification() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid or expired verification",
        detail: "Request a new registration or verification message.");

    private static string Slugify(string value)
    {
        var slug = InvalidSlugCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= 80 ? slug : slug[..80].TrimEnd('-');
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugCharacters();
}

public sealed record RegisterAccountRequest(
    [Required, EmailAddress] string Email,
    [Required] string DisplayName,
    [Required] string Password,
    [Required] string AccountType,
    string? CompanyName,
    string? OrganizationSlug,
    string? Country,
    string? Locale,
    bool AcceptTerms,
    bool AcceptPrivacy);

public sealed record VerifyRegistrationRequest([Required] string Token);

public sealed record RegistrationAcceptedResponse(string Message);
