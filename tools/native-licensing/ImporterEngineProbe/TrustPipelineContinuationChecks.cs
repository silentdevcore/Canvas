using System.Buffers;
using System.Collections;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using PVAD.Importer;
using PVAD.Importer.Redaction;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class TrustPipelineContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var document = new PdfDocument();
        var firstPage = document.AddPage(300, 200);
        firstPage.DrawText("SECRET-REMOVE", 30, 100);
        firstPage.DrawText("PUBLIC-KEEP", 30, 160);
        document.AddPage(200, 300).DrawText("SECOND-PAGE-KEEP", 30, 150);
        var plain = document.ToBytes();
        var encrypted = document.ToBytes(new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } });
        document.AddAssociatedFile("synthetic attachment"u8.ToArray(), "notes.txt", "text/plain", PdfAssociatedFileRelationship.Supplement);
        var risky = document.ToBytes();
        PvadLicense.LoadFromFile(licenses[0]);
        using var crypto = new TrustCryptoFixtures();
        using var key = new PdfPkcs12SigningKey(crypto.Pfx);
        using var client = new HttpClient(crypto, disposeHandler: false);
        var timestamp = new PdfRfc3161TimestampAuthority(client, new() { Endpoint = new("https://tsa.example.test/rfc3161"), RequireTrustedCertificateChain = false });
        var redactor = new PdfRedactionApplier(new PdfRedactionPreflight());
        var signer = new PdfPadesSigningService();
        var inspector = new PdfTrustInspector();
        var pipeline = new PdfTrustPostProcessingPipeline(redactor, signer, inspector);
        PdfRedactionRegion[] regions = [new(1, new(20, 70, 260, 50))];
        var inspection = new PdfTrustInspectionOptions
        {
            SignatureValidationPolicy = new()
            {
                UseSystemTrustStore = false, CustomTrustAnchors = [crypto.Issuer, crypto.TimestampCertificate],
                IntermediateCertificates = [crypto.Issuer],
            },
        };
        var material = new PdfLongTermValidationMaterial
        {
            Certificates = [crypto.Certificate.RawData, crypto.Issuer.RawData, crypto.TimestampCertificate.RawData],
            TrustAnchors = [crypto.Issuer.RawData, crypto.TimestampCertificate.RawData], Crls = [crypto.Crl], OcspResponses = [crypto.Ocsp],
        };
        var cases = new List<(string Name, bool Redact, PdfPadesProfile? Profile, string Trigger)>
        {
            ("validation", false, null, "source"), ("redaction-source", true, null, "source"), ("redaction-regions", true, null, "regions"),
        };
        foreach (var redact in new[] { false, true })
        foreach (var profile in Enum.GetValues<PdfPadesProfile>())
        foreach (var trigger in profile == PdfPadesProfile.BaselineB ? new[] { "source" } :
            profile == PdfPadesProfile.BaselineT ? new[] { "source", "http" } : new[] { "source", "http", "material" })
            cases.Add(($"{(redact ? "combined" : "signing")}:{profile}:{trigger}", redact, profile, trigger));

        foreach (var (name, redact, profile, trigger) in cases)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var changes = 0;
            void Change() { if (changes++ == 0) Replace(name); }
            var provider = new MaterialProvider(material, trigger == "material" ? Change : () => { });
            var options = new PdfTrustPostProcessingOptions
            {
                Redaction = redact ? new() { Regions = trigger == "regions" ? new SwitchingList(regions, Change) : regions } : null,
                Signing = profile is { } selected ? new() { Profile = selected, SigningTime = crypto.Now, FieldName = "PipelineApproval" } : null,
                Validation = new() { Inspection = inspection, RequireValidSignatures = profile is not null },
            };
            var context = new PdfTrustPipelineContext
            {
                DocumentSigningKey = profile is null ? null : key,
                DocumentTimestampAuthority = profile is null or PdfPadesProfile.BaselineB ? null : timestamp,
                LongTermValidationProvider = profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA ? provider : null,
            };
            crypto.BeforeRequest = trigger == "http" ? Change : null;
            using var source = new SwitchingMemory(plain, trigger == "source" ? Change : () => { });
            PdfTrustPostProcessingResult result;
            try { result = await pipeline.ProcessAsync(source.Memory, options, context); }
            finally { crypto.BeforeRequest = null; }
            Check(changes > 0 && source.Memory.Span.SequenceEqual(plain), name + ":replacement-and-source-preserved");
            Check(result.CompletedSteps.SequenceEqual((redact ? new[] { PdfTrustPipelineStep.Redaction } : []).Concat(profile is null ? [] : new[] { PdfTrustPipelineStep.Signing }).Append(PdfTrustPipelineStep.Validation)), name + ":ordered-stages");
            Check(result.TrustReport.Sha256 == Convert.ToHexStringLower(SHA256.HashData(result.Bytes)) && result.TrustReport.StructureStatus == PdfTrustStatus.Valid, name + ":exact-final-byte-validation");
            using var next = new SwitchingMemory(plain, () => throw new Exception("Denied pipeline touched its source."));
            await Denied(() => pipeline.ProcessAsync(next.Memory, options, context), name + ":new-pipeline-denied");
            Check(next.Reads == 0, name + ":denied-before-source-access");
            await Denied(() => signer.SignAsync(plain, key), name + ":new-signing-denied");
            await Denied(() => redactor.ApplyAsync(new MemoryStream(plain), regions), name + ":new-redaction-denied");
            await Denied(() => inspector.InspectAsync(new MemoryStream(result.Bytes)), name + ":new-inspection-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            using var output = new MemoryStream(result.Bytes);
            var reopened = await inspector.InspectAsync(output, inspection);
            Check(JsonSerializer.Serialize(reopened) == JsonSerializer.Serialize(result.TrustReport), name + ":independent-final-report");
            using var pdfInput = new MemoryStream(result.Bytes);
            var pages = (await new PdfImporter().LoadAsync(pdfInput)).Pages;
            var text = string.Join("\n", pages.SelectMany(page => page.TextObjects).Select(item => item.Text));
            Check(pages.Count == 2 && pages[0].MediaBox?.Width == 300 && pages[1].MediaBox?.Width == 200, name + ":mixed-page-geometry");
            Check(text.Contains("PUBLIC-KEEP", StringComparison.Ordinal) && text.Contains("SECOND-PAGE-KEEP", StringComparison.Ordinal) && text.Contains("SECRET-REMOVE", StringComparison.Ordinal) != redact, name + ":content-and-removal");
            if (redact)
            {
                Check(result.Redaction is { Verification.Passed: true, RemovedElementCount: > 0 } && PdfRedactionAuditVerifier.Verify(result.Redaction.Audit), name + ":verified-redaction-audit");
                Check(result.Signing is null ? result.Bytes.SequenceEqual(result.Redaction!.Bytes) : result.Bytes.AsSpan().StartsWith(result.Redaction!.Bytes), name + ":redaction-precedes-signing");
            }
            else Check(result.Redaction is null && (profile is null ? result.Bytes.SequenceEqual(plain) : result.Bytes.AsSpan().StartsWith(plain)), name + ":input-content-preserved");
            if (profile is not null)
            {
                var signature = reopened.Signatures.Single(s => !s.IsDocumentTimestamp);
                Check(signature.IntegrityStatus == PdfTrustStatus.Valid && signature.CertificateStatus == PdfTrustStatus.Valid && result.Signing!.Profile == profile, name + ":signature-integrity-and-trust");
                Check(reopened.Signatures.Count == (profile == PdfPadesProfile.BaselineLTA ? 2 : 1), name + ":signature-count");
                if (profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA)
                    Check(provider.Calls == 1 && signature.HistoricalRevocationStatus == PdfTrustStatus.Valid, name + ":async-provider-and-historical-evidence");
            }
        }

        foreach (var trigger in new[] { "source", "audit-http" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var changed = false;
            void Change() { if (!changed) { changed = true; Replace("audit:" + trigger); } }
            crypto.BeforeRequest = trigger == "audit-http" ? Change : null;
            using var source = new SwitchingMemory(plain, trigger == "source" ? Change : () => { });
            PdfTrustPostProcessingResult result;
            try { result = await pipeline.ProcessAsync(source.Memory, new() { Redaction = new() { Regions = regions }, Signing = new() { SigningTime = crypto.Now }, Validation = new() { Inspection = inspection, RequireValidSignatures = true } }, new() { DocumentSigningKey = key, RedactionAuditSigningKey = key, RedactionAuditTimestampAuthority = timestamp }); }
            finally { crypto.BeforeRequest = null; }
            Check(changed && result.CompletedSteps.Count == 3 && result.TrustReport.Signatures.Single().IntegrityStatus == PdfTrustStatus.Valid, "audit:" + trigger + ":continued-through-document-signing");
            await Denied(() => Task.FromResult(PdfRedactionAuditVerifier.Verify(result.Redaction!.Audit, true, true)), "audit:" + trigger + ":new-audit-verification-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            Check(PdfRedactionAuditVerifier.Verify(result.Redaction!.Audit, true, true), "audit:" + trigger + ":cms-and-timestamp-verified");
        }

        foreach (var boundary in new[] { "redaction", "signing", "inspector", "key", "timestamp", "material" })
        foreach (var trigger in new[] { "source", "provider" })
        {
            var name = "custom:" + boundary + ":" + trigger;
            PvadLicense.LoadFromFile(licenses[0]);
            Action change = () => Replace(name);
            Action callback = trigger == "provider" ? change : () => { };
            var customRedactor = new DelegatingRedactor(redactor, callback);
            var customSigner = new DelegatingSigner(signer, callback);
            var customInspector = new DelegatingInspector(inspector, callback);
            var customKey = new DelegatingKey(key, callback);
            var customTimestamp = new DelegatingTimestamp(timestamp, callback);
            var customMaterial = new MaterialProvider(material, () => { callback(); PvadLicense.Require(PvadLicenseProducts.Importer); });
            var service = new PdfTrustPostProcessingPipeline(boundary == "redaction" ? customRedactor : redactor, boundary == "signing" ? customSigner : signer, boundary == "inspector" ? customInspector : inspector);
            using var source = new SwitchingMemory(plain, trigger == "source" ? change : () => { });
            await Denied(() => service.ProcessAsync(source.Memory, new()
            {
                Redaction = boundary == "redaction" ? new() { Regions = regions } : null,
                Signing = boundary == "inspector" ? null : new() { SigningTime = crypto.Now, Profile = boundary == "material" ? PdfPadesProfile.BaselineLT : boundary == "timestamp" ? PdfPadesProfile.BaselineT : PdfPadesProfile.BaselineB },
            }, new()
            {
                DocumentSigningKey = boundary == "key" ? customKey : key,
                DocumentTimestampAuthority = boundary == "timestamp" ? customTimestamp : timestamp,
                LongTermValidationProvider = boundary == "material" ? customMaterial : null,
            }), name + ":fresh-native-dispatch");
            Check((boundary switch { "redaction" => customRedactor.Calls, "signing" => customSigner.Calls, "inspector" => customInspector.Calls, "key" => customKey.Calls, "timestamp" => customTimestamp.Calls, _ => customMaterial.Calls }) == 1, name + ":interface-not-bypassed");
        }

        foreach (var (name, policy, bytes, code) in new[]
        {
            ("structure", new PdfTrustPipelineValidationPolicy(), new byte[] { 1, 2, 3 }, "PVADPDFPIPELINE200"),
            ("pdfa", new PdfTrustPipelineValidationPolicy { RequirePdfA = true }, plain, "PVADPDFPIPELINE201"),
            ("pdfua", new PdfTrustPipelineValidationPolicy { RequirePdfUa = true }, plain, "PVADPDFPIPELINE202"),
            ("signatures", new PdfTrustPipelineValidationPolicy { RequireValidSignatures = true }, plain, "PVADPDFPIPELINE203"),
            ("risk", new PdfTrustPipelineValidationPolicy { RejectRiskyContent = true }, risky, "PVADPDFPIPELINE204"),
        })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var source = new SwitchingMemory(bytes, () => Replace("reject:" + name));
            try { await pipeline.ProcessAsync(source.Memory, new() { Validation = policy }); throw new Exception("Expected policy rejection: " + name); }
            catch (PdfTrustPipelineValidationException e) when (e.Code == code)
            { Check(e.Report is not null && e.Report.Sha256 == Convert.ToHexStringLower(SHA256.HashData(bytes)), "reject:" + name + ":original-policy-and-report"); }
            await Denied(() => pipeline.ProcessAsync(source.Memory), "reject:" + name + ":scope-closed");
        }

        foreach (var failure in new[] { "source-io", "cancel", "encrypted-redaction", "outside-region", "source-license", "timestamp-http" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var cancellation = new CancellationTokenSource();
            using var source = new SwitchingMemory(failure == "encrypted-redaction" ? encrypted : plain, () =>
            {
                Replace(failure);
                if (failure == "source-io") throw new IOException("Synthetic pipeline source failure.");
                if (failure == "cancel") cancellation.Cancel();
                if (failure == "source-license") PvadLicense.Require(PvadLicenseProducts.Importer);
            });
            crypto.BeforeRequest = failure == "timestamp-http" ? () => throw new HttpRequestException("Synthetic timestamp failure.") : null;
            try
            {
                await pipeline.ProcessAsync(source.Memory, new()
                {
                    Redaction = failure is "encrypted-redaction" or "outside-region" ? new() { Regions = failure == "outside-region" ? [new(1, new(1000, 1000, 20, 20))] : regions } : null,
                    Signing = failure == "timestamp-http" ? new() { Profile = PdfPadesProfile.BaselineT, SigningTime = crypto.Now } : null,
                }, new() { DocumentSigningKey = key, DocumentTimestampAuthority = timestamp }, cancellation.Token);
                throw new Exception("Expected pipeline failure: " + failure);
            }
            catch (Exception e) when (failure switch
            {
                "source-io" => e is IOException,
                "cancel" => e is OperationCanceledException,
                "encrypted-redaction" or "outside-region" => e is PdfRedactionBlockedException,
                "source-license" => e is PvadLicenseException { Code: "PVADLIC012" },
                _ => e is PdfTimestampAuthorityException { InnerException: HttpRequestException },
            }) { Check(source.Reads > 0, failure + ":original-error-not-masked"); }
            finally { crypto.BeforeRequest = null; }
            await Denied(() => pipeline.ProcessAsync(plain), failure + ":failed-admission-not-reusable");
        }

        PvadLicense.LoadFromFile(licenses[0]);
        await Denied(() => Task.FromResult(new PdfDocument()), "no-public-pdf-generation");
        return checks.Count - start;

        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Trust pipeline continuation failed: " + name); checks.Add("pipeline-continuation:" + name); }
        async Task Denied(Func<Task> action, string name)
        {
            try { await action(); } catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected fresh Importer denial: " + name);
        }
        void Replace(string name)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { PvadLicense.Require(PvadLicenseProducts.Importer); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name + ":current-license-changed"); return; }
            throw new InvalidOperationException("Replacement did not revoke Importer rights.");
        }
    }

    private sealed class SwitchingMemory(byte[] bytes, Action change) : MemoryManager<byte>
    {
        private readonly byte[] source = bytes.ToArray();
        internal int Reads { get; private set; }
        public override Memory<byte> Memory => CreateMemory(source.Length);
        public override Span<byte> GetSpan() { if (Reads++ == 0) change(); return source; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { if (disposing) CryptographicOperations.ZeroMemory(source); }
    }

    private sealed class SwitchingList(IReadOnlyList<PdfRedactionRegion> values, Action change) : IReadOnlyList<PdfRedactionRegion>
    {
        private bool changed;
        private void Before() { if (!changed) { changed = true; change(); } }
        public int Count { get { Before(); return values.Count; } }
        public PdfRedactionRegion this[int index] { get { Before(); return values[index]; } }
        public IEnumerator<PdfRedactionRegion> GetEnumerator() { Before(); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DelegatingRedactor(IPdfRedactionApplier inner, Action change) : IPdfRedactionApplier
    {
        internal int Calls { get; private set; }
        public Task<PdfRedactionApplyResult> ApplyAsync(Stream source, IReadOnlyList<PdfRedactionRegion> regions, PdfRedactionApplyOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.ApplyAsync(source, regions, options, cancellationToken); }
    }

    private sealed class DelegatingSigner(IPdfPadesSigningService inner, Action change) : IPdfPadesSigningService
    {
        internal int Calls { get; private set; }
        public PdfSigningResult Sign(ReadOnlyMemory<byte> source, IPdfSigningKey key, PdfSigningOptions? options = null)
        { Calls++; change(); return inner.Sign(source, key, options); }
        public Task<PdfSigningResult> SignAsync(ReadOnlyMemory<byte> source, IPdfSigningKey key, PdfSigningOptions? options = null, IPdfTimestampAuthority? timestampAuthority = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.SignAsync(source, key, options, timestampAuthority, cancellationToken); }
    }

    private sealed class DelegatingInspector(IPdfTrustInspector inner, Action change) : IPdfTrustInspector
    {
        internal int Calls { get; private set; }
        public Task<PdfTrustReport> InspectAsync(Stream source, PdfTrustInspectionOptions? options = null, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.InspectAsync(source, options, cancellationToken); }
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
        public Task<PdfTimestampToken> GetTimestampTokenAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName algorithm, CancellationToken cancellationToken = default)
        { Calls++; change(); return inner.GetTimestampTokenAsync(hash, algorithm, cancellationToken); }
    }

    private sealed class MaterialProvider(PdfLongTermValidationMaterial material, Action change) : IPdfLongTermValidationMaterialProvider
    {
        internal int Calls { get; private set; }
        public async Task<PdfLongTermValidationMaterial> GetMaterialAsync(PdfLongTermValidationContext context, CancellationToken cancellationToken = default)
        { await Task.Yield(); Calls++; change(); return material; }
    }
}
