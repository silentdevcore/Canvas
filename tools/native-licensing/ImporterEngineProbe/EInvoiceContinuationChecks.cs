using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Color;
using PVAD.Importer.EInvoicing;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class EInvoiceContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var document = new PdfDocument { FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) };
        document.AddPage(300, 180).DrawText("Synthetic invoice continuation", 30, 100, new() { Language = "en" });
        document.AddPage(180, 300).DrawText("Retained second page", 30, 100, new() { Language = "en" });
        var plain = document.ToBytes();
        var archive2 = document.ToBytes(new() { ComplianceProfile = PdfComplianceProfile.PdfA2B });
        var encrypted = document.ToBytes(new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } });
        document.AddAssociatedFile("retained supplement"u8.ToArray(), "notes.txt", "text/plain", PdfAssociatedFileRelationship.Supplement);
        var archive3 = document.ToBytes(new() { ComplianceProfile = PdfComplianceProfile.PdfA3B });
        PvadLicense.LoadFromFile(licenses[0]);
        var inspector = new PdfTrustInspector();
        var converter = new PdfA2bConverter(inspector);
        var clock = new AttachmentClock();
        var attacher = new PdfEInvoiceAttacher(converter, clock);
        var extractor = new PdfAssociatedFileExtractor();
        var xml = "<invoice id=\"synthetic\"><total>42.00</total></invoice>"u8.ToArray();
        var payload = new PdfEInvoiceAttachmentPayload { Xml = xml, FileName = "factur-x.xml", Description = "Synthetic licensing fixture, not a validated tax invoice" };
        var colored = WithCmykContent(archive2);

        foreach (var (name, bytes) in new[] { ("pdfa3", archive3), ("plain", plain), ("pdfa2-cmyk", colored) })
        {
            var options = new PdfEInvoiceAttachmentOptions { AllowArchiveConversion = name != "pdfa3" };
            using var preflightInput = new MemoryStream(bytes);
            var expectedPreflight = await attacher.PreflightAsync(preflightInput, payload, options);
            using var baselineInput = new MemoryStream(bytes);
            var baseline = await attacher.AttachAsync(baselineInput, payload, options);
            Check(baseline.PageCount == 2 && baseline.Bytes.Length > 0, name + ":baseline");
            Check(Encoding.ASCII.GetString(baseline.Bytes).Contains("/ModDate <443A32303236303130323033303430355A>", StringComparison.Ordinal), name + ":fixed-attachment-time");
            foreach (var asynchronous in new[] { false, true })
            {
                var phase = name + (asynchronous ? ":async" : ":sync");
                PvadLicense.LoadFromFile(licenses[0]);
                using var preflightSource = new SwitchingStream(bytes, () => Replace(phase + ":preflight"), asynchronous);
                var report = await attacher.PreflightAsync(preflightSource, payload, options);
                Check(JsonSerializer.Serialize(report) == JsonSerializer.Serialize(expectedPreflight), phase + ":identical-preflight");
                await Denied(() => attacher.PreflightAsync(preflightSource, payload), phase + ":new-preflight-denied");
                PvadLicense.LoadFromFile(licenses[0]);
                var sourceHash = SHA256.HashData(bytes);
                using var source = new SwitchingStream(bytes, () => Replace(phase + ":attach"), asynchronous);
                var result = await attacher.AttachAsync(source, payload, options);
                Check(source.Changes == 1 && source.CanRead && preflightSource.CanRead && sourceHash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-and-ownership");
                Check(result.Bytes.SequenceEqual(baseline.Bytes) && result.AttachmentSha256 == baseline.AttachmentSha256 && result.Strategy == baseline.Strategy && result.PagesUnchanged == baseline.PagesUnchanged, phase + ":identical-output-and-strategy");
                await Denied(() => extractor.ExtractAsync(new MemoryStream(result.Bytes)), phase + ":new-extraction-denied");
                var clockReads = clock.Reads;
                await Denied(() => attacher.AttachAsync(source, payload), phase + ":new-attach-denied");
                Check(clock.Reads == clockReads, phase + ":denied-before-clock");
                PvadLicense.LoadFromFile(licenses[0]);
                using var output = new MemoryStream(result.Bytes);
                var files = await extractor.ExtractAsync(output);
                var invoice = files.Single(file => file.FileName == payload.FileName);
                Check(invoice.Content.Span.SequenceEqual(xml) && invoice.SizeMatches && invoice.ChecksumMatches, phase + ":xml-integrity");
                Check(files.Count == (name == "pdfa3" ? 2 : 1) && (name != "pdfa3" || files.Any(file => file.FileName == "notes.txt" && file.Content.Span.SequenceEqual("retained supplement"u8))), phase + ":unrelated-attachments");
                using var trustInput = new MemoryStream(result.Bytes);
                Check((await inspector.InspectAsync(trustInput)).PdfA.Status == PdfTrustStatus.Valid, phase + ":archival-structure");
                using var pageInput = new MemoryStream(result.Bytes);
                var pages = (await new PdfImporter().LoadAsync(pageInput)).Pages;
                Check(pages.Count == 2 && pages[0].MediaBox?.Width == 300 && pages[1].MediaBox?.Width == 180, phase + ":mixed-geometry");
                if (name == "pdfa3") Check(result.Bytes.AsSpan(0, bytes.Length).SequenceEqual(bytes) && result.PagesUnchanged, phase + ":incremental-prefix");
                PvadLicense.LoadFromFile(licenses[0]);
                using var extraction = new SwitchingStream(result.Bytes, () => Replace(phase + ":extract"), asynchronous);
                var extracted = await extractor.ExtractAsync(extraction);
                Check(extracted.Count == files.Count && extracted.Zip(files).All(pair => pair.First.Content.Span.SequenceEqual(pair.Second.Content.Span) && pair.First.FileName == pair.Second.FileName && pair.First.SizeMatches && pair.First.ChecksumMatches), phase + ":admitted-extraction");
                await Denied(() => extractor.ExtractAsync(extraction), phase + ":subsequent-extraction-denied");
            }
            PvadLicense.LoadFromFile(licenses[0]);
        }

        foreach (var profile in new[] { PdfArchiveProfile.PdfA2b, PdfArchiveProfile.PdfA3b })
        foreach (var asynchronous in new[] { false, true })
        {
            var phase = "archive:" + profile + ":" + asynchronous;
            PvadLicense.LoadFromFile(licenses[0]);
            var options = new PdfA2bConversionOptions { Profile = profile };
            using var expectedInput = new MemoryStream(colored);
            var expected = await converter.ConvertAsync(expectedInput, options);
            using var source = new SwitchingStream(colored, () => Replace(phase), asynchronous);
            var result = await converter.ConvertAsync(source, options);
            Check(result.Bytes.SequenceEqual(expected.Bytes) && result.Validation.Status == PdfTrustStatus.Valid && JsonSerializer.Serialize(result.Preflight) == JsonSerializer.Serialize(expected.Preflight), phase + ":identical-conversion");
            await Denied(() => converter.ConvertAsync(source, options), phase + ":new-conversion-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            using var inspect = new SwitchingStream(colored, () => Replace(phase + ":preflight"), asynchronous);
            var preflight = await converter.PreflightAsync(inspect, options);
            Check(JsonSerializer.Serialize(preflight) == JsonSerializer.Serialize(expected.Preflight), phase + ":identical-preflight");
        }

        PvadLicense.LoadFromFile(licenses[0]);
        using var original = new MemoryStream(archive3);
        var attached = await attacher.AttachAsync(original, payload);
        using var crypto = new TrustCryptoFixtures();
        using var key = new PdfPkcs12SigningKey(crypto.Pfx);
        var signed = new PdfPadesSigningService().Sign(archive3, key, new() { SigningTime = crypto.Now }).Bytes;
        foreach (var failure in new[] { "unapproved-conversion", "unapproved-signature", "conflict", "encrypted", "source-limit", "output-limit", "extract-limit", "io", "cancel", "callback-license" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var bytes = failure switch { "unapproved-conversion" => plain, "unapproved-signature" => signed, "conflict" or "extract-limit" => attached.Bytes, "encrypted" => encrypted, _ => archive3 };
            using var cancellation = new CancellationTokenSource();
            using var source = new SwitchingStream(bytes, () =>
            {
                Replace(failure);
                if (failure == "io") throw new IOException("Synthetic input failure.");
                if (failure == "cancel") cancellation.Cancel();
                if (failure == "callback-license") PvadLicense.Require(PvadLicenseProducts.Importer);
            }, true);
            try
            {
                if (failure == "extract-limit") await extractor.ExtractAsync(source, new() { MaximumFileSizeBytes = 1 }, cancellation.Token);
                else await attacher.AttachAsync(source, payload, new()
                {
                    MaximumPdfSizeBytes = failure == "source-limit" ? 1 : PdfEInvoiceAttachmentOptions.DefaultMaximumPdfSizeBytes,
                    MaximumOutputSizeBytes = failure == "output-limit" ? 1 : PdfEInvoiceAttachmentOptions.DefaultMaximumOutputSizeBytes,
                }, cancellation.Token);
                throw new Exception("Expected preserved attachment failure: " + failure);
            }
            catch (Exception e) when (failure switch
            {
                "source-limit" or "extract-limit" => e is InvalidDataException,
                "io" => e is IOException,
                "cancel" => e is OperationCanceledException,
                "callback-license" => e is PvadLicenseException { Code: "PVADLIC012" },
                _ => e is PdfEInvoiceAttachmentBlockedException,
            }) { Check(source.Changes == 1 && source.CanRead, failure + ":original-failure-and-ownership"); }
            await Denied(() => attacher.AttachAsync(source, payload), failure + ":failed-operation-closed");
        }

        foreach (var custom in new[] { "converter", "inspector" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var customConverter = new DelegatingConverter(converter, () => Replace("custom-converter"));
            var customInspector = new DelegatingInspector(inspector, () => Replace("custom-inspector"));
            var service = new PdfEInvoiceAttacher(custom == "converter" ? customConverter : new PdfA2bConverter(customInspector));
            using var source = new MemoryStream(colored);
            await Denied(() => service.AttachAsync(source, payload, new() { AllowArchiveConversion = true }), custom + ":fresh-callback");
            Check((custom == "converter" ? customConverter.Calls : customInspector.Calls) == 1, custom + ":not-bypassed");
        }

        foreach (var (name, value) in Functions())
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var function = PdfFunction.TryCreate(value, o => o)!;
            var expected = function.Evaluate([0.5]);
            var result = function.Evaluate(new SwitchingList([0.5], () => Replace("function:" + name)));
            Check(result.SequenceEqual(expected), name + ":built-in-evaluation");
            await Denied(() => Task.FromResult(function.Evaluate([0.5])), name + ":new-evaluation-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            var space = new PdfResolvedColorSpace(PdfColorSpaceFamily.Separation, 1, new(PdfColorSpaceFamily.DeviceGray, 1), function);
            var rgb = PdfSrgbColorConverter.ToSrgb(space, new SwitchingList([0.5], () => Replace("tint:" + name)));
            Check(rgb is { Length: 3 } && rgb.All(channel => channel == expected[0]), name + ":admitted-tint-and-alternate");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        var customFunction = new CustomFunction();
        var customSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.Separation, 1, new(PdfColorSpaceFamily.DeviceGray, 1), customFunction);
        await Denied(() => Task.FromResult(PdfSrgbColorConverter.ToSrgb(customSpace, new SwitchingList([0.5], () => Replace("custom-function")))), "custom-function:fresh-native-evaluation");
        Check(customFunction.Calls == 0, "custom-function:denied-before-custom-body");

        PvadLicense.LoadFromFile(licenses[0]);
        var profileFactory = typeof(PdfDocument).Assembly.GetType("PVAD.Pdf.PdfComplianceResources", true)!;
        var icc = (byte[])profileFactory.GetMethod("CreateSrgbIccProfile", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        var iccSpace = PdfSrgbColorConverter.Resolve(new PdfArray([new PdfName("ICCBased"), new PdfStreamObject(new() { ["N"] = new PdfInteger(3) }, icc)]), o => o);
        var baselineColor = PdfSrgbColorConverter.ToSrgb(iccSpace, [1, 0, 0]);
        var transformed = PdfSrgbColorConverter.ToSrgb(iccSpace, new SwitchingList([1, 0, 0], () => Replace("icc")));
        Check(baselineColor is null ? transformed is null : transformed!.SequenceEqual(baselineColor), "icc:identical-admitted-transform");
        if (Environment.GetEnvironmentVariable("PVAD_REQUIRE_LCMS2") == "1") Check(transformed is { Length: 3 } && transformed[0] > 0.9 && transformed[1] < 0.1, "icc:required-native-transform");

        foreach (var callbackThrows in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var changed = false;
            var rewriter = new PdfContentColorRewriter(o =>
            {
                if (!changed) { changed = true; Replace("rewrite:" + callbackThrows); if (callbackThrows) PvadLicense.Require(PvadLicenseProducts.Importer); }
                return o;
            }, new Dictionary<string, PdfObject> { ["Ink"] = new PdfName("DeviceCMYK") });
            try
            {
                var result = rewriter.Rewrite("/Ink cs 0 1 1 0 sc BT (KEEP) Tj ET"u8.ToArray());
                var content = Encoding.ASCII.GetString(result.Content);
                Check(!callbackThrows && changed && result.ConvertedOperatorCount > 0 && content.Contains("1 0 0 rg", StringComparison.Ordinal) && content.Contains("BT (KEEP) Tj ET", StringComparison.Ordinal), "rewrite:color-and-text-preserved");
            }
            catch (PvadLicenseException e) when (callbackThrows && e.Code == "PVADLIC012") { Check(changed, "rewrite:callback-license-not-swallowed"); }
        }
        PvadLicense.LoadFromFile(licenses[0]);
        return checks.Count - start;

        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("EInvoice continuation failed: " + name); checks.Add("einvoice-continuation:" + name); }
        async Task Denied(Func<Task> action, string name)
        {
            try { await action(); } catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected fresh Importer license denial: " + name);
        }
        void Replace(string name)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { PvadLicense.Require(PvadLicenseProducts.Importer); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name + ":current-license-changed"); return; }
            throw new InvalidOperationException("Replacement did not revoke Importer rights.");
        }
    }

    private sealed class AttachmentClock : TimeProvider
    {
        internal int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        }
    }

    private static byte[] WithCmykContent(byte[] source)
    {
        var context = new PdfParseContext(source, new());
        var graph = new PdfObjectParser(context, new PdfCrossReferenceParser().Parse(context)).ParseDocumentGraph();
        var contentId = ((PdfReference)((PdfDictionary)graph.Objects.Values.First(o => o.Value is PdfDictionary d && d["Type"] is PdfName { Value: "Page" }).Value)["Contents"]!).Id;
        var content = (PdfStreamObject)graph.Resolve(contentId)!.Value;
        var decoded = new PVAD.Importer.Streams.PdfStreamDecoderRegistry().Decode(content).ToArray();
        return new PdfIncrementalUpdateWriter().Append(source, [new(contentId, new PdfStreamObject(new(), "0 1 1 0 k 10 10 20 20 re f\n"u8.ToArray().Concat(decoded).ToArray()))]).Bytes;
    }

    private static PdfArray Numbers(params double[] values) => new(values.Select(n => (PdfObject)new PdfNumber(n)));
    private static IEnumerable<(string Name, PdfObject Value)> Functions()
    {
        PdfDictionary Exponential() => new() { ["FunctionType"] = new PdfInteger(2), ["Domain"] = Numbers(0, 1), ["C0"] = Numbers(0), ["C1"] = Numbers(1), ["N"] = new PdfInteger(1) };
        yield return ("exponential", Exponential());
        yield return ("array", new PdfArray([Exponential()]));
        yield return ("stitching", new PdfDictionary { ["FunctionType"] = new PdfInteger(3), ["Domain"] = Numbers(0, 1), ["Functions"] = new PdfArray([Exponential(), Exponential()]), ["Bounds"] = Numbers(0.5), ["Encode"] = Numbers(0, 1, 0, 1) });
        yield return ("sampled", new PdfStreamObject(new() { ["FunctionType"] = new PdfInteger(0), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1), ["Size"] = Numbers(2), ["BitsPerSample"] = new PdfInteger(8) }, new byte[] { 0, 255 }));
        yield return ("postscript", new PdfStreamObject(new() { ["FunctionType"] = new PdfInteger(4), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1) }, "{ dup mul }"u8.ToArray()));
    }

    private sealed class SwitchingStream(byte[] bytes, Action change, bool asynchronous) : Stream
    {
        private readonly MemoryStream source = new(bytes, false);
        internal int Changes { get; private set; }
        private void Before() { if (Changes == 0) { Changes++; change(); } }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { if (asynchronous) await Task.Yield(); Before(); return await source.ReadAsync(buffer, cancellationToken); }
        public override int Read(byte[] buffer, int offset, int count) { Before(); return source.Read(buffer, offset, count); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }

    private sealed class SwitchingList(IReadOnlyList<double> values, Action change) : IReadOnlyList<double>
    {
        private bool changed;
        private void Before() { if (!changed) { changed = true; change(); } }
        public int Count { get { Before(); return values.Count; } }
        public double this[int index] { get { Before(); return values[index]; } }
        public IEnumerator<double> GetEnumerator() { Before(); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CustomFunction() : PdfFunction([0, 1], [0, 1])
    {
        internal int Calls { get; private set; }
        public override int OutputCount => 1;
        protected override double[] EvaluateCore(double[] inputs) { Calls++; return inputs; }
    }

    private sealed class DelegatingConverter(IPdfA2bConverter inner, Action change) : IPdfA2bConverter
    {
        internal int Calls { get; private set; }
        public Task<PdfA2bConversionPreflightReport> PreflightAsync(Stream source, PdfA2bConversionOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.PreflightAsync(source, options, cancellationToken); }
        public Task<PdfA2bConversionResult> ConvertAsync(Stream source, PdfA2bConversionOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.ConvertAsync(source, options, cancellationToken); }
    }

    private sealed class DelegatingInspector(IPdfTrustInspector inner, Action change) : IPdfTrustInspector
    {
        internal int Calls { get; private set; }
        public Task<PdfTrustReport> InspectAsync(Stream source, PdfTrustInspectionOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.InspectAsync(source, options, cancellationToken); }
    }
}
