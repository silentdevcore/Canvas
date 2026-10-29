using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Jobs;

public sealed class PvadJobMetricsPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<PvadJobOptions> options,
    ILogger<PvadJobMetricsPublisher> logger) : BackgroundService
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
                    PvadLogEvents.JobMetricsFailed,
                    exception,
                    "The PVAD background-job metrics cycle failed.");
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
        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        var now = DateTimeOffset.UtcNow;
        var queued = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(value => value.Status == PvadBackgroundJobStatus.Pending)
            .GroupBy(value => value.Type)
            .Select(group => new QueueSnapshot(
                group.Key,
                group.Count(),
                group.Min(value => value.CreatedAt)))
            .ToDictionaryAsync(value => value.Type, StringComparer.Ordinal, cancellationToken);

        foreach (var jobType in PvadJobQueue.SupportedTypes)
        {
            if (queued.TryGetValue(jobType, out var snapshot))
                PvadTelemetry.RecordJobQueue(
                    jobType,
                    snapshot.Depth,
                    now - snapshot.OldestCreatedAt);
            else
                PvadTelemetry.RecordJobQueue(jobType, 0, TimeSpan.Zero);
        }
    }

    private sealed record QueueSnapshot(string Type, int Depth, DateTimeOffset OldestCreatedAt);
}
