using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace PXA.WebApi.Services.Mail;

public sealed class PxaMailHealthCheck : IHealthCheck
{
    private readonly PxaMailOptions options;

    public PxaMailHealthCheck(IOptions<PxaMailOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsDeliveryEnabled)
            return HealthCheckResult.Healthy("Mail delivery is explicitly disabled.");
        if (!string.Equals(options.Transport, "Smtp", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy($"Mail transport {options.Transport} is configured.");
        if (string.IsNullOrWhiteSpace(options.SmtpHost))
            return HealthCheckResult.Unhealthy("SMTP host is not configured.");

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(options.SmtpHost, options.SmtpPort, cancellationToken);
            return HealthCheckResult.Healthy("SMTP endpoint is reachable.");
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("SMTP endpoint is not reachable.", exception);
        }
    }
}
