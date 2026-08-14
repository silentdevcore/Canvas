using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Retention;
using PXA.WebApi.Observability;
using PXA.WebApi.Services.Storage;

namespace PXA.WebApi.Services.Jobs;

public sealed class PxaJobRetentionService(
    PxaDbContext dbContext,
    PxaStoredObjectService storedObjects,
    PxaRetentionLegalHoldService legalHolds,
    IOptions<PxaJobOptions> options)
{
    private readonly PxaJobOptions settings = options.Value;

    public async Task<bool> PurgeTransientContentAfterDownloadAsync(
        Guid jobId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs.SingleOrDefaultAsync(value =>
            value.Id == jobId &&
            value.OrganizationId == organizationId &&
            value.RetentionMode == PxaJobRetentionMode.Transient &&
            value.Status == PxaBackgroundJobStatus.Completed,
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
            PxaTelemetry.RecordJobRetention("held", 0);
            return 0;
        }
        var heldOrganizationIds = holdScope.OrganizationIds.ToArray();
        var jobs = await dbContext.BackgroundJobs
            .Where(value =>
                value.ExpiresAt <= now &&
                !heldOrganizationIds.Contains(value.OrganizationId) &&
                (value.Status == PxaBackgroundJobStatus.Completed ||
                 value.Status == PxaBackgroundJobStatus.Cancelled ||
                 value.Status == PxaBackgroundJobStatus.DeadLetter))
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
        PxaTelemetry.RecordJobRetention("expired", jobs.Length);
        PxaTelemetry.RecordJobRetention("storage_missing", missing);
        return jobs.Length;
    }

    private async Task PurgeContentAsync(
        PxaBackgroundJob job,
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
            job.Status = PxaBackgroundJobStatus.Expired;
        job.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PxaJobRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PxaJobOptions> options,
    ILogger<PxaJobRetentionWorker> logger) : BackgroundService
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
                await scope.ServiceProvider.GetRequiredService<PxaJobRetentionService>()
                    .CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    PxaLogEvents.JobRetentionFailed,
                    exception,
                    "PXA job retention and storage reconciliation failed.");
            }
        }
    }
}
