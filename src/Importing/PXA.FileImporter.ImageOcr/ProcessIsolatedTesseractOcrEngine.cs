using System.Diagnostics;
using System.Text.Json;

namespace PXA.FileImporter.ImageOcr;

public sealed class ProcessIsolatedTesseractOcrEngine : IOcrEngine
{
    public const string ActivitySourceName = "PXA.ImageOcr";
    private static readonly ActivitySource Activities = new(ActivitySourceName);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _workerPath;
    private readonly string? _tessDataPath;
    private readonly string? _nativeLibraryPath;
    private readonly string _tempRoot;
    private readonly IOcrWorkerProcessRunner _runner;

    public ProcessIsolatedTesseractOcrEngine(
        string? workerPath = null,
        string? tessDataPath = null,
        string? nativeLibraryPath = null,
        string? tempRoot = null,
        IOcrWorkerProcessRunner? runner = null)
    {
        _workerPath = string.IsNullOrWhiteSpace(workerPath)
            ? ResolveDefaultWorkerPath()
            : workerPath;
        _tessDataPath = tessDataPath;
        _nativeLibraryPath = nativeLibraryPath;
        _tempRoot = string.IsNullOrWhiteSpace(tempRoot)
            ? Path.GetTempPath()
            : tempRoot;
        _runner = runner ?? new DefaultOcrWorkerProcessRunner();
    }

    public string Name => "Tesseract";

    public string Version => "5.2.0-isolated";

    public async Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(options);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(options.MaxOcrRuntimeSeconds, 5, 180));
        var workDir = Path.Combine(_tempRoot, $"pxa-image-ocr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        using var activity = Activities.StartActivity(
            "pxa.ocr.worker.execute",
            ActivityKind.Client);
        activity?.SetTag("ocr.worker.type", "process");

        try
        {
            var requestPath = Path.Combine(workDir, "request.json");
            var responsePath = Path.Combine(workDir, "response.json");
            var request = await BuildRequestAsync(workDir, pages, options, cancellationToken);
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);

            var result = await _runner.RunAsync(_workerPath, requestPath, responsePath, workDir, timeout, cancellationToken);
            if (result.TimedOut)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "timeout");
                throw new InvalidOperationException(
                    $"OCR did not finish within {Math.Round(timeout.TotalSeconds)} seconds. The isolated OCR worker was terminated.");
            }

            if (!File.Exists(responsePath))
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;
                throw new InvalidOperationException(
                    $"OCR worker did not produce a response. Exit code: {result.ExitCode}. {detail}".Trim());
            }

            var responseJson = await File.ReadAllTextAsync(responsePath, cancellationToken);
            var response = JsonSerializer.Deserialize<OcrWorkerResponse>(responseJson, JsonOptions)
                ?? throw new InvalidOperationException("OCR worker produced an invalid response.");

            if (!response.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "worker_error");
                throw new InvalidOperationException(response.Error ?? "OCR worker failed.");
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return response.Pages;
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch
        {
            if (activity?.Status != ActivityStatusCode.Error)
                activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private async Task<OcrWorkerRequest> BuildRequestAsync(
        string workDir,
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken)
    {
        var workerPages = new List<OcrWorkerImagePage>(pages.Count);
        foreach (var page in pages)
        {
            var imagePath = Path.Combine(workDir, $"page-{page.PageIndex}.png");
            await File.WriteAllBytesAsync(imagePath, page.EncodedImageBytes, cancellationToken);
            workerPages.Add(new OcrWorkerImagePage
            {
                PageIndex = page.PageIndex,
                WidthPx = page.WidthPx,
                HeightPx = page.HeightPx,
                EncodedImagePath = imagePath,
            });
        }

        return new OcrWorkerRequest
        {
            TraceParent = Activity.Current?.IdFormat == ActivityIdFormat.W3C
                ? Activity.Current.Id
                : null,
            TraceState = Activity.Current?.TraceStateString,
            Languages = options.Languages,
            TessDataPath = _tessDataPath,
            NativeLibraryPath = options.NativeLibraryPath ?? _nativeLibraryPath,
            MaxOcrRuntimeSeconds = Math.Clamp(options.MaxOcrRuntimeSeconds, 5, 180),
            Pages = workerPages,
        };
    }

    private static string ResolveDefaultWorkerPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var workerDir = Path.Combine(baseDir, "ocr-worker");
        var dllPath = Path.Combine(workerDir, "PXA.FileImporter.ImageOcr.Worker.dll");
        if (File.Exists(dllPath))
            return dllPath;

        dllPath = Path.Combine(baseDir, "PXA.FileImporter.ImageOcr.Worker.dll");
        if (File.Exists(dllPath))
            return dllPath;

        return Path.Combine(baseDir, "PXA.FileImporter.ImageOcr.Worker.dll");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; OCR import must not fail because temp deletion raced with the OS.
        }
    }
}

public interface IOcrWorkerProcessRunner
{
    Task<OcrWorkerProcessResult> RunAsync(
        string workerPath,
        string requestPath,
        string responsePath,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed record OcrWorkerProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);

public sealed class DefaultOcrWorkerProcessRunner : IOcrWorkerProcessRunner
{
    public async Task<OcrWorkerProcessResult> RunAsync(
        string workerPath,
        string requestPath,
        string responsePath,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = BuildStartInfo(workerPath, requestPath, responsePath, workingDirectory);
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
            return new OcrWorkerProcessResult(
                process.ExitCode,
                TimedOut: false,
                await stdoutTask,
                await stderrTask);
        }
        catch (TimeoutException)
        {
            KillProcessTree(process);
            return new OcrWorkerProcessResult(-1, TimedOut: true, await ReadCompletedAsync(stdoutTask), await ReadCompletedAsync(stderrTask));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            return new OcrWorkerProcessResult(-1, TimedOut: true, await ReadCompletedAsync(stdoutTask), await ReadCompletedAsync(stderrTask));
        }
        catch
        {
            KillProcessTree(process);
            throw;
        }
    }

    private static ProcessStartInfo BuildStartInfo(
        string workerPath,
        string requestPath,
        string responsePath,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (string.Equals(Path.GetExtension(workerPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "dotnet";
            startInfo.ArgumentList.Add(workerPath);
        }
        else
        {
            startInfo.FileName = workerPath;
        }

        startInfo.ArgumentList.Add(requestPath);
        startInfo.ArgumentList.Add(responsePath);
        return startInfo;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort; the caller will still return a timeout error to the WebApi.
        }
    }

    private static async Task<string> ReadCompletedAsync(Task<string> task)
    {
        try
        {
            return task.IsCompleted ? await task : "";
        }
        catch
        {
            return "";
        }
    }
}
