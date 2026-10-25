using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PVAD.Core.Contracts;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Jobs;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PvadAuthenticationSchemes.DesignerCookie)]
[EnableRateLimiting("designer-code")]
[Route("api/pvad/v1/designer/templates/{templateId:guid}/code-workspace")]
public sealed class DesignerCodeWorkspacesController(
    PvadDbContext dbContext,
    IPvadTenantContext tenantContext,
    IPvadCodeConversionService conversionService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [HttpGet]
    [Authorize(Policy = PvadDesignerPermissions.CodeRead)]
    public async Task<ActionResult<CodeWorkspaceDocument>> Get(Guid templateId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out var userId)) return Unauthorized();
        var template = await FindTemplate(templateId, organizationId, tracked: false, cancellationToken);
        if (template is null) return NotFound();
        var workspace = await dbContext.DesignerCodeWorkspaces
            .SingleOrDefaultAsync(value => value.TemplateId == templateId && value.OrganizationId == organizationId, cancellationToken);
        if (workspace is null)
        {
            workspace = CreateWorkspace(template, userId);
            dbContext.DesignerCodeWorkspaces.Add(workspace);
            AddAudit(organizationId, userId, workspace.Id, "designer.code-workspace.created", new { templateId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        if (string.IsNullOrEmpty(workspace.CSharpBase64Draft) &&
            workspace.CSharpModelDraft.Contains("FromBase64String", StringComparison.Ordinal))
        {
            workspace.CSharpBase64Draft = workspace.CSharpModelDraft;
            workspace.CSharpBase64Checksum = workspace.CSharpModelChecksum;
            var model = await conversionService.ConvertAsync(PvadCodeLanguages.Json, PvadCodeLanguages.CSharpModel,
                workspace.CanonicalDesignJson, cancellationToken);
            if (model.CanonicalDesign is not null && !model.Diagnostics.Any(value => value.Severity == "error"))
            {
                workspace.CSharpModelDraft = model.GeneratedSource;
                workspace.CSharpModelChecksum = model.ResultChecksum;
            }
            workspace.Revision++;
            workspace.UpdatedAt = DateTimeOffset.UtcNow;
            workspace.UpdatedByUserId = userId;
            AddAudit(organizationId, userId, workspace.Id, "designer.code-workspace.base64-migrated", new { workspace.Revision });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        SetEtag(workspace.Revision);
        return Ok(ToDocument(workspace, persisted: true));
    }

    [HttpPut]
    [Authorize(Policy = PvadDesignerPermissions.CodeEdit)]
    public async Task<ActionResult<CodeWorkspaceDocument>> Save(
        Guid templateId, SaveCodeWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out var userId)) return Unauthorized();
        if (!PvadCodeLanguages.Supported.Contains(request.Language)) return BadRequest(Problem("PXACODE120", "Unsupported code language."));
        if (System.Text.Encoding.UTF8.GetByteCount(request.Source) > PvadCodeLimits.MaximumSourceBytes)
            return BadRequest(Problem("PXACODE009", "Source exceeds the 32 MiB code-workspace limit."));
        var template = await FindTemplate(templateId, organizationId, tracked: false, cancellationToken);
        if (template is null) return NotFound();
        var workspace = await dbContext.DesignerCodeWorkspaces
            .SingleOrDefaultAsync(value => value.TemplateId == templateId && value.OrganizationId == organizationId, cancellationToken);
        if (workspace is null)
        {
            if (request.Revision is not (0 or 1)) return Conflict(Problem("PXACODE121", "The workspace was created by another session. Reload it."));
            workspace = CreateWorkspace(template, userId);
            dbContext.DesignerCodeWorkspaces.Add(workspace);
        }
        else if (workspace.Revision != request.Revision)
        {
            SetEtag(workspace.Revision);
            return Conflict(new
            {
                code = "PXACODE122",
                canonicalCode = PvadDiagnosticAliases.CanonicalCode("PXACODE122"),
                current = ToDocument(workspace, persisted: true),
            });
        }

        SetDraft(workspace, request.Language, request.Source);
        workspace.UpdatedByUserId = userId;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        workspace.Revision++;
        AddAudit(organizationId, userId, workspace.Id, "designer.code-workspace.saved", new { request.Language, workspace.Revision });
        await dbContext.SaveChangesAsync(cancellationToken);
        SetEtag(workspace.Revision);
        return Ok(ToDocument(workspace, persisted: true));
    }

    [HttpPost("validate")]
    [Authorize(Policy = PvadDesignerPermissions.CodeEdit)]
    public async Task<ActionResult<object>> Validate(
        Guid templateId, CodeOperationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateId, cancellationToken)) return NotFound();
        if (request.Language == PvadCodeLanguages.Json)
            return Ok(conversionService.ValidateJson(request.Source));
        var result = await conversionService.ExecuteAsync(request.Language, request.Source, cancellationToken);
        return Ok(new
        {
            result.Success,
            result.Fidelity,
            DocumentFidelity = result.Fidelity,
            SourcePreservation = PvadCodeSourcePreservation.Preserved,
            result.Diagnostics,
            result.SourceMap,
            result.CanonicalDesign,
        });
    }

    [HttpPost("convert")]
    [Authorize(Policy = PvadDesignerPermissions.CodeEdit)]
    public async Task<ActionResult<PvadCodeConversionResultDto>> Convert(
        Guid templateId, ConvertCodeWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateId, cancellationToken)) return NotFound();
        try
        {
            return Ok(await conversionService.ConvertAsync(
                request.SourceLanguage, request.TargetLanguage, request.Source, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem("PXACODE120", exception.Message));
        }
    }

    [HttpPost("execute")]
    [Authorize(Policy = PvadDesignerPermissions.CodeExecute)]
    public async Task<ActionResult<PvadCodeWorkerResponse>> Execute(
        Guid templateId, CodeOperationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateId, cancellationToken)) return NotFound();
        if (!TryTenant(out var organizationId, out var userId)) return Unauthorized();
        var started = DateTimeOffset.UtcNow;
        var job = new PvadBackgroundJob
        {
            OrganizationId = organizationId,
            CreatedByUserId = userId,
            Type = PvadJobQueue.DesignerCodeExecutionType,
            PayloadJson = JsonSerializer.Serialize(new
            {
                request.Language,
                operation = "execute",
                sourceChecksum = PvadCodeConversionService.Hash(request.Source),
            }, JsonOptions),
            Status = PvadBackgroundJobStatus.Processing,
            Attempts = 1,
            MaximumAttempts = 1,
            StartedAt = started,
            RetentionMode = PvadJobRetentionMode.Transient,
            ExpiresAt = started.AddHours(1),
            MetadataExpiresAt = started.AddDays(7),
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        var result = await conversionService.ExecuteAsync(request.Language, request.Source, cancellationToken);
        result.JobId = job.Id;
        job.Status = result.Success ? PvadBackgroundJobStatus.Completed : PvadBackgroundJobStatus.Failed;
        job.ProgressPercent = 100;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.UpdatedAt = job.CompletedAt.Value;
        job.DiagnosticsJson = JsonSerializer.Serialize(new
        {
            result.Fidelity,
            durationMilliseconds = Math.Max(0, (job.CompletedAt.Value - started).TotalMilliseconds),
            diagnosticCodes = result.Diagnostics.Select(value => value.Code).Distinct().Take(32),
        }, JsonOptions);
        job.FailureReason = result.Success ? null : result.Diagnostics.FirstOrDefault(value => value.Severity == "error")?.Code ?? "PXACODE_EXECUTION_FAILED";
        AddAudit(organizationId, userId, templateId, "designer.code-workspace.executed",
            new { jobId = job.Id, request.Language, result.Success, result.Fidelity, diagnosticCodes = result.Diagnostics.Select(value => value.Code).Distinct() });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("compare")]
    [Authorize(Policy = PvadDesignerPermissions.CodeApply)]
    public async Task<ActionResult<CompareCodeWorkspaceResponse>> Compare(
        Guid templateId, CompareCodeWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out _)) return Unauthorized();
        var template = await FindTemplate(templateId, organizationId, tracked: false, cancellationToken);
        var workspace = await dbContext.DesignerCodeWorkspaces.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TemplateId == templateId && value.OrganizationId == organizationId, cancellationToken);
        if (template is null || workspace is null) return NotFound();
        if (workspace.Revision != request.WorkspaceRevision)
            return Conflict(Problem("PXACODE122", "The code workspace changed. Preserve your drafts and reload before comparing."));

        PvadCodeConversionResultDto conversion;
        try
        {
            conversion = await conversionService.ConvertAsync(request.Language, PvadCodeLanguages.Json, request.Source, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem("PXACODE120", exception.Message));
        }
        if (conversion.CanonicalDesign is null || conversion.Diagnostics.Any(value => value.Severity == "error"))
            return UnprocessableEntity(conversion);

        // Reading a comparison never rebases the workspace or replaces any language draft.
        return Ok(new CompareCodeWorkspaceResponse(template.Revision, workspace.Revision,
            workspace.BaseTemplateRevision, Parse(CanonicalFromStoredDesign(template.DraftJson)), conversion));
    }

    [HttpPost("apply")]
    [Authorize(Policy = PvadDesignerPermissions.CodeApply)]
    public async Task<ActionResult<ApplyCodeWorkspaceResponse>> Apply(
        Guid templateId, ApplyCodeWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out var userId)) return Unauthorized();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var template = await FindTemplate(templateId, organizationId, tracked: true, cancellationToken);
        var workspace = await dbContext.DesignerCodeWorkspaces.SingleOrDefaultAsync(value =>
            value.TemplateId == templateId && value.OrganizationId == organizationId, cancellationToken);
        if (template is null || workspace is null) return NotFound();
        if (template.Revision != request.TemplateRevision || workspace.Revision != request.WorkspaceRevision)
            return Conflict(Problem("PXACODE123", "Template or code workspace changed. Compare and retry."));

        PvadCodeConversionResultDto result;
        try
        {
            result = await conversionService.ConvertAsync(request.Language, PvadCodeLanguages.Json, request.Source, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(Problem("PXACODE120", exception.Message));
        }
        if (result.CanonicalDesign is null || result.Diagnostics.Any(value => value.Severity == "error"))
            return UnprocessableEntity(result);
        if (request.ReviewedChecksum is not null && request.ReviewedChecksum != result.CanonicalChecksum)
            return Conflict(Problem("PXACODE123", "The code result changed since comparison. Compare again before applying."));

        var canonical = JsonSerializer.Serialize(result.CanonicalDesign, JsonOptions);
        var persistedDesign = ApplyCanonicalToStoredDesign(template.DraftJson, result.CanonicalDesign);
        template.DraftJson = persistedDesign;
        template.DraftChecksum = PvadCodeConversionService.Hash(persistedDesign);
        template.Revision++;
        template.UpdatedByUserId = userId;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        workspace.CanonicalDesignJson = canonical;
        workspace.CanonicalChecksum = result.CanonicalChecksum;
        workspace.SourceMapJson = JsonSerializer.Serialize(result.SourceMap, JsonOptions);
        workspace.BaseTemplateRevision = template.Revision;
        workspace.Revision++;
        workspace.UpdatedByUserId = userId;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        SetDraft(workspace, request.Language, request.Source);
        AddAudit(organizationId, userId, workspace.Id, "designer.code-workspace.applied",
            new { request.Language, result.Fidelity, templateRevision = template.Revision, workspaceRevision = workspace.Revision });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(Problem("PXACODE123", "Template or code workspace changed. Compare and retry."));
        }
        await transaction.CommitAsync(cancellationToken);
        SetEtag(workspace.Revision);
        return Ok(new ApplyCodeWorkspaceResponse(template.Revision, workspace.Revision, result));
    }

    [HttpGet("source-map")]
    [Authorize(Policy = PvadDesignerPermissions.CodeRead)]
    public async Task<ActionResult<IReadOnlyList<PvadCodeSourceMapEntryDto>>> SourceMap(
        Guid templateId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out _)) return Unauthorized();
        var json = await dbContext.DesignerCodeWorkspaces.AsNoTracking()
            .Where(value => value.TemplateId == templateId && value.OrganizationId == organizationId)
            .Select(value => value.SourceMapJson).SingleOrDefaultAsync(cancellationToken);
        return json is null ? NotFound() : Ok(JsonSerializer.Deserialize<List<PvadCodeSourceMapEntryDto>>(json, JsonOptions) ?? []);
    }

    [HttpPost("restore")]
    [Authorize(Policy = PvadDesignerPermissions.CodeEdit)]
    public async Task<ActionResult<CodeWorkspaceDocument>> Restore(
        Guid templateId, RestoreCodeWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var organizationId, out var userId)) return Unauthorized();
        var workspace = await dbContext.DesignerCodeWorkspaces.SingleOrDefaultAsync(value =>
            value.TemplateId == templateId && value.OrganizationId == organizationId, cancellationToken);
        if (workspace is null) return NotFound();
        if (workspace.Revision != request.Revision)
            return Conflict(Problem("PXACODE122", "The workspace changed. Reload it before restoring."));
        if (!PvadCodeLanguages.Supported.Contains(request.Language))
            return BadRequest(Problem("PXACODE120", "Unsupported code language."));
        SetDraft(workspace, request.Language,
            request.Language == PvadCodeLanguages.Json ? workspace.CanonicalDesignJson : string.Empty);
        workspace.Revision++;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        workspace.UpdatedByUserId = userId;
        AddAudit(organizationId, userId, workspace.Id, "designer.code-workspace.restored", new { request.Language, workspace.Revision });
        await dbContext.SaveChangesAsync(cancellationToken);
        SetEtag(workspace.Revision);
        return Ok(ToDocument(workspace, persisted: true));
    }

    private Task<DesignerTemplate?> FindTemplate(Guid id, Guid organizationId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.DesignerTemplates.AsQueryable() : dbContext.DesignerTemplates.AsNoTracking();
        return query.SingleOrDefaultAsync(value => value.Id == id && value.OrganizationId == organizationId && value.Status != DesignerTemplateStatus.Archived, cancellationToken);
    }

    private async Task<bool> CanAccess(Guid templateId, CancellationToken cancellationToken) =>
        TryTenant(out var organizationId, out _) && await dbContext.DesignerTemplates.AsNoTracking()
            .AnyAsync(value => value.Id == templateId && value.OrganizationId == organizationId && value.Status != DesignerTemplateStatus.Archived, cancellationToken);

    private bool TryTenant(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private static DesignerCodeWorkspace CreateWorkspace(DesignerTemplate template, Guid userId) => new()
    {
        TemplateId = template.Id, OrganizationId = template.OrganizationId, UpdatedByUserId = userId,
        Revision = 0,
        JsonDraft = CanonicalFromStoredDesign(template.DraftJson), JsonChecksum = PvadCodeConversionService.Hash(CanonicalFromStoredDesign(template.DraftJson)),
        CanonicalDesignJson = CanonicalFromStoredDesign(template.DraftJson), CanonicalChecksum = PvadCodeConversionService.Hash(CanonicalFromStoredDesign(template.DraftJson)),
        CSharpModelChecksum = PvadCodeConversionService.Hash(""), CSharpPdfChecksum = PvadCodeConversionService.Hash(""),
        CSharpBase64Checksum = PvadCodeConversionService.Hash(""),
        BaseTemplateRevision = template.Revision,
    };

    private static void SetDraft(DesignerCodeWorkspace workspace, string language, string source)
    {
        var checksum = PvadCodeConversionService.Hash(source);
        switch (language)
        {
            case PvadCodeLanguages.Json: workspace.JsonDraft = source; workspace.JsonChecksum = checksum; break;
            case PvadCodeLanguages.CSharpModel: workspace.CSharpModelDraft = source; workspace.CSharpModelChecksum = checksum; break;
            case PvadCodeLanguages.CSharpPdf: workspace.CSharpPdfDraft = source; workspace.CSharpPdfChecksum = checksum; break;
            case PvadCodeLanguages.CSharpBase64: workspace.CSharpBase64Draft = source; workspace.CSharpBase64Checksum = checksum; break;
        }
    }

    private static CodeWorkspaceDocument InitialDocument(DesignerTemplate template) => new(
        Guid.Empty, template.Id, 0, template.Revision, false,
        new CodeDraftDocument(CanonicalFromStoredDesign(template.DraftJson), PvadCodeConversionService.Hash(CanonicalFromStoredDesign(template.DraftJson))),
        new CodeDraftDocument("", PvadCodeConversionService.Hash("")),
        new CodeDraftDocument("", PvadCodeConversionService.Hash("")),
        new CodeDraftDocument("", PvadCodeConversionService.Hash("")),
        Parse(CanonicalFromStoredDesign(template.DraftJson)), [], PvadCodeConversionService.Hash(CanonicalFromStoredDesign(template.DraftJson)), template.UpdatedAt);

    private static CodeWorkspaceDocument ToDocument(DesignerCodeWorkspace value, bool persisted) => new(
        value.Id, value.TemplateId, value.Revision, value.BaseTemplateRevision, persisted,
        new CodeDraftDocument(value.JsonDraft, value.JsonChecksum),
        new CodeDraftDocument(value.CSharpModelDraft, value.CSharpModelChecksum),
        new CodeDraftDocument(value.CSharpPdfDraft, value.CSharpPdfChecksum),
        new CodeDraftDocument(value.CSharpBase64Draft, value.CSharpBase64Checksum),
        Parse(value.CanonicalDesignJson),
        JsonSerializer.Deserialize<List<PvadCodeSourceMapEntryDto>>(value.SourceMapJson, JsonOptions) ?? [],
        value.CanonicalChecksum, value.UpdatedAt);

    private static JsonElement Parse(string value) { using var document = JsonDocument.Parse(value); return document.RootElement.Clone(); }
    internal static string CanonicalFromStoredDesign(string storedJson)
    {
        var root = JsonNode.Parse(storedJson) as JsonObject;
        if (root?["template"] is null)
            return JsonSerializer.Serialize(JsonSerializer.Deserialize<DesignExportDto>(storedJson, JsonOptions), JsonOptions);
        var design = root["template"]!.Deserialize<DesignExportDto>(JsonOptions) ?? new DesignExportDto();
        if (root["pageSettings"] is not null)
            design.PageSettings = root["pageSettings"]!.Deserialize<PageSettingsDto>(JsonOptions);
        return JsonSerializer.Serialize(design, JsonOptions);
    }

    internal static string ApplyCanonicalToStoredDesign(string storedJson, DesignExportDto design)
    {
        var root = JsonNode.Parse(storedJson) as JsonObject ?? new JsonObject();
        if (root["template"] is null)
            return JsonSerializer.Serialize(design, JsonOptions);
        var templateNode = JsonSerializer.SerializeToNode(design, JsonOptions) as JsonObject ?? new JsonObject();
        templateNode.Remove("pageSettings");
        templateNode.Remove("importDiagnostics");
        templateNode.Remove("extensions");
        root["template"] = templateNode;
        root["pageSettings"] = JsonSerializer.SerializeToNode(design.PageSettings ?? new PageSettingsDto(), JsonOptions);
        return root.ToJsonString(JsonOptions);
    }
    private static ProblemDetails Problem(string code, string detail) => new() { Status = 400, Title = "Code workspace request failed", Detail = detail, Extensions = { ["code"] = code } };
    private void SetEtag(long revision) => Response.Headers.ETag = $"\"{revision}\"";
    private void AddAudit(Guid organizationId, Guid userId, Guid targetId, string action, object details) => dbContext.AuditEvents.Add(new AuditEvent
    {
        OrganizationId = organizationId, ActorUserId = userId, Action = action, TargetType = "designer_code_workspace",
        TargetId = targetId.ToString(), Outcome = "succeeded", DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
    });
}

