using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PXA.Importer.EInvoicing;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfEInvoiceAttacherTests
{
    private static readonly byte[] InvoiceXml = Encoding.UTF8.GetBytes("""
        <rsm:CrossIndustryInvoice
          xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
          xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext>
            <ram:GuidelineSpecifiedDocumentContextParameter>
              <ram:ID>urn:factur-x.eu:1p0:basic</ram:ID>
            </ram:GuidelineSpecifiedDocumentContextParameter>
          </rsm:ExchangedDocumentContext>
          <rsm:ExchangedDocument><ram:ID>ATTACH-1</ram:ID></rsm:ExchangedDocument>
        </rsm:CrossIndustryInvoice>
        """);

    private static PdfEInvoiceAttacher CreateAttacher() =>
        new(new PdfA2bConverter(new PdfTrustInspector()));

    private static PdfEInvoiceAttachmentPayload Payload(string fileName = "factur-x.xml") => new()
    {
        Xml = InvoiceXml,
        FileName = fileName,
        MimeType = "application/xml",
        Relationship = PdfAssociatedFileRelationship.Alternative,
        Description = "Factur-X 1.09.2 BASIC invoice XML",
        XmpMetadata = new PdfAssociatedFileXmpMetadata
        {
            SchemaName = "Factur-X PDFA Extension Schema",
            NamespaceUri = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#",
            Prefix = "fx",
            Properties =
            [
                new("DocumentFileName", fileName, "Text", "external", "The name of the embedded XML document."),
                new("DocumentType", "INVOICE", "Text", "external", "The type of the hybrid document."),
                new("Version", "1.0", "Text", "external", "The version of the specification."),
                new("ConformanceLevel", "BASIC", "Text", "external", "The conformance profile.")
            ]
        }
    };

    /// <summary>A visible, already archival source that only lacks the invoice attachment.</summary>
    private static byte[] CreatePdfA3Source(string text = "Invoice ATTACH-1 Seller GmbH")
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText(text, 40, 40, new PdfDrawTextOptions { Language = "en" });
        document.AddAssociatedFile(
            "unrelated"u8.ToArray(),
            "notes.txt",
            "text/plain",
            PdfAssociatedFileRelationship.Supplement,
            "Unrelated supplement");
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static byte[] CreatePlainSource()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText("Plain invoice", 40, 40, new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes();
    }

    [Fact]
    public async Task AttachAsync_AppendsInvoiceToPdfA3WithoutRewritingTheSource()
    {
        var source = CreatePdfA3Source();

        var result = await CreateAttacher().AttachAsync(new MemoryStream(source), Payload());

        Assert.Equal(PdfEInvoiceAttachmentStrategy.IncrementalUpdate, result.Strategy);
        Assert.True(result.PagesUnchanged);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(source)), result.SourceSha256);
        // The strongest possible preservation evidence: the original file is a byte prefix.
        Assert.Equal(source, result.Bytes[..source.Length]);
    }

    [Fact]
    public async Task AttachAsync_ExtractsTheEmbeddedInvoiceByteForByte()
    {
        var result = await CreateAttacher().AttachAsync(new MemoryStream(CreatePdfA3Source()), Payload());

        var files = await new PdfAssociatedFileExtractor().ExtractAsync(new MemoryStream(result.Bytes));
        var invoice = Assert.Single(files, file => file.FileName == "factur-x.xml");
        Assert.Equal(InvoiceXml, invoice.Content.ToArray());
        Assert.Equal("application/xml", invoice.MimeType);
        Assert.Equal("Alternative", invoice.Relationship);
        Assert.Equal("Factur-X 1.09.2 BASIC invoice XML", invoice.Description);
        Assert.True(invoice.SizeMatches);
        Assert.True(invoice.ChecksumMatches);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(InvoiceXml)), result.AttachmentSha256);
    }

    [Fact]
    public async Task AttachAsync_RetainsUnrelatedEmbeddedFiles()
    {
        var result = await CreateAttacher().AttachAsync(new MemoryStream(CreatePdfA3Source()), Payload());

        var files = await new PdfAssociatedFileExtractor().ExtractAsync(new MemoryStream(result.Bytes));
        Assert.Contains(files, file => file.FileName == "notes.txt");
        Assert.Contains(files, file => file.FileName == "factur-x.xml");
    }

    [Fact]
    public async Task AttachAsync_KeepsTheXmpFacturXExtensionSchemaAndPdfA3Identification()
    {
        var result = await CreateAttacher().AttachAsync(new MemoryStream(CreatePdfA3Source()), Payload());

        var xmp = Encoding.UTF8.GetString(result.Bytes);
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", xmp, StringComparison.Ordinal);
        Assert.Contains("urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#", xmp, StringComparison.Ordinal);
        Assert.Contains("<fx:DocumentType>INVOICE</fx:DocumentType>", xmp, StringComparison.Ordinal);
        Assert.Contains("<fx:ConformanceLevel>BASIC</fx:ConformanceLevel>", xmp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreflightAsync_ReportsIncrementalReadinessForAnArchivalSource()
    {
        var report = await CreateAttacher().PreflightAsync(new MemoryStream(CreatePdfA3Source()), Payload());

        Assert.Equal("PDF/A-3b", report.TargetProfile);
        Assert.Equal(PdfComplianceConversionCapability.Ready, report.Capability);
        Assert.Equal(PdfEInvoiceAttachmentStrategy.IncrementalUpdate, report.Strategy);
        Assert.False(report.RequiresArchiveConversion);
        Assert.False(report.RequiresApproval);
        Assert.True(report.OriginalPreserved);
        Assert.Equal(1, report.PageCount);
        Assert.Equal("PDF/A-3B", report.SourceArchiveProfile);
        Assert.Empty(report.ConflictingAttachmentNames);
        Assert.Contains(report.Findings, finding => finding.Code == "PXAEINVATTACH000");
    }

    [Fact]
    public async Task PreflightAsync_RequiresApprovalWhenTheSourceIsNotArchival()
    {
        var report = await CreateAttacher().PreflightAsync(new MemoryStream(CreatePlainSource()), Payload());

        Assert.Equal(PdfComplianceConversionCapability.ReviewRequired, report.Capability);
        Assert.Equal(PdfEInvoiceAttachmentStrategy.ArchiveConversion, report.Strategy);
        Assert.True(report.RequiresArchiveConversion);
        Assert.True(report.RequiresApproval);
        Assert.Contains(report.Findings, finding => finding.Code == "PXAEINVATTACH004");
    }

    [Fact]
    public async Task PreflightAsync_DoesNotTrustAnInvalidPdfA3MetadataClaim()
    {
        var source = ReplaceAscii(CreatePdfA3Source(), "/OutputIntents", "/OutputIntentz");

        var report = await CreateAttacher().PreflightAsync(new MemoryStream(source), Payload());

        Assert.Equal("PDF/A-3B", report.SourceArchiveProfile);
        Assert.True(report.RequiresArchiveConversion);
        Assert.Equal(PdfComplianceConversionCapability.ReviewRequired, report.Capability);
        Assert.Contains(report.Findings, finding =>
            finding.Code == "PXAEINVATTACH004" &&
            finding.Message.Contains("does not pass", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AttachAsync_RequiresApprovalBeforeAddingARevisionAfterASignature()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA E-Invoice Attachment Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var key = new PdfPkcs12SigningKey(
            certificate.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
        var signed = new PdfPadesSigningService().Sign(
            CreatePdfA3Source(),
            key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = now }).Bytes;

        var report = await CreateAttacher().PreflightAsync(new MemoryStream(signed), Payload());
        var exception = await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(
            () => CreateAttacher().AttachAsync(new MemoryStream(signed), Payload()));

        Assert.Equal(1, report.SignatureCount);
        Assert.True(report.RequiresApproval);
        Assert.Contains(report.Findings, finding => finding.Code is "PXAEINVATTACH007" or "PXAEINVATTACH008");
        Assert.Contains("Approve", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAsync_BlocksUnapprovedArchiveConversionAndLeavesTheOriginalAlone()
    {
        var source = CreatePlainSource();

        var exception = await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(
            () => CreateAttacher().AttachAsync(new MemoryStream(source), Payload()));

        Assert.True(exception.Preflight.RequiresArchiveConversion);
        Assert.True(exception.Preflight.OriginalPreserved);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(source)),
            exception.Preflight.SourceSha256);
    }

    [Fact]
    public async Task AttachAsync_ConvertsToPdfA3OnlyAfterExplicitApproval()
    {
        var result = await CreateAttacher().AttachAsync(
            new MemoryStream(CreatePlainSource()),
            Payload(),
            new PdfEInvoiceAttachmentOptions { AllowArchiveConversion = true });

        Assert.Equal(PdfEInvoiceAttachmentStrategy.ArchiveConversion, result.Strategy);
        Assert.Equal(1, result.PageCount);
        Assert.True(result.PagesUnchanged);
        var files = await new PdfAssociatedFileExtractor().ExtractAsync(new MemoryStream(result.Bytes));
        Assert.Equal(InvoiceXml, Assert.Single(files).Content.ToArray());
    }

    [Fact]
    public async Task AttachAsync_BlocksAConflictingInvoiceAttachmentWithoutApproval()
    {
        var withInvoice = await CreateAttacher().AttachAsync(
            new MemoryStream(CreatePdfA3Source()),
            Payload());

        var exception = await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(
            () => CreateAttacher().AttachAsync(new MemoryStream(withInvoice.Bytes), Payload()));

        Assert.Equal(PdfComplianceConversionCapability.Unsupported, exception.Preflight.Capability);
        Assert.Contains("factur-x.xml", exception.Preflight.ConflictingAttachmentNames);
        Assert.Contains(exception.Preflight.Findings, finding => finding.Code == "PXAEINVATTACH010");
    }

    [Fact]
    public async Task AttachAsync_TreatsAReservedInvoiceNameAsAConflict()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText("Invoice", 40, 40, new PdfDrawTextOptions { Language = "en" });
        document.AddAssociatedFile(
            InvoiceXml,
            "zugferd-invoice.xml",
            "application/xml",
            PdfAssociatedFileRelationship.Alternative);
        var source = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });

        var report = await CreateAttacher().PreflightAsync(new MemoryStream(source), Payload());

        Assert.Contains("zugferd-invoice.xml", report.ConflictingAttachmentNames);
        Assert.Equal(PdfComplianceConversionCapability.Unsupported, report.Capability);
    }

    [Fact]
    public async Task AttachAsync_ReplacesAConflictingInvoiceWhenApproved()
    {
        var withInvoice = await CreateAttacher().AttachAsync(
            new MemoryStream(CreatePdfA3Source()),
            Payload());
        var replacement = Payload() with { Xml = Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\" />") };

        var result = await CreateAttacher().AttachAsync(
            new MemoryStream(withInvoice.Bytes),
            replacement,
            new PdfEInvoiceAttachmentOptions { ReplaceExistingInvoiceAttachment = true });

        var files = await new PdfAssociatedFileExtractor().ExtractAsync(new MemoryStream(result.Bytes));
        var invoice = Assert.Single(files, file => file.FileName == "factur-x.xml");
        Assert.Equal(replacement.Xml.ToArray(), invoice.Content.ToArray());
        Assert.Contains(result.Preflight.Findings, finding => finding.Code == "PXAEINVATTACH009");
    }

    [Fact]
    public async Task AttachAsync_RejectsAnOutputAboveTheConfiguredLimit()
    {
        var exception = await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(
            () => CreateAttacher().AttachAsync(
                new MemoryStream(CreatePdfA3Source()),
                Payload(),
                new PdfEInvoiceAttachmentOptions { MaximumOutputSizeBytes = 1024 }));

        Assert.Contains(exception.Preflight.Findings, finding => finding.Code == "PXAEINVATTACH011");
    }

    [Fact]
    public async Task AttachAsync_RejectsASourceAboveTheConfiguredLimit()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateAttacher().AttachAsync(
                new MemoryStream(CreatePdfA3Source()),
                Payload(),
                new PdfEInvoiceAttachmentOptions { MaximumPdfSizeBytes = 512 }));
    }

    [Theory]
    [InlineData("../factur-x.xml")]
    [InlineData("sub/factur-x.xml")]
    [InlineData("factur-x .xml")]
    [InlineData("")]
    public async Task AttachAsync_RejectsUnsafeFileNames(string fileName)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAttacher().AttachAsync(new MemoryStream(CreatePdfA3Source()), Payload(fileName)));
    }

    [Fact]
    public async Task AttachAsync_RejectsAnInvalidMimeType()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAttacher().AttachAsync(
                new MemoryStream(CreatePdfA3Source()),
                Payload() with { MimeType = "application xml" }));
    }

    [Fact]
    public async Task AttachAsync_RejectsAnEmptyInvoicePayload()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAttacher().AttachAsync(
                new MemoryStream(CreatePdfA3Source()),
                Payload() with { Xml = ReadOnlyMemory<byte>.Empty }));
    }

    [Fact]
    public async Task AttachAsync_RejectsAnEncryptedSource()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText("Encrypted", 40, 40, new PdfDrawTextOptions { Language = "en" });
        var source = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions
            {
                OwnerPassword = "owner-secret-1",
                UserPassword = "user-secret-1"
            }
        });

        var exception = await Assert.ThrowsAsync<PdfEInvoiceAttachmentBlockedException>(
            () => CreateAttacher().AttachAsync(new MemoryStream(source), Payload()));

        Assert.Contains(exception.Preflight.Findings, finding => finding.Code == "PXAEINVATTACH001");
    }

    [Fact]
    public async Task AttachAsync_IsDeterministicForTheSameSourceAndPayload()
    {
        var source = CreatePdfA3Source();

        var first = await CreateAttacher().AttachAsync(new MemoryStream(source), Payload());
        var second = await CreateAttacher().AttachAsync(new MemoryStream(source), Payload());

        // Only the attachment ModDate differs between runs; the invoice bytes never do.
        Assert.Equal(first.AttachmentSha256, second.AttachmentSha256);
        Assert.Equal(first.PageCount, second.PageCount);
        Assert.Equal(first.Preflight.SourceSha256, second.Preflight.SourceSha256);
    }

    [Fact]
    public async Task AttachAsync_PreservesExistingDocumentMetadataInTheXmpPacket()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.Info.Title = "Quarterly statement";
        document.Info.Author = "Seller GmbH";
        document.AddPage().DrawText("Invoice", 40, 40, new PdfDrawTextOptions { Language = "en" });
        var source = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });

        var result = await CreateAttacher().AttachAsync(new MemoryStream(source), Payload());

        var text = Encoding.UTF8.GetString(result.Bytes);
        Assert.Contains("Quarterly statement", text, StringComparison.Ordinal);
        Assert.Contains("Seller GmbH", text, StringComparison.Ordinal);
    }

    private static byte[] ReplaceAscii(byte[] source, string oldValue, string newValue)
    {
        Assert.Equal(oldValue.Length, newValue.Length);
        var result = source.ToArray();
        var oldBytes = Encoding.ASCII.GetBytes(oldValue);
        var offset = result.AsSpan().IndexOf(oldBytes);
        Assert.True(offset >= 0, $"'{oldValue}' was not found in the PDF fixture.");
        Encoding.ASCII.GetBytes(newValue).CopyTo(result, offset);
        return result;
    }
}
