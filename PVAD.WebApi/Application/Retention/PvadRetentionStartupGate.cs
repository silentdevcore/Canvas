namespace PVAD.WebApi.Application.Retention;

public sealed class PvadRetentionStartupGate(
    PvadRetentionPolicyCatalog catalog,
    IHostEnvironment environment,
    ILogger<PvadRetentionStartupGate> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsProduction() && !catalog.IsProductionReady)
        {
            throw new InvalidOperationException(
                "Production startup is blocked because retention policies or their legal approvals are incomplete.");
        }

        if (!catalog.IsProductionReady)
        {
            logger.LogWarning(
                "Retention governance is not production-ready. Unapproved policies remain visible in the protected Admin status.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
