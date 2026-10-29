using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Storage;
using PVAD.WebApi.Services.Jobs;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pvad/v1/jobs")]
public sealed class JobsController(
    PvadDbContext dbContext,
    IPvadTenantContext tenantContext,
    PvadStoredObjectService storedObjects,
    IServiceScopeFactory scopeFactory,
    IOptions<PvadJobOptions> jobOptions) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PvadJobResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var job = await dbContext.BackgroundJobs.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
        return job is null ? NotFound() : Ok(ToResponse(job));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PvadJobResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var job = await dbContext.BackgroundJobs.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
        if (job is null)
            return NotFound();
        if (job.Status is PvadBackgroundJobStatus.Pending)
        {
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ExpiresAt = job.RetentionMode == PvadJobRetentionMode.Transient
                ? job.CompletedAt.Value.AddHours(job.Type == PvadJobQueue.PdfTrustInspectionType
                    ? jobOptions.Value.PdfTrustInspectionRetentionHours
                    : jobOptions.Value.TransientRetentionHours)
                : job.CompletedAt.Value.AddDays(jobOptions.Value.ResultRetentionDays);
            job.MetadataExpiresAt = job.CompletedAt.Value.AddDays(jobOptions.Value.TerminalMetadataRetentionDays);
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.BackgroundJobs.Where(value => value.Id == id && value.OrganizationId == organizationId &&
                    value.Status == PvadBackgroundJobStatus.Pending)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.Status, PvadBackgroundJobStatus.Cancelled)
                    .SetProperty(value => value.CompletedAt, job.CompletedAt)
                    .SetProperty(value => value.ExpiresAt, job.ExpiresAt)
                    .SetProperty(value => value.MetadataExpiresAt, job.MetadataExpiresAt)
                    .SetProperty(value => value.UpdatedAt, job.UpdatedAt)
                    .SetProperty(value => value.LeaseId, (Guid?)null)
                    .SetProperty(value => value.LeaseExpiresAt, (DateTimeOffset?)null), cancellationToken);
            job = await dbContext.BackgroundJobs.AsNoTracking().SingleAsync(
                value => value.Id == id && value.OrganizationId == organizationId, cancellationToken);
        }
        return Ok(ToResponse(job));
    }

    [HttpGet("{id:guid}/result")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var resultId = await dbContext.BackgroundJobs.AsNoTracking()
            .Where(value =>
                value.Id == id &&
                value.OrganizationId == organizationId &&
                value.Status == PvadBackgroundJobStatus.Completed)
            .Select(value => value.ResultObjectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (resultId is not { } objectId)
            return NotFound();
        var result = await storedObjects.OpenAsync(objectId, organizationId, cancellationToken);
        if (result is not null)
        {
            var retentionMode = await dbContext.BackgroundJobs.AsNoTracking()
                .Where(value => value.Id == id && value.OrganizationId == organizationId)
                .Select(value => value.RetentionMode)
                .SingleAsync(cancellationToken);
            if (retentionMode == PvadJobRetentionMode.Transient)
            {
                Response.OnCompleted(async () =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PvadJobRetentionService>()
                        .PurgeTransientContentAfterDownloadAsync(id, organizationId, CancellationToken.None);
                });
            }
        }
        return result is null
            ? NotFound()
            : File(result.Value.Content, result.Value.Metadata.ContentType, result.Value.Metadata.FileName);
    }

    private static PvadJobResponse ToResponse(PvadBackgroundJob job) => new(
        job.Id,
        job.Type,
        job.Status.ToString(),
        job.Attempts,
        job.ProgressPercent,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.ExpiresAt,
        job.RetentionMode.ToString(),
        job.ContentPurgedAt,
        job.Status == PvadBackgroundJobStatus.Completed && job.ResultObjectId is not null
            ? $"/api/pvad/v1/jobs/{job.Id}/result"
            : null,
        ParseDiagnostics(job.DiagnosticsJson),
        job.Status is PvadBackgroundJobStatus.Failed or PvadBackgroundJobStatus.DeadLetter
            ? job.FailureReason
            : null);

    private static JsonElement? ParseDiagnostics(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<JsonElement>(json);
}

public sealed record PvadJobResponse(
    Guid Id,
    string Type,
    string Status,
    int Attempts,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ExpiresAt,
    string RetentionMode,
    DateTimeOffset? ContentPurgedAt,
    string? DownloadUrl,
    JsonElement? Diagnostics,
    string? FailureReason);
