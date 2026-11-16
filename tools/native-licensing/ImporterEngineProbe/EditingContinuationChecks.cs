using System.Buffers;
using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Content;
using PVAD.Importer.Document;
using PVAD.Importer.Editing;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Importer.Security;
using PVAD.Importer.Signing;
using PVAD.Licensing;

internal static class EditingContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var fixture = new PVAD.Pdf.PdfDocument();
        fixture.AddPage(300, 180).DrawText("First page", 30, 100);
        fixture.AddPage(180, 300).DrawText("Second page", 30, 140);
        fixture.AddPage(400, 200).DrawText("Third page", 30, 120);
        var plain = fixture.ToBytes();
        PvadLicense.LoadFromFile(licenses[0]);
        var editor = new PdfPageEditor();
        var composer = new PdfPageComposer();
        var encrypted = new PVAD.Importer.Saving.PdfFullRewriteWriter().Rewrite(plain, new()
        {
            Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" },
        }).Bytes;
        var cases = new (string Name, byte[] Input, Func<ReadOnlyMemory<byte>, Outcome> Run, string? Password)[]
        {
            ("move", plain, m => Edited(editor.Apply(m, [new PdfMovePageOperation(1, 3)])), null),
            ("delete", plain, m => Edited(editor.Apply(m, [new PdfDeletePageOperation(2)])), null),
            ("crop", plain, m => Edited(editor.Apply(m, [new PdfSetPageBoxOperation(1, PdfPageBoxKind.CropBox, 10, 170, 290, 10)])), null),
            ("encrypted-move", encrypted, m => Edited(editor.Apply(m, [new PdfMovePageOperation(1, 3)], new()
            {
                SourcePassword = "synthetic-owner", Encryption = new() { OwnerPassword = "synthetic-new-owner", UserPassword = "synthetic-new-reader" },
            })), "synthetic-new-owner"),
            ("read-box", plain, m => new([], 3, JsonSerializer.Serialize(editor.ReadPageBox(m, 1, PdfPageBoxKind.CropBox))), null),
            ("apply-target", plain, m => Composed(composer.Apply(m, [new PdfPageImport(plain, 2)])), null),
            ("apply-source", plain, m => Composed(composer.Apply(plain, [new PdfPageImport(m, 2)])), null),
            ("merge-first", plain, m => Composed(composer.Merge([m, plain])), null),
            ("merge-later", plain, m => Composed(composer.Merge([plain, m])), null),
            ("merge-single", plain, m => Composed(composer.Merge([m])), null),
            ("append", plain, m => Composed(composer.AppendPages(plain, m, [3, 1])), null),
            ("split", plain, m => new(composer.Split(m, [new(1, 1), new(2, 3)]).Documents, 3), null),
            ("extract", plain, m => new([composer.ExtractPages(m, [3, 1])], 2), null),
            ("count", plain, m => new([], composer.GetPageCount(m)), null),
        };
        foreach (var (name, input, run, password) in cases)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var baseline = run(input);
            var expected = await Fingerprint(baseline, password);
            using var memory = new SwitchingMemory(input, () => Replace(name));
            var sourceHash = SHA256.HashData(input);
            var result = run(memory.Memory);
            Check(memory.Reads > 0, name + ":renewal-during-processing");
            Check(result.Count == baseline.Count && result.Info == baseline.Info, name + ":operation-result-preserved");
            Denied(() => run(input), name + ":next-public-call-denied");
            Check(memory.Memory.Span.SequenceEqual(input) && sourceHash.SequenceEqual(SHA256.HashData(input)), name + ":source-unchanged");
            PvadLicense.LoadFromFile(licenses[0]);
            Check(await Fingerprint(result, password) == expected, name + ":layout-order-content-and-encryption-roundtrip");
        }

        foreach (var mode in new[] { "edit-list", "page-list", "range-list", "document-list" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var result = mode switch
            {
                "edit-list" => editor.Apply(plain, new SwitchingList<PdfPageEditOperation>([new PdfMovePageOperation(1, 3)], () => Replace(mode))).Bytes,
                "page-list" => composer.ExtractPages(plain, new SwitchingList<int>([3, 1], () => Replace(mode))),
                "range-list" => composer.Split(plain, new SwitchingList<PdfPageRange>([new(1, 2)], () => Replace(mode))).Documents.Single(),
                _ => composer.Merge(new SwitchingList<ReadOnlyMemory<byte>>([plain, plain], () => Replace(mode))).Bytes,
            };
            Denied(() => composer.GetPageCount(result), mode + ":new-count-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            Check(composer.GetPageCount(result) == (mode == "document-list" ? 6 : mode == "edit-list" ? 3 : 2), mode + ":valid-output");
        }

        var command = new PdfContentCommand(new("Tj", PdfOperatorCategory.TextShowing, 1), [], default, 0);
        var text = new PdfTextElement(0, PdfMatrix.Identity, command, "Rewritten content") { FontResourceName = "F1", FontSize = 12 };
        var rewriter = new PdfContentStreamRewriter();
        PvadLicense.LoadFromFile(licenses[0]);
        var expectedContent = rewriter.Rewrite([text]);
        var rewritten = rewriter.Rewrite(Elements());
        Check(rewritten.Span.SequenceEqual(expectedContent.Span), "rewrite:identical-content");
        Denied(() => rewriter.Rewrite([text]), "rewrite:new-call-denied");

        var directory = Directory.CreateTempSubdirectory("pvad-editing-continuation-").FullName;
        PvadLicense.LoadFromFile(licenses[0]);
        var replacement = composer.ExtractPages(plain, [2]);
        try
        {
            foreach (var own in new[] { false, true })
            foreach (var failure in new[] { "none", "invalid", "io" })
            {
                var phase = $"facade:{own}:{failure}";
                PvadLicense.LoadFromFile(licenses[0]);
                using var document = new PdfEditableDocument();
                using (var source = new MemoryStream(plain)) Check(document.LoadFromStream(source) == PdfPageOperationStatus.Success, phase + ":baseline-loaded");
                Check(document.SelectPage(2) == PdfPageOperationStatus.Success && document.SetMeasurementUnit(PdfEditableMeasurementUnit.Inch) == PdfPageOperationStatus.Success,
                    phase + ":baseline-selection-and-units");
                using var input = new SwitchingStream(failure == "invalid" ? new byte[] { 1, 2, 3 } : replacement, () =>
                {
                    Replace(phase);
                    if (failure == "io") throw new IOException("Synthetic editing input failure.");
                });
                var status = document.LoadFromStream(input, own);
                Check(status == (failure == "none" ? PdfPageOperationStatus.Success : PdfPageOperationStatus.Failed), phase + ":status-preserved");
                Check(input.CanRead == !own, phase + ":ownership-preserved");
                var error = document.LastError;
                Check(failure == "none" ? error is null : failure == "io" ? error is IOException : error is not null, phase + ":original-error-preserved");
                var path = Path.Combine(directory, own + "-" + failure + ".pdf");
                Denied(() => document.SaveToFile(path), phase + ":new-save-denied");
                Check(!File.Exists(path) && ReferenceEquals(document.LastError, error), phase + ":no-file-or-error-mutation");
                PvadLicense.LoadFromFile(licenses[0]);
                Check(document.ToBytes().SequenceEqual(failure == "none" ? replacement : plain), phase + ":loaded-bytes-retained");
                float left = 0, top = 0, right = 0, bottom = 0;
                var scale = failure == "none" ? 1d : 72d;
                Check(document.GetPageBox(PdfEditablePageBox.CropBox, ref left, ref top, ref right, ref bottom) == PdfPageOperationStatus.Success &&
                    Math.Abs(right - 180d / scale) < 0.0001 && Math.Abs(top - 300d / scale) < 0.0001,
                    phase + ":selected-page-and-units-preserved-or-reset");
                Check(document.InsertPage(PdfEditablePageSize.A4, 2) == PdfPageOperationStatus.Success && document.GetPageCount() == (failure == "none" ? 2 : 4), phase + ":renewed-blank-insertion");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
        Check(!Directory.Exists(directory), "facade:temporary-output-cleaned");

        Reject("invalid-edit", m => editor.Apply(m, [new PdfDeletePageOperation(99)]), plain, e => e is ArgumentOutOfRangeException);
        Reject("invalid-range", m => composer.Split(m, [new(3, 2)]), plain, e => e is ArgumentOutOfRangeException);
        Reject("duplicate-pages", m => composer.ExtractPages(m, [1, 1]), plain, e => e is ArgumentException);
        Reject("encrypted-composition", m => composer.Merge([m, plain]), encrypted, e => e is NotSupportedException);
        PvadLicense.LoadFromFile(licenses[0]);
        using (var crypto = new TrustCryptoFixtures())
        using (var key = new PdfPkcs12SigningKey(crypto.Pfx))
        {
            var signed = new PdfPadesSigningService().Sign(plain, key).Bytes;
            Reject("signed-edit", m => editor.Apply(m, [new PdfMovePageOperation(1, 3)]), signed, e => e is InvalidOperationException && e.Message.Contains("signature", StringComparison.OrdinalIgnoreCase));
            Reject("signed-split", m => composer.Split(m, [new(1, 1)]), signed, e => e is InvalidOperationException && e.Message.Contains("signature", StringComparison.OrdinalIgnoreCase));
            Reject("signed-append-source", m => composer.AppendPages(plain, m, [1]), signed, e => e is InvalidOperationException && e.Message.Contains("signature", StringComparison.OrdinalIgnoreCase));
            Check(crypto.Requests == 0, "signature:synthetic-offline-only");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        Denied(() => new PVAD.Pdf.PdfDocument(), "blank-dependency:does-not-grant-public-pdf");
        return checks.Count - start;

        IEnumerable<PdfGraphicsElement> Elements() { Replace("rewrite"); yield return text; }
        void Replace(string name)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            Denied(() => editor.ReadPageBox(plain, 1, PdfPageBoxKind.CropBox), name + ":reentrant-public-operation-denied");
        }
        void Reject(string name, Action<ReadOnlyMemory<byte>> action, byte[] source, Func<Exception, bool> expected)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var hash = SHA256.HashData(source);
            using var memory = new SwitchingMemory(source, () => Replace(name));
            try { action(memory.Memory); throw new Exception("Expected validation failure: " + name); }
            catch (Exception e) when (expected(e)) { Check(memory.Reads > 0, name + ":original-validation-error"); }
            Check(memory.Memory.Span.SequenceEqual(source) && hash.SequenceEqual(SHA256.HashData(source)), name + ":source-preserved");
            Denied(() => composer.GetPageCount(plain), name + ":no-reusable-admission");
        }
        void Denied(Action call, string name)
        {
            try { call(); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new Exception("Expected license denial: " + name);
        }
        void Check(bool value, string name)
        {
            name = "editing-continuation:" + name;
            if (!value || checks.Contains(name)) throw new InvalidOperationException(name);
            checks.Add(name);
        }
    }

    private static async Task<string> Fingerprint(Outcome outcome, string? password)
    {
        var documents = new List<object>();
        foreach (var bytes in outcome.Documents)
        {
            if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new Exception("Not PDF output.");
            using var source = new MemoryStream(bytes);
            var importer = new PdfImporter();
            var model = password is null ? await importer.LoadAsync(source) : await importer.LoadAsync(source, password);
            documents.Add(model.Pages.Select(page => new
            {
                page.MediaBox, page.CropBox, page.Rotate,
                Elements = page.GraphicsObjects.Select(element => new
                {
                    Kind = element.GetType().Name, element.Transform,
                    Text = (element as PdfTextElement)?.Text, Size = (element as PdfTextElement)?.FontSize,
                }).ToArray(),
            }).ToArray());
        }
        return JsonSerializer.Serialize(new { outcome.Count, outcome.Info, Documents = documents });
    }

    private sealed record Outcome(IReadOnlyList<byte[]> Documents, int Count, string? Info = null);

    private static Outcome Edited(PdfPageEditResult result) => new([result.Bytes], result.PageCount,
        JsonSerializer.Serialize(new { result.OriginalPageCount, result.PageCount, Operations = result.AppliedOperations.Select(o => o.ToString()), result.InvalidatedSignatures }));

    private static Outcome Composed(PdfPageCompositionResult result) => new([result.Bytes], result.PageCount,
        JsonSerializer.Serialize(new { result.OriginalPageCount, result.PageCount }));

    private sealed class SwitchingMemory(byte[] bytes, Action change) : MemoryManager<byte>
    {
        private readonly byte[] source = bytes.ToArray();
        public int Reads { get; private set; }
        public override Memory<byte> Memory => CreateMemory(source.Length);
        public override Span<byte> GetSpan() { if (Reads++ == 0) change(); return source; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { if (disposing) Array.Clear(source); }
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

    private sealed class SwitchingStream(byte[] bytes, Action change) : Stream
    {
        private readonly MemoryStream source = new(bytes, writable: false);
        private bool changed;
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        { if (!changed) { changed = true; change(); } return source.Read(buffer, offset, count); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
