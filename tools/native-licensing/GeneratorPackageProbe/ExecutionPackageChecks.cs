using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Converters;
using PVAD.Licensing;
using SkiaSharp;

internal static class ExecutionPackageChecks
{
    private static IDocumentExporter[] Exporters() => typeof(DocumentExporter).Assembly.GetExportedTypes()
        .Where(type => !type.IsAbstract && typeof(IDocumentExporter).IsAssignableFrom(type))
        .OrderBy(type => type.Name).Select(type => (IDocumentExporter)Activator.CreateInstance(type)!).ToArray();

    internal static void Missing(List<string> checks)
    {
        var calls = Calls(Exporters());
        Check(calls.Length == 31, "converter-complete-public-and-interface-call-set", checks);
        foreach (var call in calls)
            Denied(() => call.Run(null!), "PVADLIC001", "missing-converter-before-input:" + call.Name, checks);
    }

    internal static async Task<object> LicensedAsync(string[] args, List<string> checks)
    {
        var exporters = Exporters();
        Check(exporters.Select(value => value.FormatKey).Order().SequenceEqual(new[] { "csv", "html", "jpeg", "md", "odt", "png", "svg", "tiff", "xml" }), "converter-nine-formats", checks);
        var calls = Calls(exporters);
        var design = Design();
        var input = JsonSerializer.Serialize(design);
        PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_WRONG_LICENSE_PATH")!);
        foreach (var call in calls)
            Denied(() => call.Run(design), "PVADLIC012", "wrong-converter:" + call.Name, checks);
        PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_CONVERTER_LICENSE_PATH")!);
        Denied(() => PvadLicense.Require(PvadLicenseProducts.Pdf), "PVADLIC012", "converter-does-not-grant-pdf", checks);
        var converted = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var call in calls)
        foreach (var multiple in new[] { false, true })
        {
            var name = call.Name + (multiple ? ":multiple" : ":single");
            converted[name] = Inspect(call.Run(multiple ? design : Design(false)), call.Format, multiple ? 2 : 1, name, checks);
        }
        Check(JsonSerializer.Serialize(design) == input, "converter-input-unchanged", checks);
        PvadLicense.LoadFromFile(args[0]);

