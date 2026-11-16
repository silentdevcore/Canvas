using System.Reflection;
using System.IO.Compression;
using PVAD.Importer;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Debugging;
using PVAD.Importer.Document;
using PVAD.Importer.Generation;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Infrastructure.Pdf;
using PVAD.Licensing;

internal sealed class RegenerationChecks : IDisposable
{
    private readonly Dictionary<string, Action> calls = [];
    private readonly PvadPdfGeneratorBridge bridge = new();
    private readonly CountingBridge custom = new();
    private readonly MemoryStream output = new();
    private PdfDocumentModel document = new();
    private PdfScenePage scene = new(0, null);
    private PVAD.Importer.Graphics.PdfGraphicsElement graphic = null!;
    private PVAD.Pdf.PdfDocument? captured;
    private readonly byte[] source;
    private readonly string configuredKey;
    private int callbacks;

    internal RegenerationChecks(byte[] source)
    {
        this.source = source;
        Add<PvadPdfGeneratorBridge>("RegenerateAsync", () => Await(bridge.RegenerateAsync(document, output)), typeof(PdfDocumentModel), typeof(Stream), typeof(CancellationToken));
        configuredKey = Add<PvadPdfGeneratorBridge>("RegenerateAsync", () => Await(bridge.RegenerateAsync(document, output, generated =>
        {
            callbacks++; captured = generated; generated.Info.Title = "Synthetic configured regeneration";
        })), typeof(PdfDocumentModel), typeof(Stream), typeof(Action<PVAD.Pdf.PdfDocument>), typeof(CancellationToken));
        Add<PvadPdfGeneratorBridge>("MapObject", () => Equal((long)bridge.MapObject(new PdfInteger(42)) == 42), typeof(PdfObject));
        Add<PvadPdfGeneratorBridge>("MapGraphicsElement", () => Equal(bridge.MapGraphicsElement(graphic) is Dictionary<string, object?>), typeof(PVAD.Importer.Graphics.PdfGraphicsElement));
        Add<PvadPdfGeneratorBridge>("MapResources", () => Equal(bridge.MapResources(new PdfDictionary()) is Dictionary<string, object>), typeof(PdfDictionary));
        var pipeline = new PdfGeneratorBridgePipeline(bridge);
        Add<PdfGeneratorBridgePipeline>("SaveAsync", () => Await(pipeline.SaveAsync(document, output)), typeof(PdfDocumentModel), typeof(Stream), typeof(CancellationToken));
        var overlays = new PdfDebugOverlayBuilder();
        Add<PdfDebugOverlayBuilder>("Build", () => Equal(overlays.Build(scene).Count > 0), typeof(PdfScenePage));
        var types = new[] { typeof(PvadPdfGeneratorBridge), typeof(PdfGeneratorBridgePipeline), typeof(PdfDebugOverlayBuilder) };
        Equal(types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).Where(method => !method.IsSpecialName).Select(Key).ToHashSet().SetEquals(calls.Keys));
    }

    internal void CheckMissing(List<string> checks)
    {
        checks.Add("regeneration:public-processing-signature-inventory");
        Denials("missing", "PVADLIC001", checks);
    }

    internal int CheckLicensed(string[] licenses, List<string> checks)
    {
        PvadLicense.LoadFromFile(licenses[0]);
        using (var input = new MemoryStream(source)) document = new PdfImporter().LoadAsync(input).GetAwaiter().GetResult();
        graphic = document.Pages[0].GraphicsObjects[0];
        scene = new SceneGraphEngine().BuildPage(0, document.Pages[0]);
        Positive("valid", checks);
        RichOutput("valid", checks);
        Configured(licenses, "valid", checks);
        var sourceState = document.Pages[0].GraphicsObjects.ToArray();
        PvadLicense.LoadFromFile(licenses[1]);
        Denials("wrong", "PVADLIC012", checks);
        Equal(sourceState.SequenceEqual(document.Pages[0].GraphicsObjects));
        Denied(() => PvadLicense.LoadFromFile(licenses[2]), "PVADLIC009");
        Denied(calls.First().Value, "PVADLIC012");
        checks.Add("regeneration:failed-renewal-preserves-denial");
        PvadLicense.LoadFromFile(licenses[0]);
        Positive("renewed", checks);
        RichOutput("renewed", checks);
        Configured(licenses, "renewed", checks);
        using var reentrant = new ReentrantOutput(() =>
        {
            Denied(() => new PVAD.Pdf.PdfDocument(), "PVADLIC012");
            Denied(() => captured!.ToBytes(), "PVADLIC012");
            Denied(() => new PdfDocumentRenderer().Render(captured!), "PVADLIC012");
        });
        Await(bridge.RegenerateAsync(document, reentrant));
        Equal(reentrant.Calls == 1 && reentrant.ToArray().AsSpan().StartsWith("%PDF-"u8));
        checks.Add("regeneration:output-callback-cannot-inherit-generator-rights");
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); ResetOutput();
        try { Await(bridge.RegenerateAsync(document, output, canceled.Token)); throw new InvalidOperationException("Expected cancellation."); }
        catch (OperationCanceledException) { Equal(output.ToArray().SequenceEqual("sentinel"u8.ToArray()) && output.Position == 3); }
        checks.Add("regeneration:cancellation-preserves-output");
        var writer = typeof(PVAD.Pdf.PdfDocument).Assembly.GetType("PVAD.Pdf.Serialization.PdfWriter", true)!;
        Equal(writer.GetMethod("RegenerateForImporter", BindingFlags.Public | BindingFlags.Static) is null);
        Equal(typeof(PVAD.Pdf.PdfDocument).GetConstructors().Length == 1);
        checks.Add("regeneration:dependency-boundary-is-not-public");
        return calls.Count;
    }

    private void Positive(string phase, List<string> checks)
    {
        foreach (var (name, call) in calls.Where(pair => pair.Key != configuredKey))
        {
            output.SetLength(0); call();
            if (name.Contains("RegenerateAsync", StringComparison.Ordinal) || name.Contains("SaveAsync", StringComparison.Ordinal)) VerifyPdf();
            checks.Add($"regeneration:{phase}:{name}");
        }
        ResetOutput(); var before = callbacks;
        Denied(calls[configuredKey], "PVADLIC012");
        Equal(callbacks == before && output.Position == 3 && output.ToArray().SequenceEqual("sentinel"u8.ToArray()));
        checks.Add($"regeneration:{phase}:caller-generator-callback-needs-explicit-pdf-rights");
        Denied(() => new PVAD.Pdf.PdfDocument(), "PVADLIC012");
        checks.Add($"regeneration:{phase}:ordinary-generator-still-denied");
        Await(new PdfGeneratorBridgePipeline(custom).SaveAsync(document, output));
        checks.Add($"regeneration:{phase}:custom-pipeline-admitted");
    }

    private void Configured(string[] licenses, string phase, List<string> checks)
    {
        PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(licenses[0])!, "integrated.json"));
        output.SetLength(0); calls[configuredKey](); VerifyPdf();
        Equal(captured!.Info.Title == "Synthetic configured regeneration");
        checks.Add($"regeneration:{phase}:{configuredKey}");
        ResetOutput();
        try
        {
            Await(bridge.RegenerateAsync(document, output, _ => throw new InvalidOperationException("Synthetic callback failure.")));
            throw new InvalidOperationException("Expected the caller callback to fail.");
        }
        catch (InvalidOperationException error) when (error.Message == "Synthetic callback failure.")
        {
            Equal(output.Position == 3 && output.ToArray().SequenceEqual("sentinel"u8.ToArray()));
        }
        checks.Add($"regeneration:{phase}:callback-failure-leaves-output-intact");
        foreach (var target in new[] { licenses[0], licenses[1] })
        {
            ResetOutput();
            Denied(() => Await(bridge.RegenerateAsync(document, output, _ => PvadLicense.LoadFromFile(target))), "PVADLIC012");
            Equal(output.Position == 3 && output.ToArray().SequenceEqual("sentinel"u8.ToArray()));
            checks.Add($"regeneration:{phase}:callback-revocation-preserves-output:{Path.GetFileNameWithoutExtension(target)}");
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(licenses[0])!, "integrated.json"));
        }
        PvadLicense.LoadFromFile(licenses[0]);
        Denied(() => captured.ToBytes(), "PVADLIC012");
        checks.Add($"regeneration:{phase}:retained-caller-document-cannot-export-with-importer-only");
    }

    private void RichOutput(string phase, List<string> checks)
    {
        using var input = new MemoryStream(source);
        var rich = new PdfImporter().LoadAsync(input).GetAwaiter().GetResult();
        var page = rich.Pages[0];
        using var encodedImage = new MemoryStream();
        using (var zlib = new ZLibStream(encodedImage, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(new byte[] { 255, 0, 0 });
        var image = new PdfStreamObject(new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"), ["Subtype"] = new PdfName("Image"),
            ["Width"] = new PdfInteger(1), ["Height"] = new PdfInteger(1),
            ["ColorSpace"] = new PdfName("DeviceRGB"), ["BitsPerComponent"] = new PdfInteger(8), ["Filter"] = new PdfName("FlateDecode")
        }, encodedImage.ToArray());
        page.Resources["XObject"] = new PdfDictionary { ["NativeImage"] = image };
        page.GraphicsObjects.Add(new PdfImageElement(3, new(20, 0, 0, 20, 200, 40), Command("Do", PdfOperatorCategory.XObject, 3), "NativeImage"));
        page.GraphicsObjects.Add(new PdfPathElement(2, PdfMatrix.Identity, Command("f", PdfOperatorCategory.PathPainting, 2), [new RectangleSegment(new(30, 20, 50, 25))]) { FillColor = PdfColor.Black });
        page.Resources["Shading"] = new PdfDictionary
        {
            ["Sh1"] = new PdfDictionary
            {
                ["ShadingType"] = new PdfInteger(2), ["ColorSpace"] = new PdfName("DeviceRGB"),
                ["Coords"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(0)]),
                ["Function"] = new PdfDictionary
                {
                    ["FunctionType"] = new PdfInteger(2), ["N"] = new PdfInteger(1),
                    ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                    ["C0"] = new PdfArray([new PdfInteger(1), new PdfInteger(0), new PdfInteger(0)]),
                    ["C1"] = new PdfArray([new PdfInteger(0), new PdfInteger(0), new PdfInteger(1)])
                },
                ["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)])
            }
        };
        page.GraphicsObjects.Add(new PdfShadingElement(4, PdfMatrix.Identity, Command("sh", PdfOperatorCategory.Shading, 4), "Sh1"));
        rich.Pages.Add(new PdfPageModel(null, new PdfDictionary()) { MediaBox = new(0, 0, 120, 300), Rotate = 90 });
        using var result = new MemoryStream(); Await(bridge.RegenerateAsync(rich, result)); result.Position = 0;
        var reimported = new PdfImporter().LoadAsync(result).GetAwaiter().GetResult();
        Equal(reimported.Pages.Count == 2 && reimported.Pages[1].MediaBox?.Width == 120 && reimported.Pages[1].Rotate == 90);
        var first = reimported.Pages[0];
        Equal(first.TextObjects.Any(t => t.Text.Contains("Imported content", StringComparison.Ordinal)) && first.GraphicsObjects.OfType<PdfImageElement>().Count() == 1 && first.GraphicsObjects.OfType<PdfShadingElement>().Count() == 1 && first.GraphicsObjects.OfType<PdfPathElement>().Any());
        checks.Add($"regeneration:{phase}:importer-only-mixed-pages-text-path-image-and-shading");
        static PdfContentCommand Command(string name, PdfOperatorCategory category, int sequence) => new(new(name, category, null), [], default, sequence);
    }

    private void Denials(string phase, string code, List<string> checks)
    {
        var before = callbacks; var delegated = custom.Calls;
        foreach (var (name, call) in calls)
        {
            ResetOutput(); Denied(call, code);
            Equal(output.CanWrite && output.Position == 3 && output.ToArray().SequenceEqual("sentinel"u8.ToArray()));
            checks.Add($"regeneration:{phase}:{name}");
        }
        Denied(() => Await(new PdfGeneratorBridgePipeline(custom).SaveAsync(document, output)), code);
        Equal(callbacks == before && custom.Calls == delegated);
        checks.Add($"regeneration:{phase}:no-output-or-caller-dispatch");
    }
    private void VerifyPdf()
    {
        using var input = new MemoryStream(output.ToArray());
        var result = new PdfImporter().LoadAsync(input).GetAwaiter().GetResult();
        Equal(result.Pages.Count == 1 && result.Pages[0].MediaBox?.Width == 300 && result.Pages[0].TextObjects.Any(text => text.Text.Contains("Imported content", StringComparison.Ordinal)));
    }
    private void ResetOutput() { output.SetLength(0); output.Write("sentinel"u8); output.Position = 3; }
    private string Add<T>(string name, Action call, params Type[] parameters)
    {
        var key = Key(typeof(T).GetMethod(name, parameters)!); calls.Add(key, call); return key;
    }
    private static string Key(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.ToString()))})";
    private static void Await(Task task) => task.GetAwaiter().GetResult();
    private static void Equal(bool value) { if (!value) throw new InvalidOperationException("Regeneration admission check failed."); }
    private static void Denied(Action call, string code)
    {
        try { call(); } catch (PvadLicenseException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected regeneration denial: " + code);
    }
    public void Dispose() => output.Dispose();
    private sealed class CountingBridge : IPdfGeneratorBridge
    {
        internal int Calls { get; private set; }
        public Task RegenerateAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default) { Calls++; return Task.CompletedTask; }
        public object MapObject(PdfObject value) => throw new NotSupportedException();
        public object MapGraphicsElement(PVAD.Importer.Graphics.PdfGraphicsElement value) => throw new NotSupportedException();
        public object MapResources(PdfDictionary value) => throw new NotSupportedException();
    }
    private sealed class ReentrantOutput(Action callback) : MemoryStream
    {
        internal int Calls { get; private set; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Calls++; callback(); return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
