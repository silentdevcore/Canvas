using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Storage;
using System.Text.Json;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/jobs")]
public sealed class JobsController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    PxaStoredObjectService storedObjects) : ControllerBase
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
        job.Status == PxaBackgroundJobStatus.Completed
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
    string? DownloadUrl,
    JsonElement? Diagnostics,
    string? FailureReason);
