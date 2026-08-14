using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Application.Identity;

namespace PXA.WebApi.Application.Legal;

public sealed class AccountLegalObligationService(
    PxaDbContext dbContext,
    PxaLegalDocumentService legalDocuments,
    IOptions<PxaRegistrationOptions> registrationOptions)
{
    private readonly PxaRegistrationOptions options = registrationOptions.Value;

    public async Task<AccountLegalObligations> ResolveAsync(
        PxaIdentityUser user,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var terms = await legalDocuments.FindCurrentAsync(
            LegalDocumentType.TermsAndConditions,
            user.Locale,
            LegalDocumentAudience.All,
            now,
            cancellationToken);
        var privacy = await legalDocuments.FindCurrentAsync(
            LegalDocumentType.PrivacyNotice,
            user.Locale,
            LegalDocumentAudience.All,
            now,
            cancellationToken);

        if (terms is not null && privacy is not null)
        {
            var predecessorIds = new[] { terms.PreviousVersionId, privacy.PreviousVersionId }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToArray();
            var predecessorVersions = await dbContext.LegalDocumentVersions.AsNoTracking()
                .Where(value => predecessorIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, value => value.Version, cancellationToken);
            var acceptedVersionIds = await dbContext.LegalAcceptanceEvents.AsNoTracking()
                .Where(value =>
                    value.UserId == user.Id &&
                    value.OrganizationId == organizationId &&
                    (value.LegalDocumentVersionId == terms.Id ||
                     value.LegalDocumentVersionId == privacy.Id))
                .Select(value => value.LegalDocumentVersionId)
                .Distinct()
                .ToListAsync(cancellationToken);
            return new AccountLegalObligations(
                true,
                ToDocument(
                    terms,
                    terms.RequiresAcceptance && !acceptedVersionIds.Contains(terms.Id),
                    FindPredecessorVersion(terms, predecessorVersions)),
                ToDocument(
                    privacy,
                    !acceptedVersionIds.Contains(privacy.Id),
                    FindPredecessorVersion(privacy, predecessorVersions)));
        }

        if (options.RequireDatabaseLegalDocuments)
            return AccountLegalObligations.Unavailable();

        return new AccountLegalObligations(
            true,
            new AccountLegalObligationDocument(
                null,
                options.TermsVersion,
                PxaLegalDocumentService.NormalizeLocale(user.Locale),
                null,
                null,
                null,
                options.RequireCurrentTermsAcceptance &&
                !string.Equals(
                    user.TermsAcceptedVersion,
                    options.TermsVersion,
                    StringComparison.Ordinal)),
            new AccountLegalObligationDocument(
                null,
                options.PrivacyVersion,
                PxaLegalDocumentService.NormalizeLocale(user.Locale),
                null,
                null,
                null,
                options.RequireCurrentPrivacyAcknowledgement &&
                !string.Equals(
                    user.PrivacyAcknowledgedVersion,
                    options.PrivacyVersion,
                    StringComparison.Ordinal)));
    }

    private static AccountLegalObligationDocument ToDocument(
        LegalDocumentVersion version,
        bool actionRequired,
        string? previousVersion) =>
        new(
            version.Id,
            version.Version,
            version.Locale,
            version.ContentHash,
            version.ChangeSummary,
            previousVersion,
            actionRequired);

    private static string? FindPredecessorVersion(
        LegalDocumentVersion version,
        IReadOnlyDictionary<Guid, string> predecessors) =>
        version.PreviousVersionId is { } id && predecessors.TryGetValue(id, out var predecessor)
            ? predecessor
            : null;
}

public sealed record AccountLegalObligationDocument(
    Guid? Id,
    string Version,
    string Locale,
    string? ContentHash,
    string? ChangeSummary,
    string? PreviousVersion,
    bool ActionRequired);

public sealed record AccountLegalObligations(
    bool Available,
    AccountLegalObligationDocument? Terms,
    AccountLegalObligationDocument? Privacy)
{
    public static AccountLegalObligations Unavailable() => new(false, null, null);
}
