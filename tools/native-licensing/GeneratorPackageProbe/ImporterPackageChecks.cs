using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.FileImporter.ImageAnalysis;
using PVAD.FileImporter.ImageOcr;
using PVAD.Importer;
using PVAD.Importer.Generation;
using PVAD.Importer.Saving;
using PVAD.Licensing;
using SkiaSharp;

internal static class ImporterPackageChecks
{
    private static void Check(bool condition, string name, List<string> checks)
    { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }

    internal static async Task MissingAsync(List<string> checks)
    {
        foreach (var (key, bytes) in ImportFixtures.Create())
        {
            using var stream = new MemoryStream(bytes);
            await Denied(() => FileImporterRegistry.Create(key).ImportAsync(stream, "synthetic." + key), "PVADLIC001", "missing-import:" + key, checks);
            Check(stream.Position == 0 && stream.CanRead, "missing-import-keeps-stream:" + key, checks);
        }
    }

    internal static async Task<object> LicensedAsync(string[] args, List<string> checks)
    {
        var fixtures = ImportFixtures.Create();
        var projections = new SortedDictionary<string, object>();
        PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_WRONG_LICENSE_PATH")!);
        foreach (var (key, bytes) in fixtures)
        {
            using var stream = new MemoryStream(bytes);
            await Denied(() => FileImporterRegistry.Create(key).ImportAsync(stream, "synthetic." + key), "PVADLIC012", "wrong-import:" + key, checks);
            Check(stream.Position == 0 && stream.CanRead, "wrong-import-keeps-stream:" + key, checks);
        }
        PvadLicense.LoadFromFile(args[0]);
        foreach (var (key, bytes) in fixtures)
        {
            using var stream = new MemoryStream(bytes);
            var design = await FileImporterRegistry.Create(key).ImportAsync(stream, "synthetic." + key);
            Check(stream.CanRead && design.Pages.Count > 0, "licensed-import:" + key, checks);
            if (key is not "image") Check(design.Pages.SelectMany(page => page.Elements).Any(element =>
                element.Content?.Contains("Imported content", StringComparison.Ordinal) == true), "import-text:" + key, checks);
            if (key == "pdf") Check(design.Pages[0].Extensions!["pdfPageBoxes"].TryGetProperty("mediaBox", out _), "import-anonymous-page-box-json-contract", checks);
            projections[key] = Projection(design);
        }
        using var source = new MemoryStream(fixtures["pdf"]);
        var model = await new PdfImporter().LoadAsync(source);
        Check(model.Pages.Count == 1 && model.Pages[0].MediaBox?.Width == 300, "typed-pdf-geometry", checks);
        var rewritten = new PdfFullRewriteWriter().Rewrite(fixtures["pdf"]).Bytes;
        using var rewrittenStream = new MemoryStream(rewritten);
        Check((await new PdfImporter().LoadAsync(rewrittenStream)).Pages.Count == 1, "pdf-private-rewrite-continuation", checks);
        var pdfAssembly = typeof(PVAD.Pdf.PdfPage).Assembly;
        foreach (var name in new[] { "PVAD.Pdf.PdfImageData", "PVAD.Pdf.Layout.ImageElement", "PVAD.Pdf.Layout.RectangleElement", "PVAD.Pdf.Layout.PolygonElement" })
            Check(pdfAssembly.GetType(name) is not null, "regeneration-reflection-type:" + name, checks);
        Check(typeof(PVAD.Pdf.PdfPage).GetField("_elements", BindingFlags.Instance | BindingFlags.NonPublic) is not null, "regeneration-reflection-field", checks);
        using var regenerated = new MemoryStream();
        await new PvadPdfGeneratorBridge().RegenerateAsync(model, regenerated);
        Check(regenerated.CanRead && regenerated.Length > 5, "regeneration-private-pdf-dependency", checks);
        File.WriteAllBytes(Path.Combine(args[4], "regenerated.pdf"), regenerated.ToArray());

        using var bitmap = new SKBitmap(160, 80);
        using (var canvas = new SKCanvas(bitmap)) { canvas.Clear(SKColors.White); using var paint = new SKPaint { Color = SKColors.DarkBlue }; canvas.DrawRect(20, 20, 40, 20, paint); }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var imageStream = new MemoryStream(encoded.ToArray());
        var analyzed = await new ImageAnalysisFileImporter().ImportWithAnalysisAsync(imageStream, "Synthetic analysis", options: new ImageAnalysisOptions { IncludeDebugOverlay = true });
        Check(imageStream.CanRead && analyzed.Design.Pages.Count == 1 && analyzed.DebugOverlayPng is { Length: > 8 }, "image-analysis-native-and-overlay", checks);
        projections["analysis"] = Projection(analyzed.Design);

        var workerPath = Environment.GetEnvironmentVariable("PVAD_PACKAGE_OCR_WORKER_PATH")!;
        var workerDirectory = Path.GetDirectoryName(workerPath)!;
        var scratch = Path.Combine(args[4], "ocr-tests"); Directory.CreateDirectory(scratch);
        var nativeExecuted = OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.X64;
        object? recognizedProjection = null;
        if (nativeExecuted)
        {
            using var textBitmap = new SKBitmap(800, 140);
            using (var canvas = new SKCanvas(textBitmap))
            {
                canvas.Clear(SKColors.White);
                using var face = SKTypeface.FromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_TEST_FONT")!);
                using var font = new SKFont(face, 58);
                using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                canvas.DrawText("PVAD PACKAGE TEST", 20, 90, SKTextAlign.Left, font, paint);
            }
            using var textImage = SKImage.FromBitmap(textBitmap);
            using var textData = textImage.Encode(SKEncodedImageFormat.Png, 100);
            var engine = new ProcessIsolatedTesseractOcrEngine(workerPath,
                Path.Combine(workerDirectory, "tessdata"), Path.Combine(workerDirectory, "native"), tempRoot: scratch);
            var recognized = await engine.RecognizeAsync([new OcrImagePage(0, 800, 140, textData.ToArray())], new() { Languages = "eng" });
            var words = recognized.SelectMany(page => page.Blocks).SelectMany(block => block.Lines).SelectMany(line => line.Words).ToArray();
            Check(recognized.Count == 1 && words.Any(word => word.Text.Contains("PACKAGE", StringComparison.OrdinalIgnoreCase)), "real-protected-worker-native-ocr-text", checks);
            recognizedProjection = words.Select(word => new { word.Text, word.Bounds, word.Confidence }).ToArray();
            Check(!Directory.EnumerateFileSystemEntries(scratch).Any(), "real-worker-handoff-cleanup", checks);
        }
        foreach (var code in new[] { "PVADLIC014", "PVADLIC015" })
        {
            var engine = new ProcessIsolatedTesseractOcrEngine("unused", tempRoot: scratch, runner: new ErrorRunner(code));
            await Denied(() => engine.RecognizeAsync([], new()), code, "typed-worker-revocation-error:" + code, checks);
            Check(!Directory.EnumerateFileSystemEntries(scratch).Any(), "typed-error-cleans-handoff:" + code, checks);
        }
        await WorkerDenial(workerPath, null, null, scratch, "PVADLIC001", checks);
        await WorkerDenial(workerPath, args[2], null, scratch, "PVADLIC012", checks);
        await WorkerDenial(workerPath, args[1], args[3], scratch, "PVADLIC014", checks);
        return new { projections, nativeOcrExecuted = nativeExecuted, recognized = recognizedProjection };
    }