public sealed record CodeDraftDocument(string Source, string Checksum);
public sealed record CodeWorkspaceDocument(Guid Id, Guid TemplateId, long Revision, long BaseTemplateRevision, bool Persisted,
    CodeDraftDocument Json, CodeDraftDocument CSharpModel, CodeDraftDocument CSharpPdf, CodeDraftDocument CSharpBase64,
    JsonElement CanonicalDesign, IReadOnlyList<PvadCodeSourceMapEntryDto> SourceMap, string CanonicalChecksum, DateTimeOffset UpdatedAt);
public sealed record SaveCodeWorkspaceRequest(long Revision, string Language, string Source);
public sealed record CodeOperationRequest(string Language, string Source);
public sealed record ConvertCodeWorkspaceRequest(string SourceLanguage, string TargetLanguage, string Source);
public sealed record CompareCodeWorkspaceRequest(long WorkspaceRevision, string Language, string Source);
public sealed record CompareCodeWorkspaceResponse(long TemplateRevision, long WorkspaceRevision, long BaseTemplateRevision,
    JsonElement TemplateDesign, PvadCodeConversionResultDto Conversion);
public sealed record ApplyCodeWorkspaceRequest(long WorkspaceRevision, long TemplateRevision, string Language, string Source,
    string? ReviewedChecksum = null);
public sealed record RestoreCodeWorkspaceRequest(long Revision, string Language);
public sealed record ApplyCodeWorkspaceResponse(long TemplateRevision, long WorkspaceRevision, PvadCodeConversionResultDto Conversion);
