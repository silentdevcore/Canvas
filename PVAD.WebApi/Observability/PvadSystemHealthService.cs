using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Services.Mail;

namespace PVAD.WebApi.Observability;

public sealed class PvadSystemHealthService(
    HealthCheckService healthChecks,
    PvadDbContext dbContext,
    IHostEnvironment environment,
    IOptions<PvadMailOptions> mailOptions,
    IOptions<PvadObservabilityOptions> observabilityOptions)
{
    public async Task<PvadSystemHealthResponse> GetAsync(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);
        var database = FromHealthCheck(report, "pxa-database", "database", "PostgreSQL");
        var components = new List<PvadSystemHealthComponent>
        {
            Component("webapi", "Web API", PvadSystemHealthStatus.Healthy,
                "The administration API is responding."),
            database,
        };

        components.Add(await GetJobHealthAsync(
            Enum.Parse<PvadSystemHealthStatus>(database.Status),
            cancellationToken));
        components.Add(Component(
            "ocr",
            "OCR worker",
            PvadSystemHealthStatus.Healthy,
            "The isolated OCR worker is configured."));

        components.Add(mailOptions.Value.IsDeliveryEnabled
            ? FromHealthCheck(report, "pxa-mail", "mail", "Mail delivery")
            : Component(
                "mail",
                "Mail delivery",
                PvadSystemHealthStatus.Disabled,
                "Transactional mail delivery is disabled by configuration."));

        var telemetry = observabilityOptions.Value;
        components.Add(!telemetry.Enabled
            ? Component(
                "telemetry",
                "Telemetry",
                PvadSystemHealthStatus.Disabled,
                "Telemetry collection is disabled by configuration.")
            : string.IsNullOrWhiteSpace(telemetry.OtlpEndpoint)
                ? Component(
                    "telemetry",
                    "Telemetry",
                    PvadSystemHealthStatus.Degraded,
                    "Local instrumentation is active without a collector endpoint.")
                : Component(
                    "telemetry",
                    "Telemetry",
                    PvadSystemHealthStatus.Healthy,
                    "Telemetry export is configured."));

        var overall = components.Any(value =>
                value.Status == nameof(PvadSystemHealthStatus.Unhealthy))
            ? PvadSystemHealthStatus.Unhealthy
            : components.Any(value =>
                value.Status == nameof(PvadSystemHealthStatus.Degraded))
                ? PvadSystemHealthStatus.Degraded
                : PvadSystemHealthStatus.Healthy;
        return new PvadSystemHealthResponse(
            overall.ToString(),
            DateTimeOffset.UtcNow,
            components);
    }

    private async Task<PvadSystemHealthComponent> GetJobHealthAsync(
        PvadSystemHealthStatus databaseStatus,
        CancellationToken cancellationToken)
    {
        if (databaseStatus != PvadSystemHealthStatus.Healthy)
        {
            return Component(
                "jobs",
                "Background jobs",
                PvadSystemHealthStatus.Unhealthy,
                "Queue status is unavailable because PostgreSQL is unhealthy.");
        }

        try
        {
            var counts = await dbContext.BackgroundJobs.AsNoTracking()
                .Where(value =>
                    value.Status == PvadBackgroundJobStatus.Pending ||
                    value.Status == PvadBackgroundJobStatus.Processing ||
                    value.Status == PvadBackgroundJobStatus.DeadLetter)
                .GroupBy(value => value.Status)
                .Select(group => new { Status = group.Key, Count = group.LongCount() })
                .ToDictionaryAsync(value => value.Status, value => value.Count, cancellationToken);
            var oldestPendingAt = await dbContext.BackgroundJobs.AsNoTracking()
                .Where(value => value.Status == PvadBackgroundJobStatus.Pending)
                .Select(value => (DateTimeOffset?)value.CreatedAt)
                .MinAsync(cancellationToken);
            var deadLetter = counts.GetValueOrDefault(PvadBackgroundJobStatus.DeadLetter);
            var workerDisabled = environment.IsEnvironment("Testing");
            return new PvadSystemHealthComponent(
                "jobs",
                "Background jobs",
                (workerDisabled
                    ? PvadSystemHealthStatus.Disabled
                    : deadLetter > 0
                    ? PvadSystemHealthStatus.Degraded
                    : PvadSystemHealthStatus.Healthy).ToString(),
                workerDisabled
                    ? "The worker is disabled; queue aggregates remain available."
                    : deadLetter > 0
                    ? "One or more jobs require operator attention."
                    : "The background-job queue is operational.",
                counts.GetValueOrDefault(PvadBackgroundJobStatus.Pending),
                counts.GetValueOrDefault(PvadBackgroundJobStatus.Processing),
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
                PvadSystemHealthStatus.Unhealthy,
                "Queue status could not be read.");
        }
    }

    private static PvadSystemHealthComponent FromHealthCheck(
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
                PvadSystemHealthStatus.Unhealthy,
                "The health check is not registered.");
        }

        return entry.Status switch
        {
            HealthStatus.Healthy => Component(
                key, name, PvadSystemHealthStatus.Healthy, $"{name} is operational."),
            HealthStatus.Degraded => Component(
                key, name, PvadSystemHealthStatus.Degraded, $"{name} is degraded."),
            _ => Component(
                key, name, PvadSystemHealthStatus.Unhealthy, $"{name} is unavailable."),
        };
    }

    private static PvadSystemHealthComponent Component(
        string key,
        string name,
        PvadSystemHealthStatus status,
        string summary) =>
        new(key, name, status.ToString(), summary, null, null, null, null);
}

public sealed record PvadSystemHealthResponse(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<PvadSystemHealthComponent> Components);

public sealed record PvadSystemHealthComponent(
    string Key,
    string Name,
    string Status,
    string Summary,
    long? PendingJobs,
    long? ProcessingJobs,
    long? DeadLetterJobs,
    long? OldestPendingSeconds);

public enum PvadSystemHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Disabled,
}
