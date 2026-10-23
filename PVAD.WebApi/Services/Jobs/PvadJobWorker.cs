using Microsoft.Extensions.Options;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Jobs;

public sealed class PvadJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PvadJobOptions> options,
    ILogger<PvadJobWorker> logger) : BackgroundService
{
    private readonly TimeSpan idleDelay = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processed = await scope.ServiceProvider
                    .GetRequiredService<PvadJobProcessor>()
                    .ProcessNextAsync(stoppingToken);
                if (processed)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    PvadLogEvents.JobPollingFailed,
                    exception,
                    "The PVAD background-job polling cycle failed.");
            }

            try
            {
                await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
