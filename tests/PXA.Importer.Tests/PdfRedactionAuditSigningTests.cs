using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfRedactionAuditSigningTests
{
    private const string Secret = "CONFIDENTIAL-AUDIT-33027";

    [Fact]
    public async Task Apply_ShouldLeaveTheRecordUnsignedWhenNoAuditKeyIsConfigured()
    {
        var result = await ApplyAsync(null);

        Assert.Null(result.Audit.Signature);
        // The hash still stands on its own: it detects change, it just cannot prove origin.
        Assert.Equal(64, result.Audit.RecordSha256.Length);
    }

    [Fact]
    public async Task Apply_ShouldSignTheRecordWithTheAuditKey()
    {
        using var key = CreateAuditKey();

        var result = await ApplyAsync(key);

        var signature = Assert.IsType<PdfRedactionAuditSignature>(result.Audit.Signature);
        Assert.Equal("CMS/SHA-256", signature.Algorithm);
        Assert.Equal(key.Certificate.Thumbprint, signature.SignerThumbprint);
        Assert.Contains("PXA Redaction Audit", signature.SignerSubject, StringComparison.Ordinal);
        Assert.NotEmpty(Convert.FromBase64String(signature.Cms));
        Assert.True(PdfRedactionAuditVerifier.Verify(result.Audit, requireSignature: true));
    }

    [Fact]
    public async Task Verify_ShouldRejectTamperedSignedRecordMetadata()
    {
        using var key = CreateAuditKey();
        var result = await ApplyAsync(key);

        Assert.False(PdfRedactionAuditVerifier.Verify(
            result.Audit with { Reason = "Changed after signing" },
            requireSignature: true));
        Assert.False(PdfRedactionAuditVerifier.Verify(
            result.Audit with
            {
                Signature = result.Audit.Signature! with { SignerSubject = "CN=Impostor" }
            },
            requireSignature: true));
    }

    [Fact]
    public async Task Verify_ShouldDistinguishOptionalAndRequiredUnsignedRecords()
    {
        var result = await ApplyAsync(null);

        Assert.True(PdfRedactionAuditVerifier.Verify(result.Audit));
        Assert.False(PdfRedactionAuditVerifier.Verify(result.Audit, requireSignature: true));
    }

    [Fact]
    public async Task Apply_ShouldSignTheRecordHashThatItPublishes()
    {
        using var key = CreateAuditKey();

        var result = await ApplyAsync(key);

        // Recomputing the canonical hash from the record's own fields must reproduce the
        // value the signature was taken over.
        var recomputed = PdfRedactionAuditSignerAccessor.ComputeRecordHash(result.Audit);
        Assert.Equal(result.Audit.RecordSha256, recomputed);
    }

    [Fact]
    public async Task Apply_ShouldNotPlaceTheRemovedContentInTheSignedRecord()
    {
        using var key = CreateAuditKey();

        var result = await ApplyAsync(key);

        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Audit);
        Assert.DoesNotContain(Secret, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ShouldProduceADifferentRecordHashForADifferentRedaction()
    {
        using var key = CreateAuditKey();

        var first = await ApplyAsync(key, reason: "Privacy request");
        var second = await ApplyAsync(key, reason: "Legal hold");

        Assert.NotEqual(first.Audit.RecordSha256, second.Audit.RecordSha256);
        Assert.NotEqual(first.Audit.Signature!.Cms, second.Audit.Signature!.Cms);
    }

    [Fact]
    public async Task CanonicalHash_ShouldNotBeAmbiguousWhenTextContainsNewlines()
    {
        var result = await ApplyAsync(null);
        var first = result.Audit with { Actor = "alpha\nbeta", Reason = "gamma" };
        var second = result.Audit with { Actor = "alpha", Reason = "beta\ngamma" };

        Assert.NotEqual(
            PdfRedactionAuditSignerAccessor.ComputeRecordHash(first),
            PdfRedactionAuditSignerAccessor.ComputeRecordHash(second));
    }

    private static async Task<PdfRedactionApplyResult> ApplyAsync(
        IPdfSigningKey? key,
        string? reason = "Privacy request")
    {
        var pdf = CreatePdf();
        await using var stream = new MemoryStream(pdf);
        var preflight = new PdfRedactionPreflight();
        return await new PdfRedactionApplier(preflight).ApplyAsync(
            stream,
            [await RegionAsync(pdf)],
            new PdfRedactionApplyOptions
            {
                Reason = reason,
                Actor = "auditor@example.test",
                AuditSigningKey = key
            });
    }

    private static PdfPkcs12SigningKey CreateAuditKey()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA Redaction Audit",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        return new PdfPkcs12SigningKey(
            certificate.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
    }

    private static async Task<PdfRedactionRegion> RegionAsync(byte[] pdf)
    {
        await using var stream = new MemoryStream(pdf);
        var document = await new PdfImporter().LoadAsync(stream);
        var element = document.Pages[0].GraphicsObjects
            .OfType<Graphics.PdfTextElement>()
            .First(item => item.Text.Contains(Secret, StringComparison.Ordinal));
        var bounds = element.Bounds ?? new Analysis.BoundingBoxCalculator().ComputeElementBounds(element);
        return new PdfRedactionRegion(1, new Graphics.PdfRectangle(
            bounds.Left - 4, bounds.Bottom - 4, bounds.Width + 8, bounds.Height + 8));
    }

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawText("Public heading", 40, 700);
        page.DrawText(Secret, 40, 400);
        return document.ToBytes();
    }

    private static class PdfRedactionAuditSignerAccessor
    {
        public static string ComputeRecordHash(PdfRedactionAuditRecord record) =>
            (string)typeof(PdfRedactionPreflight).Assembly
                .GetType("PXA.Importer.Redaction.PdfRedactionAuditSigner")!
                .GetMethod("ComputeRecordHash")!
                .Invoke(null, [
                    record.SourceSha256,
                    record.ResultSha256,
                    record.AppliedAt,
                    record.Actor,
                    record.Reason,
                    record.Regions,
                    record.AppliedSanitizations,
                    record.VerificationPassed
                ])!;
    }
}
