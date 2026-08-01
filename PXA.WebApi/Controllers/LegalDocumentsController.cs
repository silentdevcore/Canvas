using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Legal;

namespace PXA.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pxa/v1/legal")]
public sealed class LegalDocumentsController(
    PxaDbContext dbContext,
    PxaLegalDocumentService legalDocuments) : ControllerBase
{
    [HttpGet("documents")]
    public async Task<ActionResult<LegalDocumentCatalogResponse>> GetDocuments(
        [FromQuery] string locale = "de",
        [FromQuery] LegalDocumentAudience audience = LegalDocumentAudience.All,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentDocumentsAsync(
            locale, audience, DateTimeOffset.UtcNow, cancellationToken);
        return Ok(new LegalDocumentCatalogResponse(current));
    }

    [HttpGet("snapshot")]
    public async Task<ActionResult<LegalDocumentSnapshotResponse>> GetSnapshot(
        [FromQuery] string locale = "de",
        [FromQuery] LegalDocumentAudience audience = LegalDocumentAudience.All,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
        var current = await GetCurrentDocumentsAsync(
            normalizedLocale, audience, generatedAt, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new LegalDocumentSnapshotResponse(
            1,
            generatedAt,
            normalizedLocale,
            audience.ToString(),
            current));
    }

    [HttpGet("documents/{type}/current")]
    public async Task<ActionResult<PublicLegalDocumentResponse>> GetCurrent(
        string type,
        [FromQuery] string locale = "de",
        [FromQuery] LegalDocumentAudience audience = LegalDocumentAudience.All,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseType(type, out var documentType))
            return NotFound();
        var document = await dbContext.LegalDocuments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Type == documentType, cancellationToken);
        if (document is null)
            return NotFound();
        var version = await legalDocuments.FindCurrentAsync(
            documentType, locale, audience, DateTimeOffset.UtcNow, cancellationToken);
        if (version is null)
            return NotFound();
        SetCacheHeaders(version);
        return Ok(ToPublic(document, version));
    }

    [HttpGet("documents/{type}/versions/{version}")]
    public async Task<ActionResult<PublicLegalDocumentResponse>> GetVersion(
        string type,
        string version,
        [FromQuery] string locale = "de",
        [FromQuery] LegalDocumentAudience audience = LegalDocumentAudience.All,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseType(type, out var documentType))
            return NotFound();
        var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
        var now = DateTimeOffset.UtcNow;
        var match = await (
                from document in dbContext.LegalDocuments.AsNoTracking()
                join legalVersion in dbContext.LegalDocumentVersions.AsNoTracking()
                    on document.Id equals legalVersion.LegalDocumentId
                where document.Type == documentType &&
                      legalVersion.Version == version &&
                      legalVersion.Locale == normalizedLocale &&
                      legalVersion.Audience == audience &&
                      (legalVersion.Status == LegalDocumentStatus.Published ||
                       legalVersion.Status == LegalDocumentStatus.Scheduled) &&
                      legalVersion.EffectiveAt <= now
                select new { Document = document, Version = legalVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (match is null)
            return NotFound();
        SetCacheHeaders(match.Version);
        return Ok(ToPublic(match.Document, match.Version));
    }

    [HttpGet("storage-policy")]
    public Task<ActionResult<PublicLegalDocumentResponse>> GetStoragePolicy(
        [FromQuery] string locale = "de",
        CancellationToken cancellationToken = default) =>
        GetCurrent("cookie-storage", locale, LegalDocumentAudience.All, cancellationToken);

    private void SetCacheHeaders(LegalDocumentVersion version)
    {
        Response.Headers.ETag = $"\"{version.ContentHash}\"";
        Response.Headers.CacheControl = "public,max-age=300";
    }

    private async Task<IReadOnlyList<PublicLegalDocumentResponse>> GetCurrentDocumentsAsync(
        string locale,
        LegalDocumentAudience audience,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
        var documents = await dbContext.LegalDocuments.AsNoTracking()
            .OrderBy(value => value.Key)
            .ToListAsync(cancellationToken);
        var current = new List<PublicLegalDocumentResponse>();
        foreach (var document in documents)
        {
            var version = await legalDocuments.FindCurrentAsync(
                document.Type, normalizedLocale, audience, now, cancellationToken);
            if (version is not null)
                current.Add(ToPublic(document, version));
        }

        return current;
    }

    private static PublicLegalDocumentResponse ToPublic(
        LegalDocument document,
        LegalDocumentVersion version) =>
        new(
            document.Type.ToString(),
            document.Key,
            document.DisplayName,
            version.Version,
            version.Locale,
            version.Audience.ToString(),
            version.SourceMarkdown,
            version.RenderedHtml,
            version.ContentHash,
            version.IsAuthoritative,
            version.RequiresAcceptance,
            version.EffectiveAt!.Value,
            version.ChangeSummary);

    internal static bool TryParseType(string value, out LegalDocumentType type)
    {
        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        var aliases = new Dictionary<string, LegalDocumentType>(StringComparer.OrdinalIgnoreCase)
        {
            ["terms"] = LegalDocumentType.TermsAndConditions,
            ["termsandconditions"] = LegalDocumentType.TermsAndConditions,
            ["privacy"] = LegalDocumentType.PrivacyNotice,
            ["privacynotice"] = LegalDocumentType.PrivacyNotice,
            ["cookies"] = LegalDocumentType.CookieAndStoragePolicy,
            ["storage"] = LegalDocumentType.CookieAndStoragePolicy,
            ["cookiestorage"] = LegalDocumentType.CookieAndStoragePolicy,
            ["cookieandstoragepolicy"] = LegalDocumentType.CookieAndStoragePolicy,
            ["imprint"] = LegalDocumentType.Imprint,
            ["withdrawal"] = LegalDocumentType.ConsumerWithdrawal,
            ["consumerwithdrawal"] = LegalDocumentType.ConsumerWithdrawal,
            ["dpa"] = LegalDocumentType.DataProcessingAgreement,
            ["dataprocessingagreement"] = LegalDocumentType.DataProcessingAgreement,
            ["license"] = LegalDocumentType.LicenseAgreement,
            ["licenseagreement"] = LegalDocumentType.LicenseAgreement,
            ["subprocessors"] = LegalDocumentType.SubprocessorList,
            ["subprocessorlist"] = LegalDocumentType.SubprocessorList,
            ["sla"] = LegalDocumentType.ServiceLevelAgreement,
            ["servicelevelagreement"] = LegalDocumentType.ServiceLevelAgreement,
        };
        return aliases.TryGetValue(normalized, out type) ||
               Enum.TryParse(normalized, true, out type);
    }
}

public sealed record LegalDocumentCatalogResponse(IReadOnlyList<PublicLegalDocumentResponse> Documents);

public sealed record LegalDocumentSnapshotResponse(
    int SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Locale,
    string Audience,
    IReadOnlyList<PublicLegalDocumentResponse> Documents);

public sealed record PublicLegalDocumentResponse(
    string Type,
    string Key,
    string DisplayName,
    string Version,
    string Locale,
    string Audience,
    string SourceMarkdown,
    string RenderedHtml,
    string ContentHash,
    bool IsAuthoritative,
    bool RequiresAcceptance,
    DateTimeOffset EffectiveAt,
    string? ChangeSummary);
