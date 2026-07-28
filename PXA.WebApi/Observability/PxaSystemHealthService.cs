using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Observability;

public sealed class PxaSystemHealthService(
    HealthCheckService healthChecks,
    PxaDbContext dbContext,
    IHostEnvironment environment,
    IOptions<PxaMailOptions> mailOptions,
    IOptions<PxaObservabilityOptions> observabilityOptions)
{
    public async Task<PxaSystemHealthResponse> GetAsync(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);
        var database = FromHealthCheck(report, "pxa-database", "database", "PostgreSQL");
        var components = new List<PxaSystemHealthComponent>
        {
            Component("webapi", "Web API", PxaSystemHealthStatus.Healthy,
                "The administration API is responding."),
            database,
        };

        components.Add(await GetJobHealthAsync(
            Enum.Parse<PxaSystemHealthStatus>(database.Status),
            cancellationToken));
        components.Add(Component(
            "ocr",
            "OCR worker",
            PxaSystemHealthStatus.Healthy,
            "The isolated OCR worker is configured."));

        components.Add(mailOptions.Value.IsDeliveryEnabled
            ? FromHealthCheck(report, "pxa-mail", "mail", "Mail delivery")
            : Component(
                "mail",
                "Mail delivery",
                PxaSystemHealthStatus.Disabled,
                "Transactional mail delivery is disabled by configuration."));

        var telemetry = observabilityOptions.Value;
        components.Add(!telemetry.Enabled
            ? Component(
                "telemetry",
                "Telemetry",
                PxaSystemHealthStatus.Disabled,
                "Telemetry collection is disabled by configuration.")
            : string.IsNullOrWhiteSpace(telemetry.OtlpEndpoint)
                ? Component(
                    "telemetry",
                    "Telemetry",
                    PxaSystemHealthStatus.Degraded,
                    "Local instrumentation is active without a collector endpoint.")
                : Component(
                    "telemetry",
                    "Telemetry",
                    PxaSystemHealthStatus.Healthy,
                    "Telemetry export is configured."));

        var overall = components.Any(value =>
                value.Status == nameof(PxaSystemHealthStatus.Unhealthy))
            ? PxaSystemHealthStatus.Unhealthy
            : components.Any(value =>
                value.Status == nameof(PxaSystemHealthStatus.Degraded))
                ? PxaSystemHealthStatus.Degraded
                : PxaSystemHealthStatus.Healthy;
        return new PxaSystemHealthResponse(
            overall.ToString(),
            DateTimeOffset.UtcNow,
            components);
    }

    private async Task<PxaSystemHealthComponent> GetJobHealthAsync(
        PxaSystemHealthStatus databaseStatus,
        CancellationToken cancellationToken)
    {
        if (databaseStatus != PxaSystemHealthStatus.Healthy)
        {
            return Component(
                "jobs",
                "Background jobs",
                PxaSystemHealthStatus.Unhealthy,
                "Queue status is unavailable because PostgreSQL is unhealthy.");
        }

        try
        {
            var counts = await dbContext.BackgroundJobs.AsNoTracking()
                .Where(value =>
                    value.Status == PxaBackgroundJobStatus.Pending ||
                    value.Status == PxaBackgroundJobStatus.Processing ||
                    value.Status == PxaBackgroundJobStatus.DeadLetter)
                .GroupBy(value => value.Status)
                .Select(group => new { Status = group.Key, Count = group.LongCount() })
                .ToDictionaryAsync(value => value.Status, value => value.Count, cancellationToken);
            var oldestPendingAt = await dbContext.BackgroundJobs.AsNoTracking()
                .Where(value => value.Status == PxaBackgroundJobStatus.Pending)
                .Select(value => (DateTimeOffset?)value.CreatedAt)
                .MinAsync(cancellationToken);
            var deadLetter = counts.GetValueOrDefault(PxaBackgroundJobStatus.DeadLetter);
            var workerDisabled = environment.IsEnvironment("Testing");
            return new PxaSystemHealthComponent(
                "jobs",
                "Background jobs",
                (workerDisabled
                    ? PxaSystemHealthStatus.Disabled
                    : deadLetter > 0
                    ? PxaSystemHealthStatus.Degraded
                    : PxaSystemHealthStatus.Healthy).ToString(),
                workerDisabled
                    ? "The worker is disabled; queue aggregates remain available."
                    : deadLetter > 0
                    ? "One or more jobs require operator attention."
                    : "The background-job queue is operational.",
                counts.GetValueOrDefault(PxaBackgroundJobStatus.Pending),
                counts.GetValueOrDefault(PxaBackgroundJobStatus.Processing),
                deadLetter,
                oldestPendingAt is null
                    ? null
                    : Math.Max(0, (long)(DateTimeOffset.UtcNow - oldestPendingAt.Value).TotalSeconds));
        }
        catch
        {
            return Component(
                "jobs",
                "Background jobs",
                PxaSystemHealthStatus.Unhealthy,
                "Queue status could not be read.");
        }
    }

    private static PxaSystemHealthComponent FromHealthCheck(
        HealthReport report,
        string healthCheckName,
        string key,
        string name)
    {
        if (!report.Entries.TryGetValue(healthCheckName, out var entry))
        {
            return Component(
                key,
                name,
                PxaSystemHealthStatus.Unhealthy,
                "The health check is not registered.");
        }

        return entry.Status switch
        {
            HealthStatus.Healthy => Component(
                key, name, PxaSystemHealthStatus.Healthy, $"{name} is operational."),
            HealthStatus.Degraded => Component(
                key, name, PxaSystemHealthStatus.Degraded, $"{name} is degraded."),
            _ => Component(
                key, name, PxaSystemHealthStatus.Unhealthy, $"{name} is unavailable."),
        };
    }

    private static PxaSystemHealthComponent Component(
        string key,
        string name,
        PxaSystemHealthStatus status,
        string summary) =>
        new(key, name, status.ToString(), summary, null, null, null, null);
}

public sealed record PxaSystemHealthResponse(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<PxaSystemHealthComponent> Components);

public sealed record PxaSystemHealthComponent(
    string Key,
    string Name,
    string Status,
    string Summary,
    long? PendingJobs,
    long? ProcessingJobs,
    long? DeadLetterJobs,
    long? OldestPendingSeconds);

public enum PxaSystemHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Disabled,
}
