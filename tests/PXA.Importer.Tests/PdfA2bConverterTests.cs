using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfA2bConverterTests
{
    [Fact]
    public async Task PreflightAsync_ReportsReadyForConvertibleEmbeddedFontDocument()
    {
        var source = RemovePdfAIdentification(CreatePdfA2b());
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        await using var stream = new MemoryStream(source, writable: false);
        var report = await converter.PreflightAsync(stream);

        Assert.Equal(PdfComplianceConversionCapability.Ready, report.Capability);
        Assert.Equal("PDF/A-2b", report.TargetProfile);
        Assert.True(report.OriginalPreserved);
        Assert.Equal(64, report.SourceSha256.Length);
        Assert.Contains(report.Findings, finding => finding.Code == "PXAPDFACONVERT000");
    }

    [Fact]
    public async Task ConvertAsync_RewritesToValidatedPdfA2bWithoutMutatingSource()
    {
        var source = RemovePdfAIdentification(CreatePdfA2b());
        var original = source.ToArray();
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        await using var stream = new MemoryStream(source, writable: false);
        var result = await converter.ConvertAsync(stream);

        if (Environment.GetEnvironmentVariable("PXA_PDFA_CONVERT_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, "pxa-converted-pdfa2b.pdf"), result.Bytes);
        }

        Assert.Equal(original, source);
        Assert.NotEqual(result.SourceSha256, result.ResultSha256);
        Assert.Equal(PdfTrustStatus.Valid, result.Validation.Status);
        Assert.Equal("PDF/A-2b", result.Validation.Profile);
        Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(result.Bytes));
    }

    [Fact]
    public async Task PreflightAsync_BlocksUnembeddedFonts()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Standard font", 40, 40);
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        await using var stream = new MemoryStream(document.ToBytes(), writable: false);
        var report = await converter.PreflightAsync(stream);

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
        Assert.Contains(report.Findings, finding => finding.Code == "PXAPDFTRUST314");
    }

    [Fact]
    public async Task ConvertAsync_PreservesOriginalWhenPreflightBlocks()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Standard font", 40, 40);
        var source = document.ToBytes();
        var original = source.ToArray();
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        await using var stream = new MemoryStream(source, writable: false);
        var exception = await Assert.ThrowsAsync<PdfA2bConversionBlockedException>(() => converter.ConvertAsync(stream));

        Assert.Equal(original, source);
        Assert.True(exception.Preflight.OriginalPreserved);
        Assert.Equal(PdfComplianceConversionCapability.Unsupported, exception.Preflight.Capability);
    }

    [Fact]
    public async Task ConvertAsync_RequiresExplicitApprovalBeforeInvalidatingSignature()
    {
        using var signingKey = CreateSigningKey();
        var unsigned = RemovePdfAIdentification(CreatePdfA2b());
        var signed = new PdfPadesSigningService().Sign(
            unsigned,
            signingKey,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = DateTimeOffset.UtcNow }).Bytes;
        var converter = new PdfA2bConverter(new PdfTrustInspector());

        await using var preflightStream = new MemoryStream(signed, writable: false);
        var preflight = await converter.PreflightAsync(preflightStream);

        Assert.Equal(PdfComplianceConversionCapability.ReviewRequired, preflight.Capability);
        Assert.True(preflight.InvalidatesSignatures);
        Assert.Contains(preflight.Findings, finding => finding.Code == "PXAPDFACONVERT007");

        await using var blockedStream = new MemoryStream(signed, writable: false);
        await Assert.ThrowsAsync<PdfA2bConversionBlockedException>(() => converter.ConvertAsync(blockedStream));

        await using var approvedStream = new MemoryStream(signed, writable: false);
        var converted = await converter.ConvertAsync(
            approvedStream,
            new PdfA2bConversionOptions { AllowSignatureInvalidation = true });

        if (Environment.GetEnvironmentVariable("PXA_PDFA_CONVERT_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, "pxa-converted-signed-pdfa2b.pdf"), converted.Bytes);
        }

        Assert.Equal(PdfTrustStatus.Valid, converted.Validation.Status);
        Assert.Equal(signed, blockedStream.ToArray());
        await using var convertedStream = new MemoryStream(converted.Bytes, writable: false);
        var convertedTrust = await new PdfTrustInspector().InspectAsync(convertedStream);
        Assert.Empty(convertedTrust.Signatures);
    }

    private static byte[] CreatePdfA2b()
    {
        var document = new PdfDocument
        {
            FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"))
        };
        document.Info.Title = "Conversion fixture";
        document.Info.Author = "PXA QA";
        document.Info.Subject = "Conservative PDF/A conversion";
        document.Info.Keywords = "PXA, PDF/A";
        document.Info.Creator = "PXA tests";
        document.Info.Producer = "PXA";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = new DateTimeOffset(2026, 8, 31, 10, 30, 0, TimeSpan.Zero);
        document.AddPage().DrawText("Convertible archival text", 40, 40, new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B });
    }

    private static byte[] RemovePdfAIdentification(byte[] source)
    {
        var text = Encoding.Latin1.GetString(source)
            .Replace("pdfaid:part", "pdfaid:xxxx", StringComparison.Ordinal)
            .Replace("pdfaid:conformance", "pdfaid:xxxxxxxxxxx", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(text);
    }

    private static PdfPkcs12SigningKey CreateSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA PDF-A Conversion Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
            critical: true));
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        return new PdfPkcs12SigningKey(
            certificate.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
    }
}
