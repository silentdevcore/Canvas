using System.Buffers;
using System.Collections;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Color;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Redaction;
using PVAD.Importer.Saving;
using PVAD.Importer.Signing;
using PVAD.Importer.Streams;
using PVAD.Importer.Xref;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class RedactionContinuationChecks
{
    private const string Removed = "SECRET-REMOVE";
    private const string Retained = "PUBLIC-KEEP";

    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var doc = new PdfDocument();
        var page = doc.AddPage(300, 200);
        page.DrawText(Removed, 30, 100); page.DrawText(Retained, 30, 160);
        doc.AddPage(200, 300).DrawText("KEEP-PAGE-TWO", 30, 200);
        var plain = doc.ToBytes();
        var encrypted = doc.ToBytes(new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } });
        var empty = new PdfDocument(); empty.AddPage(300, 300); var imageBase = empty.ToBytes();
        PvadLicense.LoadFromFile(licenses[0]);
        using var crypto = new TrustCryptoFixtures();
        using var key = new PdfPkcs12SigningKey(crypto.Pfx);
        using var client = new HttpClient(crypto, disposeHandler: false);
        var timestamp = new PdfRfc3161TimestampAuthority(client, new()
        {
            Endpoint = new Uri("https://tsa.example.test/rfc3161"), RequireTrustedCertificateChain = false,
        });
        var preflight = new PdfRedactionPreflight();
        var applier = new PdfRedactionApplier(preflight);
        PdfRedactionRegion[] textRegion = [new(1, new(20, 70, 260, 50))];
        PdfRedactionRegion[] imageRegion = [new(1, new(50, 50, 100, 200))];
        var cases = new List<(string Name, byte[] Bytes, PdfRedactionRegion[] Regions)>
        {
            ("mixed-page-text", plain, textRegion), ("metadata-and-attachment", WithCarriers(plain), textRegion),
        };
        foreach (var kind in new[] { "rgb", "gray", "cmyk", "indexed", "inline", "mask" })
            cases.Add((kind, Image(imageBase, kind), imageRegion));

        PdfRedactionAuditRecord? record = null;
        foreach (var (name, bytes, regions) in cases)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var before = new MemoryStream(bytes);
            var baselinePreflight = await preflight.PreflightAsync(before, regions);
            using var source = new MemoryStream(bytes);
            var baseline = await applier.ApplyAsync(source, regions);
            Check(baseline.Verification.Passed && baseline.RemovedElementCount > 0, name + ":baseline-removes-content");
            foreach (var asynchronous in new[] { false, true })
            {
                var phase = name + (asynchronous ? ":async" : ":sync");
                PvadLicense.LoadFromFile(licenses[0]);
                var hash = SHA256.HashData(bytes);
                using var inspect = new SwitchingStream(bytes, () => Replace(phase + ":preflight"), asynchronous);
                var report = await preflight.PreflightAsync(inspect, regions);
                Check(inspect.Changes == 1 && JsonSerializer.Serialize(report) == JsonSerializer.Serialize(baselinePreflight), phase + ":identical-preflight");
                await Denied(() => preflight.PreflightAsync(inspect, regions), phase + ":next-preflight-denied");
                PvadLicense.LoadFromFile(licenses[0]);
                using var input = new SwitchingStream(bytes, () => Replace(phase + ":apply"), asynchronous);
                var result = await applier.ApplyAsync(input, regions);
                Check(input.Changes == 1 && input.CanRead && inspect.CanRead, phase + ":read-callback-and-ownership");
                Check(hash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-unchanged");
                Check(result.Bytes.SequenceEqual(baseline.Bytes) && result.RemovedElementCount == baseline.RemovedElementCount &&
                    result.RemovedAnnotationCount == baseline.RemovedAnnotationCount && result.RewrittenPages.SequenceEqual(baseline.RewrittenPages), phase + ":identical-document-and-removal-counts");
                Check(JsonSerializer.Serialize(result.Verification) == JsonSerializer.Serialize(baseline.Verification) &&
                    JsonSerializer.Serialize(result.Preflight) == JsonSerializer.Serialize(baseline.Preflight), phase + ":identical-verification-and-diagnostics");
                await Denied(() => applier.ApplyAsync(input, regions), phase + ":next-apply-denied");
                PvadLicense.LoadFromFile(licenses[0]);
                Check(PdfRedactionAuditVerifier.Verify(result.Audit), phase + ":audit-hash-valid");
                using var output = new MemoryStream(result.Bytes);
                var loaded = await new PdfImporter().LoadAsync(output);
                var (table, graph) = Parse(result.Bytes);
                Check(table.Revisions.Count == 1, phase + ":no-historical-revisions");
                if (name is "mixed-page-text" or "metadata-and-attachment")
                {
                    var text = string.Join("\n", loaded.Pages.SelectMany(p => p.TextObjects).Select(t => t.Text));
                    Check(!text.Contains(Removed, StringComparison.Ordinal) && text.Contains(Retained, StringComparison.Ordinal) && text.Contains("KEEP-PAGE-TWO", StringComparison.Ordinal), phase + ":extraction-and-unaffected-page");
                    Check(loaded.Pages.Count == 2 && loaded.Pages[0].MediaBox?.Width == 300 && loaded.Pages[1].MediaBox?.Width == 200, phase + ":mixed-page-geometry");
                }
                else
                {
                    var images = graph.Objects.Values.Where(o => o.Value is PdfStreamObject s && s.Dictionary["Subtype"] is PdfName { Value: "Image" }).ToArray();
                    Check(images.Length > 0 || name == "inline", phase + ":retained-image-representation");
                    foreach (var image in images.Select(o => (PdfStreamObject)o.Value))
                    {
                        var samples = new PdfStreamDecoderRegistry().Decode(image);
                        Check(samples.Span.Contains((byte)0), phase + ":covered-samples-zero:" + Array.IndexOf(images, images.First(o => ReferenceEquals(o.Value, image))));
                    }
                }
                if (name == "metadata-and-attachment")
                {
                    var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
                    var names = catalog["Names"] is PdfReference reference ? graph.Resolve(reference.Id)?.Value : catalog["Names"];
                    Check(catalog["Metadata"] is null && (names is null || names is PdfDictionary d && d["EmbeddedFiles"] is null) &&
                        !Encoding.Latin1.GetString(result.Bytes).Contains("PRIVATE-CARRIER", StringComparison.Ordinal) &&
                        !graph.Objects.Values.Any(o => o.Value is PdfStreamObject s && s.Dictionary["Type"] is PdfName { Value: "Metadata" or "EmbeddedFile" }),
                        phase + ":metadata-attachment-and-residue-removed");
                }
                record = result.Audit;
            }
        }

        foreach (var signed in new[] { false, true })
        foreach (var trigger in signed ? new[] { "source", "http" } : new[] { "source" })
        {
            var phase = "audit:" + signed + ":" + trigger;
            PvadLicense.LoadFromFile(licenses[0]);
            crypto.BeforeRequest = trigger == "http" ? () => Replace(phase) : null;
            using var input = new SwitchingStream(plain, trigger == "source" ? () => Replace(phase) : () => { }, true);
            PdfRedactionApplyResult result;
            try { result = await applier.ApplyAsync(input, textRegion, new() { AuditSigningKey = key, AuditTimestampAuthority = signed ? timestamp : null }); }
            finally { crypto.BeforeRequest = null; }
            await Denied(() => Task.FromResult(PdfRedactionAuditVerifier.Verify(result.Audit, true, signed)), phase + ":new-verification-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            Check(result.Verification.Passed && PdfRedactionAuditVerifier.Verify(result.Audit, true, signed), phase + ":signature-and-timestamp-verified");
            Check(!PdfRedactionAuditVerifier.Verify(result.Audit with { Reason = "tampered" }, true, signed) &&
                !PdfRedactionAuditVerifier.Verify(result.Audit with { Signature = result.Audit.Signature! with { SignerSubject = "tampered" } }, true, signed), phase + ":tampering-remains-invalid");
            record = result.Audit;
        }
        foreach (var field in new[] { "regions", "sanitizations" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var changed = field == "regions" ? record! with { Regions = new SwitchingList<PdfRedactionRegion>(record!.Regions, () => Replace("verify:" + field)) } :
                record! with { AppliedSanitizations = new SwitchingList<string>(record!.AppliedSanitizations, () => Replace("verify:" + field)) };
            Check(PdfRedactionAuditVerifier.Verify(changed), "verify:" + field + ":admitted-hash-and-cms-processing");
            await Denied(() => Task.FromResult(PdfRedactionAuditVerifier.Verify(record!)), "verify:" + field + ":next-call-denied");
        }

        foreach (var kind in new[] { "preflight", "key", "timestamp" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var customPreflight = new DelegatingPreflight(preflight, () => Replace("custom-preflight"));
            var customKey = new DelegatingKey(key, () => Replace("custom-key"));
            var customTimestamp = new DelegatingTimestamp(timestamp, () => Replace("custom-timestamp"));
            var service = kind == "preflight" ? new PdfRedactionApplier(customPreflight) : applier;
            using var source = new MemoryStream(plain);
            var requests = crypto.Requests;
            await Denied(() => service.ApplyAsync(source, textRegion, new()
            {
                AuditSigningKey = kind == "key" ? customKey : key,
                AuditTimestampAuthority = kind == "timestamp" ? customTimestamp : null,
            }), kind + ":custom-provider-keeps-fresh-native-dispatch");
            Check((kind == "preflight" ? customPreflight.Calls : kind == "key" ? customKey.Calls : customTimestamp.Calls) == 1 && crypto.Requests == requests,
                kind + ":callback-not-bypassed-and-no-http");
        }

        foreach (var failure in new[] { "encrypted", "outside", "limit", "io", "cancel", "callback-license" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var bytes = failure == "encrypted" ? encrypted : plain;
            using var cancellation = new CancellationTokenSource();
            using var input = new SwitchingStream(bytes, () =>
            {
                Replace(failure);
                if (failure == "io") throw new IOException("Synthetic redaction input failure.");
                if (failure == "cancel") cancellation.Cancel();
                if (failure == "callback-license") PvadLicense.Require(PvadLicenseProducts.Importer);
            }, true);
            try
            {
                if (failure == "limit") await preflight.PreflightAsync(input, textRegion, new() { MaximumFileSizeBytes = 1 }, cancellation.Token);
                else await applier.ApplyAsync(input, failure == "outside" ? [new(1, new(1000, 1000, 20, 20))] : textRegion, cancellationToken: cancellation.Token);
                throw new Exception("Expected redaction failure: " + failure);
            }
            catch (Exception e) when (failure switch
            {
                "encrypted" or "outside" => e is PdfRedactionBlockedException,
                "limit" => e is InvalidDataException,
                "io" => e is IOException,
                "cancel" => e is OperationCanceledException,
                _ => e is PvadLicenseException { Code: "PVADLIC012" },
            }) { Check(input.Changes == 1 && input.CanRead, failure + ":original-failure-and-ownership"); }
            await Denied(() => applier.ApplyAsync(input, textRegion), failure + ":failed-admission-not-reusable");
        }

        var colorIndex = 0;
        foreach (var value in ColorFixtures())
        {
            var phase = "color:" + colorIndex++;
            PvadLicense.LoadFromFile(licenses[0]);
            var changed = false;
            var resolved = PdfSrgbColorConverter.Resolve(value, o => { if (!changed) { changed = true; Replace(phase); } return o; });
            Check(changed && resolved.ComponentCount > 0, "color:" + resolved.Family + ":recursive-resolution-survives-replacement");
            await Denied(() => Task.FromResult(PdfSrgbColorConverter.ToSrgb(resolved, [0.5])), "color:" + resolved.Family + ":new-evaluation-not-granted");
        }
        foreach (var (name, value) in FunctionFixtures())
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var changed = false;
            var function = PdfFunction.TryCreate(value, o => { if (!changed) { changed = true; Replace("function:" + name); } return o; });
            Check(changed && function is { OutputCount: > 0 }, "function:" + name + ":factory-completes");
            await Denied(() => Task.FromResult(function!.Evaluate([0.25])), "function:" + name + ":retained-function-stays-fresh");
            PvadLicense.LoadFromFile(licenses[0]);
            Check(function!.Evaluate([0.25]).All(double.IsFinite), "function:" + name + ":renewal-restores-evaluation");
        }
        foreach (var name in new[] { "sampled", "postscript", "icc" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var original = name == "icc" ? new PdfStreamObject(new PdfDictionary { ["N"] = new PdfInteger(3) }, new byte[128]) :
                (PdfStreamObject)FunctionFixtures().Single(f => f.Name == name).Value;
            using var memory = new FailingDecoderMemory(original.EncodedBytes.ToArray(), () =>
            {
                Replace("decoder:" + name);
                PvadLicense.Require(PvadLicenseProducts.Importer);
            });
            var stream = new PdfStreamObject(original.Dictionary, memory.Memory);
            await Denied(() => Task.FromResult(name == "icc"
                ? (object)PdfSrgbColorConverter.Resolve(new PdfArray([new PdfName("ICCBased"), stream]), o => o)
                : PdfFunction.TryCreate(stream, o => o)!), "decoder:" + name + ":native-error-not-swallowed");
            Check(memory.Reads > 0, "decoder:" + name + ":failure-during-decoded-byte-access");
        }
        PvadLicense.LoadFromFile(licenses[1]);
        using var untouched = new SwitchingStream(plain, () => throw new Exception("Denied redaction touched input."), false);
        await Denied(() => applier.ApplyAsync(untouched, textRegion), "denial:before-input-read");
        Check(untouched.Reads == 0 && untouched.CanRead, "denial:input-ownership-preserved");
        PvadLicense.LoadFromFile(licenses[0]);
        await Denied(() => Task.FromResult(new PdfDocument()), "redaction:no-public-pdf-rights");
        return checks.Count - start;

        void Replace(string phase)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { PdfRedactionAuditVerifier.Verify(null!); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, phase + ":reentrant-public-denial"); return; }
            throw new Exception("Redaction callback inherited native admission.");
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Redaction continuation assertion failed: " + name);
            checks.Add("redaction-continuation:" + name);
        }
        async Task Denied(Func<Task> call, string name)
        {
            try { await call(); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected redaction denial: " + name);
        }
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) Parse(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new()); var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private static byte[] Image(byte[] source, string kind)
    {
        var (_, graph) = Parse(source);
        var page = graph.Objects.Values.First(o => o.Value is PdfDictionary d && d["Type"] is PdfName { Value: "Page" });
        var next = graph.Objects.Keys.Max(id => id.Number) + 1;
        var imageId = new PdfObjectId(next++, 0); var contentId = new PdfObjectId(next++, 0); var maskId = new PdfObjectId(next, 0);
        var components = kind == "gray" ? 1 : kind == "cmyk" ? 4 : 3;
        var samples = Enumerable.Range(0, 8 * components).Select(n => (byte)(n + 40)).ToArray();
        PdfObject space = new PdfName(kind == "gray" ? "DeviceGray" : kind == "cmyk" ? "DeviceCMYK" : "DeviceRGB");
        if (kind == "indexed")
        {
            samples = [0, 1, 0, 1, 1, 0, 1, 0];
            space = new PdfArray([new PdfName("Indexed"), new PdfName("DeviceRGB"), new PdfInteger(1), new PdfString(new byte[] { 100, 80, 60, 200, 180, 160 }, false)]);
        }
        var dictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"), ["Subtype"] = new PdfName("Image"), ["Width"] = new PdfInteger(4), ["Height"] = new PdfInteger(2),
            ["BitsPerComponent"] = new PdfInteger(8), ["ColorSpace"] = space, ["Filter"] = new PdfName("FlateDecode"),
        };
        if (kind == "mask") dictionary["SMask"] = new PdfReference(maskId);
        var updated = new PdfDictionary(((PdfDictionary)page.Value).Values.ToDictionary(e => e.Key, e => e.Value));
        var updates = new List<PdfIncrementalObjectUpdate>();
        byte[] content;
        if (kind == "inline")
        {
            using var stream = new MemoryStream(); stream.Write("q 200 0 0 200 50 50 cm BI /W 4 /H 2 /BPC 8 /CS /RGB ID\n"u8);
            stream.Write(samples); stream.Write("\nEI Q"u8); content = stream.ToArray();
        }
        else
        {
            updated["Resources"] = new PdfDictionary { ["XObject"] = new PdfDictionary { ["Im0"] = new PdfReference(imageId) } };
            updates.Add(new(imageId, new PdfStreamObject(dictionary, Compress(samples)))); content = "q 200 0 0 200 50 50 cm /Im0 Do Q"u8.ToArray();
        }
        if (kind == "mask") updates.Add(new(maskId, new PdfStreamObject(new PdfDictionary
        {
            ["Type"] = new PdfName("XObject"), ["Subtype"] = new PdfName("Image"), ["Width"] = new PdfInteger(4), ["Height"] = new PdfInteger(2),
            ["BitsPerComponent"] = new PdfInteger(8), ["ColorSpace"] = new PdfName("DeviceGray"),
        }, new byte[] { 255, 160, 100, 80, 255, 160, 100, 80 })));
        updates.Add(new(contentId, new PdfStreamObject(new PdfDictionary(), content))); updated["Contents"] = new PdfReference(contentId);
        updates.Add(new(page.Id, updated)); return new PdfIncrementalUpdateWriter().Append(source, updates).Bytes;
    }

    private static byte[] WithCarriers(byte[] bytes)
    {
        var (_, graph) = Parse(bytes); var root = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = new PdfDictionary(((PdfDictionary)graph.Resolve(root)!.Value).Values.ToDictionary(e => e.Key, e => e.Value));
        var next = graph.Objects.Keys.Max(id => id.Number) + 1; var meta = new PdfObjectId(next++, 0); var file = new PdfObjectId(next++, 0); var spec = new PdfObjectId(next, 0);
        catalog["Metadata"] = new PdfReference(meta);
        catalog["Names"] = new PdfDictionary { ["EmbeddedFiles"] = new PdfDictionary { ["Names"] = new PdfArray([new PdfString("synthetic.txt"u8.ToArray(), false), new PdfReference(spec)]) } };
        return new PdfIncrementalUpdateWriter().Append(bytes, [
            new(meta, new PdfStreamObject(new PdfDictionary { ["Type"] = new PdfName("Metadata"), ["Subtype"] = new PdfName("XML") }, "<x:xmpmeta>PRIVATE-CARRIER</x:xmpmeta>"u8.ToArray())),
            new(file, new PdfStreamObject(new PdfDictionary { ["Type"] = new PdfName("EmbeddedFile") }, "PRIVATE-CARRIER"u8.ToArray())),
            new(spec, new PdfDictionary { ["Type"] = new PdfName("Filespec"), ["F"] = new PdfString("synthetic.txt"u8.ToArray(), false), ["EF"] = new PdfDictionary { ["F"] = new PdfReference(file) } }),
            new(root, catalog),
        ]).Bytes;
    }

    private static byte[] Compress(byte[] samples)
    { using var output = new MemoryStream(); using (var zip = new ZLibStream(output, CompressionLevel.SmallestSize, true)) zip.Write(samples); return output.ToArray(); }

    private static IEnumerable<PdfObject> ColorFixtures()
    {
        yield return new PdfArray([new PdfName("Indexed"), new PdfName("DeviceRGB"), new PdfInteger(1), new PdfString(new byte[] { 0, 0, 0, 255, 255, 255 }, false)]);
        yield return new PdfArray([new PdfName("ICCBased"), new PdfStreamObject(new PdfDictionary { ["N"] = new PdfInteger(3), ["Alternate"] = new PdfName("DeviceRGB"), ["Filter"] = new PdfName("FlateDecode") }, Compress(new byte[128]))]);
        yield return new PdfArray([new PdfName("Separation"), new PdfName("SyntheticInk"), new PdfName("DeviceGray"), Exponential()]);
        yield return new PdfArray([new PdfName("Pattern"), new PdfName("DeviceRGB")]);
    }

    private static PdfDictionary Exponential() => new()
    { ["FunctionType"] = new PdfInteger(2), ["Domain"] = Numbers(0, 1), ["C0"] = Numbers(0), ["C1"] = Numbers(1), ["N"] = new PdfInteger(1) };
    private static PdfArray Numbers(params double[] numbers) => new(numbers.Select(n => (PdfObject)new PdfNumber(n)));

    private static IEnumerable<(string Name, PdfObject Value)> FunctionFixtures()
    {
        yield return ("array", new PdfArray([Exponential(), Exponential()]));
        yield return ("stitch", new PdfDictionary { ["FunctionType"] = new PdfInteger(3), ["Domain"] = Numbers(0, 1), ["Functions"] = new PdfArray([Exponential(), Exponential()]), ["Bounds"] = Numbers(0.5), ["Encode"] = Numbers(0, 1, 0, 1) });
        yield return ("sampled", new PdfStreamObject(new PdfDictionary { ["FunctionType"] = new PdfInteger(0), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1), ["Size"] = Numbers(2), ["BitsPerSample"] = new PdfInteger(8), ["Filter"] = new PdfName("FlateDecode") }, Compress([0, 255])));
        yield return ("postscript", new PdfStreamObject(new PdfDictionary { ["FunctionType"] = new PdfInteger(4), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1), ["Filter"] = new PdfName("FlateDecode") }, Compress("{ dup mul }"u8.ToArray())));
    }

    private sealed class SwitchingStream(byte[] bytes, Action change, bool asynchronous) : Stream
    {
        private readonly MemoryStream source = new(bytes, false);
        internal int Reads { get; private set; }
        internal int Changes { get; private set; }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        private void Before() { Reads++; if (Changes == 0) { Changes++; change(); } }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { if (asynchronous) await Task.Yield(); Before(); return await source.ReadAsync(buffer, cancellationToken); }
        public override int Read(byte[] buffer, int offset, int count) { Before(); return source.Read(buffer, offset, count); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
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

    private sealed class FailingDecoderMemory(byte[] bytes, Action fail) : MemoryManager<byte>
    {
        internal int Reads { get; private set; }
        public override Memory<byte> Memory => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan() { if (Reads++ == 0) fail(); return bytes; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { if (disposing) Array.Clear(bytes); }
    }

    private sealed class DelegatingPreflight(IPdfRedactionPreflight inner, Action change) : IPdfRedactionPreflight
    {
        internal int Calls { get; private set; }
        public Task<PdfRedactionPreflightReport> PreflightAsync(Stream stream, IReadOnlyList<PdfRedactionRegion> regions, PdfRedactionPreflightOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.PreflightAsync(stream, regions, options, cancellationToken); }
    }

    private sealed class DelegatingKey(PdfPkcs12SigningKey inner, Action change) : IPdfSigningKey
    {
        internal int Calls { get; private set; }
        public X509Certificate2 Certificate => inner.Certificate;
        public byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName algorithm)
        { Calls++; change(); return inner.SignHash(hash, algorithm); }
    }

    private sealed class DelegatingTimestamp(IPdfTimestampAuthority inner, Action change) : IPdfTimestampAuthority
    {
        internal int Calls { get; private set; }
        public Task<PdfTimestampToken> GetTimestampTokenAsync(ReadOnlyMemory<byte> digest, HashAlgorithmName algorithm, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.GetTimestampTokenAsync(digest, algorithm, cancellationToken); }
    }
}
