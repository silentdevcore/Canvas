using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Application.Retention;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Services.Storage;

namespace PVAD.WebApi.Services.Jobs;

public sealed class PvadJobRetentionService(
    PvadDbContext dbContext,
    PvadStoredObjectService storedObjects,
    PvadRetentionLegalHoldService legalHolds,
    IOptions<PvadJobOptions> options)
{
    private readonly PvadJobOptions settings = options.Value;

    public async Task<bool> PurgeTransientContentAfterDownloadAsync(
        Guid jobId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs.SingleOrDefaultAsync(value =>
            value.Id == jobId &&
            value.OrganizationId == organizationId &&
            value.RetentionMode == PvadJobRetentionMode.Transient &&
            value.Status == PvadBackgroundJobStatus.Completed,
            cancellationToken);
        if (job is null)
            return false;

        var holdScope = await legalHolds.GetActiveScopeAsync(
            "background-document-jobs",
            cancellationToken);
        if (holdScope.Holds(organizationId))
            return false;

        await PurgeContentAsync(job, DateTimeOffset.UtcNow, downloaded: true, cancellationToken);
        return true;
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var holdScope = await legalHolds.GetActiveScopeAsync(
            "background-document-jobs",
            cancellationToken);
        if (holdScope.Global)
        {
            PvadTelemetry.RecordJobRetention("held", 0);
            return 0;
        }
        var heldOrganizationIds = holdScope.OrganizationIds.ToArray();
        var jobs = await dbContext.BackgroundJobs
            .Where(value =>
                value.ExpiresAt <= now &&
                !heldOrganizationIds.Contains(value.OrganizationId) &&
                (value.Status == PvadBackgroundJobStatus.Completed ||
                 value.Status == PvadBackgroundJobStatus.Cancelled ||
                 value.Status == PvadBackgroundJobStatus.DeadLetter))
            .OrderBy(value => value.ExpiresAt)
            .Take(settings.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        foreach (var job in jobs)
            await PurgeContentAsync(job, now, downloaded: false, cancellationToken);

        var expiredMetadata = await dbContext.BackgroundJobs
            .Where(value =>
                value.MetadataExpiresAt <= now &&
                value.ContentPurgedAt != null &&
                !heldOrganizationIds.Contains(value.OrganizationId))
            .OrderBy(value => value.MetadataExpiresAt)
            .Take(settings.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);
        dbContext.BackgroundJobs.RemoveRange(expiredMetadata);
        await dbContext.SaveChangesAsync(cancellationToken);

        var missing = await storedObjects.ReconcileMissingAsync(
            settings.CleanupBatchSize,
            cancellationToken);
        PvadTelemetry.RecordJobRetention("expired", jobs.Length);
        PvadTelemetry.RecordJobRetention("storage_missing", missing);
        return jobs.Length;
    }

    private async Task PurgeContentAsync(
        PvadBackgroundJob job,
        DateTimeOffset now,
        bool downloaded,
        CancellationToken cancellationToken)
    {
        var objectIds = new[] { job.InputObjectId, job.ResultObjectId }
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        foreach (var objectId in objectIds)
            await storedObjects.DeleteAsync(objectId, job.OrganizationId, cancellationToken);
        job.InputObjectId = null;
        job.ResultObjectId = null;
        job.PayloadJson = "{}";
        job.DiagnosticsJson = null;
        job.FailureReason = null;
        job.ContentPurgedAt = now;
        job.ResultDownloadedAt = downloaded ? now : job.ResultDownloadedAt;
        if (!downloaded)
            job.Status = PvadBackgroundJobStatus.Expired;
        job.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PvadJobRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PvadJobOptions> options,
    ILogger<PvadJobRetentionWorker> logger) : BackgroundService
{
    private readonly TimeSpan interval = TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PvadJobRetentionService>()
                    .CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    PvadLogEvents.JobRetentionFailed,
                    exception,
                    "PVAD job retention and storage reconciliation failed.");
            }
        }
    }
}
