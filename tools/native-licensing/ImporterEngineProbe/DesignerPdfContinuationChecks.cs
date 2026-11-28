using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.FileImporter;
using PVAD.Importer;
using PVAD.Importer.Analysis;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class DesignerPdfContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks, byte[] plain, byte[] image)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var fixtures = new Dictionary<string, byte[]>
        {
            ["plain"] = plain, ["mixed"] = Mixed(), ["labeled-chart"] = Bars(true),
            ["review-chart"] = Bars(false), ["metadata"] = Metadata(false), ["invalid-metadata"] = Metadata(true),
        };
        var registry = FileImporterRegistry.Create("pdf");
        var instance = new PdfFileImporter();
        var entries = new Dictionary<string, Func<Stream, Task<DesignExportDto>>>
        {
            ["registry"] = s => registry.ImportAsync(s, "Synthetic continuation"),
            ["instance"] = s => instance.ImportAsync(s, "Synthetic continuation"),
            ["instance-cancellation"] = s => instance.ImportAsync(s, "Synthetic continuation", CancellationToken.None),
            ["static"] = s => PdfFileImporter.DoImportAsync(s, "Synthetic continuation"),
            ["options-review"] = s => PdfFileImporter.DoImportAsync(s, "Synthetic continuation",
                new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Review }),
        };
        foreach (var (entry, import) in entries)
        foreach (var (fixture, bytes) in fixtures)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineInput = new MemoryStream(bytes);
            var baseline = await import(baselineInput);
            VerifyFixture(baseline, fixture, entry);
            var expected = Fingerprint(baseline);
            var sourceHash = SHA256.HashData(bytes);
            foreach (var seekable in new[] { true, false })
            {
                var phase = $"{entry}:{fixture}:{seekable}";
                PvadLicense.LoadFromFile(licenses[0]);
                using var input = new SwitchingStream(bytes, seekable, () => Replace(phase));
                var result = await import(input);
                Check(Fingerprint(result) == expected, phase + ":identical-dto-layout-content-and-diagnostics");
                VerifyFixture(result, fixture, entry);
                Check(input.Reads > 0 && input.CanRead, phase + ":input-remains-open");
                Check(sourceHash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-unchanged");
                using var denied = new SwitchingStream(bytes, seekable, () => throw new Exception("Denied import read input."));
                await Denied(() => import(denied), phase + ":next-public-import-denied");
                Check(denied.Reads == 0 && denied.CanRead, phase + ":denied-input-untouched");
            }
        }

        foreach (var seekable in new[] { true, false })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineInput = new MemoryStream(fixtures["mixed"]);
            var baseline = await entries["static"](baselineInput);
            var phase = "asynchronous-read:" + seekable;
            using var input = new SwitchingStream(fixtures["mixed"], seekable, () => Replace(phase), yieldRead: true);
            var result = await entries["static"](input);
            Check(Fingerprint(result) == Fingerprint(baseline), phase + ":identical-dto-after-thread-transition");
            Check(input.Reads > 0 && input.CanRead, phase + ":input-remains-open");
            using var denied = new SwitchingStream(plain, seekable, () => throw new Exception("Denied async import read input."), yieldRead: true);
            await Denied(() => entries["static"](denied), phase + ":next-public-import-denied");
            Check(denied.Reads == 0, phase + ":denied-input-untouched");
        }

        var command = new PdfContentCommand(new("Tj", PdfOperatorCategory.TextShowing, 1),
            [new PdfString("Analysis content"u8.ToArray(), false)], default, 0);
        var text = new PdfTextElement(0, PdfMatrix.Identity, command, "Analysis content") { FontSize = 12 };
        var snapshot = new PrimitiveGraphicsStateSnapshot(PVAD.Importer.Graphics.PdfColor.Black,
            PVAD.Importer.Graphics.PdfColor.Black, 0, null);
        var primitive = new PrimitiveText(text, new(0, 0, 100, 12), new(new(0, 0, 100, 12), 0, new(1, 0)), snapshot);
        var line = new ReadingLine(0, [primitive], primitive.Bounds);

        Analysis("primitive-group-recursion", change =>
        {
            var group = Group(change);
            var result = new PrimitiveBuilder().Build([group]);
            return result.Count == 1 && result[0].Children.OfType<PrimitiveText>().Any(t => t.Text == text.Text);
        });
        Analysis("bounds-group-recursion", change => new BoundingBoxCalculator().ComputeGroupBounds(Group(change)).Width >= 100);
        Analysis("reading-order", change => new ReadingOrderEngine().Analyze(Switch([primitive], change)).Lines.Single().Text == text.Text);
        Analysis("classifier-and-barcode-dispatch", change =>
        {
            new ObjectClassifier().Classify(Switch<PrimitiveObject>([primitive], change));
            return primitive.Classification == PrimitiveClassification.Text;
        });
        Analysis("grouping", change => new GroupingEngine().BuildGroups(Switch<PrimitiveObject>([primitive], change)).Count == 0);
        Analysis("paragraph-reconstruction", change => new TextReconstructionEngine().BuildParagraphText(
            new(0, Switch([line], change), line.Bounds)) == text.Text);
        Analysis("semantic-layout-reconstruction", change =>
        {
            var paragraph = new ReadingParagraph(0, Switch([line], change), line.Bounds);
            var page = new SemanticLayoutEngine().BuildPage(0, [primitive], new() { Paragraphs = [paragraph] }, [], new(0, 0, 300, 180));
            return page.Children.Any(child => child.Text == text.Text);
        });
        Analysis("scene-page", change => new SceneGraphEngine().BuildPage(0, Page(change)).Layout!.Children.Any(child => child.Text == text.Text));
        Analysis("scene-document", change =>
        {
            var document = new PdfDocumentModel();
            document.Pages.Add(Page(change));
            document.Pages.Add(Page(() => { }));
            return new SceneGraphEngine().Build(document).Pages.Count == 2;
        });

        foreach (var fail in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var cancel = new CancellationTokenSource();
            using var input = new SwitchingStream(plain, true, () =>
            {
                Replace("failure:" + fail);
                if (fail) throw new IOException("Synthetic Designer import failure.");
                cancel.Cancel();
            });
            try { await instance.ImportAsync(input, "Synthetic", cancel.Token); throw new Exception("Expected import failure."); }
            catch (Exception ex) when (fail ? ex is IOException : ex is OperationCanceledException)
            { Check(input.CanRead, "failure:" + fail + ":original-error-and-open-input"); }
            using var next = new MemoryStream(plain);
            await Denied(() => instance.ImportAsync(next), "failure:" + fail + ":no-reusable-admission");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            using var input = new SwitchingStream(plain, false, () => throw new Exception("Cancelled import read input."));
            try { await instance.ImportAsync(input, "Synthetic", cancelled.Token); throw new Exception("Cancellation ignored."); }
            catch (OperationCanceledException) { Check(input.Reads == 0, "pre-cancelled-does-not-read"); }
        }
        PvadLicense.LoadFromFile(licenses[0]);
        await DeniedAction(() => new PVAD.Pdf.PdfDocument(), "importer-only-never-unlocks-public-pdf");
        return checks.Count - start;

        void Analysis(string name, Func<Action, bool> work)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            Check(work(() => Replace("analysis:" + name)), "analysis:" + name + ":original-admission-completes");
            var enumerated = false;
            try { work(() => enumerated = true); throw new Exception("Retained analysis accepted wrong grant."); }
            catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012")
            { Check(!enumerated, "analysis:" + name + ":new-public-work-denied-before-enumeration"); }
        }
        void Replace(string phase)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { new SceneGraphEngine().Build(new()); throw new Exception("Public analysis inherited admission."); }
            catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, phase + ":reentrant-public-analysis-denied"); }
        }
        void Check(bool valid, string name)
        {
            name = "designer-pdf-continuation:" + name;
            if (!valid || checks.Contains(name)) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Denied(Func<Task> call, string name)
        {
            try { await call(); }
            catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, name); return; }
            throw new Exception("Expected license denial: " + name);
        }
        Task DeniedAction(Action call, string name) => Denied(() => { call(); return Task.CompletedTask; }, name);
        SwitchingList<T> Switch<T>(IReadOnlyList<T> values, Action change) => new(values, change);
        PdfGroupElement Group(Action change)
        {
            var group = new PdfGroupElement(0, PdfMatrix.Identity, command);
            // Clipping retains its list; path constructors copy theirs before public admission.
            group.Children.Add(new PdfTextElement(0, PdfMatrix.Identity, command, text.Text)
            {
                FontSize = 12,
                ClippingPath = new(Switch<PdfPathSegment>([new RectangleSegment(new(0, 0, 100, 20))], change), false),
            });
            group.Children.Add(new PdfPathElement(1, PdfMatrix.Identity, command,
                [new RectangleSegment(new(0, 0, 100, 20))]));
            return group;
        }
        PdfPageModel Page(Action change)
        {
            var page = new PdfPageModel(null, new PdfDictionary()) { MediaBox = new(0, 0, 300, 180) };
            page.GraphicsObjects.Add(Group(change));
            return page;
        }
        byte[] Mixed()
        {
            var document = new PdfDocument();
            foreach (var (width, height) in new[] { (595d, 842d), (842d, 595d) })
            {
                var page = document.AddPage(width, height);
                page.DrawText("Synthetic report header", 30, height - 24);
                page.DrawText("Body content", 50, 350);
                page.DrawRectangle(40, 200, 110, 45);
                page.DrawImage(image, 200, 200, 24, 24);
            }
            return document.ToBytes();
        }
        byte[] Bars(bool labeled)
        {
            var document = new PdfDocument();
            var page = document.AddPage(400, 300);
            var heights = new[] { 40d, 85d, 62d };
            for (var i = 0; i < heights.Length; i++)
            {
                var x = 70 + i * 70;
                page.DrawRectangle(x, 80, 30, heights[i], fill: true, fillColor: PVAD.Pdf.PdfColor.FromRgb(37, 99, 235));
                page.DrawText($"Q{i + 1}", x + 6, 62, 9, PdfFontFamily.Helvetica);
                if (labeled) page.DrawText(((int)heights[i]).ToString(System.Globalization.CultureInfo.InvariantCulture), x + 6, 84 + heights[i], 9, PdfFontFamily.Helvetica);
            }
            page.DrawLine(55, 80, 290, 80, 1);
            return document.ToBytes();
        }
        byte[] Metadata(bool invalid)
        {
            var document = new PdfDocument();
            document.Info.CustomProperties[ChartMetadataCodec.PdfInfoKey] = invalid ? "not-base64" : ChartMetadataCodec.Encode([
                new PlannedPage("page-1", [new ElementDto { Id = "native-metadata-chart", Type = "chart", X = 40, Y = 60, Width = 300, Height = 180,
                    Chart = new() { Categories = ["A"], Series = [new() { Id = "native-series", Values = [4] }] } }]),
            ])!;
            var page = document.AddPage(595, 842);
            page.DrawRectangle(40, 602, 300, 180, fill: true, fillColor: PVAD.Pdf.PdfColor.FromRgb(37, 99, 235));
            page.DrawText("Outside chart", 40, 500);
            return document.ToBytes();
        }
    }

    private static void VerifyFixture(DesignExportDto design, string fixture, string entry)
    {
        if (design.Pages.Count != (fixture == "mixed" ? 2 : 1)) throw new Exception("Page count changed.");
        var elements = design.Pages.SelectMany(p => p.Elements).Concat(design.SharedElements ?? []).ToArray();
        if (elements.Length == 0 || elements.Select(e => e.Id).Distinct().Count() != elements.Length) throw new Exception("Empty or duplicated elements.");
        if (fixture == "mixed" && (design.Pages[0].Layout!.Width != 595 || design.Pages[1].Layout!.Width != 842 ||
            !elements.Any(e => e.Type == "image") || !elements.Any(e => e.Content == "Body content") || design.SharedElements!.Count == 0))
            throw new Exception("Mixed layout, image, text or shared header lost.");
        if (fixture == "metadata" && !elements.Any(e => e.Id == "native-metadata-chart" && e.Chart!.Recognition!.Confidence == 1))
            throw new Exception("Metadata chart lost.");
        if (fixture == "invalid-metadata" && !(design.ImportDiagnostics ?? []).Any(d => d.Code == "PVAD-PDF-CHART-001"))
            throw new Exception("Metadata warning lost.");
        if (fixture == "labeled-chart" && !elements.Any(e => e.Chart?.Recognition?.Status == "automatic")) throw new Exception("Safe chart lost.");
        if (fixture == "review-chart" && elements.Any(e => e.Type == "chart") != (entry == "options-review")) throw new Exception("Recognition policy changed.");
    }

    private static string Fingerprint(DesignExportDto design)
    {
        var node = JsonSerializer.SerializeToNode(design)!.AsObject();
        node["Id"] = null;
        foreach (var page in node["Pages"]!.AsArray())
        foreach (var element in page!["Elements"]!.AsArray())
            if (element!["Id"]!.GetValue<string>().StartsWith("chart-", StringComparison.Ordinal)) element["Id"] = "generated-chart";
        return node.ToJsonString();
    }

    private sealed class SwitchingList<T>(IReadOnlyList<T> values, Action change) : IReadOnlyList<T>
    {
        private bool changed;
        private void Before() { if (!changed) { changed = true; change(); } }
        public int Count { get { Before(); return values.Count; } }
        public T this[int index] { get { Before(); return values[index]; } }
        public IEnumerator<T> GetEnumerator() { Before(); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SwitchingStream(byte[] bytes, bool seekable, Action change, bool yieldRead = false) : Stream
    {
        private readonly MemoryStream source = new(bytes, writable: false);
        private bool changed;
        public int Reads { get; private set; }
        private void Before() { Reads++; if (!changed) { changed = true; change(); } }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) { Before(); return source.Read(buffer, offset, count); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (yieldRead) await Task.Yield();
            Before();
            return await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
