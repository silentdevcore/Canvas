using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PVAD.Importer;
using PVAD.Importer.EInvoicing;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Redaction;
using PVAD.Importer.Saving;
using PVAD.Importer.Security;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Licensing;
using PVAD.Pdf;

internal sealed class TrustSecurityChecks : IDisposable
{
    private readonly Dictionary<string, Action> calls = [];
    private readonly HashSet<string> retained = [];
    private readonly TrustCryptoFixtures crypto = new();
    private readonly HttpClient client;
    private readonly byte[] source;
    private readonly PdfTrustInspector inspector = new();
    private readonly PdfA2bConverter converter;
    private readonly PdfRedactionPreflight preflight = new();
    private readonly PdfRedactionApplier redactor;
    private readonly PdfPadesSigningService signer = new();
    private readonly PdfEInvoiceAttacher attacher;
    private readonly CountingKey callbackKey;
    private PdfPkcs12SigningKey? key;
    private PdfStandardSecurityHandler? security;
    private PdfDictionary encryption = new();
    private byte[] ciphertext = [];
    private byte[] encrypted = [];
    private byte[] certificateEncrypted = [];
    private byte[] convertible = [];
    private byte[] archive3 = [];
    private PdfParseContext parseContext = null!;
    private PdfCrossReferenceTable xref = null!;
    private PdfObjectGraph graph = null!;
    private PdfTrustReport report = null!;
    private PdfRedactionAuditRecord audit = null!;
    private bool denying = true;

