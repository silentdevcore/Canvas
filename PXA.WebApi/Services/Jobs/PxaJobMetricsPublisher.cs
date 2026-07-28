using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Services.Jobs;

public sealed class PxaJobMetricsPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<PxaJobOptions> options,
    ILogger<PxaJobMetricsPublisher> logger) : BackgroundService
{
    private readonly TimeSpan interval = TimeSpan.FromSeconds(options.Value.MetricsIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    PxaLogEvents.JobMetricsFailed,
                    exception,
                    "The PXA background-job metrics cycle failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task PublishAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PxaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var queued = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(value => value.Status == PxaBackgroundJobStatus.Pending)
            .GroupBy(value => value.Type)
            .Select(group => new QueueSnapshot(
                group.Key,
                group.Count(),
                group.Min(value => value.CreatedAt)))
            .ToDictionaryAsync(value => value.Type, StringComparer.Ordinal, cancellationToken);

        foreach (var jobType in PxaJobQueue.SupportedTypes)
        {
            if (queued.TryGetValue(jobType, out var snapshot))
                PxaTelemetry.RecordJobQueue(
                    jobType,
                    snapshot.Depth,
                    now - snapshot.OldestCreatedAt);
            else
                PxaTelemetry.RecordJobQueue(jobType, 0, TimeSpan.Zero);
        }
    }

    private sealed record QueueSnapshot(string Type, int Depth, DateTimeOffset OldestCreatedAt);
}
