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
    bool AcceptPrivacy);

public sealed record VerifyRegistrationRequest([Required] string Token);

public sealed record RegistrationAcceptedResponse(string Message);

public sealed record RegistrationValidationResult(
    string? Error,
    string Email,
    string DisplayName,
    SubscriptionAccountType AccountType,
    string? CompanyName,
    string? RequestedSlug)
{
    public bool IsValid => Error is null;

    public static RegistrationValidationResult Invalid(string error) =>
        new(error, string.Empty, string.Empty, default, null, null);

    public static RegistrationValidationResult Valid(
        string email,
        string displayName,
        SubscriptionAccountType accountType,
        string? companyName,
        string? requestedSlug) =>
        new(null, email, displayName, accountType, companyName, requestedSlug);
}

public enum CustomerRegistrationStatus
{
    Accepted,
    Invalid,
    SlugConflict,
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
