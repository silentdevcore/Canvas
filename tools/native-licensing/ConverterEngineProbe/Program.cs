using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Converters;
using PVAD.Licensing;
using SkiaSharp;

if (args.Length != 3) return 64;
var checks = new List<string>();
var exporters = typeof(DocumentExporter).Assembly.GetExportedTypes()
    .Where(type => !type.IsAbstract && typeof(IDocumentExporter).IsAssignableFrom(type))
    .OrderBy(type => type.Name).Select(type => (IDocumentExporter)Activator.CreateInstance(type)!).ToArray();
Check(exporters.Select(e => e.FormatKey).Order().SequenceEqual(new[] { "csv", "html", "jpeg", "md", "odt", "png", "svg", "tiff", "xml" }), "complete-nine-exporter-catalog");
var options = new ExportOptions(Dpi: 72, Quality: 83);
var methods = exporters.SelectMany(exporter => exporter.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
    .Where(method => method.Name == "Export").Select(method => (exporter, method))).ToArray();
Check(methods.Length == 13, "complete-thirteen-public-overloads");
var calls = methods.Select(pair => (Name: pair.exporter.FormatKey + "-direct-" + pair.method.GetParameters().Length,
    Exporter: pair.exporter, Run: (Func<DesignExportDto, byte[]>)(design => Invoke(pair.method, pair.exporter, design))))
    .Concat(exporters.SelectMany(exporter => new[]
    {
        (Name: exporter.FormatKey + "-interface", Exporter: exporter, Run: (Func<DesignExportDto, byte[]>)(design => exporter.Export(design))),
        (Name: exporter.FormatKey + "-interface-options", Exporter: exporter, Run: (Func<DesignExportDto, byte[]>)(design => exporter.Export(design, options))),
    })).ToArray();
var design = Design();
var before = JsonSerializer.Serialize(design);
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
foreach (var exporter in exporters)
    Check(!string.IsNullOrWhiteSpace(exporter.FormatKey) && !string.IsNullOrWhiteSpace(exporter.MimeType) &&
        exporter.FileExtension.StartsWith('.') && exporter.Capabilities is not null, exporter.FormatKey + ":metadata-free");
foreach (var call in calls)
{
    Denied(() => call.Run(design), "PVADLIC001", call.Name + ":unlicensed");
    Denied(() => call.Run(null!), "PVADLIC001", call.Name + ":admission-before-input");
}
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-load-rejected");
foreach (var call in calls) Denied(() => call.Run(design), "PVADLIC001", call.Name + ":expired-load-not-activated");
PvadLicense.LoadFromFile(args[1]);
foreach (var call in calls) Denied(() => call.Run(design), "PVADLIC012", call.Name + ":wrong-product");
Check(JsonSerializer.Serialize(design) == before, "denial-preserves-design");

PvadLicense.LoadFromFile(args[0]);
foreach (var product in new[] { "pdf", "word", "spreadsheet", "importer" })
    Denied(() => PvadLicense.Require(product), "PVADLIC012", "no-additional-grant:" + product);
foreach (var call in calls)
{
    Inspect(call.Run(design), call.Exporter.FormatKey, call.Name + ":single", 1, Dpi(call.Name));
    Inspect(call.Run(Design(multiple: true)), call.Exporter.FormatKey, call.Name + ":multiple", 2, Dpi(call.Name));
}
Check(JsonSerializer.Serialize(design) == before, "licensed-export-preserves-design");

// User values can replace the live license while the built-in renderer is active.
foreach (var exporter in exporters.Where(e => e.FormatKey != "csv"))
{
    PvadLicense.LoadFromFile(args[0]);
    var document = Design(multiple: true);
    var changes = 0;
    document.Pages[0].Elements[0].Style = new() { ["fontWeight"] = new CallbackValue(() =>
    {
        changes++;
        PvadLicense.LoadFromFile(args[1]);
        Denied(() => exporter.Export(Design()), "PVADLIC012", exporter.FormatKey + ":callback-native-call-fresh");
    }) };
    var bytes = exporter.Export(document, options);
    Check(changes == 1, exporter.FormatKey + ":replacement-callback-executed");
    Inspect(bytes, exporter.FormatKey, exporter.FormatKey + ":admitted-completion", 2, 72);
    Denied(() => exporter.Export(document), "PVADLIC012", exporter.FormatKey + ":next-call-denied");

    foreach (var nativeFailure in new[] { false, true })
    {
        PvadLicense.LoadFromFile(args[0]);
        var failing = Design();
        failing.Pages[0].Elements[0].Style = new() { ["fontWeight"] = new CallbackValue(() =>
        {
            PvadLicense.LoadFromFile(args[1]);
            if (nativeFailure) PvadLicense.Require(PvadLicenseProducts.Converter);
            throw new IOException("Synthetic converter source failure.");
        }) };
        try { exporter.Export(failing); throw new InvalidOperationException("Expected original converter failure."); }
        catch (Exception error) when (nativeFailure ? error is PvadLicenseException { Code: "PVADLIC012" } : error is IOException)
        { Check(true, exporter.FormatKey + ":original-error:" + nativeFailure); }
        Denied(() => exporter.Export(Design()), "PVADLIC012", exporter.FormatKey + ":failed-scope-closed:" + nativeFailure);
    }
}

