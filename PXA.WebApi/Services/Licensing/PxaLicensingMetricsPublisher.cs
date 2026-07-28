using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Services.Licensing;

public sealed class PxaLicensingMetricsPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<PxaLicensingOptions> options,
    ILogger<PxaLicensingMetricsPublisher> logger) : BackgroundService
{
    private readonly TimeSpan interval =
        TimeSpan.FromSeconds(options.Value.MetricsIntervalSeconds);

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
                    PxaLogEvents.LicensingMetricsFailed,
                    exception,
                    "The PXA licensing metrics cycle failed.");
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
        var activeLicenses = dbContext.OfflineLicenses
            .AsNoTracking()
            .Where(value => value.Status == OfflineLicenseStatus.Active);

        var active = await activeLicenses.CountAsync(cancellationToken);
        var expiring = await activeLicenses.CountAsync(
            value => value.ValidUntil > now &&
                     value.ValidUntil <= now.AddDays(14),
            cancellationToken);
        var expired = await activeLicenses.CountAsync(
            value => value.ValidUntil <= now,
            cancellationToken);

        PxaTelemetry.RecordLicenseInventory("active", active);
        PxaTelemetry.RecordLicenseInventory("expiring_14d", expiring);
        PxaTelemetry.RecordLicenseInventory("expired_active", expired);
    }
}