    internal TrustSecurityChecks(byte[] pdf)
    {
        source = pdf;
        client = new HttpClient(crypto, disposeHandler: false);
        var authority = new PdfRfc3161TimestampAuthority(client, new()
        {
            Endpoint = new Uri("https://tsa.example.test/rfc3161"), RequireTrustedCertificateChain = false
        });
        converter = new(inspector);
        redactor = new(preflight);
        attacher = new(converter);
        callbackKey = new(() => key!);
        var regions = new[] { new PdfRedactionRegion(1, new(0, 0, 300, 180)) };
        var payload = new PdfEInvoiceAttachmentPayload { Xml = "<Invoice><ID>synthetic</ID></Invoice>"u8.ToArray(), FileName = "synthetic-invoice.xml" };
        Add<PdfTrustInspector>("InspectAsync", () => Read(source, stream => Equal(Await(inspector.InspectAsync(stream)).StructureStatus == PdfTrustStatus.Valid)));
        Add<PdfChangeImpactAnalyzer>("Analyze", () => new PdfChangeImpactAnalyzer().Analyze(report, PdfSaveStrategy.FullRewrite, PdfMutationKind.ModifyMetadata));
        Add<PdfA2bConverter>("PreflightAsync", () => Read(source, stream => Equal(Await(converter.PreflightAsync(stream)).OriginalPreserved)));
        Add<PdfA2bConverter>("ConvertAsync", () => Read(convertible, stream => Equal(Await(converter.ConvertAsync(stream)).Validation.Status == PdfTrustStatus.Valid)));
        var pipeline = new PdfTrustPostProcessingPipeline(redactor, signer, inspector);
        Add<PdfTrustPostProcessingPipeline>("ProcessAsync", () => Equal(Await(pipeline.ProcessAsync(source)).Bytes.SequenceEqual(source)));
        Add(typeof(PdfOcspEvidence), "CreateRequest", () => Equal(PdfOcspEvidence.CreateRequest(crypto.Certificate, crypto.Issuer).Length > 0));
        Add(typeof(PdfOcspEvidence), "ValidateResponse", () => Equal(PdfOcspEvidence.ValidateResponse(crypto.Ocsp, crypto.Certificate, crypto.Issuer, crypto.Now, TimeSpan.FromHours(24), "synthetic").Status == PdfTrustStatus.Valid));
        Add(typeof(PdfOcspEvidence), "ValidateHistoricalResponse", () => Equal(PdfOcspEvidence.ValidateHistoricalResponse(crypto.Ocsp, crypto.Certificate, crypto.Issuer, crypto.Now, TimeSpan.FromHours(24), "synthetic").Status == PdfTrustStatus.Valid));
        Add(typeof(PdfCrlEvidenceValidator), "Validate", () => Equal(PdfCrlEvidenceValidator.Validate(crypto.Crl, crypto.Certificate, crypto.Issuer, crypto.Now, "synthetic").Status == PdfTrustStatus.Valid));
        Add(typeof(PdfCrlEvidenceValidator), "ValidateHistorical", () => Equal(PdfCrlEvidenceValidator.ValidateHistorical(crypto.Crl, crypto.Certificate, crypto.Issuer, crypto.Now, "synthetic").Status == PdfTrustStatus.Valid));
        Add(typeof(PdfDocumentDecryptor), "Decrypt", () => PdfDocumentDecryptor.Decrypt(xref, graph, "synthetic-reader"), typeof(PdfCrossReferenceTable), typeof(PdfObjectGraph), typeof(string));
        Add(typeof(PdfDocumentDecryptor), "Decrypt", () => PdfDocumentDecryptor.Decrypt(xref, graph, crypto.Certificate), typeof(PdfCrossReferenceTable), typeof(PdfObjectGraph), typeof(System.Security.Cryptography.X509Certificates.X509Certificate2));
        Add(typeof(PdfDocumentDecryptor), "ParseAndDecrypt", () => PdfDocumentDecryptor.ParseAndDecrypt(parseContext, xref, "synthetic-reader"), typeof(PdfParseContext), typeof(PdfCrossReferenceTable), typeof(string));
        Add(typeof(PdfDocumentDecryptor), "ParseAndDecrypt", () => PdfDocumentDecryptor.ParseAndDecrypt(parseContext, xref, crypto.Certificate), typeof(PdfParseContext), typeof(PdfCrossReferenceTable), typeof(System.Security.Cryptography.X509Certificates.X509Certificate2));
        Add<PdfStandardSecurityHandler>("Create", () => { using var created = PdfStandardSecurityHandler.Create(new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" }); Equal(created.OwnerEntry.Length == 48); });
        Add<PdfStandardSecurityHandler>("Open", () => { using var opened = PdfStandardSecurityHandler.Open(encryption, "synthetic-reader", out var result); Equal(result is not null); });
        Add<PdfStandardSecurityHandler>("EncryptValue", () => Equal(security!.EncryptValue("protected"u8).Length > 0), retainedInstance: true);
        Add<PdfStandardSecurityHandler>("DecryptValue", () => Equal(security!.DecryptValue(ciphertext).AsSpan().SequenceEqual("protected"u8)), retainedInstance: true);
        Add<PdfStandardSecurityHandler>("BuildEncryptDictionary", () => Equal(security!.BuildEncryptDictionary()["Filter"] is PdfName), retainedInstance: true);
        calls.Add("PdfPkcs12SigningKey..ctor", () => { using var created = new PdfPkcs12SigningKey(crypto.Pfx); Equal(created.Certificate.HasPrivateKey); });
        NativeSurfaceCoverage.Record(typeof(PdfPkcs12SigningKey).GetConstructors().Single());
        Add<PdfPkcs12SigningKey>("SignHash", () => Equal(key!.SignHash(SHA256.HashData(source), HashAlgorithmName.SHA256).Length > 0), retainedInstance: true);
        Add<PdfPadesSigningService>("Sign", () => Signed(signer.Sign(source, callbackKey)));
        Add<PdfPadesSigningService>("SignAsync", () => Signed(Await(signer.SignAsync(source, callbackKey))));
        Add<PdfDocumentTimestampService>("AddAsync", () => Equal(Await(new PdfDocumentTimestampService().AddAsync(source, authority)).Bytes.AsSpan().StartsWith(source)));
        Add<PdfRfc3161TimestampAuthority>("GetTimestampTokenAsync", () => Equal(Await(authority.GetTimestampTokenAsync(SHA256.HashData(source), HashAlgorithmName.SHA256)).Cms.Length > 0));
        Add<PdfRedactionPreflight>("PreflightAsync", () => Read(source, stream => Equal(Await(preflight.PreflightAsync(stream, regions)).PageCount == 1)));
        Add<PdfRedactionApplier>("ApplyAsync", () => Read(source, stream =>
        {
            var result = Await(redactor.ApplyAsync(stream, regions));
            Equal(result.Verification.Passed && result.RemovedElementCount > 0);
            audit = result.Audit;
        }));
        Add(typeof(PdfRedactionAuditVerifier), "Verify", () => Equal(PdfRedactionAuditVerifier.Verify(audit)));
        Add<PdfEInvoiceAttacher>("PreflightAsync", () => Read(archive3, stream => Equal(Await(attacher.PreflightAsync(stream, payload)).PageCount == 1)));
        Add<PdfEInvoiceAttacher>("AttachAsync", () => Read(archive3, stream =>
        {
            var result = Await(attacher.AttachAsync(stream, payload));
            Equal(result.PagesUnchanged && result.Bytes.AsSpan().StartsWith(archive3));
            using var output = new MemoryStream(result.Bytes);
            var files = Await(new PdfAssociatedFileExtractor().ExtractAsync(output));
            Equal(files.Single().Content.Span.SequenceEqual(payload.Xml.Span));
        }));
        Add<PdfAssociatedFileExtractor>("ExtractAsync", () => Read(source, stream => Equal(Await(new PdfAssociatedFileExtractor().ExtractAsync(stream)).Count == 0)));

        var types = new[] { typeof(PdfTrustInspector), typeof(PdfChangeImpactAnalyzer), typeof(PdfA2bConverter), typeof(PdfTrustPostProcessingPipeline), typeof(PdfOcspEvidence), typeof(PdfCrlEvidenceValidator), typeof(PdfDocumentDecryptor), typeof(PdfStandardSecurityHandler), typeof(PdfPkcs12SigningKey), typeof(PdfPadesSigningService), typeof(PdfDocumentTimestampService), typeof(PdfRfc3161TimestampAuthority), typeof(PdfRedactionPreflight), typeof(PdfRedactionApplier), typeof(PdfRedactionAuditVerifier), typeof(PdfEInvoiceAttacher), typeof(PdfAssociatedFileExtractor) };
        var free = new[] { "IsEncrypted", "IsSupported", "Dispose" };
        var methods = types.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => !method.IsSpecialName && !free.Contains(method.Name)).Select(Key).Append("PdfPkcs12SigningKey..ctor").ToHashSet();
        Equal(methods.SetEquals(calls.Keys));
        Equal(typeof(PdfPkcs12SigningKey).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(ReadOnlySpan<byte>), typeof(string) }));
    }

