using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PXA.WebApi.Observability;

internal sealed class PxaHealthMetricsPublisher : IHealthCheckPublisher
{
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in report.Entries)
        {
            PxaTelemetry.RecordDependencyHealth(
                name,
                entry.Status == HealthStatus.Healthy,
                entry.Duration);
        }

        PxaTelemetry.RecordServiceHeartbeat(DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
