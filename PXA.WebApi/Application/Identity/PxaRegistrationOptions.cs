namespace PXA.WebApi.Application.Identity;

public sealed class PxaRegistrationOptions
{
    public const string SectionName = "Registration";

    public string TermsVersion { get; set; } = string.Empty;
    public string PrivacyVersion { get; set; } = string.Empty;
}
