namespace PVAD.WebApi.Application.Retention;

public sealed class PvadRetentionStartupGate(
    PvadRetentionPolicyCatalog catalog,
    IHostEnvironment environment,
    ILogger<PvadRetentionStartupGate> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnsureProductionApproval();

        if (!catalog.IsProductionReady)
        {
            logger.LogWarning(
                "Retention governance is not production-ready. Unapproved policies remain visible in the protected Admin status.");
        }

        return Task.CompletedTask;
    }

    public void EnsureProductionApproval()
    {
        if (environment.IsProduction() && !catalog.IsProductionReady)
        {
            logger.LogCritical(new EventId(1001, "RetentionApprovalRequired"),
                "PVADSTART001: Production startup is blocked because retention policies or their legal approvals are incomplete.");
            throw new PvadRetentionApprovalRequiredException();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class PvadRetentionApprovalRequiredException() : InvalidOperationException(
    "Production startup is blocked because retention policies or their legal approvals are incomplete.");
