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
        {
            var objectIds = new[] { job.InputObjectId, job.ResultObjectId }
                .OfType<Guid>()
                .Distinct()
                .ToArray();
            job.InputObjectId = null;
            job.ResultObjectId = null;
            job.Status = PxaBackgroundJobStatus.Expired;
            job.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var objectId in objectIds)
                await storedObjects.DeleteAsync(objectId, job.OrganizationId, cancellationToken);
        }

        var missing = await storedObjects.ReconcileMissingAsync(
            settings.CleanupBatchSize,
            cancellationToken);
        PxaTelemetry.RecordJobRetention("expired", jobs.Length);
        PxaTelemetry.RecordJobRetention("storage_missing", missing);
        return jobs.Length;
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
