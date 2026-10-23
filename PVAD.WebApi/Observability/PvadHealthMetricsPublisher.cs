using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PVAD.WebApi.Observability;

internal sealed class PvadHealthMetricsPublisher : IHealthCheckPublisher
{
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in report.Entries)
        {
            PvadTelemetry.RecordDependencyHealth(
                name,
                entry.Status == HealthStatus.Healthy,
                entry.Duration);
        }

        PvadTelemetry.RecordServiceHeartbeat(DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
