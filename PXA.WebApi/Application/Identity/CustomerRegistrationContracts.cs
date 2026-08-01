using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using PXA.Domain.Entities;

namespace PXA.WebApi.Application.Identity;

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
    bool AcceptPrivacy,
    // Optional and deliberately separate from AcceptTerms/AcceptPrivacy:
    // never gates registration, verification, or Trial creation, and is
    // recorded only as audit metadata, not a first-class marketing-consent
    // record.
    bool? SubscribeToNewsletter = null,
    // Privacy-safe campaign attribution (utm_source/utm_medium/utm_campaign
    // only - see CampaignAttribution.AllowedKeys). Re-validated server-side
    // against the same allowlist the client already applied, never trusted
    // as-is, and stored only in AuditEvent.DetailsJson - never a first-class
    // field on the user or organization.
    IReadOnlyDictionary<string, string>? CampaignContext = null,
    string? ReturnUrl = null,
    Guid? TermsVersionId = null,
    Guid? PrivacyVersionId = null);

public static class CampaignAttribution
{
    public static readonly string[] AllowedKeys = ["utm_source", "utm_medium", "utm_campaign"];

    public static IReadOnlyDictionary<string, string>? Sanitize(IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || context.Count == 0)
            return null;
        var allowed = context
            .Where(pair => AllowedKeys.Contains(pair.Key, StringComparer.Ordinal) &&
                           !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return allowed.Count == 0 ? null : allowed;
    }
}

public sealed record VerifyRegistrationRequest([Required] string Token);

public sealed record ResendVerificationRequest(
    [Required, EmailAddress] string Email,
    string? ReturnUrl = null);

public sealed record RegistrationAcceptedResponse(string Message);

public sealed record RegistrationPolicyDocumentResponse(
    Guid? Id,
    string Version,
    string Locale,
    string? ContentHash,
    DateTimeOffset? EffectiveAt);

public sealed record RegistrationPolicyResponse(
    bool Available,
    bool DatabaseBacked,
    RegistrationPolicyDocumentResponse? Terms,
    RegistrationPolicyDocumentResponse? Privacy);

public sealed record RegistrationValidationResult(
    string? Error,
    string Email,
    string DisplayName,
    SubscriptionAccountType AccountType,
    string? CompanyName,
    string? RequestedSlug,
    string? Country,
    string Locale)
{
    public bool IsValid => Error is null;

    public static RegistrationValidationResult Invalid(string error) =>
        new(error, string.Empty, string.Empty, default, null, null, null, "en");

    public static RegistrationValidationResult Valid(
        string email,
        string displayName,
        SubscriptionAccountType accountType,
        string? companyName,
        string? requestedSlug,
        string? country,
        string locale) =>
        new(null, email, displayName, accountType, companyName, requestedSlug, country, locale);
}

public enum CustomerRegistrationStatus
{
    Accepted,
    Invalid,
    SlugConflict,
    PolicyMismatch,
    Unavailable,
}

public sealed record CustomerRegistrationOutcome(CustomerRegistrationStatus Status, string? Detail)
{
    public static CustomerRegistrationOutcome Accepted() =>
        new(CustomerRegistrationStatus.Accepted, null);

    public static CustomerRegistrationOutcome Invalid(string detail) =>
        new(CustomerRegistrationStatus.Invalid, detail);

    public static CustomerRegistrationOutcome InvalidIdentity(IdentityResult result) =>
        new(CustomerRegistrationStatus.Invalid, string.Join(" ", result.Errors.Select(error => error.Description)));

    public static CustomerRegistrationOutcome SlugConflict() =>
        new(CustomerRegistrationStatus.SlugConflict, "Choose another organization identifier.");

    public static CustomerRegistrationOutcome PolicyMismatch() =>
        new(
            CustomerRegistrationStatus.PolicyMismatch,
            "The legal documents changed. Review the current Terms and Privacy Notice before continuing.");

    public static CustomerRegistrationOutcome Unavailable() =>
        new(CustomerRegistrationStatus.Unavailable, "Account registration is temporarily unavailable.");
}

public enum EmailVerificationStatus
{
    Succeeded,
    Invalid,
}

public sealed record EmailVerificationOutcome(EmailVerificationStatus Status)
{
    public static EmailVerificationOutcome Succeeded() => new(EmailVerificationStatus.Succeeded);
    public static EmailVerificationOutcome Invalid() => new(EmailVerificationStatus.Invalid);
}
