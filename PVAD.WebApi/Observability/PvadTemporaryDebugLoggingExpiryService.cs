using Microsoft.Extensions.Options;

namespace PVAD.WebApi.Observability;

internal sealed class PvadTemporaryDebugLoggingExpiryService(
    IOptions<PvadObservabilityOptions> options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<PvadTemporaryDebugLoggingExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var expiresAt = options.Value.DebugLoggingExpiresAtUtc
            ?? throw new InvalidOperationException("Temporary debug logging requires an expiry.");
        logger.LogWarning(
            PvadLogEvents.TemporaryDebugLoggingEnabled,
            "Temporary debug logging is enabled until {ExpiresAtUtc}",
            expiresAt.ToUniversalTime());

        var delay = expiresAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, stoppingToken);
        if (stoppingToken.IsCancellationRequested)
            return;

        logger.LogCritical(
            PvadLogEvents.TemporaryDebugLoggingExpired,
            "Temporary debug logging expired; stopping the application for a safe configuration reload");
        applicationLifetime.StopApplication();
    }
}
