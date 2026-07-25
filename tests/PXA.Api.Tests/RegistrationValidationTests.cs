using PXA.WebApi.Application.Identity;

namespace PXA.Api.Tests;

public sealed class RegistrationValidationTests
{
    private static RegisterAccountRequest ValidCompanyRequest(
        string? companyName = "Acme Inc.",
        string? organizationSlug = null,
        string displayName = "Ada Lovelace",
        bool acceptTerms = true,
        bool acceptPrivacy = true) =>
        new(
            Email: "ada@example.com",
            DisplayName: displayName,
            Password: "correct-horse-battery-staple",
            AccountType: "Company",
            CompanyName: companyName,
            OrganizationSlug: organizationSlug,
            Country: "US",
            Locale: "en",
            AcceptTerms: acceptTerms,
            AcceptPrivacy: acceptPrivacy);

    [Fact]
    public void Rejects_missing_terms_or_privacy_acceptance()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(acceptTerms: false));
        Assert.False(result.IsValid);
        Assert.Contains("Terms and Privacy", result.Error);
    }

    [Fact]
    public void Optional_marketing_consent_does_not_gate_registration()
    {
        var result = RegistrationValidation.Validate(
            ValidCompanyRequest() with { SubscribeToNewsletter = false });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Rejects_display_name_shorter_than_two_characters(string displayName)
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(displayName: displayName));
        Assert.False(result.IsValid);
        Assert.Contains("Display name", result.Error);
    }

    [Fact]
    public void Rejects_display_name_longer_than_two_hundred_characters()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(displayName: new string('a', 201)));
        Assert.False(result.IsValid);
        Assert.Contains("Display name", result.Error);
    }

    [Fact]
    public void Rejects_unknown_account_type()
    {
        var request = ValidCompanyRequest() with { AccountType = "Enterprise" };
        var result = RegistrationValidation.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains("Account type", result.Error);
    }

    [Fact]
    public void Rejects_company_registration_without_a_company_name()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(companyName: null));
        Assert.False(result.IsValid);
        Assert.Contains("company name", result.Error);
    }

    [Fact]
    public void Rejects_slug_shorter_than_three_letters_or_numbers()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(organizationSlug: "a$"));
        Assert.False(result.IsValid);
        Assert.Contains("Organization slug", result.Error);
    }

    [Fact]
    public void Individual_developer_registration_does_not_require_a_company_name_or_slug()
    {
        var request = ValidCompanyRequest(companyName: null) with { AccountType = "IndividualDeveloper" };
        var result = RegistrationValidation.Validate(request);
        Assert.True(result.IsValid);
        Assert.Null(result.RequestedSlug);
    }

    [Fact]
    public void Accepts_a_valid_company_registration_and_normalizes_the_slug()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(organizationSlug: "Acme, Inc.!"));
        Assert.True(result.IsValid);
        Assert.Equal("acme-inc", result.RequestedSlug);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal("Ada Lovelace", result.DisplayName);
    }

    [Fact]
    public void Falls_back_to_slugifying_the_company_name_when_no_slug_is_requested()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest(companyName: "Acme & Co", organizationSlug: null));
        Assert.True(result.IsValid);
        Assert.Equal("acme-co", result.RequestedSlug);
    }

    [Fact]
    public void Slugify_truncates_to_eighty_characters_without_a_trailing_separator()
    {
        var slug = RegistrationValidation.Slugify(new string('a', 90) + "!!!");
        Assert.Equal(80, slug.Length);
        Assert.DoesNotContain('!', slug);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Trims_email_and_display_name_whitespace()
    {
        var request = ValidCompanyRequest() with { Email = "  ada@example.com  ", DisplayName = "  Ada Lovelace  " };
        var result = RegistrationValidation.Validate(request);
        Assert.True(result.IsValid);
        Assert.Equal("ada@example.com", result.Email);
        Assert.Equal("Ada Lovelace", result.DisplayName);
    }

    [Theory]
    [InlineData("Germany")]
    [InlineData("D")]
    [InlineData("D3")]
    public void Rejects_invalid_country_codes(string country)
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest() with { Country = country });
        Assert.False(result.IsValid);
        Assert.Contains("Country", result.Error);
    }

    [Fact]
    public void Normalizes_a_valid_country_code()
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest() with { Country = " de " });
        Assert.True(result.IsValid);
        Assert.Equal("DE", result.Country);
    }

    [Theory]
    [InlineData("english")]
    [InlineData("de_DE")]
    [InlineData("en-123456789")]
    public void Rejects_invalid_locale_tags(string locale)
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest() with { Locale = locale });
        Assert.False(result.IsValid);
        Assert.Contains("Locale", result.Error);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de-DE")]
    [InlineData("zh-Hant")]
    public void Accepts_valid_locale_tags(string locale)
    {
        var result = RegistrationValidation.Validate(ValidCompanyRequest() with { Locale = locale });
        Assert.True(result.IsValid);
        Assert.Equal(locale, result.Locale);
    }
}