    internal void CheckMissing(List<string> checks)
    {
        Equal(!PdfDocumentDecryptor.IsEncrypted(null) && !PdfStandardSecurityHandler.IsSupported(null));
        Equal(PdfCertificateEvidenceLocator.GetOcspUris(crypto.Certificate).Count == 0);
        Equal(PdfCertificateEvidenceLocator.GetIssuerCertificateUris(crypto.Certificate).Count == 0);
        Equal(PdfCertificateEvidenceLocator.GetCrlDistributionPointUris(crypto.Certificate).Count == 0);
        checks.Add("trust:metadata-and-locators-free");
        checks.Add("trust:public-processing-signature-inventory");
        CheckDenials("PVADLIC001", "missing", checks, includeRetained: false);
    }

    internal int CheckLicensed(string[] licenses, List<string> checks)
    {
        // PDF generation is fixture preparation only, never a dependency grant of Importer.
        PvadLicense.LoadFromFile(licenses[1]);
        var document = new PdfDocument { FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) };
        document.AddPage().DrawText("Convertible synthetic text", 40, 40, new PdfDrawTextOptions { Language = "en" });
        convertible = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B });
        archive3 = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
        convertible = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(convertible).Replace("pdfaid:part", "pdfaid:xxxx", StringComparison.Ordinal).Replace("pdfaid:conformance", "pdfaid:xxxxxxxxxxx", StringComparison.Ordinal));
        certificateEncrypted = document.ToBytes(new PdfSaveOptions { CertificateEncryption = new() { Recipients = [crypto.Certificate] } });
        PvadLicense.LoadFromFile(licenses[0]);
        key = new PdfPkcs12SigningKey(crypto.Pfx);
        security = PdfStandardSecurityHandler.Create(new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" });
        encryption = security.BuildEncryptDictionary();
        ciphertext = security.EncryptValue("protected"u8);
        encrypted = new PdfFullRewriteWriter().Rewrite(source, new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } }).Bytes;
        using (var input = new MemoryStream(source)) report = Await(inspector.InspectAsync(input));
        RunPositive("valid", checks);
        var before = SHA256.HashData(source);
        PvadLicense.LoadFromFile(licenses[1]);
        denying = true;
        CheckDenials("PVADLIC012", "wrong", checks, includeRetained: true);
        Denied(() => PvadLicense.LoadFromFile(licenses[2]), "PVADLIC009");
        Denied(calls.First(pair => pair.Key.StartsWith("PdfTrustInspector.", StringComparison.Ordinal)).Value, "PVADLIC012");
        checks.Add("trust:failed-renewal-retains-denial");
        PvadLicense.LoadFromFile(licenses[0]);
        RunPositive("renewed", checks);
        using (var input = new LicenseSwitchingStream(source, () => PvadLicense.LoadFromFile(licenses[1])))
        {
            try
            {
                Equal(Await(inspector.InspectAsync(input)).StructureStatus == PdfTrustStatus.Valid);
                Denied(() => Read(source, stream => Await(inspector.InspectAsync(stream))), "PVADLIC012");
            }
            finally { PvadLicense.LoadFromFile(licenses[0]); }
        }
        checks.Add("trust:admitted-mid-read-inspection-completes-next-public-call-denied");
        Equal(before.SequenceEqual(SHA256.HashData(source)));
        checks.Add("trust:source-unchanged-after-all-phases");
        return calls.Count;
    }

    private void CheckDenials(string code, string phase, List<string> checks, bool includeRetained)
    {
        var beforeRequests = crypto.Requests;
        var beforeCallbacks = callbackKey.Calls;
        var beforeGraph = graph is null ? null : graph.Objects.Values.Select(value => value.Value).ToArray();
        foreach (var (name, call) in calls.Where(pair => includeRetained || !retained.Contains(pair.Key)))
        {
            Denied(call, code); checks.Add($"trust:{phase}:{name}");
        }
        Equal(beforeRequests == crypto.Requests && beforeCallbacks == callbackKey.Calls);
        if (beforeGraph is not null) Equal(beforeGraph.SequenceEqual(graph!.Objects.Values.Select(value => value.Value)));
        checks.Add($"trust:{phase}:no-http-key-callback-or-graph-mutation");
    }

    private void RunPositive(string phase, List<string> checks)
    {
        denying = false;
        foreach (var (name, call) in calls)
        {
            if (name.StartsWith("PdfDocumentDecryptor.", StringComparison.Ordinal))
            {
                var bytes = name.Contains("X509Certificate2", StringComparison.Ordinal) ? certificateEncrypted : encrypted;
                parseContext = new PdfParseContext(bytes, new PdfImporterOptions());
                xref = new PdfCrossReferenceParser().Parse(parseContext);
                graph = new PdfObjectParser(parseContext, xref).ParseDocumentGraph(parseCompressedObjects: false);
            }
            try { call(); }
            catch (Exception error) { throw new InvalidOperationException($"Trust positive check failed: {phase}:{name}", error); }
            checks.Add($"trust:{phase}:{name}");
        }
        Equal(crypto.Requests > 0 && callbackKey.Calls > 0);
    }

    private void Read(byte[] bytes, Action<Stream> call)
    {
        using var stream = new MemoryStream(bytes);
        try { call(stream); }
        finally { if (denying) Equal(stream.CanRead && stream.Position == 0 && stream.ToArray().SequenceEqual(bytes)); }
    }
    private void Signed(PdfSigningResult result)
    {
        Equal(result.Bytes.AsSpan().StartsWith(source));
        using var stream = new MemoryStream(result.Bytes);
        Equal(Await(inspector.InspectAsync(stream)).Signatures.Single().IntegrityStatus == PdfTrustStatus.Valid);
    }
    private void Add<T>(string name, Action call, bool retainedInstance = false) => Add(typeof(T), typeof(T).GetMethod(name)!, call, retainedInstance);
    private void Add(Type type, string name, Action call, params Type[] parameters) => Add(type, parameters.Length == 0 ? type.GetMethod(name)! : type.GetMethod(name, parameters)!, call, false);
    private void Add(Type type, MethodInfo method, Action call, bool retainedInstance)
    {
        if (method is null) throw new InvalidOperationException("Missing public method on " + type.Name);
        NativeSurfaceCoverage.Record(method);
        var name = Key(method); calls.Add(name, call); if (retainedInstance) retained.Add(name);
    }
    private static string Key(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.ToString()))})";
    private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();
    private static void Equal(bool value) { if (!value) throw new InvalidOperationException("Trust/security admission assertion failed."); }
    private static void Denied(Action call, string code)
    {
        try { call(); }
        catch (PvadLicenseException error) when (error.Code == code) { return; }
        throw new InvalidOperationException("Expected trust/security denial: " + code);
    }
    public void Dispose() { security?.Dispose(); key?.Dispose(); client.Dispose(); crypto.Dispose(); }
    private sealed class CountingKey(Func<PdfPkcs12SigningKey> resolve) : IPdfSigningKey
    {
        internal int Calls { get; private set; }
        public System.Security.Cryptography.X509Certificates.X509Certificate2 Certificate { get { Calls++; return resolve().Certificate; } }
        public byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName algorithm) { Calls++; return resolve().SignHash(hash, algorithm); }
    }
    private sealed class LicenseSwitchingStream(byte[] bytes, Action replace) : MemoryStream(bytes, writable: false)
    {
        private bool switched;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!switched) { switched = true; replace(); }
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
