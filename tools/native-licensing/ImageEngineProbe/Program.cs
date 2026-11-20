using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using PVAD.FileImporter.ImageAnalysis;
using PVAD.FileImporter.ImageAnalysis.Analysis;
using PVAD.FileImporter.ImageAnalysis.Templates;
using PVAD.FileImporter.ImageOcr;
using PVAD.Licensing;
using SkiaSharp;

// Test-only child modes exercise the real runner without a shell or installed test utilities.
if (args.Length == 2 && args[0] is "--sleep-fixture" or "--permission-fixture")
{
    if (args[0] == "--sleep-fixture") await Task.Delay(TimeSpan.FromMinutes(1));
    else
    {
        var file = Environment.GetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH")!;
        var restricted = File.Exists(file) && (OperatingSystem.IsWindows() ||
            File.GetUnixFileMode(file) == (UnixFileMode.UserRead | UnixFileMode.UserWrite));
        Console.WriteLine(restricted ? "restricted" : "invalid");
    }
    return;
}

var checks = new List<string>();
var analysis = new ImageAnalysisFileImporter();
var fake = new FakeOcr();
var converter = new ImageToPdfConverter(fake);
var embedded = new EmbeddedTesseractOcrEngine("/nonexistent-synthetic-tessdata");
var runner = new ResponseRunner();
var temp = Path.Combine(Path.GetTempPath(), "pvad-image-license-probe-" + Guid.NewGuid().ToString("N"));
var isolated = new ProcessIsolatedTesseractOcrEngine("/never-start.dll", tempRoot: temp, runner: runner);
var defaultRunner = new DefaultOcrWorkerProcessRunner();
var types = new Dictionary<Type, int>
{
    [typeof(ImageAnalysisFileImporter)] = 5, [typeof(Preprocessor)] = 2,
    [typeof(ColorAnalyzer)] = 6, [typeof(ShapeDetector)] = 10, [typeof(TextEngine)] = 4,
    [typeof(GlyphRecognizer)] = 5, [typeof(SceneAssembler)] = 2,
    [typeof(DebugOverlayRenderer)] = 1, [typeof(CharacterTemplates)] = 4,
    [typeof(ImageToPdfConverter)] = 1, [typeof(EmbeddedTesseractOcrEngine)] = 1,
    [typeof(ProcessIsolatedTesseractOcrEngine)] = 1, [typeof(DefaultOcrWorkerProcessRunner)] = 1,
};
var methods = types.SelectMany(pair =>
{
    var found = pair.Key.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && m.Name is not ("ColorDistance" or "NormalizedCrossCorrelation")).ToArray();
    Check(found.Length == pair.Value, "contract:" + pair.Key.Name);
    return found;
}).ToArray();
foreach (var method in methods) NativeSurfaceCoverage.Record(method);
var instances = new Dictionary<Type, object>
{
    [typeof(ImageAnalysisFileImporter)] = analysis, [typeof(ImageToPdfConverter)] = converter,
    [typeof(EmbeddedTesseractOcrEngine)] = embedded,
    [typeof(ProcessIsolatedTesseractOcrEngine)] = isolated,
    [typeof(DefaultOcrWorkerProcessRunner)] = defaultRunner,
};

await CheckDenied("missing", "PVADLIC001");
PvadLicense.LoadFromFile(args[1]);
await CheckDenied("wrong-product", "PVADLIC012");
try { PvadLicense.LoadFromFile(args[2]); throw new Exception("Expired replacement accepted."); }
catch (PvadLicenseException ex) when (ex.Code == "PVADLIC009") { Check(true, "expired-replacement-rejected"); }
await CheckDenied("failed-renewal-retains-denial", "PVADLIC012");

