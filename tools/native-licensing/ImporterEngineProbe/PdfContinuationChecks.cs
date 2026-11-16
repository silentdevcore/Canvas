using System.Collections;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PVAD.Importer;
using PVAD.Importer.Document;
using PVAD.Importer.Content;
using PVAD.Importer.Fonts;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Saving;
using PVAD.Importer.Streams;
using PVAD.Importer.Tokenizer;
using PVAD.Importer.Trust;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class PdfContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks, byte[] plain)
    {
        var start = checks.Count;
        using var crypto = new TrustCryptoFixtures();
        var importer = new PdfImporter();
        var rewrite = new PdfFullRewriteWriter();
        var append = new PdfIncrementalUpdateWriter();
        var saver = new PdfDocumentSaver();
        var cases = new List<(string Name, byte[] Bytes, Func<Stream, Task<PdfDocumentModel>> Read)>();
        cases.Add(("plain", plain, stream => importer.LoadAsync(stream)));

        PvadLicense.LoadFromFile(licenses[1]);
        var fixture = new PdfDocument();
        fixture.AddPage(300, 180).DrawText("Imported content", 30, 140);
        foreach (var algorithm in new[] { PdfEncryptionAlgorithm.Aes256, PdfEncryptionAlgorithm.Aes128, PdfEncryptionAlgorithm.Rc4_128 })
        {
            var encrypted = fixture.ToBytes(new PdfSaveOptions { Encryption = new()
            {
                Algorithm = algorithm, OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader",
            } });
            cases.Add((algorithm.ToString(), encrypted, stream => importer.LoadAsync(stream, "synthetic-owner")));
        }
        var certificateBytes = fixture.ToBytes(new PdfSaveOptions { CertificateEncryption = new() { Recipients = [crypto.Certificate] } });
        cases.Add(("certificate", certificateBytes, stream => importer.LoadAsync(stream, crypto.Certificate)));
        PvadLicense.LoadFromFile(licenses[0]);
        using var originalStream = new MemoryStream(plain);
        var original = await importer.LoadAsync(originalStream);
        var content = (PdfStreamObject)original.ObjectGraph.Resolve(new PdfObjectId(5, 0))!.Value;
        var compressed = Zip(content.EncodedBytes.Span);
        var filtered = rewrite.RewriteWithUpdates(plain, [new(new(5, 0), new PdfStreamObject(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Length"] = new PdfInteger(compressed.Length), ["Filter"] = new PdfName("FlateDecode"),
        }), compressed))]).Bytes;
        cases.Add(("flate", filtered, stream => importer.LoadAsync(stream)));
        cases.Add(("facade", plain, stream => PVAD.Importer.Pdf.LoadAsync(stream)));

        foreach (var (name, bytes, read) in cases)
        {
            var hash = SHA256.HashData(bytes);
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineInput = new MemoryStream(bytes);
            var baseline = Fingerprint(await read(baselineInput));
            foreach (var seekable in new[] { true, false })
            {
                var phase = "load:" + name + ":" + seekable;
                PvadLicense.LoadFromFile(licenses[0]);
                using var input = new SwitchingStream(bytes, seekable, () => ReplaceAndCheckNewCall(phase));
                var completed = await read(input);
                Check(Fingerprint(completed) == baseline && completed.Pages.Count == 1, phase + ":original-admission-completes");
                Check(completed.Pages.SelectMany(p => p.GraphicsObjects).OfType<PdfTextElement>().Any(t => t.Text.Contains("Imported content")), phase + ":text-preserved");
                using var deniedInput = new MemoryStream(bytes);
                await Denied(() => importer.LoadAsync(deniedInput), phase + ":next-public-load-denied");
                Check(deniedInput.Position == 0, phase + ":next-denial-before-read");
                Check(hash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-unchanged");
            }
        }

        PvadLicense.LoadFromFile(licenses[0]);
        var catalog = (PdfDictionary)original.ObjectGraph.Resolve(new PdfObjectId(1, 0))!.Value;
        var changedCatalog = new PdfDictionary(new Dictionary<string, PdfObject>(catalog.Values, StringComparer.Ordinal)
        {
            ["NativeContinuationMarker"] = new PdfString("kept"u8.ToArray(), IsHex: false),
        });
        var update = new PdfIncrementalObjectUpdate(new(1, 0), changedCatalog);
        var modes = new (string Name, Func<IReadOnlyList<PdfIncrementalObjectUpdate>, byte[]> Save)[]
        {
            ("rewrite", list => rewrite.RewriteWithUpdates(plain, list).Bytes),
            ("append", list => append.Append(plain, list).Bytes),
            ("save-rewrite", list => saver.Save(plain, new() { Strategy = PdfSaveStrategy.FullRewrite, Updates = list }).Bytes),
            ("save-append", list => saver.Save(plain, new() { Strategy = PdfSaveStrategy.IncrementalUpdate, Updates = list }).Bytes),
        };
        foreach (var (name, save) in modes)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var baseline = save([update]);
            var completed = save(new SwitchingList([update], () => ReplaceAndCheckNewCall("save:" + name)));
            Check(completed.SequenceEqual(baseline), "save:" + name + ":identical-output-and-validation");
            await Denied(() => Task.FromResult(save([update])), "save:" + name + ":next-public-save-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            using var input = new MemoryStream(completed);
            var roundtrip = await importer.LoadAsync(input);
            Check(roundtrip.Catalog.Dictionary["NativeContinuationMarker"] is PdfString marker && marker.ToLatin1String() == "kept", "save:" + name + ":updated-catalog-roundtrip");
        }

        PvadLicense.LoadFromFile(licenses[0]);
        var encryptedSource = rewrite.Rewrite(plain, new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } }).Bytes;
        var encryptedResult = rewrite.RewriteWithUpdates(encryptedSource,
            new SwitchingList([update], () => ReplaceAndCheckNewCall("save:encrypted")),
            new() { SourcePassword = "synthetic-owner", Encryption = new() { OwnerPassword = "synthetic-new-owner", UserPassword = "synthetic-new-reader" } });
        Check(encryptedResult.Bytes.AsSpan().StartsWith("%PDF-"u8), "save:encrypted:completed-under-original-admission");
        PvadLicense.LoadFromFile(licenses[0]);
        using (var input = new MemoryStream(encryptedResult.Bytes))
        {
            var result = await importer.LoadAsync(input, "synthetic-new-owner");
            Check(result.Catalog.Dictionary["NativeContinuationMarker"] is PdfString, "save:encrypted:reencryption-and-update-roundtrip");
        }

        // A custom first filter changes the license; the following built-in filter is an internal continuation.
        var decoder = new PdfStreamDecoderRegistry();
        decoder.Add(new SwitchingDecoder(() => ReplaceAndCheckNewCall("decoder:custom-filter")));
        var clear = "bounded continuation"u8.ToArray();
        var streamObject = new PdfStreamObject(new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Filter"] = new PdfArray([new PdfName("SyntheticSwitch"), new PdfName("FlateDecode")]),
        }), Zip(clear));
        var decoded = decoder.Decode(streamObject);
        Check(decoded.Span.SequenceEqual(clear), "decoder:builtin-after-custom-filter-completes");
        await Denied(() => Task.FromResult(decoder.Decode(streamObject)), "decoder:retained-registry-needs-new-admission");

        PvadLicense.LoadFromFile(licenses[0]);
        var encoding = new SwitchingEncoding(() => ReplaceAndCheckNewCall("font:custom-encoding"));
        var customFont = new PdfFontResource { ResourceName = "F1", Kind = PdfFontKind.Type1, Encoding = encoding };
        var commands = new PdfContentStreamParser().Parse("BT /F1 12 Tf (source) Tj ET"u8.ToArray());
        var graphics = new PdfGraphicsInterpreter().Interpret(commands, new Dictionary<string, PdfFontResource> { ["F1"] = customFont });
        Check(encoding.Calls > 0 && graphics.OfType<PdfTextElement>().Any(t => t.Text == "Custom decoding"), "font:custom-override-preserved-through-renewal");
        var encodingCalls = encoding.Calls;
        await Denied(() => Task.FromResult(customFont.Decode("source"u8)), "font:retained-font-is-new-public-call");
        Check(encoding.Calls == encodingCalls, "font:denied-call-does-not-invoke-override");

        PvadLicense.LoadFromFile(licenses[0]);
        var parser = new SwitchingFontParser(() => ReplaceAndCheckNewCall("font:custom-parser"));
        var builder = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter(), fontParser: parser);
        var built = builder.Build(original.ObjectGraph);
        Check(parser.Calls == 1 && built.Pages.SelectMany(p => p.GraphicsObjects).OfType<PdfTextElement>().Any(t => t.Text.Contains("Imported content")), "font:custom-parser-retained-through-renewal");
        await Denied(() => Task.FromResult(builder.Build(original.ObjectGraph)), "font:next-public-builder-needs-new-admission");
        Check(parser.Calls == 1, "font:denied-builder-does-not-invoke-parser");

        PvadLicense.LoadFromFile(licenses[0]);
        try
        {
            rewrite.RewriteWithUpdates(plain, new SwitchingList([new(new(0, 0), PdfNull.Value)],
                () => ReplaceAndCheckNewCall("save:invalid-update")));
            throw new InvalidOperationException("Invalid object identifier accepted.");
        }
        catch (ArgumentOutOfRangeException) { Check(true, "save:invalid-update:validation-not-weakened"); }
        await Denied(() => Task.FromResult(rewrite.Rewrite(plain)), "save:invalid-update:no-escaped-admission");

        PvadLicense.LoadFromFile(licenses[0]);
        using (var key = new PVAD.Importer.Signing.PdfPkcs12SigningKey(crypto.Pfx))
        {
            var signed = new PVAD.Importer.Signing.PdfPadesSigningService().Sign(plain, key).Bytes;
            var signatureHash = SHA256.HashData(signed);
            try
            {
                rewrite.RewriteWithUpdates(signed, new SwitchingList([update], () => ReplaceAndCheckNewCall("save:signed")));
                throw new InvalidOperationException("Signed full rewrite bypassed invalidation protection.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
            { Check(signatureHash.SequenceEqual(SHA256.HashData(signed)), "save:signed:signature-protection-and-source-preserved"); }
        }

        foreach (var fail in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var cancel = new CancellationTokenSource();
            using var input = new SwitchingStream(plain, true, () =>
            {
                ReplaceAndCheckNewCall("load:error:" + fail);
                if (fail) throw new IOException("Synthetic continuation read failure.");
                cancel.Cancel();
            });
            try { await importer.LoadAsync(input, cancel.Token); throw new InvalidOperationException("Expected read failure."); }
            catch (Exception ex) when (fail ? ex is IOException : ex is OperationCanceledException)
            { Check(true, "load:error:" + fail + ":original-error-preserved"); }
            using var next = new MemoryStream(plain);
            await Denied(() => importer.LoadAsync(next), "load:error:" + fail + ":no-escaped-admission");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            using var unread = new SwitchingStream(plain, true, () => throw new InvalidOperationException("Cancelled load read input."));
            try { await importer.LoadAsync(unread, cancelled.Token); throw new InvalidOperationException("Cancellation ignored."); }
            catch (OperationCanceledException) { Check(unread.Position == 0, "load:pre-cancelled-does-not-read"); }
        }
        Check(crypto.Requests == 0, "load:certificate-fixtures-never-use-network");
        return checks.Count - start;

        void ReplaceAndCheckNewCall(string phase)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { var tokenizer = new PdfTokenizer("1"u8); tokenizer.ReadToken(); throw new InvalidOperationException("New tokenizer inherited admission."); }
            catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, phase + ":reentrant-public-call-denied"); }
        }
        void Check(bool valid, string name)
        {
            name = "pdf-continuation:" + name;
            if (!valid || checks.Contains(name)) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Denied(Func<Task> call, string name)
        {
            try { await call(); }
            catch (PvadLicenseException ex) when (ex.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected license denial: " + name);
        }
    }

    private static string Fingerprint(PdfDocumentModel document) => string.Join("|", document.Pages.Select(p =>
        $"{p.MediaBox}:{p.Rotate}:" + string.Join(";", p.GraphicsObjects.Select(e => e is PdfTextElement text
            ? $"text:{text.Text}:{text.Transform}:{text.FontSize}" : e.GetType().Name))));

    private static byte[] Zip(ReadOnlySpan<byte> bytes)
    {
        using var output = new MemoryStream();
        using (var compressor = new ZLibStream(output, CompressionLevel.Optimal, true)) compressor.Write(bytes);
        return output.ToArray();
    }

    private sealed class SwitchingList(PdfIncrementalObjectUpdate[] values, Action replace) : IReadOnlyList<PdfIncrementalObjectUpdate>
    {
        private bool replaced;
        private void Before() { if (!replaced) { replaced = true; replace(); } }
        public int Count { get { Before(); return values.Length; } }
        public PdfIncrementalObjectUpdate this[int index] { get { Before(); return values[index]; } }
        public IEnumerator<PdfIncrementalObjectUpdate> GetEnumerator() { Before(); return ((IEnumerable<PdfIncrementalObjectUpdate>)values).GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SwitchingDecoder(Action replace) : IPdfStreamDecoder
    {
        public string FilterName => "SyntheticSwitch";
        public bool CanDecode(PdfName name) => name.Value == FilterName;
        public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> bytes, PdfDictionary dictionary) { replace(); return bytes; }
    }

    private sealed class SwitchingEncoding(Action replace) : PdfEncodingMap
    {
        public int Calls { get; private set; }
        public override string Decode(ReadOnlySpan<byte> bytes) { Calls++; if (Calls == 1) replace(); return "Custom decoding"; }
    }

    private sealed class SwitchingFontParser(Action replace) : IPdfFontParser
    {
        public int Calls { get; private set; }
        public PdfFontResource Parse(string resourceName, PdfDictionary fontDictionary, IPdfObjectResolver resolver)
        {
            Calls++;
            var font = new PdfSimpleFontParser().Parse(resourceName, fontDictionary, resolver);
            replace();
            return font;
        }
    }

    private sealed class SwitchingStream(byte[] bytes, bool seekable, Action replace) : Stream
    {
        private readonly MemoryStream source = new(bytes, writable: false);
        private bool replaced;
        private void Before() { if (!replaced) { replaced = true; replace(); } }
        public override bool CanRead => true;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => source.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) { Before(); return source.Read(buffer, offset, count); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Before(); return source.ReadAsync(buffer, cancellationToken); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
