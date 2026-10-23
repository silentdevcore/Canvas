using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PVAD.Importer.Graphics;
using PVAD.Importer.Redaction;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

public sealed class PdfTrustPostProcessingPipelineTests
{
    [Fact]
    public async Task ValidationOnly_ReturnsTheExactInputAndFinalTrustReport()
    {
        var bytes = CreatePdf();
        var pipeline = CreatePipeline();

        var result = await pipeline.ProcessAsync(bytes);

        Assert.Equal(bytes, result.Bytes);
        Assert.Equal([PdfTrustPipelineStep.Validation], result.CompletedSteps);
        Assert.Equal(PdfTrustStatus.Valid, result.TrustReport.StructureStatus);
        Assert.Null(result.Redaction);
        Assert.Null(result.Signing);
    }

    [Fact]
    public async Task ValidationPolicy_FailsClosedWhenRequiredPdfAClaimIsMissing()
    {
        var exception = await Assert.ThrowsAsync<PdfTrustPipelineValidationException>(() =>
            CreatePipeline().ProcessAsync(
                CreatePdf(),
                new PdfTrustPostProcessingOptions
                {
                    Validation = new PdfTrustPipelineValidationPolicy { RequirePdfA = true }
                }));

        Assert.Equal("PXAPDFPIPELINE201", exception.Code);
        Assert.NotNull(exception.Report);
    }

    [Fact]
    public async Task PlanValidation_RejectsMissingSigningKeyAndEmptyRedaction()
    {
        var pipeline = CreatePipeline();
        var signing = await Assert.ThrowsAsync<PdfTrustPipelineValidationException>(() =>
            pipeline.ProcessAsync(CreatePdf(), new PdfTrustPostProcessingOptions
            {
                Signing = new PdfTrustSigningOptions()
            }));
        var redaction = await Assert.ThrowsAsync<PdfTrustPipelineValidationException>(() =>
            pipeline.ProcessAsync(CreatePdf(), new PdfTrustPostProcessingOptions
            {
                Redaction = new PdfTrustRedactionOptions { Regions = [] }
            }));

        Assert.Equal("PXAPDFPIPELINE101", signing.Code);
        Assert.Equal("PXAPDFPIPELINE100", redaction.Code);
    }

    [Fact]
    public async Task Pipeline_AppliesRedactionBeforeSigningAndValidatesFinalBytes()
    {
        var calls = new List<string>();
        var redaction = new RecordingRedactionApplier(calls);
        var signing = new RecordingSigningService(calls);
        var pipeline = new PdfTrustPostProcessingPipeline(redaction, signing, new PdfTrustInspector());
        using var key = new TestSigningKey();

        var result = await pipeline.ProcessAsync(
            CreatePdf(),
            new PdfTrustPostProcessingOptions
            {
                Redaction = new PdfTrustRedactionOptions
                {
                    Regions = [new PdfRedactionRegion(1, new PdfRectangle(10, 10, 20, 20))]
                },
                Signing = new PdfTrustSigningOptions
                {
                    CertificateSource = new PdfSigningCertificateSource
                    {
                        Kind = PdfSigningCertificateSourceKind.OrganizationKeyProvider,
                        ProviderId = "org-provider",
                        KeyId = "document-key"
                    }
                }
            },
            new PdfTrustPipelineContext { DocumentSigningKey = key });

        Assert.Equal(["redaction", "signing"], calls);
        Assert.Equal(
            [PdfTrustPipelineStep.Redaction, PdfTrustPipelineStep.Signing, PdfTrustPipelineStep.Validation],
            result.CompletedSteps);
        Assert.Equal(PdfTrustStatus.Valid, result.TrustReport.StructureStatus);
    }

    [Fact]
    public void SerializablePlan_DoesNotExposeCredentialProperties()
    {
        var planTypes = new[]
        {
            typeof(PdfTrustPostProcessingOptions),
            typeof(PdfTrustSigningOptions),
            typeof(PdfTrustRedactionOptions),
            typeof(PdfTrustPipelineValidationPolicy)
        };
        var properties = planTypes
            .SelectMany(type => type.GetProperties())
            .ToArray();

        Assert.DoesNotContain(properties, property =>
            property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Pkcs12", StringComparison.OrdinalIgnoreCase) ||
            property.PropertyType == typeof(byte[]) ||
            typeof(IPdfSigningKey).IsAssignableFrom(property.PropertyType) ||
            typeof(IPdfTimestampAuthority).IsAssignableFrom(property.PropertyType) ||
            typeof(IPdfLongTermValidationMaterialProvider).IsAssignableFrom(property.PropertyType));
    }