PvadLicense.LoadFromFile(args[0]);
using var bitmap = new SKBitmap(160, 80);
using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.White);
using var image = SKImage.FromBitmap(bitmap);
using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
var png = encoded.ToArray();
using var stream = new MemoryStream(png);
var imported = await analysis.ImportWithAnalysisAsync(stream, "Synthetic image", options: new ImageAnalysisOptions { IncludeDebugOverlay = true });
Check(imported.Design.Pages.Count == 1 && imported.DebugOverlayPng is { Length: > 8 }, "importer-only-analysis-overlay");
Check(ImageAnalysisFileImporter.Import(bitmap, "Synthetic direct").Pages.Count == 1, "importer-only-static-analysis");
foreach (var cancellationOverload in new[] { false, true })
{
    using var interfaceInput = new MemoryStream(png);
    PVAD.FileImporter.IFileImporter reader = analysis;
    var result = cancellationOverload
        ? await reader.ImportAsync(interfaceInput, "Synthetic interface", CancellationToken.None)
        : await reader.ImportAsync(interfaceInput, "Synthetic interface");
    Check(result.Pages.Count == 1 && interfaceInput.CanRead, "importer-only-interface-dispatch:" + cancellationOverload);
}
using var converterInput = new MemoryStream(png);
var converted = await converter.ConvertAsync(converterInput, "synthetic.png", new() { IncludeDebugOverlay = true });
Check(fake.Calls == 1 && converted.Design.Pages.Count == 1 && converted.DebugOverlayPng is { Length: > 8 }, "importer-only-custom-ocr-and-overlay");
await isolated.RecognizeAsync([], new());
Check(runner.Calls == 1 && !Directory.Exists(runner.WorkDir), "isolated-success-and-cleanup");
foreach (var code in new[] { "PVADLIC009", "PVADLIC012", "PVADLIC014", "PVADLIC015" })
{
    runner.Code = code;
    await Denied(() => isolated.RecognizeAsync([], new()), code, "worker-error-transfer:" + code);
    Check(!Directory.Exists(runner.WorkDir), "worker-error-cleanup:" + code);
}
runner.Code = null;
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    var calls = runner.Calls;
    try { await isolated.RecognizeAsync([], new(), cancellation.Token); throw new Exception("Cancellation ignored."); }
    catch (OperationCanceledException) { Check(runner.Calls == calls, "cancellation-before-worker-dispatch"); }
}
try { await embedded.RecognizeAsync([], new()); throw new Exception("Missing language data accepted."); }
catch (OcrLanguageDataMissingException) { Check(true, "importer-admitted-before-native-language-validation"); }

