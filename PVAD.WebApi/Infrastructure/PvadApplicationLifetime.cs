using PVAD.WebApi.Application.Retention;

namespace PVAD.WebApi.Infrastructure;

internal static class PvadApplicationLifetime
{
    internal const int ConfigurationRejectedExitCode = 78;

    internal static async Task<int> RunAsync(IHost host, CancellationToken cancellationToken = default)
    {
        try
        {
            await host.RunAsync(cancellationToken);
            return 0;
        }
        catch (PvadRetentionApprovalRequiredException)
        {
            // RunAsync has disposed the host; a known refusal must not enter runtime abort handling.
            return ConfigurationRejectedExitCode;
        }
    }
}
