using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;

namespace PVAD.WebApi.Application.Legal;

public sealed class PvadConsumerCheckoutLegalGate(
    PvadLegalDocumentService legalDocuments,
    IOptions<PvadConsumerCheckoutOptions> checkoutOptions)
{
    private static readonly LegalDocumentType[] RequiredDocumentTypes =
    [
        LegalDocumentType.TermsAndConditions,
        LegalDocumentType.PrivacyNotice,
        LegalDocumentType.ConsumerWithdrawal,
    ];

    private readonly PvadConsumerCheckoutOptions options = checkoutOptions.Value;

    public async Task<PvadConsumerCheckoutReadiness> EvaluateAsync(
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = PvadLegalDocumentService.NormalizeLocale(locale);
        var documents = new List<PvadConsumerCheckoutLegalDocument>(RequiredDocumentTypes.Length);

        foreach (var type in RequiredDocumentTypes)
        {
            var version = await legalDocuments.FindCurrentAsync(
                type,
                normalizedLocale,
                LegalDocumentAudience.Consumer,
                now,
                cancellationToken);
            documents.Add(new PvadConsumerCheckoutLegalDocument(
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

        return new PvadConsumerCheckoutReadiness(
            available,
            options.Enabled,
            legalDocumentsReady,
            normalizedLocale,
            reason,
            documents);
    }
}

public sealed class PvadConsumerCheckoutOptions
{
    public const string SectionName = "ConsumerCheckout";

    public bool Enabled { get; set; }
}

public sealed record PvadConsumerCheckoutReadiness(
    bool Available,
    bool CommerciallyEnabled,
    bool LegalDocumentsReady,
    string Locale,
    string? Reason,
    IReadOnlyList<PvadConsumerCheckoutLegalDocument> Documents);

public sealed record PvadConsumerCheckoutLegalDocument(
    string Type,
    bool Available,
    Guid? VersionId,
    string? Version,
    string? ContentHash);
