using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Legal;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/legal")]
public sealed class AdminLegalDocumentsController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext) : ControllerBase
{
    [HttpGet("documents")]
    [Authorize(Policy = PxaPermissions.LegalRead)]
    public async Task<ActionResult<AdminLegalCatalogResponse>> GetDocuments(
        CancellationToken cancellationToken)
    {
        var documents = await dbContext.LegalDocuments.AsNoTracking()
            .OrderBy(value => value.DisplayName)
            .Select(value => new AdminLegalDocumentResponse(
                value.Id,
                value.Type.ToString(),
                value.Key,
                value.DisplayName,
                value.CreatedAt,
                dbContext.LegalDocumentVersions.Count(version => version.LegalDocumentId == value.Id)))
            .ToListAsync(cancellationToken);
        var versionEntities = await dbContext.LegalDocumentVersions.AsNoTracking()
            .OrderByDescending(value => value.CreatedAt)
            .ToListAsync(cancellationToken);
        var versions = versionEntities.Select(ToVersionResponse).ToList();
        return Ok(new AdminLegalCatalogResponse(documents, versions));
    }

    [HttpGet("versions/compare")]
    [Authorize(Policy = PxaPermissions.LegalRead)]
    public async Task<ActionResult<AdminLegalVersionComparisonResponse>> CompareVersions(
        [FromQuery] Guid baseVersionId,
        [FromQuery] Guid targetVersionId,
        CancellationToken cancellationToken)
    {
        if (baseVersionId == Guid.Empty ||
            targetVersionId == Guid.Empty ||
            baseVersionId == targetVersionId)
        {
            return Invalid("Choose two different legal document versions.");
        }
        var versions = await dbContext.LegalDocumentVersions.AsNoTracking()
            .Where(value => value.Id == baseVersionId || value.Id == targetVersionId)
            .ToListAsync(cancellationToken);
        var baseVersion = versions.SingleOrDefault(value => value.Id == baseVersionId);
        var targetVersion = versions.SingleOrDefault(value => value.Id == targetVersionId);
        if (baseVersion is null || targetVersion is null)
            return NotFound();
        if (baseVersion.LegalDocumentId != targetVersion.LegalDocumentId)
            return Invalid("Only versions of the same legal document can be compared.");
        var document = await dbContext.LegalDocuments.AsNoTracking()
            .SingleAsync(value => value.Id == baseVersion.LegalDocumentId, cancellationToken);
        var diff = PxaLegalDocumentDiff.Compare(
            baseVersion.SourceMarkdown,
            targetVersion.SourceMarkdown);
        return Ok(new AdminLegalVersionComparisonResponse(
            document.Id,
            document.DisplayName,
            ToVersionResponse(baseVersion),
            ToVersionResponse(targetVersion),
            new AdminLegalDiffSummary(
                diff.Unchanged, diff.Modified, diff.Added, diff.Removed),
            diff.Lines.Select(value => new AdminLegalDiffLine(
                value.Kind.ToString(),
                value.BaseLineNumber,
                value.TargetLineNumber,
                value.BaseText,
                value.TargetText)).ToList()));
    }

