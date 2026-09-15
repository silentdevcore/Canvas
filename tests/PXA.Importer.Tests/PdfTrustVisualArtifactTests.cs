using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PXA.Importer.Graphics;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Pdf;

namespace PXA.Importer.Tests;

/// <summary>
/// Produces deterministic operation outputs for the PDF Trust visual runner. The test also
/// keeps the operation assertions close to the real services so a baseline cannot hide a
/// failed redaction or compliance conversion.
/// </summary>
public sealed class PdfTrustVisualArtifactTests
{
    [Fact]
    public async Task TrustOperations_ProduceValidVisualArtifacts()
    {
        var sourceForSignature = CreateBasicDocument().ToBytes();
        using var signingKey = CreateSigningKey();
        var signed = new PdfPadesSigningService().Sign(
            sourceForSignature,
            signingKey,
            new PdfSigningOptions
            {
                FieldName = "GoldenSignature",
                SigningTime = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero),
                Appearance = new PdfSignatureAppearanceOptions
                {
                    PageNumber = 1,
                    X = 360,
                    Y = 700,
                    Width = 180,
                    Height = 64,
                    Title = "PXA Trust Signature"
                }
            }).Bytes;
        await AssertPdfTrustSignatureAsync(signed);
        WritePair("signature", sourceForSignature, signed);

        var sourceForRedaction = CreateRedactionDocument().ToBytes();
        var redacted = await new PdfRedactionApplier(new PdfRedactionPreflight()).ApplyAsync(
            new MemoryStream(sourceForRedaction, writable: false),
            [new PdfRedactionRegion(1, new PdfRectangle(20, 680, 260, 90))]);
        Assert.True(redacted.RemovedElementCount > 0);
        Assert.True(redacted.Verification.Passed);
        WritePair("redaction", sourceForRedaction, redacted.Bytes);

        var archivalDocument = CreateArchivalDocument();
        var sourceForPdfA = RemovePdfAIdentification(archivalDocument.ToBytes(
            new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B }));
        var convertedPdfA = await new PdfA2bConverter(new PdfTrustInspector()).ConvertAsync(
            new MemoryStream(sourceForPdfA, writable: false));
        Assert.Equal(PdfTrustStatus.Valid, convertedPdfA.Validation.Status);
        WritePair("pdf-a-2b", sourceForPdfA, convertedPdfA.Bytes);

        var accessibleDocument = CreateAccessibleDocument();
        var sourceForPdfUa = accessibleDocument.ToBytes();
        var pdfUa = accessibleDocument.ToBytes(new PdfSaveOptions
        {
            AccessibilityProfile = PdfAccessibilityProfile.PdfUa1
        });
        await using var pdfUaStream = new MemoryStream(pdfUa, writable: false);
        var pdfUaTrust = await new PdfTrustInspector().InspectAsync(pdfUaStream);
        Assert.Equal(PdfTrustStatus.Indeterminate, pdfUaTrust.PdfUa.Status);
        Assert.Equal("PDF/UA-1", pdfUaTrust.PdfUa.Profile);
        WritePair("pdf-ua-1", sourceForPdfUa, pdfUa);
    }

    private static PdfDocument CreateBasicDocument()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.Info.Title = "PXA Trust visual signature";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = document.Info.CreationDate;
        var page = document.AddPage();
        page.DrawText("PDF Trust visual baseline", 40, 760, 18);
        page.DrawText("The signature appearance is intentionally visible.", 40, 720, 12);
        page.DrawRectangle(40, 640, 240, 42);
        return document;
    }

    private static PdfDocument CreateRedactionDocument()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.Info.Title = "PXA Trust visual redaction";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = document.Info.CreationDate;
        var page = document.AddPage();
        page.DrawText("CONFIDENTIAL VALUE", 40, 720, 16);
        page.DrawText("This visible text must remain after redaction.", 40, 560, 12);
        page.DrawRectangle(40, 680, 220, 60);
        return document;
    }

    private static PdfDocument CreateArchivalDocument()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.Info.Title = "PXA Trust visual PDF/A";
        document.Info.Author = "PXA QA";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = document.Info.CreationDate;
        document.AddPage().DrawText("PDF/A visual baseline", 40, 760, new PdfDrawTextOptions { Language = "en" });
        return document;
    }

    private static PdfDocument CreateAccessibleDocument()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.Language = "en-US";
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.Info.Title = "PXA Trust visual PDF/UA";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = document.Info.CreationDate;
        var page = document.AddPage();
        using (page.BeginAccessibilityElement(new PdfAccessibilityProperties
        {
            Role = PdfStructureRole.H1,
            ElementId = "trust-heading",
            ActualText = "PDF/UA visual baseline",
            Language = "en-US"
        }))
        {
            page.DrawText("PDF/UA visual baseline", 40, 760, new PdfDrawTextOptions { Language = "en-US" });
        }
        return document;
    }

    private static async Task AssertPdfTrustSignatureAsync(byte[] signed)
    {
        await using var stream = new MemoryStream(signed, writable: false);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        Assert.Single(report.Signatures);
        Assert.True(report.Signatures[0].CoversWholeDocument);
    }

    private static void WritePair(string operation, byte[] source, byte[] output)
    {
        if (Environment.GetEnvironmentVariable("PXA_PDF_TRUST_OPERATION_ARTIFACT_DIR") is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"source-{operation}.pdf"), source);
        File.WriteAllBytes(Path.Combine(directory, $"output-{operation}.pdf"), output);
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
            "CN=PXA PDF Trust Visual Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        using var certificate = request.CreateSelfSigned(
            new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 8, 30, 0, 0, 0, TimeSpan.Zero));
        return new PdfPkcs12SigningKey(
            certificate.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
    }
}
