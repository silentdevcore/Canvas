using Microsoft.Extensions.Options;
using PXA.Domain.Entities;

namespace PXA.WebApi.Application.Legal;

public sealed class PxaConsumerCheckoutLegalGate(
    PxaLegalDocumentService legalDocuments,
    IOptions<PxaConsumerCheckoutOptions> checkoutOptions)
{
    private static readonly LegalDocumentType[] RequiredDocumentTypes =
    [
        LegalDocumentType.TermsAndConditions,
        LegalDocumentType.PrivacyNotice,
        LegalDocumentType.ConsumerWithdrawal,
    ];

    private readonly PxaConsumerCheckoutOptions options = checkoutOptions.Value;

    public async Task<PxaConsumerCheckoutReadiness> EvaluateAsync(
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
        var documents = new List<PxaConsumerCheckoutLegalDocument>(RequiredDocumentTypes.Length);

        foreach (var type in RequiredDocumentTypes)
        {
            var version = await legalDocuments.FindCurrentAsync(
                type,
                normalizedLocale,
                LegalDocumentAudience.Consumer,
                now,
                cancellationToken);
            documents.Add(new PxaConsumerCheckoutLegalDocument(
                type.ToString(),
                version is not null,
                version?.Id,
                version?.Version,
                version?.ContentHash));
        }

        var legalDocumentsReady = documents.All(value => value.Available);
        var available = options.Enabled && legalDocumentsReady;
        var reason = !options.Enabled
            ? "consumer-checkout-disabled"
            : legalDocumentsReady
                ? null
                : "required-legal-documents-unavailable";

        return new PxaConsumerCheckoutReadiness(
            available,
            options.Enabled,
            legalDocumentsReady,
            normalizedLocale,
            reason,
            documents);
    }
}

public sealed class PxaConsumerCheckoutOptions
{
    public const string SectionName = "ConsumerCheckout";

    public bool Enabled { get; set; }
}

public sealed record PxaConsumerCheckoutReadiness(
    bool Available,
    bool CommerciallyEnabled,
    bool LegalDocumentsReady,
    string Locale,
    string? Reason,
    IReadOnlyList<PxaConsumerCheckoutLegalDocument> Documents);

public sealed record PxaConsumerCheckoutLegalDocument(
    string Type,
    bool Available,
    Guid? VersionId,
    string? Version,
    string? ContentHash);