    [HttpGet("versions/{versionId:guid}/acceptance")]
    [Authorize(Policy = PxaPermissions.LegalRead)]
    public async Task<ActionResult<AdminLegalAcceptanceSummaryResponse>> GetAcceptanceSummary(
        Guid versionId,
        [FromQuery] Guid? organizationId,
        [FromQuery] string? accountType,
        [FromQuery] string? locale,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.LegalDocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (!TryParseAccountType(accountType, out var parsedAccountType))
            return Invalid("Account type must be IndividualDeveloper or Company.");
        if (from > to)
            return Invalid("The from date must be before the to date.");

        var population = BuildAffectedMemberships(
            version.Audience,
            organizationId,
            parsedAccountType,
            locale);
        var acceptances = AcceptanceEvents(versionId, from, to);
        var acceptedPopulation = (
            from membership in population
            join acceptance in acceptances
                on new { membership.UserId, OrganizationId = (Guid?)membership.OrganizationId }
                equals new { acceptance.UserId, acceptance.OrganizationId }
            select membership.Id).Distinct();
        var total = await population.CountAsync(cancellationToken);
        var completed = await acceptedPopulation.CountAsync(cancellationToken);

        var localeGroups = await (
                from membership in population
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                select user.Locale)
            .GroupBy(value => value)
            .Select(group => new { Name = group.Key, Total = group.Count() })
            .OrderBy(value => value.Name)
            .ToListAsync(cancellationToken);
        var byLocale = new List<AdminLegalAcceptanceBreakdown>();
        foreach (var localeGroup in localeGroups)
        {
            var accepted = await (
                    from membership in population
                    join user in dbContext.Users.AsNoTracking()
                        on membership.UserId equals user.Id
                    join acceptance in acceptances
                        on new
                        {
                            membership.UserId,
                            OrganizationId = (Guid?)membership.OrganizationId,
                        }
                        equals new { acceptance.UserId, acceptance.OrganizationId }
                    where user.Locale == localeGroup.Name
                    select membership.Id)
                .Distinct()
                .CountAsync(cancellationToken);
            byLocale.Add(new AdminLegalAcceptanceBreakdown(
                localeGroup.Name, localeGroup.Total, accepted));
        }

        var byAccountType = new List<AdminLegalAcceptanceBreakdown>();
        foreach (var accountTypeValue in Enum.GetValues<SubscriptionAccountType>())
        {
            var accountTypePopulation = BuildAffectedMemberships(
                version.Audience,
                organizationId,
                accountTypeValue,
                locale);
            var accountTypeAccepted = (
                from membership in accountTypePopulation
                join acceptance in acceptances
                    on new
                    {
                        membership.UserId,
                        OrganizationId = (Guid?)membership.OrganizationId,
                    }
                    equals new { acceptance.UserId, acceptance.OrganizationId }
                select membership.Id).Distinct();
            var accountTypeTotal = await accountTypePopulation.CountAsync(cancellationToken);
            if (accountTypeTotal == 0)
                continue;
            byAccountType.Add(new AdminLegalAcceptanceBreakdown(
                accountTypeValue.ToString(),
                accountTypeTotal,
                await accountTypeAccepted.CountAsync(cancellationToken)));
        }

        return Ok(new AdminLegalAcceptanceSummaryResponse(
            version.Id,
            version.Version,
            version.RequiresAcceptance,
            total,
            completed,
            total - completed,
            total == 0 ? 0 : Math.Round(completed * 100m / total, 1),
            byLocale,
            byAccountType));
    }

