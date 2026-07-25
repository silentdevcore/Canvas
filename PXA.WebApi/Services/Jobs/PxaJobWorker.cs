using Microsoft.Extensions.Options;

namespace PXA.WebApi.Services.Jobs;

public sealed class PxaJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PxaJobOptions> options,
    ILogger<PxaJobWorker> logger) : BackgroundService
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
                    .GetRequiredService<PxaJobProcessor>()
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
                logger.LogError(exception, "The PXA background-job polling cycle failed.");
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
