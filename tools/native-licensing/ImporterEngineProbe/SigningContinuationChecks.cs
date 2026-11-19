using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Licensing;
using PVAD.Pdf;

internal static class SigningContinuationChecks
{
    internal static async Task<int> Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        PvadLicense.LoadFromFile(licenses[1]);
        var document = new PdfDocument();
        document.AddPage(300, 180).DrawText("Synthetic signing admission", 30, 100);
        var plain = document.ToBytes();
        var encrypted = document.ToBytes(new() { Encryption = new() { OwnerPassword = "synthetic-owner", UserPassword = "synthetic-reader" } });
        PvadLicense.LoadFromFile(licenses[0]);
        using var crypto = new TrustCryptoFixtures();
        using var key = new PdfPkcs12SigningKey(crypto.Pfx);
        using var client = new HttpClient(crypto, disposeHandler: false);
        var timestamp = new PdfRfc3161TimestampAuthority(client, new()
        {
            Endpoint = new Uri("https://tsa.example.test/rfc3161"), RequireTrustedCertificateChain = false,
        });
        var signer = new PdfPadesSigningService();
        var material = new PdfLongTermValidationMaterial
        {
            Certificates = [crypto.Certificate.RawData, crypto.Issuer.RawData, crypto.TimestampCertificate.RawData],
            TrustAnchors = [crypto.Issuer.RawData, crypto.TimestampCertificate.RawData],
            Crls = [crypto.Crl], OcspResponses = [crypto.Ocsp],
        };
        var inspection = new PdfTrustInspectionOptions
        {
            SignatureValidationPolicy = new()
            {
                UseSystemTrustStore = false, CustomTrustAnchors = [crypto.Issuer, crypto.TimestampCertificate],
                IntermediateCertificates = [crypto.Issuer],
            },
        };
        foreach (var profile in Enum.GetValues<PdfPadesProfile>())
        foreach (var digest in Enum.GetValues<PdfSignatureDigestAlgorithm>())
        foreach (var trigger in profile == PdfPadesProfile.BaselineB ? new[] { "source", "sync-source" } :
            profile == PdfPadesProfile.BaselineT ? new[] { "source", "http" } : new[] { "source", "http", "material" })
        {
            var phase = $"{profile}:{digest}:{trigger}";
            PvadLicense.LoadFromFile(licenses[0]);
            var sourceHash = SHA256.HashData(plain);
            var changes = 0;
            void Change()
            {
                if (changes++ != 0) return;
                Replace(phase);
            }
            crypto.BeforeRequest = trigger == "http" ? Change : null;
            var provider = new MaterialProvider(material, trigger == "material" ? Change : () => { });
            var options = new PdfSigningOptions
            {
                FieldName = "SyntheticApproval", SigningTime = crypto.Now, Profile = profile, DigestAlgorithm = digest,
                Appearance = new() { X = 10, Y = 10, Width = 150, Height = 40 },
                LongTermValidationProvider = profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA ? provider : null,
            };
            using var memory = new SwitchingMemory(plain, trigger.EndsWith("source", StringComparison.Ordinal) ? Change : () => { });
            PdfSigningResult result;
            try
            {
                result = trigger == "sync-source" ? signer.Sign(memory.Memory, key, options) :
                    await signer.SignAsync(memory.Memory, key, options, profile == PdfPadesProfile.BaselineB ? null : timestamp);
            }
            finally { crypto.BeforeRequest = null; }
            Check(changes > 0 && memory.Reads > 0, phase + ":replacement-inside-admitted-processing");
            Check(result.Bytes.AsSpan().StartsWith(plain) && result.Profile == profile &&
                result.DigestAlgorithm == (digest == PdfSignatureDigestAlgorithm.Sha256 ? "SHA-256" : digest == PdfSignatureDigestAlgorithm.Sha384 ? "SHA-384" : "SHA-512"), phase + ":result-contract");
            Check(sourceHash.SequenceEqual(SHA256.HashData(plain)) && memory.Memory.Span.SequenceEqual(plain), phase + ":original-source-preserved");
            await Denied(() => signer.SignAsync(plain, key), phase + ":next-sign-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            using var input = new MemoryStream(result.Bytes);
            var report = await new PdfTrustInspector().InspectAsync(input, inspection);
            var signature = report.Signatures.Single(s => !s.IsDocumentTimestamp);
            Check(signature.IntegrityStatus == PdfTrustStatus.Valid && signature.CertificateStatus == PdfTrustStatus.Valid &&
                signature.PadesProfile == (profile == PdfPadesProfile.BaselineB ? "B-B" : profile == PdfPadesProfile.BaselineT ? "B-T" : profile == PdfPadesProfile.BaselineLT ? "B-LT" : "B-LTA"),
                phase + ":cryptographic-integrity-trust-and-requested-profile");
            Check(report.RevisionCount == result.RevisionCount && report.Signatures.Count == (profile == PdfPadesProfile.BaselineLTA ? 2 : 1), phase + ":revision-and-signature-count");
            if (profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA)
                Check(provider.Calls == 1 && signature.HistoricalRevocationStatus == PdfTrustStatus.Valid, phase + ":provider-and-embedded-evidence");
            if (profile == PdfPadesProfile.BaselineLTA)
                Check(signature.ArchiveTimestampStatus == PdfTrustStatus.Valid && result.ArchiveTimestampTime == crypto.Now, phase + ":archive-timestamp");
        }

        foreach (var trigger in new[] { "source", "http" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            crypto.BeforeRequest = trigger == "http" ? () => Replace("document-timestamp:http") : null;
            using var memory = new SwitchingMemory(plain, trigger == "source" ? () => Replace("document-timestamp:source") : () => { });
            PdfDocumentTimestampResult result;
            try { result = await new PdfDocumentTimestampService().AddAsync(memory.Memory, timestamp); }
            finally { crypto.BeforeRequest = null; }
            Check(result.Bytes.AsSpan().StartsWith(plain) && result.RevisionCount == 2, "document-timestamp:" + trigger + ":incremental-output");
            await Denied(() => new PdfDocumentTimestampService().AddAsync(plain, timestamp), "document-timestamp:" + trigger + ":next-call-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            using var input = new MemoryStream(result.Bytes);
            var signature = (await new PdfTrustInspector().InspectAsync(input, inspection)).Signatures.Single();
            Check(signature.IsDocumentTimestamp && signature.IntegrityStatus == PdfTrustStatus.Valid, "document-timestamp:" + trigger + ":verified");
        }
        foreach (var algorithm in new[] { HashAlgorithmName.SHA256, HashAlgorithmName.SHA384, HashAlgorithmName.SHA512 })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var digest = algorithm == HashAlgorithmName.SHA256 ? SHA256.HashData(plain) : algorithm == HashAlgorithmName.SHA384 ? SHA384.HashData(plain) : SHA512.HashData(plain);
            using var memory = new SwitchingMemory(digest, () => Replace("timestamp:" + algorithm.Name));
            var token = await timestamp.GetTimestampTokenAsync(memory.Memory, algorithm);
            var cms = new SignedCms(); cms.Decode(token.Cms); cms.CheckSignature(verifySignatureOnly: true);
            Check(memory.Reads > 0 && token.Timestamp == crypto.Now && token.AuthorityThumbprint == crypto.TimestampCertificate.Thumbprint,
                "timestamp:" + algorithm.Name + ":token-verified");
            await Denied(() => timestamp.GetTimestampTokenAsync(digest, algorithm), "timestamp:" + algorithm.Name + ":retained-authority-denied");
        }

        foreach (var kind in new[] { "custom-key", "custom-timestamp", "material-provider" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var custom = new DelegatingKey(key, () => Replace(kind));
            var customTimestamp = new DelegatingTimestamp(timestamp, () => Replace(kind));
            var provider = new MaterialProvider(material, () => { Replace(kind); PvadLicense.Require(PvadLicenseProducts.Importer); });
            var before = crypto.Requests;
            var options = new PdfSigningOptions { SigningTime = crypto.Now,
                Profile = kind == "custom-timestamp" ? PdfPadesProfile.BaselineT : kind == "material-provider" ? PdfPadesProfile.BaselineLT : PdfPadesProfile.BaselineB,
                LongTermValidationProvider = kind == "material-provider" ? provider : null };
            await Denied(() => signer.SignAsync(plain, kind == "custom-key" ? custom : key,
                options, kind == "custom-timestamp" ? customTimestamp : timestamp), kind + ":public-callback-cannot-inherit-admission");
            Check(kind switch { "custom-key" => custom.Calls == 1,
                "custom-timestamp" => customTimestamp.Calls == 1, _ => provider.Calls == 1 }, kind + ":interface-dispatch-preserved");
            if (kind != "material-provider") Check(crypto.Requests == before, kind + ":denied-before-http");
        }

        foreach (var failure in new[] { "malformed", "encrypted", "cancel", "http" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var bytes = failure == "malformed" ? new byte[] { 1, 2, 3 } : failure == "encrypted" ? encrypted : plain;
            using var cancellation = new CancellationTokenSource();
            using var memory = new SwitchingMemory(bytes, () =>
            {
                Replace("failure:" + failure);
                if (failure == "cancel") cancellation.Cancel();
            });
            crypto.BeforeRequest = failure == "http" ? () => throw new HttpRequestException("Synthetic timestamp transport failure.") : null;
            try
            {
                await signer.SignAsync(memory.Memory, key, new() { SigningTime = crypto.Now,
                    Profile = failure == "http" ? PdfPadesProfile.BaselineT : PdfPadesProfile.BaselineB }, timestamp, cancellation.Token);
                throw new Exception("Expected signing failure: " + failure);
            }
            catch (Exception e) when (failure switch
            {
                "malformed" => e is InvalidDataException,
                "encrypted" => e is NotSupportedException or InvalidOperationException,
                "cancel" => e is OperationCanceledException,
                _ => e is PdfTimestampAuthorityException { InnerException: HttpRequestException },
            }) { Check(memory.Reads > 0 && memory.Memory.Span.SequenceEqual(bytes), failure + ":validation-and-source-preserved"); }
            finally { crypto.BeforeRequest = null; }
            await Denied(() => signer.SignAsync(plain, key), failure + ":failed-admission-not-reusable");
        }
        PvadLicense.LoadFromFile(licenses[0]);
        var signedHash = key.SignHash(SHA256.HashData(plain), HashAlgorithmName.SHA256);
        using var rsa = crypto.Certificate.GetRSAPublicKey()!;
        Check(rsa.VerifyHash(SHA256.HashData(plain), signedHash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), "key:public-valid-after-renewal");
        await Denied(() => Task.FromResult(new PdfDocument()), "signing:no-public-pdf-rights");
        return checks.Count - start;

        void Replace(string phase)
        {
            PvadLicense.LoadFromFile(licenses[1]);
            try { signer.SignAsync(plain, key); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, phase + ":synchronous-reentrant-denial"); return; }
            throw new Exception("SignAsync did not retain synchronous public denial: " + phase);
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Signing continuation assertion failed: " + name);
            checks.Add("signing-continuation:" + name);
        }
        async Task Denied(Func<Task> call, string name)
        {
            try { await call(); }
            catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected signing denial: " + name);
        }
    }

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

    private sealed class DelegatingKey(PdfPkcs12SigningKey key, Action change) : IPdfSigningKey
    {
        internal int Calls { get; private set; }
        public X509Certificate2 Certificate => key.Certificate;
        public byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName algorithm)
        { Calls++; change(); return key.SignHash(hash, algorithm); }
    }

    private sealed class DelegatingTimestamp(IPdfTimestampAuthority authority, Action change) : IPdfTimestampAuthority
    {
        internal int Calls { get; private set; }
        public Task<PdfTimestampToken> GetTimestampTokenAsync(ReadOnlyMemory<byte> hash, HashAlgorithmName algorithm, CancellationToken cancellationToken = default)
        { Calls++; change(); return authority.GetTimestampTokenAsync(hash, algorithm, cancellationToken); }
    }

    private sealed class MaterialProvider(PdfLongTermValidationMaterial material, Action change) : IPdfLongTermValidationMaterialProvider
    {
        internal int Calls { get; private set; }
        public async Task<PdfLongTermValidationMaterial> GetMaterialAsync(PdfLongTermValidationContext context, CancellationToken cancellationToken = default)
        { await Task.Yield(); Calls++; change(); return material; }
    }
}