Directory.CreateDirectory(temp);
try
{
    var response = Path.Combine(temp, "response.json");
    var request = Path.Combine(temp, "request.json");
    await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new OcrWorkerRequest
    {
        TessDataPath = "/nonexistent-synthetic-worker-tessdata", Languages = "eng",
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    foreach (var (name, path, code) in new[]
    {
        ("missing", (string?)null, "PVADLIC001"), ("wrong-product", args[1], "PVADLIC012"),
        ("expired", args[2], "PVADLIC009"),
    })
    {
        var result = await StartWorker(path, "/nonexistent-synthetic-request.json", response);
        Check(result.ExitCode == 1 && result.Response.LicenseErrorCode == code && !result.Response.Success,
            "real-worker-before-request-read:" + name);
        Check(result.Error.Trim() == code, "real-worker-content-free-error:" + name);
    }
    var malformed = Path.Combine(temp, "malformed.json");
    await File.WriteAllTextAsync(malformed, "{}");
    var invalid = await StartWorker(malformed, request, response);
    Check(!invalid.Response.Success && invalid.Response.LicenseErrorCode is not null, "real-worker-malformed-license");
    using (var original = JsonDocument.Parse(await File.ReadAllTextAsync(args[0])))
    {
        var envelope = original.RootElement.Deserialize<Dictionary<string, JsonElement>>()!;
        envelope["signature"] = JsonSerializer.SerializeToElement(Convert.ToBase64String(new byte[64]));
        await File.WriteAllTextAsync(malformed, JsonSerializer.Serialize(envelope));
        var tampered = await StartWorker(malformed, request, response);
        Check(tampered.Response.LicenseErrorCode == "PVADLIC007", "real-worker-invalid-signature-rejected");
    }

    try
    {
        await defaultRunner.RunAsync(Path.Combine(temp, "missing-worker"), request, response, temp, TimeSpan.FromSeconds(20), default);
        throw new Exception("Missing process started.");
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Check(!Directory.EnumerateFiles(temp, "native-license-*.json").Any(), "snapshot-cleanup-on-process-start-failure");
    }

    // Deliberately point inherited configuration at a wrong-product license. The runner must
    // transfer the in-memory signed Importer snapshot, not blindly inherit that setting.
    var inherited = Environment.GetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH");
    try
    {
        Environment.SetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH", args[1]);
        var result = await defaultRunner.RunAsync(args[3], request, response, temp, TimeSpan.FromSeconds(20), default);
        var body = JsonSerializer.Deserialize<OcrWorkerResponse>(await File.ReadAllTextAsync(response), WebJson())!;
        Check(result.ExitCode == 1 && body.LicenseErrorCode is null && body.Error!.Contains("language data folder"),
            "real-runner-verified-snapshot-overrides-inherited-license");
        Check(!Directory.EnumerateFiles(temp, "native-license-*.json").Any(), "real-runner-snapshot-cleanup-on-worker-failure");
        var helper = Assembly.GetExecutingAssembly().Location;
        var permissions = await defaultRunner.RunAsync(helper, "--permission-fixture", response, temp, TimeSpan.FromSeconds(20), default);
        Check(permissions.ExitCode == 0 && permissions.StandardOutput.Trim() == "restricted", "worker-snapshot-file-permissions");
        var timedOut = await defaultRunner.RunAsync(helper, "--sleep-fixture", response, temp, TimeSpan.FromMilliseconds(300), default);
        Check(timedOut.TimedOut && !Directory.EnumerateFiles(temp, "native-license-*.json").Any(), "real-runner-timeout-snapshot-cleanup");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            await defaultRunner.RunAsync(helper, "--sleep-fixture", response, temp, TimeSpan.FromSeconds(20), cancellation.Token);
            throw new Exception("Process cancellation ignored.");
        }
        catch (OperationCanceledException) { Check(!Directory.EnumerateFiles(temp, "native-license-*.json").Any(), "real-runner-cancelled-snapshot-cleanup"); }
    }
    finally { Environment.SetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH", inherited); }

    var tessdata = Path.Combine(AppContext.BaseDirectory, "tessdata");
    var native = Path.Combine(AppContext.BaseDirectory, "native");
    var realNativeSucceeded = false;
    if (OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64)
    {
        var nativeEngine = new ProcessIsolatedTesseractOcrEngine(args[3], tessdata, native, tempRoot: temp);
        var recognized = await nativeEngine.RecognizeAsync([new OcrImagePage(0, bitmap.Width, bitmap.Height, png)], new() { Languages = "eng" });
        Check(recognized.Count == 1 && recognized[0].WidthPx == bitmap.Width, "real-native-ocr-importer-only");
        realNativeSucceeded = true;
    }
    if (Environment.GetEnvironmentVariable("PVAD_REQUIRE_NATIVE_OCR") == "1")
        Check(realNativeSucceeded, "required-native-ocr-executed");

    using (var renewingInput = new RenewalReadStream(png, () =>
    {
        PvadLicense.LoadFromFile(args[1]);
        try { ImageAnalysisFileImporter.Import(bitmap, "New reentrant import"); throw new InvalidOperationException("Reentrant public import inherited admission."); }
        catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, "reentrant-public-analysis-does-not-inherit-admission"); }
    }))
    {
        var completed = await analysis.ImportWithAnalysisAsync(renewingInput, "Admitted before renewal", options: new ImageAnalysisOptions { IncludeDebugOverlay = true });
        Check(completed.Design.Pages.Count == 1 && completed.DebugOverlayPng is { Length: > 8 }, "analysis-finishes-original-admission-after-mid-read-renewal");
    }
    await Denied(() => analysis.ImportAsync(new MemoryStream(png)), "PVADLIC012", "next-analysis-after-renewal-is-a-new-admission");
    PvadLicense.LoadFromFile(args[0]);
    using (var workerInput = new RenewalReadStream(png, () => PvadLicense.LoadFromFile(args[1])))
    {
        var workerConverter = new ImageToPdfConverter(new ProcessIsolatedTesseractOcrEngine(args[3], tempRoot: temp));
        await Denied(() => workerConverter.ConvertAsync(workerInput, "worker.png", new()), "PVADLIC012",
            "new-worker-dispatch-requires-current-admission-even-in-admitted-pipeline");
        Check(!Directory.EnumerateDirectories(temp).Any(), "denied-worker-dispatch-cleans-staged-image-input");
    }
    PvadLicense.LoadFromFile(args[0]);
    fake.OnRecognize = () =>
    {
        try { PvadLicense.Require(PvadLicenseProducts.Pdf); throw new InvalidOperationException("OCR context granted public PDF access."); }
        catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, "ocr-context-does-not-grant-public-pdf-access"); }
        PvadLicense.LoadFromFile(args[1]);
        try { PvadLicense.Require(PvadLicenseProducts.Importer); throw new InvalidOperationException("Callback inherited old public admission."); }
        catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, "ocr-callback-public-admission-is-current"); }
    };
    using (var renewedInput = new MemoryStream(png))
    {
        var completed = await converter.ConvertAsync(renewedInput, "admitted.png", new() { IncludeDebugOverlay = true });
        Check(completed.Design.Pages.Count == 1 && completed.DebugOverlayPng is { Length: > 8 }, "ocr-completes-after-callback-renewal");
    }
    var previousCalls = fake.Calls;
    using (var newInput = new MemoryStream(png))
    {
        await Denied(() => converter.ConvertAsync(newInput, "new.png", new()), "PVADLIC012", "next-ocr-needs-new-admission");
        Check(fake.Calls == previousCalls && newInput.Position == 0, "next-ocr-denial-has-no-input-or-callback-effects");
    }
    fake.OnRecognize = null;
    PvadLicense.LoadFromFile(args[1]);
    await CheckDenied("revoked-after-cache-and-native-use", "PVADLIC012");
    PvadLicense.LoadFromFile(args[0]);
    Check(CharacterTemplates.MatchTop(new float[1024], 1).Count == 1, "renewed-template-atlas-access");
    Console.WriteLine(JsonSerializer.Serialize(new { checks, protectedProcessingSignatures = methods.Length,
        scope = "image-analysis-ocr-and-worker", completeImporterEnforcement = false, realNativeSucceeded, testedSurface = NativeSurfaceCoverage.Snapshot() }));
}
finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }

