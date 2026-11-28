using Microsoft.Extensions.Options;
using PVAD.Licensing;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadNativeLicenseOptions
{
    public string? LicensePath { get; set; }
    public string? RevocationListPath { get; set; }
}

/// <summary>Explicit host activation; never grants rights from server editions or roles.</summary>
public sealed class PvadNativeLicenseBootstrap(
    IOptions<PvadNativeLicenseOptions> options,
    IWebHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.RevocationListPath))
            PvadLicense.ImportRevocationsFromFile(Path.GetFullPath(options.Value.RevocationListPath, environment.ContentRootPath));
        if (!string.IsNullOrWhiteSpace(options.Value.LicensePath))
            PvadLicense.LoadFromFile(Path.GetFullPath(options.Value.LicensePath, environment.ContentRootPath));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
