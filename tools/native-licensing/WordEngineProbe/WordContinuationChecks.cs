using System.IO.Compression;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Word;
using PVAD.Licensing;

internal static class WordContinuationChecks
{
    internal static void Run(string wordLicense, string otherLicense, DesignExportDto design,
        byte[] document, byte[] certificate, List<string> checks, Action<byte[], string> verifyDocument)
    {
        foreach (var fail in new[] { false, true })
        {
            PvadLicense.LoadFromFile(wordLicense);
            var input = JsonSerializer.Deserialize<DesignExportDto>(JsonSerializer.Serialize(design))!;
            var failure = new IOException("Synthetic Word callback failure.");
            var calls = 0;
            input.Pages[0].Elements[0].Style = new()
            {
                ["fontWeight"] = new CallbackValue(() =>
                {
                    calls++;
                    Replace("export:" + fail + ":" + calls);
                    if (fail) throw failure;
                }),
            };
            var before = JsonSerializer.Serialize(input);
            try
            {
                var bytes = new WordDocumentExporter().Export(input);
                Check(!fail, "export-no-unexpected-success:" + fail);
                verifyDocument(bytes, "word-inflight-export-content");
            }
            catch (IOException error) when (fail) { Check(ReferenceEquals(error, failure), "export-original-failure"); }
            Check(calls > 0, "export-callback-executed:" + fail);
            Check(JsonSerializer.Serialize(input) == before, "export-input-unchanged:" + fail);
            Denied(() => new WordDocumentExporter().Export(design), "export-next-call-denied:" + fail);

            PvadLicense.LoadFromFile(wordLicense);
            using var source = new CallbackStream(document, () =>
            {
                Replace("sign:" + fail);
                if (fail) throw failure;
            });
            try
            {
                var signed = DigitalSigningService.SignDocx(source, certificate);
                Check(!fail, "sign-no-unexpected-success:" + fail);
                verifyDocument(signed, "word-inflight-sign-content");
                using var zip = new ZipArchive(new MemoryStream(signed));
                Check(zip.GetEntry("_xmlsignatures/sig1.xml") is not null, "sign-has-signature");
            }
            catch (IOException error) when (fail) { Check(ReferenceEquals(error, failure), "sign-original-failure"); }
            Check(source.CallbackInvoked && source.CanRead, "sign-source-left-open:" + fail);
            Check(source.Bytes.SequenceEqual(document), "sign-source-bytes-unchanged:" + fail);
            Check(source.Position == (fail ? 0 : document.Length), "sign-source-position:" + fail);
            Denied(() => DigitalSigningService.SignDocx(source, certificate), "sign-next-call-denied:" + fail);
        }
        PvadLicense.LoadFromFile(wordLicense);
        verifyDocument(new WordDocumentExporter().Export(design), "word-inflight-renewal-restores-export");

        void Replace(string name)
        {
            PvadLicense.LoadFromFile(otherLicense);
            Denied(() => PVAD.Generator.Word.Export(design), name + ":callback-public-export-denied");
            using var source = new MemoryStream([1, 2, 3]);
            Denied(() => DigitalSigningService.SignDocx(source, []), name + ":callback-public-sign-denied");
            Check(source.Position == 0 && source.CanRead, name + ":callback-source-untouched");
        }
        void Denied(Action action, string name)
        {
            try { action(); throw new InvalidOperationException("Expected a fresh Word admission."); }
            catch (PvadLicenseException error) when (error.Code == "PVADLIC012") { Check(true, name); }
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Word continuation failed: " + name);
            checks.Add("word-continuation:" + name);
        }
    }

    private sealed class CallbackValue(Action callback)
    {
        public override string ToString() { callback(); return "bold"; }
    }

    private sealed class CallbackStream(byte[] bytes, Action callback) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public bool CallbackInvoked { get; private set; }
        public byte[] Bytes => inner.ToArray();
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CallbackInvoked) { CallbackInvoked = true; callback(); }
            return inner.Read(buffer, offset, count);
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() => inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