async Task CheckDenied(string prefix, string code)
{
    var fakeCalls = fake.Calls;
    var runnerCalls = runner.Calls;
    var rootExisted = Directory.Exists(temp);
    foreach (var method in methods)
    {
        var arguments = method.GetParameters().Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        await Denied(async () =>
        {
            object? value;
            try { value = method.Invoke(method.IsStatic ? null : instances[method.DeclaringType!], arguments); }
            catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
            if (value is Task task) await task;
        }, code, prefix + ":" + method.DeclaringType!.Name + ":" + method);
    }
    using var unread = new MemoryStream([1, 2, 3]);
    await Denied(() => analysis.ImportAsync(unread), code, prefix + ":input-stream");
    PVAD.FileImporter.IFileImporter interfaceReader = analysis;
    await Denied(() => interfaceReader.ImportAsync(unread), code, prefix + ":explicit-interface");
    await Denied(() => interfaceReader.ImportAsync(unread, null, CancellationToken.None), code, prefix + ":default-interface-cancellation");
    Check(unread.Position == 0 && fake.Calls == fakeCalls && runner.Calls == runnerCalls && Directory.Exists(temp) == rootExisted, prefix + ":no-side-effects");
    Check(ColorAnalyzer.ColorDistance(SKColors.White, SKColors.White) == 0, prefix + ":free-color-distance");
    Check(CharacterTemplates.NormalizedCrossCorrelation([0, 1], [0, 1]) > 0.9, prefix + ":free-correlation");
}
async Task Denied(Func<Task> action, string code, string name)
{
    try { await action(); }
    catch (PvadLicenseException ex) when (ex.Code == code) { Check(true, name); return; }
    throw new InvalidOperationException("Expected license rejection: " + name);
}
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    if (checks.Contains(name)) throw new InvalidOperationException("Duplicate check: " + name);
    checks.Add(name);
}
JsonSerializerOptions WebJson() => new(JsonSerializerDefaults.Web);
async Task<(int ExitCode, OcrWorkerResponse Response, string Error)> StartWorker(string? license, string request, string response)
{
    var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
    info.ArgumentList.Add(args[3]); info.ArgumentList.Add(request); info.ArgumentList.Add(response);
    info.Environment.Remove("PVAD_NATIVE_LICENSE_PATH");
    if (license is not null) info.Environment["PVAD_NATIVE_LICENSE_PATH"] = license;
    using var process = Process.Start(info)!;
    var error = process.StandardError.ReadToEndAsync();
    var output = process.StandardOutput.ReadToEndAsync();
    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
    catch { if (!process.HasExited) process.Kill(true); throw; }
    await output;
    return (process.ExitCode, JsonSerializer.Deserialize<OcrWorkerResponse>(await File.ReadAllTextAsync(response), WebJson())!, await error);
}

sealed class FakeOcr : IOcrEngine
{
    public int Calls { get; private set; }
    public Action? OnRecognize { get; set; }
    public string Name => "Synthetic";
    public string Version => "1";
    public Task<IReadOnlyList<OcrPage>> RecognizeAsync(IReadOnlyList<OcrImagePage> pages, ImageToPdfConversionOptions options, CancellationToken cancellationToken = default)
    {
        Calls++;
        OnRecognize?.Invoke();
        return Task.FromResult<IReadOnlyList<OcrPage>>([new OcrPage { PageIndex = 0, WidthPx = pages[0].WidthPx, HeightPx = pages[0].HeightPx }]);
    }
}

sealed class RenewalReadStream(byte[] bytes, Action renew) : Stream
{
    private readonly MemoryStream source = new(bytes);
    private bool renewed;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => source.Length;
    public override long Position { get => source.Position; set => throw new NotSupportedException(); }
    private void BeforeRead() { if (!renewed) { renewed = true; renew(); } }
    public override int Read(byte[] buffer, int offset, int count) { BeforeRead(); return source.Read(buffer, offset, count); }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { BeforeRead(); return source.ReadAsync(buffer, cancellationToken); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
}
sealed class ResponseRunner : IOcrWorkerProcessRunner
{
    public int Calls { get; private set; }
    public string WorkDir { get; private set; } = "";
    public string? Code { get; set; }
    public async Task<OcrWorkerProcessResult> RunAsync(string workerPath, string requestPath, string responsePath, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Calls++; WorkDir = workingDirectory;
        await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(new OcrWorkerResponse { Success = Code is null, LicenseErrorCode = Code }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
        return new(Code is null ? 0 : 1, false, "", "");
    }
}
