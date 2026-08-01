using PXA.Domain.Entities;
using PXA.WebApi.Application.Legal;

namespace PXA.WebApi.Application.Identity;

public sealed class RegistrationLegalPolicyService(
    PxaLegalDocumentService legalDocuments,
    Microsoft.Extensions.Options.IOptions<PxaRegistrationOptions> registrationOptions)
{
    private readonly PxaRegistrationOptions options = registrationOptions.Value;

    public async Task<RegistrationLegalPolicy> ResolveAsync(
        string? locale,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
        var terms = await legalDocuments.FindCurrentAsync(
            LegalDocumentType.TermsAndConditions,
            normalizedLocale,
            LegalDocumentAudience.All,
            now,
            cancellationToken);
        var privacy = await legalDocuments.FindCurrentAsync(
            LegalDocumentType.PrivacyNotice,
            normalizedLocale,
            LegalDocumentAudience.All,
            now,
            cancellationToken);

        if (terms is not null && privacy is not null)
        {
            return new RegistrationLegalPolicy(
                true,
                true,
                ToDocument(terms),
                ToDocument(privacy));
        }

        if (options.RequireDatabaseLegalDocuments)
            return RegistrationLegalPolicy.Unavailable();

        return new RegistrationLegalPolicy(
            true,
            false,
            new RegistrationLegalPolicyDocument(
                null, options.TermsVersion, normalizedLocale, null, null),
            new RegistrationLegalPolicyDocument(
                null, options.PrivacyVersion, normalizedLocale, null, null));
    }

    private static RegistrationLegalPolicyDocument ToDocument(LegalDocumentVersion version) =>
        new(version.Id, version.Version, version.Locale, version.ContentHash, version.EffectiveAt);
}

public sealed record RegistrationLegalPolicyDocument(
    Guid? Id,
    string Version,
    string Locale,
    string? ContentHash,
    DateTimeOffset? EffectiveAt);

public sealed record RegistrationLegalPolicy(
    bool Available,
    bool DatabaseBacked,
    RegistrationLegalPolicyDocument? Terms,
    RegistrationLegalPolicyDocument? Privacy)
{
    public static RegistrationLegalPolicy Unavailable() => new(false, false, null, null);

    public RegistrationPolicyResponse ToResponse() =>
        new(
            Available,
            DatabaseBacked,
            Terms is null
                ? null
                : new RegistrationPolicyDocumentResponse(
                    Terms.Id, Terms.Version, Terms.Locale, Terms.ContentHash, Terms.EffectiveAt),
            Privacy is null
                ? null
                : new RegistrationPolicyDocumentResponse(
                    Privacy.Id, Privacy.Version, Privacy.Locale, Privacy.ContentHash, Privacy.EffectiveAt));
}
