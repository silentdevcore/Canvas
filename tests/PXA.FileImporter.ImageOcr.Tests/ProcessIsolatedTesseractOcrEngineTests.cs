using System.Text.Json;
using PXA.FileImporter.ImageOcr;

namespace PXA.FileImporter.ImageOcr.Tests;

public sealed class ProcessIsolatedTesseractOcrEngineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RecognizeAsync_WhenWorkerSucceeds_ReturnsPagesAndDeletesTempDirectory()
    {
        var runner = new FakeRunner(async context =>
        {
            var request = await ReadRequestAsync(context.RequestPath);
            Assert.Single(request.Pages);
            Assert.True(File.Exists(request.Pages[0].EncodedImagePath));

            await WriteResponseAsync(context.ResponsePath, new OcrWorkerResponse
            {
                Success = true,
                Pages =
                [
                    new OcrPage
                    {
                        PageIndex = 0,
                        WidthPx = 100,
                        HeightPx = 50,
                        Confidence = 0.9,
                    },
                ],
            });

            return new OcrWorkerProcessResult(0, TimedOut: false, "", "");
        });

        using var tempRoot = new TestTempDirectory();
        var engine = new ProcessIsolatedTesseractOcrEngine(
            workerPath: "/fake/worker.dll",
            tempRoot: tempRoot.Path,
            runner: runner);

        var pages = await engine.RecognizeAsync(
            [new OcrImagePage(0, 100, 50, [1, 2, 3])],
            new ImageToPdfConversionOptions());

        var page = Assert.Single(pages);
        Assert.Equal(100, page.WidthPx);
        Assert.Equal(50, page.HeightPx);
        Assert.False(Directory.Exists(runner.LastWorkingDirectory));
    }

    [Fact]
    public async Task RecognizeAsync_WhenWorkerReturnsError_ThrowsAndDeletesTempDirectory()
    {
        var runner = new FakeRunner(async context =>
        {
            await WriteResponseAsync(context.ResponsePath, new OcrWorkerResponse
            {
                Success = false,
                Error = "worker failed",
            });
            return new OcrWorkerProcessResult(1, TimedOut: false, "", "worker failed");
        });

        using var tempRoot = new TestTempDirectory();
        var engine = new ProcessIsolatedTesseractOcrEngine("/fake/worker.dll", tempRoot: tempRoot.Path, runner: runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecognizeAsync([new OcrImagePage(0, 10, 10, [1])], new ImageToPdfConversionOptions()));

        Assert.Contains("worker failed", ex.Message);
        Assert.False(Directory.Exists(runner.LastWorkingDirectory));
    }

    [Fact]
    public async Task RecognizeAsync_WhenWorkerTimesOut_ThrowsAndDeletesTempDirectory()
    {
        var runner = new FakeRunner(context =>
            Task.FromResult(new OcrWorkerProcessResult(-1, TimedOut: true, "", "")));

        using var tempRoot = new TestTempDirectory();
        var engine = new ProcessIsolatedTesseractOcrEngine("/fake/worker.dll", tempRoot: tempRoot.Path, runner: runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecognizeAsync(
                [new OcrImagePage(0, 10, 10, [1])],
                new ImageToPdfConversionOptions { MaxOcrRuntimeSeconds = 5 }));

        Assert.Contains("OCR did not finish within 5 seconds", ex.Message);
        Assert.Contains("terminated", ex.Message);
        Assert.False(Directory.Exists(runner.LastWorkingDirectory));
    }

    [Fact]
    public async Task RecognizeAsync_WhenWorkerProducesNoResponse_ThrowsDiagnosticError()
    {
        var runner = new FakeRunner(context =>
            Task.FromResult(new OcrWorkerProcessResult(2, TimedOut: false, "stdout detail", "stderr detail")));

        using var tempRoot = new TestTempDirectory();
        var engine = new ProcessIsolatedTesseractOcrEngine("/fake/worker.dll", tempRoot: tempRoot.Path, runner: runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RecognizeAsync([new OcrImagePage(0, 10, 10, [1])], new ImageToPdfConversionOptions()));

        Assert.Contains("did not produce a response", ex.Message);
        Assert.Contains("stderr detail", ex.Message);
        Assert.False(Directory.Exists(runner.LastWorkingDirectory));
    }

    private static async Task<OcrWorkerRequest> ReadRequestAsync(string requestPath)
    {
        var json = await File.ReadAllTextAsync(requestPath);
        return JsonSerializer.Deserialize<OcrWorkerRequest>(json, JsonOptions)!;
    }

    private static Task WriteResponseAsync(string responsePath, OcrWorkerResponse response) =>
        File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response, JsonOptions));

    private sealed class FakeRunner : IOcrWorkerProcessRunner
    {
        private readonly Func<RunContext, Task<OcrWorkerProcessResult>> _handler;

        public FakeRunner(Func<RunContext, Task<OcrWorkerProcessResult>> handler)
        {
            _handler = handler;
        }

        public string LastWorkingDirectory { get; private set; } = "";

        public Task<OcrWorkerProcessResult> RunAsync(
            string workerPath,
            string requestPath,
            string responsePath,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastWorkingDirectory = workingDirectory;
            return _handler(new RunContext(workerPath, requestPath, responsePath, workingDirectory, timeout));
        }
    }

    private sealed record RunContext(
        string WorkerPath,
        string RequestPath,
        string ResponsePath,
        string WorkingDirectory,
        TimeSpan Timeout);

    private sealed class TestTempDirectory : IDisposable
    {
        public TestTempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"canvas-ocr-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