    [Theory]
    [InlineData(PdfPadesProfile.BaselineT, "PXAPDFPIPELINE105")]
    [InlineData(PdfPadesProfile.BaselineLT, "PXAPDFPIPELINE105")]
    public async Task PlanValidation_RejectsMissingRuntimeTrustServices(
        PdfPadesProfile profile,
        string expectedCode)
    {
        using var key = new TestSigningKey();

        var exception = await Assert.ThrowsAsync<PdfTrustPipelineValidationException>(() =>
            CreatePipeline().ProcessAsync(
                CreatePdf(),
                new PdfTrustPostProcessingOptions
                {
                    Signing = new PdfTrustSigningOptions { Profile = profile }
                },
                new PdfTrustPipelineContext { DocumentSigningKey = key }));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task PlanValidation_RejectsContradictoryTimestampPolicyBeforeSigning()
    {
        using var key = new TestSigningKey();

        var exception = await Assert.ThrowsAsync<PdfTrustPipelineValidationException>(() =>
            CreatePipeline().ProcessAsync(
                CreatePdf(),
                new PdfTrustPostProcessingOptions
                {
                    Signing = new PdfTrustSigningOptions
                    {
                        Profile = PdfPadesProfile.BaselineB,
                        Timestamp = new PdfTimestampPolicy { Required = true }
                    }
                },
                new PdfTrustPipelineContext { DocumentSigningKey = key }));

        Assert.Equal("PXAPDFPIPELINE104", exception.Code);
    }

    private static PdfTrustPostProcessingPipeline CreatePipeline()
    {
        var preflight = new PdfRedactionPreflight();
        return new PdfTrustPostProcessingPipeline(
            new PdfRedactionApplier(preflight),
            new PdfPadesSigningService(),
            new PdfTrustInspector());
    }

    private static byte[] CreatePdf()
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        document.AddPage().DrawTextFromTop("Trust pipeline", 36, 36, 12);
        return document.ToBytes();
    }

    private sealed class RecordingRedactionApplier(List<string> calls) : IPdfRedactionApplier
    {
        public async Task<PdfRedactionApplyResult> ApplyAsync(
            Stream pdfStream,
            IReadOnlyList<PdfRedactionRegion> regions,
            PdfRedactionApplyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            calls.Add("redaction");
            using var output = new MemoryStream();
            await pdfStream.CopyToAsync(output, cancellationToken);
            var bytes = output.ToArray();
            var preflight = new PdfRedactionPreflightReport(
                PdfRedactionCapability.Removable, "source", bytes.Length, 1, 1, [], []);
            var verification = new PdfRedactionVerificationReport(true, 0, 0, []);
            var audit = new PdfRedactionAuditRecord(
                "source", "result", DateTimeOffset.UnixEpoch, options?.Actor, options?.Reason,
                regions, [], true, "record");
            return new PdfRedactionApplyResult(bytes, preflight, 0, 0, [1], verification, audit);
        }
    }

    private sealed class RecordingSigningService(List<string> calls) : IPdfPadesSigningService
    {
        public PdfSigningResult Sign(
            ReadOnlyMemory<byte> source,
            IPdfSigningKey signingKey,
            PdfSigningOptions? options = null) => Result(source, signingKey, options);

        public Task<PdfSigningResult> SignAsync(
            ReadOnlyMemory<byte> source,
            IPdfSigningKey signingKey,
            PdfSigningOptions? options = null,
            IPdfTimestampAuthority? timestampAuthority = null,
            CancellationToken cancellationToken = default)
        {
            calls.Add("signing");
            return Task.FromResult(Result(source, signingKey, options));
        }

        private static PdfSigningResult Result(
            ReadOnlyMemory<byte> source,
            IPdfSigningKey signingKey,
            PdfSigningOptions? options) => new(
                source.ToArray(),
                options?.Profile ?? PdfPadesProfile.BaselineB,
                options?.FieldName ?? "Signature1",
                "SHA256",
                signingKey.Certificate.Subject,
                signingKey.Certificate.Thumbprint,
                DateTimeOffset.UnixEpoch,
                1);
    }

    private sealed class TestSigningKey : IPdfSigningKey, IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public TestSigningKey()
        {
            var request = new CertificateRequest("CN=PXA Pipeline Test", _rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        }

        public X509Certificate2 Certificate { get; }

        public byte[] SignHash(ReadOnlySpan<byte> hash, HashAlgorithmName digestAlgorithm) =>
            _rsa.SignHash(hash, digestAlgorithm, RSASignaturePadding.Pkcs1);

        public void Dispose()
        {
            Certificate.Dispose();
            _rsa.Dispose();
        }
    }
}