PvadLicense.LoadFromFile(args[1]);
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "failed-renewal-rejected");
foreach (var call in calls) Denied(() => call.Run(design), "PVADLIC012", call.Name + ":failed-renewal-preserves-snapshot");
PvadLicense.LoadFromFile(args[0]);
foreach (var call in calls) Inspect(call.Run(design), call.Exporter.FormatKey, call.Name + ":renewal", 1, Dpi(call.Name));
Check(checks.Distinct().Count() == checks.Count, "unique-check-ids");
Console.WriteLine(JsonSerializer.Serialize(new { checks, exporters = exporters.Length, publicExportMethods = methods.Length,
    interfaceCalls = calls.Length - methods.Length, processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
return 0;

byte[] Invoke(MethodInfo method, object exporter, DesignExportDto source)
{
    try { return (byte[])method.Invoke(exporter, method.GetParameters().Length == 1 ? [source] : [source, options])!; }
    catch (TargetInvocationException error) when (error.InnerException is not null)
    { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
}

void Inspect(byte[] bytes, string format, string name, int pages, int dpi)
{
    if (format == "odt")
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("mimetype")!.Open());
        Check(reader.ReadToEnd() == "application/vnd.oasis.opendocument.text", name + ":odt-mimetype");
        using var content = archive.GetEntry("content.xml")!.Open();
        Check(XDocument.Load(content).Root!.Value.Contains("Synthetic converter content"), name + ":odt-content");
        return;
    }
    if (pages > 1 && format is "png" or "jpeg" or "tiff" or "svg")
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Check(archive.Entries.Count == pages, name + ":zip-page-count");
        for (var i = 0; i < pages; i++)
        {
            var extension = format == "jpeg" ? "jpg" : format;
            Check(archive.Entries[i].Name == $"page-{i + 1}.{extension}", name + ":page-name:" + i);
            using var input = archive.Entries[i].Open(); using var buffer = new MemoryStream(); input.CopyTo(buffer);
            Page(buffer.ToArray(), format, name + ":page:" + i, i, dpi);
        }
    }
    else if (format is "png" or "jpeg" or "tiff" or "svg") Page(bytes, format, name, 0, dpi);
    else
    {
        var text = Encoding.UTF8.GetString(bytes);
        Check(text.Contains("Synthetic converter content"), name + ":text-content");
        if (format == "xml") Check(XDocument.Parse(text.TrimStart('\uFEFF')).Root?.Name.LocalName == "PvadDocument", name + ":xml-root");
        if (format == "html") Check(text.Contains("<!DOCTYPE html>"), name + ":html-header");
        if (format == "csv") Check(text.Contains("# Metadata"), name + ":csv-header");
    }
}

void Page(byte[] bytes, string format, string name, int index, int dpi)
{
    var scale = format == "svg" ? 1f : dpi / 72f;
    var expected = index == 0 ? ((int)(120 * scale), (int)(160 * scale)) : ((int)(160 * scale), (int)(120 * scale));
    if (format is "png" or "jpeg")
    {
        Check(format == "png" ? bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }) : bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }), name + ":image-signature");
        using var bitmap = SKBitmap.Decode(bytes);
        Check(bitmap is not null && (bitmap.Width, bitmap.Height) == expected, name + ":image-dimensions");
    }
    else if (format == "svg")
    {
        using var input = new MemoryStream(bytes);
        var xml = XDocument.Load(input);
        Check(xml.Root!.Name.LocalName == "svg" && (string?)xml.Root.Attribute("width") == expected.Item1.ToString() &&
            (string?)xml.Root.Attribute("height") == expected.Item2.ToString(), name + ":svg-dimensions");
        Check(string.Concat(xml.Root.Value.Where(c => !char.IsWhiteSpace(c))).Contains("Syntheticconvertercontent"), name + ":svg-content");
    }
    else
    {
        Check(bytes.AsSpan().StartsWith(new byte[] { 73, 73, 42, 0 }), name + ":tiff-signature");
        using var reader = new BinaryReader(new MemoryStream(bytes));
        reader.BaseStream.Position = BitConverter.ToUInt32(bytes, 4);
        var count = reader.ReadUInt16(); var width = 0; var height = 0;
        for (var i = 0; i < count; i++)
        {
            var tag = reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32(); var value = reader.ReadUInt32();
            if (tag == 256) width = (int)value; if (tag == 257) height = (int)value;
        }
        Check((width, height) == expected, name + ":tiff-dimensions");
    }
}

static DesignExportDto Design(bool multiple = false) => new()
{
    Id = "synthetic-converter", Name = "Synthetic converter", PageSettings = new() { Width = 120, Height = 160 },
    Pages = Enumerable.Range(0, multiple ? 2 : 1).Select(i => new PageDto
    {
        Id = "page-" + i, Layout = i == 0 ? null : new() { Width = 160, Height = 120, Orientation = "landscape" },
        Elements = [new() { Id = "text-" + i, Type = "text", Content = "Synthetic converter content", X = 5, Y = 5, Width = 100, Height = 40 }],
    }).ToList(),
};

static int Dpi(string call) => call.EndsWith("-direct-1", StringComparison.Ordinal) || call.EndsWith("-interface", StringComparison.Ordinal) ? 150 : 72;

void Check(bool condition, string name)
{ if (!condition) throw new InvalidOperationException("Converter probe failed: " + name); checks.Add(name); }
void Denied(Action action, string code, string name)
{
    try { action(); } catch (PvadLicenseException error) when (error.Code == code) { Check(true, name); return; }
    throw new InvalidOperationException("Expected converter admission failure: " + name);
}

sealed class CallbackValue(Action action)
{
    private bool called;
    public override string ToString() { if (!called) { called = true; action(); } return "bold"; }
}