    [HttpPost("versions/{versionId:guid}/acceptance/export")]
    [Authorize(Policy = PxaPermissions.LegalRead)]
    [PxaValidateAntiforgery]
    public async Task<IActionResult> ExportAcceptanceEvidence(
        Guid versionId,
        AdminLegalAcceptanceExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var format = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
        var version = await dbContext.LegalDocumentVersions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (format is not ("csv" or "json"))
            return Invalid("Export format must be csv or json.");
        if (!TryParseAccountType(request.AccountType, out var parsedAccountType))
            return Invalid("Account type must be IndividualDeveloper or Company.");
        if (request.From > request.To)
            return Invalid("The from date must be before the to date.");

        var population = BuildAffectedMemberships(
            version.Audience,
            request.OrganizationId,
            parsedAccountType,
            request.Locale);
        var acceptanceEvents = AcceptanceEvents(versionId, request.From, request.To);
        var evidence = await (
                from acceptance in acceptanceEvents
                join membership in population
                    on new { acceptance.UserId, acceptance.OrganizationId }
                    equals new
                    {
                        membership.UserId,
                        OrganizationId = (Guid?)membership.OrganizationId,
                    }
                orderby acceptance.CreatedAt
                select new AdminLegalAcceptanceEvidence(
                    acceptance.Id,
                    membership.OrganizationId,
                    acceptance.LegalDocumentVersionId,
                    acceptance.DocumentType,
                    acceptance.Decision,
                    acceptance.ContentHash,
                    acceptance.Locale,
                    acceptance.Source,
                    acceptance.CreatedAt))
            .ToListAsync(cancellationToken);
        AddAudit(actor.Value, "legal.acceptance.exported", "legal_document_version", version.Id,
            new
            {
                Format = format,
                Rows = evidence.Count,
                request.OrganizationId,
                request.AccountType,
                request.Locale,
                request.From,
                request.To,
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        var filename = $"pxa-legal-acceptance-{version.Version}-{DateTimeOffset.UtcNow:yyyyMMdd}";
        if (format == "json")
        {
            return File(
                JsonSerializer.SerializeToUtf8Bytes(evidence, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }),
                "application/json",
                $"{filename}.json");
        }

        var csv = new StringBuilder(
            "evidenceId,organizationId,legalDocumentVersionId,documentType,decision,contentHash,locale,source,createdAt\n");
        foreach (var item in evidence)
        {
            csv.Append(Csv(item.EvidenceId))
                .Append(',').Append(Csv(item.OrganizationId))
                .Append(',').Append(Csv(item.LegalDocumentVersionId))
                .Append(',').Append(Csv(item.DocumentType))
                .Append(',').Append(Csv(item.Decision))
                .Append(',').Append(Csv(item.ContentHash))
                .Append(',').Append(Csv(item.Locale))
                .Append(',').Append(Csv(item.Source))
                .Append(',').Append(Csv(item.CreatedAt.ToString("O")))
                .Append('\n');
        }
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"{filename}.csv");
    }

    [HttpPost("documents")]
    [Authorize(Policy = PxaPermissions.LegalAuthor)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalDocumentResponse>> CreateDocument(
        CreateLegalDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Key) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            !Enum.TryParse<LegalDocumentType>(request.Type, true, out var type))
        {
            return Invalid("A valid type, key, and display name are required.");
        }

