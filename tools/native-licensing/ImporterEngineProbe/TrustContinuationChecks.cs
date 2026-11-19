using System.Buffers;
using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class TrustContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var document = new PdfDocument { FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) };
        document.AddPage(300, 180).DrawText("Synthetic Trust continuation", 30, 100, new PdfDrawTextOptions { Language = "en" });
        var plain = document.ToBytes();
        var archive2 = document.ToBytes(new() { ComplianceProfile = PdfComplianceProfile.PdfA2B });
        var archive3 = document.ToBytes(new() { ComplianceProfile = PdfComplianceProfile.PdfA3B });
        var encrypted = document.ToBytes(new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } });
        PvadLicense.LoadFromFile(licenses[0]);
        using var crypto = new TrustCryptoFixtures();
        using var key = new PdfPkcs12SigningKey(crypto.Pfx);
        using var client = new HttpClient(crypto, disposeHandler: false);
        var tsa = new PdfRfc3161TimestampAuthority(client, new()
        {
            Endpoint = new Uri("https://tsa.example.test/rfc3161"), RequireTrustedCertificateChain = false,
        });
        var signer = new PdfPadesSigningService();
        var signed = signer.Sign(plain, key, new() { SigningTime = crypto.Now }).Bytes;
        var certified = signer.Sign(plain, key, new() { SigningTime = crypto.Now, Certification = PdfCertificationLevel.NoChangesAllowed }).Bytes;
        var context = new PdfParseContext(certified, new());
        var graph = new PdfObjectParser(context, new PdfCrossReferenceParser().Parse(context)).ParseDocumentGraph();
        var content = graph.Objects.Values.First(o => o.Value is PdfStreamObject);
        var changed = new PdfIncrementalUpdateWriter().Append(certified,
            [new(content.Id, new PdfStreamObject(new PdfDictionary(), "BT /F1 12 Tf (changed) Tj ET"u8.ToArray()))]).Bytes;
        var longTerm = (await signer.SignAsync(plain, key, new()
        {
            SigningTime = crypto.Now, Profile = PdfPadesProfile.BaselineLT,
            LongTermValidation = new()
            {
                Certificates = [crypto.Certificate.RawData, crypto.Issuer.RawData, crypto.TimestampCertificate.RawData],
                TrustAnchors = [crypto.Issuer.RawData, crypto.TimestampCertificate.RawData],
                Crls = [crypto.Crl], OcspResponses = [crypto.Ocsp],
            },
        }, tsa)).Bytes;
        var fixtureHttpRequests = crypto.Requests;
        var tampered = signed.ToArray();
        tampered[7] = tampered[7] == (byte)'7' ? (byte)'6' : (byte)'7';
        var options = new PdfTrustInspectionOptions
        {
            SignatureValidationPolicy = new()
            {
                UseSystemTrustStore = false, CustomTrustAnchors = [crypto.Issuer, crypto.TimestampCertificate],
                IntermediateCertificates = [crypto.Issuer],
            },
        };
        var inspector = new PdfTrustInspector();
        var cases = new (string Name, byte[] Bytes)[]
        {
            ("plain", plain), ("aes256", encrypted), ("pdfa2b", archive2), ("pdfa3b", archive3),
            ("signed", signed), ("certified", certified), ("certification-violation", changed),
            ("long-term-evidence", longTerm), ("tampered-signature", tampered), ("malformed", [1, 2, 3]),
        };
        var reports = new Dictionary<string, PdfTrustReport>();
        foreach (var (name, bytes) in cases)
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var baselineInput = new MemoryStream(bytes);
            var baseline = await inspector.InspectAsync(baselineInput, options);
            reports[name] = baseline;
            foreach (var asynchronous in new[] { false, true })
            {
                var phase = name + (asynchronous ? ":async" : ":sync");
                PvadLicense.LoadFromFile(licenses[0]);
                var hash = SHA256.HashData(bytes);
                using var input = new SwitchingStream(bytes, () => Replace(phase), asynchronous);
                var report = await inspector.InspectAsync(input, options);
                Check(input.Reads > 0 && input.Changes == 1, phase + ":replacement-inside-read");
                Check(EqualReport(report, baseline), phase + ":same-complete-trust-report");
                Check(input.CanRead && hash.SequenceEqual(SHA256.HashData(bytes)), phase + ":source-and-ownership-preserved");
                using var next = new SwitchingStream(bytes, () => throw new Exception("Denied inspection read its input."), asynchronous);
                await Denied(() => inspector.InspectAsync(next, options), phase + ":next-inspection-denied");
                Check(next.Reads == 0 && next.CanRead, phase + ":denial-before-io");
            }
        }
        Check(reports["plain"].StructureStatus == PdfTrustStatus.Valid, "plain:structure-valid");
        Check(reports["aes256"].Encryption is { IsEncrypted: true, KeyLengthBits: 256 }, "aes256:encryption-status-preserved");
        Check(reports["pdfa2b"].PdfA.Status == PdfTrustStatus.Valid && reports["pdfa3b"].PdfA.Status == PdfTrustStatus.Valid, "archive:structural-validation-preserved");
        Check(reports["signed"].Signatures.Single().IntegrityStatus == PdfTrustStatus.Valid, "signed:integrity-valid");
        Check(reports["certified"].Signatures.Single().CertificationStatus == PdfTrustStatus.Valid, "certified:policy-valid");
        Check(reports["certification-violation"].Signatures.Single().CertificationStatus == PdfTrustStatus.Invalid &&
            reports["certification-violation"].Diagnostics.Any(d => d.Code == "PVADPDFTRUST147"), "certified:disallowed-change-still-invalid");
        Check(reports["tampered-signature"].Signatures.Single().IntegrityStatus == PdfTrustStatus.Invalid, "tampered:integrity-still-invalid");
        Check(reports["malformed"].StructureStatus == PdfTrustStatus.Invalid, "malformed:not-misreported-as-license-error");
        Check(reports["long-term-evidence"].EmbeddedValidationMaterial is { CrlCount: 1, OcspResponseCount: 1 } &&
            reports["long-term-evidence"].Signatures.Single().HistoricalRevocationStatus == PdfTrustStatus.Valid,
            "embedded:crl-ocsp-historical-verification-preserved");

        foreach (var strategy in Enum.GetValues<PdfSaveStrategy>())
        foreach (var mutation in Enum.GetValues<PdfMutationKind>())
        {
            var phase = $"impact:{strategy}:{mutation}";
            PvadLicense.LoadFromFile(licenses[0]);
            var analyzer = new PdfChangeImpactAnalyzer();
            var source = reports["signed"] with { PdfA = reports["pdfa2b"].PdfA, PdfUa = new("PDF/UA", "1", true, PdfTrustStatus.Indeterminate, "synthetic") };
            var baseline = analyzer.Analyze(source, strategy, mutation);
            var report = analyzer.Analyze(source with { Signatures = new SwitchingList<PdfSignatureInspection>(source.Signatures, () => Replace(phase)) }, strategy, mutation);
            Check(JsonSerializer.Serialize(report) == JsonSerializer.Serialize(baseline), phase + ":same-impact-and-diagnostics");
            Check(report.RequiresExplicitConfirmation && report.Signatures.Single().Status ==
                (strategy == PdfSaveStrategy.FullRewrite ? PdfChangeImpactStatus.Invalidated : PdfChangeImpactStatus.RequiresValidation), phase + ":no-false-preservation");
            Check(report.ComplianceClaims.Count == 2 && report.ComplianceClaims.All(c => c.Status == PdfChangeImpactStatus.RequiresValidation), phase + ":claims-require-validation");
            await Denied(() => Task.FromResult(analyzer.Analyze(source, strategy, mutation)), phase + ":new-analysis-denied");
        }

        foreach (var kind in new[] { "crl", "historical-crl", "ocsp", "historical-ocsp" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var memory = new SwitchingMemory(kind.Contains("ocsp", StringComparison.Ordinal) ? crypto.Ocsp : crypto.Crl, () => Replace(kind));
            PdfRevocationEvidence Validate(ReadOnlyMemory<byte> m) => kind switch
            {
                "crl" => PdfCrlEvidenceValidator.Validate(m, crypto.Certificate, crypto.Issuer, crypto.Now, "synthetic"),
                "historical-crl" => PdfCrlEvidenceValidator.ValidateHistorical(m, crypto.Certificate, crypto.Issuer, crypto.Now, "synthetic"),
                "ocsp" => PdfOcspEvidence.ValidateResponse(m, crypto.Certificate, crypto.Issuer, crypto.Now, TimeSpan.FromHours(24), "synthetic"),
                _ => PdfOcspEvidence.ValidateHistoricalResponse(m, crypto.Certificate, crypto.Issuer, crypto.Now, TimeSpan.FromHours(24), "synthetic"),
            };
            var result = Validate(memory.Memory);
            Check(result.Status == PdfTrustStatus.Valid && memory.Reads > 0, kind + ":admitted-validator-completes");
            await Denied(() => Task.FromResult(Validate(memory.Memory)), kind + ":public-validator-stays-fresh");
        }

        foreach (var failure in new[] { "limit", "io", "cancel", "license-callback" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            using var cancellation = new CancellationTokenSource();
            using var input = new SwitchingStream(plain, () =>
            {
                Replace(failure);
                if (failure == "io") throw new IOException("Synthetic Trust input failure.");
                if (failure == "cancel") cancellation.Cancel();
                if (failure == "license-callback") PvadLicense.Require(PvadLicenseProducts.Importer);
            }, true);
            try
            {
                await inspector.InspectAsync(input, failure == "limit" ? options with { MaximumFileSizeBytes = 1 } : options, cancellation.Token);
                throw new Exception("Expected Trust failure: " + failure);
            }
            catch (Exception e) when (failure switch
            {
                "limit" => e is InvalidDataException,
                "io" => e is IOException,
                "cancel" => e is OperationCanceledException,
                _ => e is PvadLicenseException { Code: "PVADLIC012" },
            }) { Check(input.Changes == 1 && input.CanRead, failure + ":original-failure-and-ownership"); }
            await Denied(() => inspector.InspectAsync(input, options), failure + ":failed-operation-grant-not-reusable");
        }

        foreach (var mode in new[] { PdfCertificateRevocationMode.Offline, PdfCertificateRevocationMode.Online })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var provider = new SwitchingEvidenceProvider(() => Replace("revocation:" + mode), crypto);
            using var input = new MemoryStream(signed);
            var result = await inspector.InspectAsync(input, options with
            {
                SignatureValidationPolicy = options.SignatureValidationPolicy with { RevocationMode = mode, RevocationEvidenceProvider = provider },
            });
            Check(provider.Calls > 0 && result.Signatures.Single().RevocationStatus == PdfTrustStatus.Valid, mode + ":custom-provider-result-preserved");
            using var next = new MemoryStream(signed);
            await Denied(() => inspector.InspectAsync(next, options), mode + ":callback-does-not-grant-new-inspection");
        }
        foreach (var allow in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var provider = new SwitchingIssuerProvider(() => Replace("issuer"), crypto);
            using var input = new MemoryStream(signed);
            var result = await inspector.InspectAsync(input, options with
            {
                SignatureValidationPolicy = options.SignatureValidationPolicy with { AllowCertificateDownloads = allow, IssuerCertificateProvider = provider },
            });
            Check((provider.Calls > 0) == allow && result.Signatures.Single().CertificateStatus == PdfTrustStatus.Valid,
                "issuer:" + allow + ":configured-provider-policy-preserved");
            if (allow)
            {
                using var next = new MemoryStream(signed);
                await Denied(() => inspector.InspectAsync(next, options), "issuer:callback-does-not-grant-new-inspection");
            }
        }
        PvadLicense.LoadFromFile(licenses[0]);
        using var finalInput = new MemoryStream(signed);
        await inspector.InspectAsync(finalInput, options);
        Check(fixtureHttpRequests > 0 && crypto.Requests == fixtureHttpRequests, "inspection:no-unconfigured-http");
        await Denied(() => Task.FromResult(new PdfDocument()), "inspection:no-public-pdf-dependency-rights");
        return checks.Count - start;

        void Replace(string name)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { new PdfChangeImpactAnalyzer().Analyze(reports.GetValueOrDefault("plain")!, PdfSaveStrategy.FullRewrite, PdfMutationKind.ModifyMetadata); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name + ":reentrant-public-call-denied"); return; }
            throw new Exception("Callback inherited an admission: " + name);
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Trust continuation assertion failed: " + name);
            checks.Add("trust-continuation:" + name);
        }
        async Task Denied(Func<Task> call, string name)
        {
            try { await call(); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected Trust denial: " + name);
        }
    }

    private static bool EqualReport(PdfTrustReport left, PdfTrustReport right) =>
        JsonSerializer.Serialize(left with { PdfA = left.PdfA with { ValidatedAtUtc = null }, PdfUa = left.PdfUa with { ValidatedAtUtc = null } }) ==
        JsonSerializer.Serialize(right with { PdfA = right.PdfA with { ValidatedAtUtc = null }, PdfUa = right.PdfUa with { ValidatedAtUtc = null } });

    private sealed class SwitchingStream(byte[] bytes, Action change, bool asynchronous) : Stream
    {
        private readonly MemoryStream source = new(bytes, writable: false);
        internal int Reads { get; private set; }
        internal int Changes { get; private set; }
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        private void Before() { Reads++; if (Changes == 0) { Changes++; change(); } }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (asynchronous) await Task.Yield();
            Before();
            return await source.ReadAsync(buffer, cancellationToken);
        }
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

    private sealed class SwitchingMemory(byte[] bytes, Action change) : MemoryManager<byte>
    {
        public int Reads { get; private set; }
        public override Memory<byte> Memory => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan() { if (Reads++ == 0) change(); return bytes; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }

    private sealed class SwitchingEvidenceProvider(Action change, TrustCryptoFixtures crypto) : IPdfRevocationEvidenceProvider
    {
        internal int Calls { get; private set; }
        public async Task<PdfRevocationEvidenceResult> GetEvidenceAsync(PdfRevocationEvidenceRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            Calls++;
            change();
            return new(PdfTrustStatus.Valid, [new(PdfRevocationEvidenceKind.Crl, PdfTrustStatus.Valid, "synthetic", crypto.Now.AddHours(-1), crypto.Now.AddHours(1), crypto.Crl)]);
        }
    }

    private sealed class SwitchingIssuerProvider(Action change, TrustCryptoFixtures crypto) : IPdfIssuerCertificateProvider
    {
        internal int Calls { get; private set; }
        public async Task<PdfIssuerCertificateResult> GetIssuerCertificatesAsync(PdfIssuerCertificateRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            Calls++;
            change();
            return new([crypto.Issuer.RawData]);
        }
    }
}
