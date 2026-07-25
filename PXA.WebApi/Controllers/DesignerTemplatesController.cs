using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[Route("api/pxa/v1/designer/templates")]
public sealed class DesignerTemplatesController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    IOptions<PxaDesignerTemplateOptions> options) : ControllerBase
{
    private readonly PxaDesignerTemplateOptions settings = options.Value;

    [HttpGet]
    public async Task<ActionResult<DesignerTemplatePage>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] bool archived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenant(out var organizationId, out _))
            return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, settings.MaximumPageSize);
        var query = dbContext.DesignerTemplates.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId &&
                            (archived
                                ? value.Status == DesignerTemplateStatus.Archived
                                : value.Status != DesignerTemplateStatus.Archived));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(value =>
                EF.Functions.ILike(value.Name, pattern) ||
                (value.Description != null && EF.Functions.ILike(value.Description, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            query = query.Where(value => value.Tags.Contains(normalizedTag));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(value => new DesignerTemplateSummary(
                value.Id,
                value.Name,
                value.Description,
                value.Tags,
                value.Status.ToString(),
                value.Revision,
                value.PublishedVersionId,
                value.CreatedAt,
                value.UpdatedAt,
                value.ArchivedAt))
            .ToArrayAsync(cancellationToken);
        return Ok(new DesignerTemplatePage(items, page, pageSize, total));
    }

    [HttpPost]
    public async Task<ActionResult<DesignerTemplateDocument>> Create(
        CreateDesignerTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryValidateName(request.Name, out var name))
            return TemplateValidationProblem("Template name is required and must not exceed 200 characters.");
        if (!TryReadDesign(request.DesignDocument, out var designJson, out var checksum, out var failure))
            return failure!;

        var now = DateTimeOffset.UtcNow;
        var template = new DesignerTemplate
        {
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            Name = name,
            Description = NormalizeDescription(request.Description),
            Tags = NormalizeTags(request.Tags),
            DraftJson = designJson,
            DraftChecksum = checksum,
            SchemaVersion = NormalizeVersion(request.SchemaVersion, "1.0"),
            DesignerVersion = NormalizeVersion(request.DesignerVersion, "1.0"),
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.DesignerTemplates.Add(template);
        AddAudit(template, userId, "designer.templates.created", new { template.Revision });
        await dbContext.SaveChangesAsync(cancellationToken);
        SetRevisionHeader(template.Revision);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, ToDocument(template));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DesignerTemplateDocument>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out _))
            return Unauthorized();
        var template = await FindTemplateAsync(id, organizationId, tracked: false, cancellationToken);
        if (template is null)
            return NotFound();
        SetRevisionHeader(template.Revision);
        return Ok(ToDocument(template));
    }

    [HttpPut("{id:guid}/metadata")]
    public async Task<ActionResult<DesignerTemplateDocument>> UpdateMetadata(
        Guid id,
        UpdateDesignerTemplateMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryGetExpectedRevision(request.Revision, out var expectedRevision))
            return RevisionRequired();
        if (!TryValidateName(request.Name, out var name))
            return TemplateValidationProblem("Template name is required and must not exceed 200 characters.");

        var template = await FindTemplateAsync(id, organizationId, tracked: true, cancellationToken);
        if (template is null)
            return NotFound();
        if (template.Revision != expectedRevision)
            return await ConflictAsync(template, userId, cancellationToken);

        template.Name = name;
        template.Description = NormalizeDescription(request.Description);
        template.Tags = NormalizeTags(request.Tags);
        Touch(template, userId);
        return await SaveTemplateAsync(template, userId, "designer.templates.metadata-updated", cancellationToken);
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<ActionResult<DesignerTemplateDocument>> UpdateDraft(
        Guid id,
        UpdateDesignerTemplateDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryGetExpectedRevision(request.Revision, out var expectedRevision))
            return RevisionRequired();
        if (!TryReadDesign(request.DesignDocument, out var designJson, out var checksum, out var failure))
            return failure!;

        var template = await FindTemplateAsync(id, organizationId, tracked: true, cancellationToken);
        if (template is null)
            return NotFound();
        if (template.Revision != expectedRevision)
            return await ConflictAsync(template, userId, cancellationToken);
        if (string.Equals(template.DraftChecksum, checksum, StringComparison.Ordinal))
        {
            SetRevisionHeader(template.Revision);
            return Ok(ToDocument(template));
        }

        template.DraftJson = designJson;
        template.DraftChecksum = checksum;
        template.SchemaVersion = NormalizeVersion(request.SchemaVersion, template.SchemaVersion);
        template.DesignerVersion = NormalizeVersion(request.DesignerVersion, template.DesignerVersion);
        Touch(template, userId);
        return await SaveTemplateAsync(template, userId, "designer.templates.draft-updated", cancellationToken);
    }

    [HttpPost("{id:guid}/archive")]
    public Task<ActionResult<DesignerTemplateDocument>> Archive(
        Guid id,
        TemplateRevisionRequest request,
        CancellationToken cancellationToken) =>
        ChangeArchiveStateAsync(id, request.Revision, true, cancellationToken);

    [HttpPost("{id:guid}/restore")]
    public Task<ActionResult<DesignerTemplateDocument>> Restore(
        Guid id,
        TemplateRevisionRequest request,
        CancellationToken cancellationToken) =>
        ChangeArchiveStateAsync(id, request.Revision, false, cancellationToken);

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<DesignerTemplateVersionInfo>>> ListVersions(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out _))
            return Unauthorized();
        if (!await TemplateExistsAsync(id, organizationId, cancellationToken))
            return NotFound();
        var versions = await dbContext.DesignerTemplateVersions.AsNoTracking()
            .Where(value => value.TemplateId == id && value.OrganizationId == organizationId)
            .OrderByDescending(value => value.VersionNumber)
            .Select(value => new DesignerTemplateVersionInfo(
                value.Id,
                value.VersionNumber,
                value.Label,
                value.Checksum,
                value.SchemaVersion,
                value.DesignerVersion,
                value.CreatedByUserId,
                value.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return Ok(versions);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:long}")]
    public async Task<ActionResult<DesignerTemplateVersionDocument>> GetVersion(
        Guid id,
        long versionNumber,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out _))
            return Unauthorized();
        var version = await dbContext.DesignerTemplateVersions.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TemplateId == id &&
                value.OrganizationId == organizationId &&
                value.VersionNumber == versionNumber,
                cancellationToken);
        return version is null ? NotFound() : Ok(ToVersionDocument(version));
    }

    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<CreateDesignerTemplateVersionResponse>> CreateVersion(
        Guid id,
        CreateDesignerTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        var template = await FindTemplateAsync(id, organizationId, tracked: false, cancellationToken);
        if (template is null)
            return NotFound();
        if (template.Revision != request.Revision)
            return await VersionConflictAsync(template, userId, cancellationToken);

        var result = await CreateVersionInternalAsync(template, userId, request.Label, cancellationToken);
        var response = new CreateDesignerTemplateVersionResponse(
            result.Created, ToVersionDocument(result.Version));
        return result.Created
            ? CreatedAtAction(
                nameof(GetVersion),
                new { id, versionNumber = result.Version.VersionNumber },
                response)
            : Ok(response);
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<DesignerTemplateDocument>> Publish(
        Guid id,
        PublishDesignerTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        var template = await FindTemplateAsync(id, organizationId, tracked: true, cancellationToken);
        if (template is null)
            return NotFound();
        if (template.Revision != request.Revision)
            return await ConflictAsync(template, userId, cancellationToken);

        DesignerTemplateVersion? version;
        if (request.VersionNumber is { } versionNumber)
        {
            version = await dbContext.DesignerTemplateVersions.SingleOrDefaultAsync(value =>
                value.TemplateId == id &&
                value.OrganizationId == organizationId &&
                value.VersionNumber == versionNumber,
                cancellationToken);
            if (version is null)
                return NotFound();
        }
        else
        {
            version = (await CreateVersionInternalAsync(template, userId, request.Label, cancellationToken)).Version;
        }

        template.PublishedVersionId = version.Id;
        Touch(template, userId);
        return await SaveTemplateAsync(
            template,
            userId,
            "designer.templates.published",
            cancellationToken,
            new { version.VersionNumber });
    }

    private async Task<ActionResult<DesignerTemplateDocument>> ChangeArchiveStateAsync(
        Guid id,
        long revision,
        bool archived,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(out var organizationId, out var userId))
            return Unauthorized();
        var template = await FindTemplateAsync(id, organizationId, tracked: true, cancellationToken);
        if (template is null)
            return NotFound();
        if (template.Revision != revision)
            return await ConflictAsync(template, userId, cancellationToken);
        var targetStatus = archived ? DesignerTemplateStatus.Archived : DesignerTemplateStatus.Draft;
        if (template.Status != targetStatus)
        {
            template.Status = targetStatus;
            template.ArchivedAt = archived ? DateTimeOffset.UtcNow : null;
            Touch(template, userId);
        }
        return await SaveTemplateAsync(
            template,
            userId,
            archived ? "designer.templates.archived" : "designer.templates.restored",
            cancellationToken);
    }

    private async Task<ActionResult<DesignerTemplateDocument>> SaveTemplateAsync(
        DesignerTemplate template,
        Guid userId,
        string action,
        CancellationToken cancellationToken,
        object? details = null)
    {
        AddAudit(template, userId, action, details ?? new { template.Revision });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var current = await FindTemplateAsync(
                template.Id, template.OrganizationId, tracked: false, cancellationToken);
            return current is null ? NotFound() : await ConflictAsync(current, userId, cancellationToken);
        }
        SetRevisionHeader(template.Revision);
        return Ok(ToDocument(template));
    }

    private async Task<VersionCreationResult> CreateVersionInternalAsync(
        DesignerTemplate template,
        Guid userId,
        string? label,
        CancellationToken cancellationToken)
    {
        var latest = await dbContext.DesignerTemplateVersions
            .Where(value => value.TemplateId == template.Id &&
                            value.OrganizationId == template.OrganizationId)
            .OrderByDescending(value => value.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null &&
            string.Equals(latest.Checksum, template.DraftChecksum, StringComparison.Ordinal))
        {
            return new VersionCreationResult(latest, false);
        }

        var version = new DesignerTemplateVersion
        {
            TemplateId = template.Id,
            OrganizationId = template.OrganizationId,
            CreatedByUserId = userId,
            VersionNumber = (latest?.VersionNumber ?? 0) + 1,
            Label = NormalizeLabel(label),
            DesignJson = template.DraftJson,
            Checksum = template.DraftChecksum,
            SchemaVersion = template.SchemaVersion,
            DesignerVersion = template.DesignerVersion,
        };
        dbContext.DesignerTemplateVersions.Add(version);
        AddAudit(template, userId, "designer.templates.version-created", new { version.VersionNumber });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new VersionCreationResult(version, true);
    }

    private Task<DesignerTemplate?> FindTemplateAsync(
        Guid id,
        Guid organizationId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = tracked
            ? dbContext.DesignerTemplates.AsQueryable()
            : dbContext.DesignerTemplates.AsNoTracking();
        return query.SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
    }

    private Task<bool> TemplateExistsAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.DesignerTemplates.AsNoTracking().AnyAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);

    private async Task<ActionResult<DesignerTemplateDocument>> ConflictAsync(
        DesignerTemplate template,
        Guid userId,
        CancellationToken cancellationToken) =>
        Conflict(await CreateAuditedConflictBodyAsync(template, userId, cancellationToken));

    private async Task<ActionResult<CreateDesignerTemplateVersionResponse>> VersionConflictAsync(
        DesignerTemplate template,
        Guid userId,
        CancellationToken cancellationToken) =>
        Conflict(await CreateAuditedConflictBodyAsync(template, userId, cancellationToken));

    private async Task<object> CreateAuditedConflictBodyAsync(
        DesignerTemplate template,
        Guid userId,
        CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = template.OrganizationId,
            ActorUserId = userId,
            Action = "designer.templates.conflict",
            TargetType = "designer_template",
            TargetId = template.Id.ToString(),
            Outcome = "rejected",
            DetailsJson = JsonSerializer.Serialize(new { template.Revision }),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CreateConflictBodyAsync(template, cancellationToken);
    }

    private async Task<object> CreateConflictBodyAsync(
        DesignerTemplate template,
        CancellationToken cancellationToken)
    {
        var updater = await dbContext.Users.AsNoTracking()
            .Where(value => value.Id == template.UpdatedByUserId)
            .Select(value => value.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        return new
        {
            type = "https://powerdoxautomation.com/problems/designer-template-conflict",
            title = "Template revision conflict",
            status = StatusCodes.Status409Conflict,
            detail = "The template was changed by another session. Reload it before saving again.",
            code = "PXADESIGNER001",
            currentRevision = template.Revision,
            updatedBy = updater,
            template.UpdatedAt,
        };
    }

    private bool TryReadDesign(
        JsonElement document,
        out string json,
        out string checksum,
        out ActionResult<DesignerTemplateDocument>? failure)
    {
        json = string.Empty;
        checksum = string.Empty;
        failure = null;
        if (document.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            failure = TemplateValidationProblem("A design document is required.");
            return false;
        }
        json = document.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > settings.MaximumDesignJsonBytes)
        {
            failure = StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Design document is too large",
                    Detail = $"The uncompressed design JSON limit is {settings.MaximumDesignJsonBytes} bytes.",
                });
            return false;
        }
        checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return true;
    }

    private bool TryGetExpectedRevision(long bodyRevision, out long revision)
    {
        revision = bodyRevision;
        if (!Request.Headers.TryGetValue("If-Match", out var values))
            return revision > 0;
        var raw = values.ToString().Trim();
        if (raw.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            raw = raw[2..];
        raw = raw.Trim('"');
        return long.TryParse(raw, out revision) && revision > 0 && revision == bodyRevision;
    }

    private bool TryGetTenant(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private void Touch(DesignerTemplate template, Guid userId)
    {
        template.UpdatedByUserId = userId;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.Revision++;
    }

    private void AddAudit(DesignerTemplate template, Guid userId, string action, object details) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = template.OrganizationId,
            ActorUserId = userId,
            Action = action,
            TargetType = "designer_template",
            TargetId = template.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private void SetRevisionHeader(long revision)
    {
        Response.Headers.ETag = $"\"{revision}\"";
        Response.Headers["X-PXA-Template-Revision"] = revision.ToString();
    }

    private ActionResult<DesignerTemplateDocument> RevisionRequired() =>
        BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Template revision required",
            Detail = "Supply the current positive revision in the request body and, when available, If-Match.",
        });

    private ActionResult<DesignerTemplateDocument> TemplateValidationProblem(string detail) =>
        BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Designer template",
            Detail = detail,
        });

    private static bool TryValidateName(string? value, out string name)
    {
        name = value?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= 200;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized[..Math.Min(normalized.Length, 2000)];
    }

    private static string[] NormalizeTags(IReadOnlyList<string>? tags) =>
        (tags ?? [])
        .Select(value => value.Trim().ToLowerInvariant())
        .Where(value => value.Length is > 0 and <= 80)
        .Distinct(StringComparer.Ordinal)
        .Take(50)
        .ToArray();

    private static string NormalizeVersion(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized)
            ? fallback
            : normalized[..Math.Min(normalized.Length, 32)];
    }

    private static string? NormalizeLabel(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized)
            ? null
            : normalized[..Math.Min(normalized.Length, 200)];
    }

    private static DesignerTemplateDocument ToDocument(DesignerTemplate value) =>
        new(
            value.Id,
            value.Name,
            value.Description,
            value.Tags,
            value.Status.ToString(),
            value.Revision,
            ParseJson(value.DraftJson),
            value.DraftChecksum,
            value.SchemaVersion,
            value.DesignerVersion,
            value.PublishedVersionId,
            value.CreatedByUserId,
            value.UpdatedByUserId,
            value.CreatedAt,
            value.UpdatedAt,
            value.ArchivedAt);

    private static DesignerTemplateVersionDocument ToVersionDocument(DesignerTemplateVersion value) =>
        new(
            value.Id,
            value.VersionNumber,
            value.Label,
            ParseJson(value.DesignJson),
            value.Checksum,
            value.SchemaVersion,
            value.DesignerVersion,
            value.CreatedByUserId,
            value.CreatedAt);

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed record VersionCreationResult(DesignerTemplateVersion Version, bool Created);
}

