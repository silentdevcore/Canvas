using System.Buffers;
using System.Collections;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Debugging;
using PVAD.Importer.Document;
using PVAD.Importer.Generation;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Licensing;

internal static class RegenerationContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks, byte[] sourcePdf)
    {
        var start = checks.Count;
        var bridge = new PvadPdfGeneratorBridge();
        var pipeline = new PdfGeneratorBridgePipeline(bridge);
        var sourceHash = SHA256.HashData(sourcePdf);
        foreach (var kind in new[] { "metadata", "rgb-image", "indexed-image", "indexed-palette", "soft-mask", "shading" })
        foreach (var viaPipeline in new[] { false, true })
        {
            var phase = kind + (viaPipeline ? ":pipeline" : ":bridge");
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineMemory = Payload(kind, () => { });
            var baselineModel = await Model(kind, baselineMemory.Memory);
            using var baseline = new MemoryStream();
            await bridge.RegenerateAsync(baselineModel, baseline);
            using var memory = Payload(kind, () => Replace(phase));
            var model = await Model(kind, memory.Memory);
            using var output = new MemoryStream();
            if (viaPipeline) await pipeline.SaveAsync(model, output);
            else await bridge.RegenerateAsync(model, output);
            Check(memory.Reads > 0 && output.CanWrite && output.ToArray().SequenceEqual(baseline.ToArray()), phase + ":identical-regenerated-bytes");
            Check(sourceHash.SequenceEqual(SHA256.HashData(sourcePdf)), phase + ":source-pdf-preserved");
            await Denied(() => bridge.RegenerateAsync(model, output), phase + ":new-regeneration-denied");
            await Denied(() => pipeline.SaveAsync(model, output), phase + ":new-pipeline-denied");
            await Denied(() => Task.FromResult(bridge.MapResources(model.Pages[0].Resources)), phase + ":new-mapping-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            await Denied(() => Task.FromResult(new PVAD.Pdf.PdfDocument()), phase + ":importer-only-has-no-general-pdf-grant");
            using var reopened = new MemoryStream(output.ToArray());
            var result = await new PdfImporter().LoadAsync(reopened);
            Check(result.Pages.Count == 2 && result.Pages[1].MediaBox?.Width == 120 && result.Pages[1].Rotate == 90, phase + ":mixed-geometry-and-rotation");
            Check(result.Pages[0].TextObjects.Any(text => text.Text.Contains("Imported content", StringComparison.Ordinal)) && result.Pages[0].GraphicsObjects.OfType<PdfPathElement>().Any(), phase + ":text-and-paths");
            Check(result.Pages[0].GraphicsObjects.OfType<PdfImageElement>().Count() == (kind.Contains("image", StringComparison.Ordinal) || kind is "indexed-palette" or "soft-mask" ? 1 : 0), phase + ":image-count");
            Check(result.Pages[0].GraphicsObjects.OfType<PdfShadingElement>().Count() == (kind == "shading" ? 1 : 0), phase + ":shading-count");
        }

        foreach (var method in new[] { "object", "resources" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var bytes = "synthetic mapped payload"u8.ToArray();
            object Map(ReadOnlyMemory<byte> payload)
            {
                var dictionary = new PdfDictionary
                {
                    ["First"] = new PdfStreamObject(new() { ["Nested"] = new PdfArray([new PdfInteger(1)]) }, payload),
                    ["Second"] = new PdfArray([new PdfDictionary { ["Retained"] = new PdfInteger(42) }]),
                };
                return method == "object" ? bridge.MapObject(new PdfArray([dictionary, new PdfInteger(7)])) : bridge.MapResources(dictionary);
            }
            var expected = JsonSerializer.Serialize(Map(bytes));
            using var memory = new SwitchingMemory(bytes, () => Replace("map:" + method));
            var mapped = Map(memory.Memory);
            Check(memory.Reads > 0 && JsonSerializer.Serialize(mapped) == expected, "map:" + method + ":recursive-output-preserved");
            await Denied(() => Task.FromResult(Map(bytes)), "map:" + method + ":next-call-denied");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        var group = new PdfGroupElement(1, PdfMatrix.Identity, Command("BMC", 1));
        group.Children.Add(new PdfTextElement(2, PdfMatrix.Identity, Command("Tj", 2), "Mapped text") { FontSize = 12 });
        group.Children.Add(new PdfGroupElement(3, PdfMatrix.Identity, Command("BMC", 3)));
        var mappedGroup = (Dictionary<string, object?>)bridge.MapGraphicsElement(group);
        Check(((object[])mappedGroup["Children"]!).Length == 2, "map:graphics:children-preserved");

        foreach (var entry in new[] { "resolve", "form" })
        foreach (var empty in new[] { false, true })
        {
            var phase = "xobject:" + entry + ":" + empty;
            PvadLicense.LoadFromFile(licenses[0]);
            var bytes = empty ? Array.Empty<byte>() : "10 10 30 20 re f"u8.ToArray();
            // Whitespace-only content exercises the existing empty-children/BBox fallback.
            using var memory = new SwitchingMemory(empty ? " "u8.ToArray() : bytes, () => Replace(phase));
            var resolver = new XObjectResolver(new PdfObjectResolver(new PdfObjectGraph()));
            ResolvedXObject? Resolve(ReadOnlyMemory<byte> content)
            {
                var form = new PdfStreamObject(new() { ["Subtype"] = new PdfName("Form"), ["BBox"] = Numbers(0, 0, 20, 30), ["Matrix"] = Numbers(1, 0, 0, 1, 5, 10) }, content);
                return entry == "resolve" ? resolver.Resolve("Fm", new() { ["XObject"] = new PdfDictionary { ["Fm"] = form } }, PdfMatrix.Identity) : resolver.ResolveForm("Fm", form, PdfMatrix.Identity);
            }
            var expected = Resolve(empty ? " "u8.ToArray() : bytes)!;
            var result = Resolve(memory.Memory)!;
            Check(memory.Reads > 0 && result.Transform == expected.Transform && result.Bounds == expected.Bounds && result.Children.Count == expected.Children.Count, phase + ":bounds-transform-and-children");
            await Denied(() => Task.FromResult(Resolve(bytes)), phase + ":new-resolution-denied");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        var imageResolver = new XObjectResolver(new PdfObjectResolver(new PdfObjectGraph()));
        var imageResult = imageResolver.ResolveImage("Im", new(new(), ReadOnlyMemory<byte>.Empty), new(20, 0, 0, 30, 5, 10));
        Check(imageResult.Bounds == new PdfRectangle(5, 10, 20, 30), "xobject:image:transformed-bounds");
        Check(imageResolver.Resolve("missing", new(), PdfMatrix.Identity) is null && imageResolver.Resolve("missing", new(), PdfMatrix.Identity, 17) is null, "xobject:missing-and-recursion-limit-unchanged");

        PvadLicense.LoadFromFile(licenses[0]);
        using (var source = new MemoryStream(sourcePdf))
        {
            var page = (await new PdfImporter().LoadAsync(source)).Pages[0];
            var scene = new SceneGraphEngine().BuildPage(0, page);
            var overlays = new PdfDebugOverlayBuilder();
            var expected = JsonSerializer.Serialize(overlays.Build(scene));
            var objects = new ArmedList<PrimitiveObject>(scene.Layers[0].Objects);
            var equivalent = new PdfScenePage(scene.PageIndex, scene.PageBounds) { ReadingOrder = scene.ReadingOrder, Layout = scene.Layout };
            equivalent.Layers.Add(new PdfSceneLayer("Content", objects)); equivalent.VisualGroups.AddRange(scene.VisualGroups);
            objects.BeforeRead = () => Replace("debug");
            Check(JsonSerializer.Serialize(overlays.Build(equivalent)) == expected && objects.Changes == 1, "debug:all-overlay-items-and-labels-preserved");
            await Denied(() => Task.FromResult(overlays.Build(scene)), "debug:new-build-denied");
        }

        foreach (var failure in new[] { "input-io", "input-license", "cancel", "output-io", "output-license" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var cancellation = new CancellationTokenSource();
            using var memory = Payload("metadata", () =>
            {
                Replace(failure);
                if (failure == "input-io") throw new IOException("Synthetic regeneration source failure.");
                if (failure == "input-license") PvadLicense.Require(PvadLicenseProducts.Importer);
                if (failure == "cancel") cancellation.Cancel();
            });
            var model = await Model("metadata", memory.Memory);
            using var output = new CallbackOutput(() =>
            {
                if (failure == "output-io") throw new IOException("Synthetic regeneration output failure.");
                if (failure == "output-license") PvadLicense.Require(PvadLicenseProducts.Importer);
            }, asynchronous: true);
            output.Write("sentinel"u8); output.Position = 3;
            try { await pipeline.SaveAsync(model, output, cancellation.Token); throw new Exception("Expected regeneration failure: " + failure); }
            catch (Exception e) when (failure switch
            {
                "input-io" or "output-io" => e is IOException,
                "cancel" => e is OperationCanceledException,
                _ => e is PvadLicenseException { Code: "PVADLIC012" },
            }) { Check(output.CanWrite && output.Position == 3 && output.ToArray().AsSpan().SequenceEqual("sentinel"u8), failure + ":failure-before-output-preserves-stream"); }
            await Denied(() => pipeline.SaveAsync(model, output), failure + ":failed-admission-closed");
        }

        foreach (var asynchronous in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var memory = Payload("metadata", () => { });
            var model = await Model("metadata", memory.Memory);
            using var output = new CallbackOutput(() =>
            {
                Replace("output:" + asynchronous);
                try { pipeline.SaveAsync(model, Stream.Null); }
                catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, "output:" + asynchronous + ":synchronous-new-save-denial"); return; }
                throw new Exception("Public SaveAsync did not check the current license synchronously.");
            }, asynchronous);
            await pipeline.SaveAsync(model, output);
            Check(output.Calls == 1 && output.ToArray().AsSpan().StartsWith("%PDF-"u8), "output:" + asynchronous + ":admitted-write-finishes");
        }

        foreach (var asynchronous in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var memory = Payload("metadata", () => { });
            var model = await Model("metadata", memory.Memory);
            var custom = new DelegatingBridge(bridge, () => Replace("custom:" + asynchronous), asynchronous);
            using var output = new MemoryStream();
            await Denied(() => new PdfGeneratorBridgePipeline(custom).SaveAsync(model, output), "custom:" + asynchronous + ":public-native-dispatch");
            Check(custom.Calls == 1 && output.Length == 0, "custom:" + asynchronous + ":not-bypassed-and-no-output");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        return checks.Count - start;

        async Task<PdfDocumentModel> Model(string kind, ReadOnlyMemory<byte> trigger)
        {
            using var source = new MemoryStream(sourcePdf);
            var model = await new PdfImporter().LoadAsync(source);
            var page = model.Pages[0];
            model.Metadata["Title"] = new PdfString(kind is "metadata" or "shading" ? trigger : "Synthetic regeneration"u8.ToArray(), false);
            page.GraphicsObjects.Add(new PdfPathElement(2, PdfMatrix.Identity, Command("f", 2), [new RectangleSegment(new(20, 20, 30, 40))]) { FillColor = PdfColor.Black });
            if (kind is "rgb-image" or "indexed-image" or "indexed-palette" or "soft-mask")
            {
                var indexed = kind.StartsWith("indexed", StringComparison.Ordinal);
                var image = new PdfStreamObject(new()
                {
                    ["Subtype"] = new PdfName("Image"), ["Type"] = new PdfName("XObject"), ["Width"] = new PdfInteger(1), ["Height"] = new PdfInteger(1),
                    ["BitsPerComponent"] = new PdfInteger(8), ["Filter"] = new PdfName("FlateDecode"),
                    ["ColorSpace"] = indexed ? new PdfArray([new PdfName("Indexed"), new PdfName("DeviceRGB"), new PdfInteger(0), new PdfStreamObject(new() { ["Filter"] = new PdfName("FlateDecode") }, kind == "indexed-palette" ? trigger : Compress([255, 0, 0]))]) : new PdfName("DeviceRGB"),
                }, kind is "rgb-image" or "indexed-image" ? trigger : Compress(indexed ? [0] : [255, 0, 0]));
                if (kind == "soft-mask") image.Dictionary["SMask"] = new PdfStreamObject(new() { ["Subtype"] = new PdfName("Image"), ["Width"] = new PdfInteger(1), ["Height"] = new PdfInteger(1), ["BitsPerComponent"] = new PdfInteger(8), ["ColorSpace"] = new PdfName("DeviceGray"), ["Filter"] = new PdfName("FlateDecode") }, trigger);
                page.Resources["XObject"] = new PdfDictionary { ["Synthetic"] = image };
                page.GraphicsObjects.Add(new PdfImageElement(3, new(20, 0, 0, 20, 200, 40), Command("Do", 3), "Synthetic"));
            }
            if (kind == "shading")
            {
                page.Resources["Shading"] = new PdfDictionary { ["Sh"] = new PdfDictionary
                {
                    ["ShadingType"] = new PdfInteger(2), ["ColorSpace"] = new PdfName("DeviceRGB"), ["Coords"] = Numbers(0, 0, 100, 0),
                    ["Function"] = new PdfDictionary { ["FunctionType"] = new PdfInteger(2), ["Domain"] = Numbers(0, 1), ["C0"] = Numbers(1, 0, 0), ["C1"] = Numbers(0, 0, 1), ["N"] = new PdfInteger(1) },
                    ["Extend"] = new PdfArray([new PdfBoolean(true), new PdfBoolean(true)]),
                } };
                page.GraphicsObjects.Add(new PdfShadingElement(4, PdfMatrix.Identity, Command("sh", 4), "Sh"));
            }
            model.Pages.Add(new(null, new()) { MediaBox = new(0, 0, 120, 300), Rotate = 90 });
            return model;
        }
        void Replace(string phase)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { PvadLicense.Require(PvadLicenseProducts.Importer); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, phase + ":current-license-changed"); return; }
            throw new Exception("Replacement did not remove Importer rights.");
        }
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Regeneration continuation failed: " + name); checks.Add("regeneration-continuation:" + name); }
        async Task Denied(Func<Task> action, string name)
        {
            try { await action(); } catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected fresh native denial: " + name);
        }
    }

    private static SwitchingMemory Payload(string kind, Action change) => new(kind switch
    {
        "rgb-image" or "indexed-palette" => Compress([255, 0, 0]), "indexed-image" => Compress([0]), "soft-mask" => Compress([128]), _ => "Synthetic regeneration"u8.ToArray(),
    }, change);
    private static PdfArray Numbers(params double[] values) => new(values.Select(v => (PdfObject)new PdfNumber(v)));
    private static PdfContentCommand Command(string name, int sequence) => new(new(name, PdfOperatorCategory.XObject, null), [], default, sequence);
    private static byte[] Compress(byte[] bytes)
    { using var result = new MemoryStream(); using (var zip = new ZLibStream(result, CompressionLevel.SmallestSize, true)) zip.Write(bytes); return result.ToArray(); }

    private sealed class SwitchingMemory(byte[] bytes, Action change) : MemoryManager<byte>
    {
        internal int Reads { get; private set; }
        public override Memory<byte> Memory => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan() { if (Reads++ == 0) change(); return bytes; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { if (disposing) CryptographicOperations.ZeroMemory(bytes); }
    }
    private sealed class ArmedList<T>(IReadOnlyList<T> values) : IReadOnlyList<T>
    {
        internal Action? BeforeRead { get; set; }
        internal int Changes { get; private set; }
        private void Before() { if (BeforeRead is not null && Changes == 0) { Changes++; BeforeRead(); } }
        public int Count { get { Before(); return values.Count; } }
        public T this[int index] { get { Before(); return values[index]; } }
        public IEnumerator<T> GetEnumerator() { Before(); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CallbackOutput(Action change, bool asynchronous) : MemoryStream
    {
        internal int Calls { get; private set; }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { if (asynchronous) await Task.Yield(); Calls++; change(); await base.WriteAsync(buffer, cancellationToken); }
    }
    private sealed class DelegatingBridge(PvadPdfGeneratorBridge inner, Action change, bool asynchronous) : IPdfGeneratorBridge
    {
        internal int Calls { get; private set; }
        public async Task RegenerateAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default)
        { if (asynchronous) await Task.Yield(); Calls++; change(); await inner.RegenerateAsync(document, output, cancellationToken); }
        public object MapObject(PdfObject value) => inner.MapObject(value);
        public object MapGraphicsElement(PdfGraphicsElement value) => inner.MapGraphicsElement(value);
        public object MapResources(PdfDictionary value) => inner.MapResources(value);
    }
}
