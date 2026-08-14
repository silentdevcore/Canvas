using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Storage;
using PXA.WebApi.Services.Jobs;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/jobs")]
public sealed class JobsController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    PxaStoredObjectService storedObjects,
    IServiceScopeFactory scopeFactory,
    IOptions<PxaJobOptions> jobOptions) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PxaJobResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var job = await dbContext.BackgroundJobs.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
        return job is null ? NotFound() : Ok(ToResponse(job));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PxaJobResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();
        var job = await dbContext.BackgroundJobs.SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
        if (job is null)
            return NotFound();
        if (job.Status is PxaBackgroundJobStatus.Pending)
        {
            job.Status = PxaBackgroundJobStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ExpiresAt = job.RetentionMode == PxaJobRetentionMode.Transient
                ? job.CompletedAt.Value.AddHours(jobOptions.Value.TransientRetentionHours)
                : job.CompletedAt.Value.AddDays(jobOptions.Value.ResultRetentionDays);
            job.MetadataExpiresAt = job.CompletedAt.Value.AddDays(jobOptions.Value.TerminalMetadataRetentionDays);
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
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
                value.Status == PxaBackgroundJobStatus.Completed)
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
            if (retentionMode == PxaJobRetentionMode.Transient)
            {
                Response.OnCompleted(async () =>
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PxaJobRetentionService>()
                        .PurgeTransientContentAfterDownloadAsync(id, organizationId, CancellationToken.None);
                });
            }
        }
        return result is null
            ? NotFound()
            : File(result.Value.Content, result.Value.Metadata.ContentType, result.Value.Metadata.FileName);
    }

    private static PxaJobResponse ToResponse(PxaBackgroundJob job) => new(
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
        job.Status == PxaBackgroundJobStatus.Completed && job.ResultObjectId is not null
            ? $"/api/pxa/v1/jobs/{job.Id}/result"
            : null,
        ParseDiagnostics(job.DiagnosticsJson),
        job.Status is PxaBackgroundJobStatus.Failed or PxaBackgroundJobStatus.DeadLetter
            ? job.FailureReason
            : null);

    private static JsonElement? ParseDiagnostics(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<JsonElement>(json);
}

public sealed record PxaJobResponse(
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
