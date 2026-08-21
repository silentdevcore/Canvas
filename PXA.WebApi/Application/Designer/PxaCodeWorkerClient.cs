using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PXA.Core.Contracts;

namespace PXA.WebApi.Application.Designer;

public sealed class PxaCodeWorkerOptions
{
    public bool Enabled { get; set; }
    public bool Hardened { get; set; }
    public string? WorkerPath { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int MaximumSourceBytes { get; set; } = PxaCodeLimits.MaximumSourceBytes;
    public int MaximumOutputBytes { get; set; } = 25_000_000;
}

public interface IPxaCodeWorkerClient
{
    Task<PxaCodeWorkerResponse> RunAsync(PxaCodeWorkerRequest request, CancellationToken cancellationToken);
}

public sealed class PxaCodeWorkerClient(
    IWebHostEnvironment environment,
    IOptions<PxaCodeWorkerOptions> options,
    ILogger<PxaCodeWorkerClient> logger) : IPxaCodeWorkerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PxaCodeWorkerOptions settings = options.Value;

    public async Task<PxaCodeWorkerResponse> RunAsync(PxaCodeWorkerRequest request, CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
            return Failure("PXACODE040", "The code execution worker is disabled.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.Source) > settings.MaximumSourceBytes)
            return Failure("PXACODE009", "Source code exceeds the configured sandbox limit.");

        var directory = Directory.CreateTempSubdirectory("pxa-code-").FullName;
        var inputPath = Path.Combine(directory, "request.json");
        var outputPath = Path.Combine(directory, "response.json");
        try
        {
            await File.WriteAllTextAsync(inputPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);
            string fileName;
            string arguments;
            try
            {
                (fileName, arguments) = ResolveCommand(inputPath, outputPath);
            }
            catch (FileNotFoundException exception)
            {
                logger.LogWarning(exception, "Code worker package is unavailable.");
                return Failure("PXACODE041", "The code execution worker is unavailable.");
            }
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };
            process.StartInfo.Environment.Clear();
            process.StartInfo.Environment["DOTNET_EnableDiagnostics"] = "0";
            process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            try
            {
                if (!process.Start())
                    return Failure("PXACODE041", "The code execution worker could not be started.");
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                logger.LogWarning(exception, "Code worker process could not be started.");
                return Failure("PXACODE041", "The code execution worker could not be started.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 15) + 2));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return Failure("PXACODE031", "Execution exceeded the sandbox time limit.");
            }

            if (!File.Exists(outputPath))
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                logger.LogWarning("Code worker returned no response. Exit={ExitCode}; stderr={WorkerError}", process.ExitCode, stderr[..Math.Min(stderr.Length, 500)]);
                return Failure("PXACODE042", "The code execution worker returned no result.");
            }
            if (new FileInfo(outputPath).Length > settings.MaximumOutputBytes * 2L)
                return Failure("PXACODE043", "The code execution result exceeds the configured limit.");
            return JsonSerializer.Deserialize<PxaCodeWorkerResponse>(
                       await File.ReadAllTextAsync(outputPath, cancellationToken), JsonOptions)
                   ?? Failure("PXACODE044", "The code execution result is invalid.");
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception exception) { logger.LogDebug(exception, "Could not remove code worker directory {Directory}", directory); }
        }
    }

    private (string FileName, string Arguments) ResolveCommand(string inputPath, string outputPath)
    {
        var configured = settings.WorkerPath;
        var basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "bin", environment.EnvironmentName == "Development" ? "Debug" : "Release", "net10.0", "code-worker")
            : Path.GetFullPath(configured, environment.ContentRootPath);
        var dll = Path.Combine(basePath, "PXA.CodeWorker.dll");
        if (File.Exists(dll))
            return (ResolveDotnetHost(), $"\"{dll}\" \"{inputPath}\" \"{outputPath}\"");
        var executable = Path.Combine(basePath, OperatingSystem.IsWindows() ? "PXA.CodeWorker.exe" : "PXA.CodeWorker");
        if (File.Exists(executable))
            return (executable, $"\"{inputPath}\" \"{outputPath}\"");
        throw new FileNotFoundException("PXA.CodeWorker is not packaged. Build the WebApi or configure CodeWorker:WorkerPath.", dll);
    }

    private static string ResolveDotnetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
                return candidate;
        }

        return executableName;
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { }
    }

    private static PxaCodeWorkerResponse Failure(string code, string message) => new()
    {
        Diagnostics = [new PxaCodeDiagnosticDto { Code = code, Severity = "error", Message = message }],
    };
}