    private static object Projection(DesignExportDto design) => design.Pages.Select(page => new
    {
        page.Layout,
        page.Extensions,
        elements = page.Elements.Select(element => new { element.Type, element.Content, element.X, element.Y, element.Width, element.Height }).ToArray(),
    }).ToArray();

    private static async Task Denied(Func<Task> action, string code, string name, List<string> checks)
    {
        try { await action(); throw new InvalidOperationException(name); }
        catch (PvadLicenseException error) { Check(error.Code == code, name, checks); }
    }

    private static async Task WorkerDenial(string worker, string? license, string? list, string scratch, string code, List<string> checks)
    {
        var response = Path.Combine(scratch, "denied.json");
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.Environment.Remove("PVAD_NATIVE_LICENSE_PATH"); process.StartInfo.Environment.Remove("PVAD_NATIVE_REVOCATIONS_PATH");
        if (license is not null) process.StartInfo.Environment["PVAD_NATIVE_LICENSE_PATH"] = license;
        if (list is not null) process.StartInfo.Environment["PVAD_NATIVE_REVOCATIONS_PATH"] = list;
        process.StartInfo.ArgumentList.Add(worker); process.StartInfo.ArgumentList.Add(Path.Combine(scratch, "never-read.json")); process.StartInfo.ArgumentList.Add(response);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
        catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } throw; }
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(response));
        Check(process.ExitCode == 1 && !json.RootElement.GetProperty("success").GetBoolean() && json.RootElement.GetProperty("licenseErrorCode").GetString() == code, "real-worker-denial-before-input:" + code, checks);
        await stdout; await stderr; File.Delete(response);
    }

    private sealed class ErrorRunner(string code) : IOcrWorkerProcessRunner
    {
        public async Task<OcrWorkerProcessResult> RunAsync(string workerPath, string requestPath, string responsePath, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(new OcrWorkerResponse { LicenseErrorCode = code }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
            return new(1, false, "", "");
        }
    }
}
