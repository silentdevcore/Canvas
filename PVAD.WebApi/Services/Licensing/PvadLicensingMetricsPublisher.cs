using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadLicensingMetricsPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<PvadLicensingOptions> options,
    ILogger<PvadLicensingMetricsPublisher> logger) : BackgroundService
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
                    PvadLogEvents.LicensingMetricsFailed,
                    exception,
                    "The PVAD licensing metrics cycle failed.");
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

        PvadTelemetry.RecordLicenseInventory("active", active);
        PvadTelemetry.RecordLicenseInventory("expiring_14d", expiring);
        PvadTelemetry.RecordLicenseInventory("expired_active", expired);
    }
}