        var scratch = Path.Combine(args[4], "code-worker-tests");
        Directory.CreateDirectory(scratch);
        var model = """
            return new DesignExportDto { Id = "worker-document", Name = "Synthetic worker", PageSettings = new PageSettingsDto { Width = 120, Height = 160 },
                Pages = new List<PageDto> { new PageDto { Id = "worker-page", Elements = new List<ElementDto> {
            // pvad-element-id: worker-text
                new ElementDto { Id = "worker-text", Type = "text", Content = "Synthetic worker", X = 5, Y = 5, Width = 100, Height = 20 }
                } } } };
            """;
        var modelResult = await Run(new() { Language = PvadCodeLanguages.CSharpModel, Source = model }, null, null, "model");
        Success(modelResult, "model");
        Check(modelResult.Fidelity == PvadCodeFidelity.Exact && modelResult.SourceMap.Single().ElementId == "worker-text", "worker-model-exact-map", checks);
        var modelJson = JsonSerializer.Serialize(modelResult.CanonicalDesign);
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(modelJson));
        var base64 = $"return JsonSerializer.Deserialize<DesignExportDto>(new UTF8Encoding(false, true).GetString(Convert.FromBase64String(\"{payload}\")))!;";
        var base64Result = await Run(new() { Language = PvadCodeLanguages.CSharpBase64, Source = base64 }, null, null, "base64");
        Success(base64Result, "base64");
        Check(JsonSerializer.Serialize(base64Result.CanonicalDesign) == modelJson && base64Result.SourceMap.Single().ElementId == "__payload__", "worker-base64-exact-model-and-map", checks);
        var builder = """
            var builder = new PvadPdfCodeBuilder(new DesignExportDto { Id = "worker-document", Name = "Synthetic worker", PageSettings = new PageSettingsDto { Width = 120, Height = 160 } });
            var page = builder.AddPage(new PageDto { Id = "worker-page" });
            // pvad-element-id: worker-text
            page.Add(new ElementDto { Id = "worker-text", Type = "text", Content = "Synthetic worker", X = 5, Y = 5, Width = 100, Height = 20 });
            return builder.Build();
            """;
        var builderResult = await Run(new() { Language = PvadCodeLanguages.CSharpPdf, Source = builder }, args[0], args[3], "builder");
        Success(builderResult, "builder");
        Check(JsonSerializer.Serialize(builderResult.CanonicalDesign) == modelJson && builderResult.Fidelity == PvadCodeFidelity.Exact && builderResult.SourceMap.Single().ElementId == "worker-text", "worker-builder-exact-model-and-map", checks);
        var legacy = """
            var document = new PdfDocument();
            // pvad-element-id: legacy-first
            document.AddPage(120, 160).DrawText("Synthetic worker", 5, 100, 10);
            // pvad-element-id: legacy-second
            document.AddPage(160, 120).DrawText("Second page", 5, 80, 10);
            return document;
            """;
        var pdfRequest = new PvadCodeWorkerRequest { Language = PvadCodeLanguages.CSharpPdf, Source = legacy, Operation = "execute" };
        var legacyResult = await Run(pdfRequest, args[0], args[3], "legacy");
        Success(legacyResult, "legacy");
        Check(legacyResult.PdfBytes is { Length: > 5 } && legacyResult.PdfBytes.AsSpan().StartsWith("%PDF-"u8), "worker-actual-pdf-signature", checks);
        Check(legacyResult.Fidelity == PvadCodeFidelity.Compatible && legacyResult.CanonicalDesign!.Pages.Count == 2 &&
            legacyResult.CanonicalDesign.Pages[0].Layout is { Width: 120, Height: 160 } &&
            legacyResult.CanonicalDesign.Pages[1].Layout is { Width: 160, Height: 120 } && legacyResult.SourceMap.Count == 2, "worker-legacy-fidelity-mixed-layout-map", checks);
        File.WriteAllBytes(Path.Combine(args[4], "code-worker.pdf"), legacyResult.PdfBytes!);

        foreach (var (name, license, list, code) in new (string, string?, string?, string)[]
        {
            ("missing", null, null, "PVADLIC001"),
            ("wrong", Environment.GetEnvironmentVariable("PVAD_PACKAGE_WRONG_LICENSE_PATH"), null, "PVADLIC012"),
            ("expired", Environment.GetEnvironmentVariable("PVAD_PACKAGE_EXPIRED_LICENSE_PATH"), null, "PVADLIC009"),
            ("revoked", args[2], args[3], "PVADLIC014"),
        }) Failure(await Run(pdfRequest, license, list, name), code, name);
        Failure(await Run(new() { Language = PvadCodeLanguages.CSharpModel, Source = "System.IO.File.ReadAllText(\"never-read\"); return new DesignExportDto();" }, null, null, "sandbox"), "PVADCODE008", "sandbox");
        Failure(await Run(new() { Language = PvadCodeLanguages.CSharpModel, Source = "return new UnknownSyntheticType();" }, null, null, "compile"), "PVADCODE030", "compile");
        Failure(await Run(new() { Language = PvadCodeLanguages.CSharpBase64, Source = "Convert.FromBase64String(\"%%%\"); return new DesignExportDto();" }, null, null, "invalid-base64"), "PVADCODE130", "invalid-base64");
        Failure(await Run(new() { Language = PvadCodeLanguages.CSharpModel, Source = "return new DesignExportDto { Pages = new List<PageDto> { new PageDto { Id = \"same\" }, new PageDto { Id = \"same\" } } };" }, null, null, "duplicate"), "PVADCODE104", "duplicate");
        Check(!Directory.EnumerateFileSystemEntries(scratch).Any(), "worker-request-response-cleanup", checks);
        return new { converters = converted, model = Projection(modelResult), base64 = Projection(base64Result), builder = Projection(builderResult), legacy = Projection(legacyResult) };

        void Success(PvadCodeWorkerResponse result, string name) => Check(result.Success && result.CanonicalDesign is not null && !result.Diagnostics.Any(d => d.Severity == "error"), "worker-success:" + name, checks);
        void Failure(PvadCodeWorkerResponse result, string code, string name) => Check(!result.Success && result.CanonicalDesign is null && result.PdfBytes is null && result.Diagnostics.Any(d => d.Code == code), "worker-denied:" + name + ":" + code, checks);
        async Task<PvadCodeWorkerResponse> Run(PvadCodeWorkerRequest request, string? license, string? list, string name)
        {
            var requestPath = Path.Combine(scratch, name + ".request.json");
            var responsePath = Path.Combine(scratch, name + ".response.json");
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));
            using var process = new Process { StartInfo = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
            process.StartInfo.ArgumentList.Add(Environment.GetEnvironmentVariable("PVAD_PACKAGE_CODE_WORKER_PATH")!);
            process.StartInfo.ArgumentList.Add(requestPath); process.StartInfo.ArgumentList.Add(responsePath);
            process.StartInfo.Environment.Remove("PVAD_NATIVE_LICENSE_PATH"); process.StartInfo.Environment.Remove("PVAD_NATIVE_REVOCATIONS_PATH");
            if (license is not null) process.StartInfo.Environment["PVAD_NATIVE_LICENSE_PATH"] = license;
            if (list is not null) process.StartInfo.Environment["PVAD_NATIVE_REVOCATIONS_PATH"] = list;
            try
            {
                process.Start();
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
                catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } throw; }
                var result = JsonSerializer.Deserialize<PvadCodeWorkerResponse>(await File.ReadAllTextAsync(responsePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                Check(process.ExitCode == (result.Success ? 0 : 2), "worker-exit-contract:" + name, checks);
                await stdout; await stderr;
                return result;
            }
            finally { File.Delete(requestPath); File.Delete(responsePath); }
        }
    }

    private static object Projection(PvadCodeWorkerResponse result) => new { result.CanonicalDesign, result.Diagnostics, result.SourceMap, result.Fidelity };

    private static (string Name, string Format, Func<DesignExportDto, byte[]> Run)[] Calls(IDocumentExporter[] exporters)
    {
        var result = new List<(string Name, string Format, Func<DesignExportDto, byte[]> Run)>();
        foreach (var exporter in exporters)
        {
            foreach (var method in exporter.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => method.Name == "Export"))
            {
                result.Add((exporter.FormatKey + "-direct-" + method.GetParameters().Length, exporter.FormatKey, design =>
                {
                    try { return (byte[])method.Invoke(exporter, method.GetParameters().Length == 1 ? [design] : [design, new ExportOptions(Dpi: 72)])!; }
                    catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
                }));
            }
            result.Add((exporter.FormatKey + "-interface", exporter.FormatKey, design => exporter.Export(design)));
            result.Add((exporter.FormatKey + "-interface-options", exporter.FormatKey, design => exporter.Export(design, new(Dpi: 72))));
        }
        return result.ToArray();
    }

    private static object Inspect(byte[] bytes, string format, int pages, string name, List<string> checks)
    {
        if (format == "odt" || (pages > 1 && format is "png" or "jpeg" or "tiff" or "svg"))
        {
            using var archive = new ZipArchive(new MemoryStream(bytes));
            var parts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Check(archive.Entries.Select(e => e.FullName).Distinct().Count() == archive.Entries.Count, "converter-unique-zip-parts:" + name, checks);
            if (format != "odt") Check(archive.Entries.Count == pages, "converter-page-count:" + name, checks);
            foreach (var entry in archive.Entries)
            {
                using var input = entry.Open(); using var buffer = new MemoryStream(); input.CopyTo(buffer);
                var content = buffer.ToArray();
                if (format == "odt" && entry.FullName == "meta.xml")
                {
                    var xml = XDocument.Parse(Encoding.UTF8.GetString(content).TrimStart('\uFEFF'));
                    var date = xml.Descendants(XName.Get("creation-date", "urn:oasis:names:tc:opendocument:xmlns:meta:1.0")).Single();
                    Check(DateTime.TryParseExact(date.Value, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "converter-odt-creation-date:" + name, checks);
                    date.Value = "generated-creation-date";
                    content = Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
                }
                parts.Add(entry.FullName, Convert.ToHexStringLower(SHA256.HashData(content)));
                if (format != "odt") Signature(content, format, name + ":" + entry.Name, checks);
            }
            if (format == "odt") Check(parts.Keys.SequenceEqual(new[] { "META-INF/manifest.xml", "content.xml", "meta.xml", "mimetype", "styles.xml" }), "converter-odt-parts:" + name, checks);
            return parts;
        }
        Signature(bytes, format, name, checks);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static void Signature(byte[] bytes, string format, string name, List<string> checks)
    {
        if (format is "png" or "jpeg")
        {
            Check(format == "png" ? bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }) : bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }), "converter-image-signature:" + name, checks);
            using var bitmap = SKBitmap.Decode(bytes);
            Check(bitmap is not null && bitmap.Width > 0 && bitmap.Height > 0, "converter-image-decodes:" + name, checks);
        }
        else if (format == "tiff") Check(bytes.AsSpan().StartsWith(new byte[] { 73, 73, 42, 0 }), "converter-tiff-signature:" + name, checks);
        else
        {
            var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            var content = format == "svg" ? string.Concat(XDocument.Parse(text).Root!.Value.Where(c => !char.IsWhiteSpace(c))) : text;
            Check(content.Contains(format == "svg" ? "Syntheticconvertercontent" : "Synthetic converter content", StringComparison.Ordinal), "converter-text-content:" + name, checks);
            if (format == "xml") Check(XDocument.Parse(text).Root!.Name.LocalName == "PvadDocument", "converter-xml-root:" + name, checks);
            if (format == "svg") Check(XDocument.Parse(text).Root!.Name.LocalName == "svg", "converter-svg-root:" + name, checks);
        }
    }

    private static DesignExportDto Design(bool multiple = true) => new()
    {
        Id = "synthetic-converter", Name = "Synthetic converter", PageSettings = new() { Width = 120, Height = 160 },
        Pages = Enumerable.Range(0, multiple ? 2 : 1).Select(i => new PageDto
        {
            Id = "page-" + i, Layout = i == 0 ? null : new() { Width = 160, Height = 120, Orientation = "landscape" },
            Elements = [new() { Id = "text-" + i, Type = "text", Content = "Synthetic converter content", X = 5, Y = 5, Width = 100, Height = 40 }],
        }).ToList(),
    };

    private static void Check(bool condition, string name, List<string> checks)
    { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
    private static void Denied(Action action, string code, string name, List<string> checks)
    {
        try { action(); throw new InvalidOperationException(name); }
        catch (PvadLicenseException error) { Check(error.Code == code, name, checks); }
    }
}