        var key = request.Key.Trim().ToLowerInvariant();
        if (await dbContext.LegalDocuments.AnyAsync(
                value => value.Key == key || value.Type == type, cancellationToken))
            return ConflictProblem("A legal document with this key or type already exists.");
        var document = new LegalDocument
        {
            Type = type,
            Key = key,
            DisplayName = request.DisplayName.Trim(),
            CreatedByUserId = actor.Value,
        };
        dbContext.LegalDocuments.Add(document);
        AddAudit(actor.Value, "legal.document.created", "legal_document", document.Id,
            new { document.Type, document.Key });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created(
            $"/api/pxa/v1/admin/legal/documents/{document.Id}",
            new AdminLegalDocumentResponse(
                document.Id, document.Type.ToString(), document.Key, document.DisplayName,
                document.CreatedAt, 0));
    }

    [HttpPost("documents/{documentId:guid}/versions")]
    [Authorize(Policy = PxaPermissions.LegalAuthor)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalVersionResponse>> CreateVersion(
        Guid documentId,
        CreateLegalVersionRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        if (!await dbContext.LegalDocuments.AnyAsync(value => value.Id == documentId, cancellationToken))
            return NotFound();
        if (!ValidateVersionRequest(request, out var audience, out var detail))
            return Invalid(detail);
        var locale = PxaLegalDocumentService.NormalizeLocale(request.Locale);
        var versionName = request.Version.Trim();
        if (await dbContext.LegalDocumentVersions.AnyAsync(value =>
                value.LegalDocumentId == documentId &&
                value.Locale == locale &&
                value.Audience == audience &&
                value.Version == versionName,
                cancellationToken))
        {
            return ConflictProblem("This document version already exists for the locale and audience.");
        }

        var source = PxaLegalDocumentService.NormalizeMarkdown(request.SourceMarkdown);
        var previousId = await dbContext.LegalDocumentVersions.AsNoTracking()
            .Where(value => value.LegalDocumentId == documentId &&
                            value.Locale == locale &&
                            value.Audience == audience)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var version = new LegalDocumentVersion
        {
            LegalDocumentId = documentId,
            Version = versionName,
            Locale = locale,
            Audience = audience,
            SourceMarkdown = source,
            RenderedHtml = PxaLegalDocumentService.RenderSafeHtml(source),
            ContentHash = PxaLegalDocumentService.ComputeHash(source),
            ChangeSummary = CleanOptional(request.ChangeSummary),
            RequiresAcceptance = request.RequiresAcceptance,
            IsAuthoritative = request.IsAuthoritative,
            CreatedByUserId = actor.Value,
            PreviousVersionId = previousId,
        };
        dbContext.LegalDocumentVersions.Add(version);
        AddAudit(actor.Value, "legal.version.created", "legal_document_version", version.Id,
            new { documentId, version.Version, version.Locale, version.Audience, version.ContentHash });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created(
            $"/api/pxa/v1/admin/legal/versions/{version.Id}",
            ToVersionResponse(version));
    }

    [HttpPut("versions/{versionId:guid}")]
    [Authorize(Policy = PxaPermissions.LegalAuthor)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalVersionResponse>> UpdateDraft(
        Guid versionId,
        UpdateLegalVersionRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var version = await dbContext.LegalDocumentVersions.SingleOrDefaultAsync(
            value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (version.Status != LegalDocumentStatus.Draft)
            return ConflictProblem("Only draft versions can be edited.");
        if (version.CreatedByUserId != actor)
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.SourceMarkdown))
            return Invalid("Document content is required.");
        var source = PxaLegalDocumentService.NormalizeMarkdown(request.SourceMarkdown);
        version.SourceMarkdown = source;
        version.RenderedHtml = PxaLegalDocumentService.RenderSafeHtml(source);
        version.ContentHash = PxaLegalDocumentService.ComputeHash(source);
        version.ChangeSummary = CleanOptional(request.ChangeSummary);
        version.RequiresAcceptance = request.RequiresAcceptance;
        AddAudit(actor.Value, "legal.version.updated", "legal_document_version", version.Id,
            new { version.ContentHash, version.RequiresAcceptance });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToVersionResponse(version));
    }

    [HttpPost("versions/{versionId:guid}/submit")]
    [Authorize(Policy = PxaPermissions.LegalAuthor)]
    [PxaValidateAntiforgery]
    public Task<ActionResult<AdminLegalVersionResponse>> Submit(
        Guid versionId,
        CancellationToken cancellationToken) =>
        TransitionAsync(versionId, LegalDocumentStatus.Draft, LegalDocumentStatus.InReview,
            "legal.version.submitted", cancellationToken);

    [HttpPost("versions/{versionId:guid}/review")]
    [Authorize(Policy = PxaPermissions.LegalApprove)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalVersionResponse>> Review(
        Guid versionId,
        ReviewLegalVersionRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var version = await dbContext.LegalDocumentVersions.SingleOrDefaultAsync(
            value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (version.Status != LegalDocumentStatus.InReview)
            return ConflictProblem("Only versions in review can receive a decision.");
        if (version.CreatedByUserId == actor)
            return ConflictProblem("The author cannot approve or reject their own legal version.");
        if (version.PreviousVersionId is not null &&
            request.ComparedToVersionId != version.PreviousVersionId)
        {
            return ConflictProblem(
                "Compare this version with its recorded predecessor before review.");
        }
        var decision = request.Approve ? LegalApprovalDecision.Approved : LegalApprovalDecision.Rejected;
        dbContext.LegalPublicationApprovals.Add(new LegalPublicationApproval
        {
            LegalDocumentVersionId = version.Id,
            ReviewerUserId = actor.Value,
            Decision = decision,
            Comment = CleanOptional(request.Comment),
        });
        if (request.Approve)
        {
            version.Status = LegalDocumentStatus.Approved;
            version.ApprovedAt = DateTimeOffset.UtcNow;
            version.ApprovedByUserId = actor;
        }
        else
        {
            version.Status = LegalDocumentStatus.Draft;
            version.ApprovedAt = null;
            version.ApprovedByUserId = null;
        }
        AddAudit(actor.Value, request.Approve ? "legal.version.approved" : "legal.version.rejected",
            "legal_document_version", version.Id, new
            {
                Decision = decision.ToString(),
                request.ComparedToVersionId,
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToVersionResponse(version));
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = PxaPermissions.LegalApprove)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalVersionResponse>> Publish(
        Guid versionId,
        PublishLegalVersionRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var version = await dbContext.LegalDocumentVersions.SingleOrDefaultAsync(
            value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (version.Status != LegalDocumentStatus.Approved)
            return ConflictProblem("Only approved versions can be published.");
        if (version.CreatedByUserId == actor || version.ApprovedByUserId != actor)
            return ConflictProblem("The independent approving reviewer must publish this version.");
        if (version.PreviousVersionId is not null &&
            request.ComparedToVersionId != version.PreviousVersionId)
        {
            return ConflictProblem(
                "Compare this version with its recorded predecessor before publication.");
        }
        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var now = DateTimeOffset.UtcNow;
        version.EffectiveAt = effectiveAt;
        version.PublishedAt = now;
        version.PublishedByUserId = actor;
        version.Status = effectiveAt > now ? LegalDocumentStatus.Scheduled : LegalDocumentStatus.Published;
        AddAudit(actor.Value, "legal.version.published", "legal_document_version", version.Id,
            new
            {
                version.Status,
                version.EffectiveAt,
                version.ContentHash,
                request.ComparedToVersionId,
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToVersionResponse(version));
    }

    [HttpPost("versions/{versionId:guid}/retire")]
    [Authorize(Policy = PxaPermissions.LegalApprove)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLegalVersionResponse>> Retire(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var version = await dbContext.LegalDocumentVersions.SingleOrDefaultAsync(
            value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (version.Status is not (LegalDocumentStatus.Published or LegalDocumentStatus.Scheduled))
            return ConflictProblem("Only published or scheduled versions can be retired.");
        version.Status = LegalDocumentStatus.Retired;
        version.RetiredAt = DateTimeOffset.UtcNow;
        AddAudit(actor.Value, "legal.version.retired", "legal_document_version", version.Id,
            new { version.ContentHash });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToVersionResponse(version));
    }

    private async Task<ActionResult<AdminLegalVersionResponse>> TransitionAsync(
        Guid versionId,
        LegalDocumentStatus expected,
        LegalDocumentStatus target,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        if (actor is null)
            return Forbid();
        var version = await dbContext.LegalDocumentVersions.SingleOrDefaultAsync(
            value => value.Id == versionId, cancellationToken);
        if (version is null)
            return NotFound();
        if (version.Status != expected)
            return ConflictProblem($"Only {expected} versions can enter {target}.");
        if (version.CreatedByUserId != actor)
            return Forbid();
        version.Status = target;
        version.SubmittedAt = DateTimeOffset.UtcNow;
        AddAudit(actor.Value, auditAction, "legal_document_version", version.Id,
            new { version.ContentHash });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToVersionResponse(version));
    }

    private Guid? RequireActor() => tenantContext.UserId;

    private IQueryable<OrganizationMembership> BuildAffectedMemberships(
        LegalDocumentAudience audience,
        Guid? organizationId,
        SubscriptionAccountType? accountType,
        string? locale)
    {
        var memberships = dbContext.OrganizationMemberships.AsNoTracking()
            .Where(membership =>
                membership.Status == OrganizationMembershipStatus.Active &&
                dbContext.Users.Any(user =>
                    user.Id == membership.UserId && user.IsActive) &&
                dbContext.Organizations.Any(organization =>
                    organization.Id == membership.OrganizationId &&
                    organization.Status == OrganizationStatus.Active));

        if (organizationId is not null)
            memberships = memberships.Where(value => value.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(locale))
        {
            var normalizedLocale = PxaLegalDocumentService.NormalizeLocale(locale);
            memberships = memberships.Where(membership =>
                dbContext.Users.Any(user =>
                    user.Id == membership.UserId &&
                    user.Locale == normalizedLocale));
        }
        if (accountType is not null)
        {
            memberships = memberships.Where(account =>
                dbContext.OrganizationSubscriptions.Any(subscription =>
                    subscription.OrganizationId == account.OrganizationId &&
                    subscription.AccountType == accountType));
        }

        memberships = audience switch
        {
            LegalDocumentAudience.IndividualDeveloper or LegalDocumentAudience.Consumer =>
                memberships.Where(account =>
                    dbContext.OrganizationSubscriptions.Any(subscription =>
                        subscription.OrganizationId == account.OrganizationId &&
                        subscription.AccountType == SubscriptionAccountType.IndividualDeveloper)),
            LegalDocumentAudience.Company or LegalDocumentAudience.Business =>
                memberships.Where(account =>
                    dbContext.OrganizationSubscriptions.Any(subscription =>
                        subscription.OrganizationId == account.OrganizationId &&
                        subscription.AccountType == SubscriptionAccountType.Company)),
            LegalDocumentAudience.Cloud =>
                memberships.Where(account =>
                    dbContext.OrganizationSubscriptions.Any(subscription =>
                        subscription.OrganizationId == account.OrganizationId &&
                        subscription.DeploymentMode == SubscriptionDeploymentMode.Cloud)),
            LegalDocumentAudience.OnPremise =>
                memberships.Where(account =>
                    dbContext.OrganizationSubscriptions.Any(subscription =>
                        subscription.OrganizationId == account.OrganizationId &&
                        subscription.DeploymentMode == SubscriptionDeploymentMode.OnPremise)),
            _ => memberships,
        };
        return memberships;
    }

    private IQueryable<LegalAcceptanceEvent> AcceptanceEvents(
        Guid versionId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var query = dbContext.LegalAcceptanceEvents.AsNoTracking()
            .Where(value =>
                value.LegalDocumentVersionId == versionId &&
                (value.Decision == "accepted" || value.Decision == "acknowledged"));
        if (from is not null)
            query = query.Where(value => value.CreatedAt >= from);
        if (to is not null)
            query = query.Where(value => value.CreatedAt <= to);
        return query;
    }

    private static bool TryParseAccountType(
        string? value,
        out SubscriptionAccountType? accountType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            accountType = null;
            return true;
        }
        if (Enum.TryParse<SubscriptionAccountType>(value, true, out var parsed))
        {
            accountType = parsed;
            return true;
        }
        accountType = null;
        return false;
    }

    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private void AddAudit(
        Guid actor,
        string action,
        string targetType,
        Guid targetId,
        object details) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            ActorUserId = actor,
            Action = action,
            TargetType = targetType,
            TargetId = targetId.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private static bool ValidateVersionRequest(
        CreateLegalVersionRequest request,
        out LegalDocumentAudience audience,
        out string detail)
    {
        if (!Enum.TryParse(request.Audience, true, out audience))
        {
            detail = "A valid legal document audience is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Version) ||
            request.Version.Length > 64 ||
            string.IsNullOrWhiteSpace(request.SourceMarkdown) ||
            request.SourceMarkdown.Length > 1_000_000)
        {
            detail = "Version and document content are required and must stay within supported limits.";
            return false;
        }
        detail = string.Empty;
        return true;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ObjectResult Invalid(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid legal document request",
        detail: detail);

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Legal document operation rejected",
        detail: detail);

    internal static AdminLegalVersionResponse ToVersionResponse(LegalDocumentVersion value) =>
        new(
            value.Id,
            value.LegalDocumentId,
            value.Version,
            value.Locale,
            value.Audience.ToString(),
            value.Status.ToString(),
            value.SourceMarkdown,
            value.RenderedHtml,
            value.ContentHash,
            value.ChangeSummary,
            value.RequiresAcceptance,
            value.IsAuthoritative,
            value.CreatedByUserId,
            value.CreatedAt,
            value.SubmittedAt,
            value.ApprovedAt,
            value.ApprovedByUserId,
            value.PublishedAt,
            value.PublishedByUserId,
            value.EffectiveAt,
            value.RetiredAt,
            value.PreviousVersionId);

}

public sealed record CreateLegalDocumentRequest(string Type, string Key, string DisplayName);
public sealed record CreateLegalVersionRequest(
    string Version,
    string Locale,
    string Audience,
    string SourceMarkdown,
    string? ChangeSummary,
    bool RequiresAcceptance,
    bool IsAuthoritative);
public sealed record UpdateLegalVersionRequest(
    string SourceMarkdown,
    string? ChangeSummary,
    bool RequiresAcceptance);
public sealed record ReviewLegalVersionRequest(
    bool Approve,
    string? Comment,
    Guid? ComparedToVersionId = null);
public sealed record PublishLegalVersionRequest(
    DateTimeOffset? EffectiveAt,
    Guid? ComparedToVersionId = null);
public sealed record AdminLegalCatalogResponse(
    IReadOnlyList<AdminLegalDocumentResponse> Documents,
    IReadOnlyList<AdminLegalVersionResponse> Versions);
public sealed record AdminLegalDocumentResponse(
    Guid Id,
    string Type,
    string Key,
    string DisplayName,
    DateTimeOffset CreatedAt,
    int VersionCount);
public sealed record AdminLegalVersionResponse(
    Guid Id,
    Guid LegalDocumentId,
    string Version,
    string Locale,
    string Audience,
    string Status,
    string SourceMarkdown,
    string RenderedHtml,
    string ContentHash,
    string? ChangeSummary,
    bool RequiresAcceptance,
    bool IsAuthoritative,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    Guid? ApprovedByUserId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset? RetiredAt,
    Guid? PreviousVersionId);
public sealed record AdminLegalVersionComparisonResponse(
    Guid LegalDocumentId,
    string DisplayName,
    AdminLegalVersionResponse BaseVersion,
    AdminLegalVersionResponse TargetVersion,
    AdminLegalDiffSummary Summary,
    IReadOnlyList<AdminLegalDiffLine> Lines);
public sealed record AdminLegalDiffSummary(
    int Unchanged,
    int Modified,
    int Added,
    int Removed);
public sealed record AdminLegalDiffLine(
    string Kind,
    int? BaseLineNumber,
    int? TargetLineNumber,
    string? BaseText,
    string? TargetText);
public sealed record AdminLegalAcceptanceSummaryResponse(
    Guid LegalDocumentVersionId,
    string Version,
    bool RequiresAcceptance,
    int AffectedAccounts,
    int Completed,
    int Pending,
    decimal CompletionPercentage,
    IReadOnlyList<AdminLegalAcceptanceBreakdown> ByLocale,
    IReadOnlyList<AdminLegalAcceptanceBreakdown> ByAccountType);
public sealed record AdminLegalAcceptanceBreakdown(
    string? Name,
    int AffectedAccounts,
    int Completed);
public sealed record AdminLegalAcceptanceExportRequest(
    string Format,
    Guid? OrganizationId = null,
    string? AccountType = null,
    string? Locale = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
public sealed record AdminLegalAcceptanceEvidence(
    Guid EvidenceId,
    Guid OrganizationId,
    Guid LegalDocumentVersionId,
    string DocumentType,
    string Decision,
    string ContentHash,
    string Locale,
    string Source,
    DateTimeOffset CreatedAt);
