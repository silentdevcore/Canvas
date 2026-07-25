using System.Text.RegularExpressions;
using PXA.Domain.Entities;

namespace PXA.WebApi.Application.Identity;

/// <summary>
/// Pure, dependency-free validation extracted from registration so it can be
/// unit-tested without a database. Duplicate-email and duplicate-slug checks
/// stay in <see cref="CustomerRegistrationService"/> since they need one.
/// </summary>
public static partial class RegistrationValidation
{
    public static RegistrationValidationResult Validate(RegisterAccountRequest request)
    {
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();

        if (!request.AcceptTerms || !request.AcceptPrivacy)
            return RegistrationValidationResult.Invalid("Terms and Privacy acceptance are required.");

        if (displayName.Length is < 2 or > 200)
            return RegistrationValidationResult.Invalid("Display name must contain between 2 and 200 characters.");

        if (!Enum.TryParse<SubscriptionAccountType>(request.AccountType, true, out var accountType))
            return RegistrationValidationResult.Invalid("Account type must be IndividualDeveloper or Company.");

        var companyName = request.CompanyName?.Trim();
        if (accountType == SubscriptionAccountType.Company &&
            (string.IsNullOrWhiteSpace(companyName) || companyName.Length is < 2 or > 200))
        {
            return RegistrationValidationResult.Invalid("Company registrations require a company name.");
        }

        var requestedSlug = accountType == SubscriptionAccountType.Company
            ? Slugify(request.OrganizationSlug ?? companyName!)
            : null;
        if (accountType == SubscriptionAccountType.Company && requestedSlug is { Length: < 3 })
            return RegistrationValidationResult.Invalid("Organization slug must contain at least three letters or numbers.");

        var country = request.Country?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(country) && !CountryCode().IsMatch(country))
            return RegistrationValidationResult.Invalid("Country must be a two-letter ISO country code.");

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale.Trim();
        if (locale.Length > 16 || !LocaleTag().IsMatch(locale))
            return RegistrationValidationResult.Invalid("Locale must be a valid language tag of at most 16 characters.");

        return RegistrationValidationResult.Valid(
            email, displayName, accountType, companyName, requestedSlug,
            string.IsNullOrEmpty(country) ? null : country,
            locale);
    }

    public static string Slugify(string value)
    {
        var slug = InvalidSlugCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= 80 ? slug : slug[..80].TrimEnd('-');
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidSlugCharacters();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCode();

    [GeneratedRegex("^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex LocaleTag();
}
