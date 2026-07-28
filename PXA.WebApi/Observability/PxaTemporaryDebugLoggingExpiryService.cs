using Microsoft.Extensions.Options;

namespace PXA.WebApi.Observability;

internal sealed class PxaTemporaryDebugLoggingExpiryService(
    IOptions<PxaObservabilityOptions> options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<PxaTemporaryDebugLoggingExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var expiresAt = options.Value.DebugLoggingExpiresAtUtc
            ?? throw new InvalidOperationException("Temporary debug logging requires an expiry.");
        logger.LogWarning(
            PxaLogEvents.TemporaryDebugLoggingEnabled,
            "Temporary debug logging is enabled until {ExpiresAtUtc}",
            expiresAt.ToUniversalTime());

        var delay = expiresAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, stoppingToken);
        if (stoppingToken.IsCancellationRequested)
            return;

        logger.LogCritical(
            PxaLogEvents.TemporaryDebugLoggingExpired,
            "Temporary debug logging expired; stopping the application for a safe configuration reload");
        applicationLifetime.StopApplication();
    }
}