public sealed record CreateDesignerTemplateRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? Tags,
    JsonElement DesignDocument,
    string? SchemaVersion = null,
    string? DesignerVersion = null);

public sealed record UpdateDesignerTemplateMetadataRequest(
    long Revision,
    string Name,
    string? Description,
    IReadOnlyList<string>? Tags);

public sealed record UpdateDesignerTemplateDraftRequest(
    long Revision,
    JsonElement DesignDocument,
    string? SchemaVersion = null,
    string? DesignerVersion = null);

public sealed record TemplateRevisionRequest(long Revision);
public sealed record CreateDesignerTemplateVersionRequest(long Revision, string? Label = null);
public sealed record PublishDesignerTemplateRequest(long Revision, long? VersionNumber = null, string? Label = null);

public sealed record DesignerTemplatePage(
    IReadOnlyList<DesignerTemplateSummary> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DesignerTemplateSummary(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    Guid? PublishedVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);

public sealed record DesignerTemplateDocument(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    JsonElement DesignDocument,
    string Checksum,
    string SchemaVersion,
    string DesignerVersion,
    Guid? PublishedVersionId,
    Guid CreatedByUserId,
    Guid UpdatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);

public sealed record DesignerTemplateVersionInfo(
    Guid Id,
    long VersionNumber,
    string? Label,
    string Checksum,
    string SchemaVersion,
    string DesignerVersion,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record DesignerTemplateVersionDocument(
    Guid Id,
    long VersionNumber,
    string? Label,
    JsonElement DesignDocument,
    string Checksum,
    string SchemaVersion,
    string DesignerVersion,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record CreateDesignerTemplateVersionResponse(
    bool Created,
    DesignerTemplateVersionDocument Version);
