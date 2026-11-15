using PVAD.WebApi.Application.Retention;

namespace PVAD.WebApi.Infrastructure;

internal static class PvadApplicationLifetime
{
    internal const int ConfigurationRejectedExitCode = 78;

    internal static async Task<int> RunAsync(IHost host, CancellationToken cancellationToken = default,
        Func<CancellationToken, Task>? initialize = null)
    {
        var handedToHost = false;
        try
        {
            if (initialize is not null)
                await initialize(cancellationToken);
            handedToHost = true;
            await host.RunAsync(cancellationToken);
            return 0;
        }
        catch (PvadRetentionApprovalRequiredException)
        {
            return ConfigurationRejectedExitCode;
        }
        finally
        {
            // Host.RunAsync owns disposal only after initialization has succeeded.
            if (!handedToHost)
            {
                if (host is IAsyncDisposable asyncHost)
                    await asyncHost.DisposeAsync();
                else
                    host.Dispose();
            }
        }
    }
}
