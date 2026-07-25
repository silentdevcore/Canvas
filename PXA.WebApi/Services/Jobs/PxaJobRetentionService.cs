using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Services.Storage;

namespace PXA.WebApi.Services.Jobs;

public sealed class PxaJobRetentionService(
    PxaDbContext dbContext,
    PxaStoredObjectService storedObjects,
    IOptions<PxaJobOptions> options)
{
    private readonly PxaJobOptions settings = options.Value;

    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = await dbContext.BackgroundJobs
            .Where(value =>
                value.ExpiresAt <= now &&
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

        await storedObjects.ReconcileMissingAsync(settings.CleanupBatchSize, cancellationToken);
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
                logger.LogError(exception, "PXA job retention and storage reconciliation failed.");
            }
        }
    }
}
