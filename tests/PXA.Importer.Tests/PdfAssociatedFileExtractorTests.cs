using System.Text;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Importer.Trust;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfAssociatedFileExtractorTests
{
    [Fact]
    public async Task ExtractAsync_RoundTripsPdfA3InvoiceXmlWithoutChangingBytes()
    {
        var invoice = Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\" />");
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText("Factur-X invoice", 40, 40, new PdfDrawTextOptions { Language = "en" });
        document.AddAssociatedFile(
            invoice,
            "factur-x.xml",
            "application/xml",
            PdfAssociatedFileRelationship.Alternative,
            "Factur-X invoice data");
        var pdf = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });

        var files = await new PdfAssociatedFileExtractor().ExtractAsync(new MemoryStream(pdf));

        var file = Assert.Single(files);
        Assert.Equal("factur-x.xml", file.FileName);
        Assert.Equal("application/xml", file.MimeType);
        Assert.Equal("Alternative", file.Relationship);
        Assert.Equal("Factur-X invoice data", file.Description);
        Assert.True(file.SizeMatches);
        Assert.True(file.ChecksumMatches);
        Assert.Equal(invoice, file.Content.ToArray());
    }

    [Fact]
    public async Task ExtractAsync_ReturnsNoFilesWhenCatalogHasNoAssociatedFiles()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.AddPage().DrawText("No attachment", 40, 40);

        var files = await new PdfAssociatedFileExtractor().ExtractAsync(
            new MemoryStream(document.ToBytes()));

        Assert.Empty(files);
    }

    [Fact]
    public async Task ExtractAsync_RejectsPayloadAboveConfiguredLimit()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.AddPage().DrawText("Attachment", 40, 40);
        document.AddAssociatedFile(
            new byte[] { 1, 2, 3, 4 },
            "invoice.xml",
            "application/xml",
            PdfAssociatedFileRelationship.Data);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new PdfAssociatedFileExtractor().ExtractAsync(
                new MemoryStream(document.ToBytes()),
                new PdfAssociatedFileExtractionOptions { MaximumFileSizeBytes = 3 }));

        Assert.Contains("item limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedFacturXEnvelope_RemainsStructurallyValidPdfA3()
    {
        var invoice = Encoding.UTF8.GetBytes("""
            <rsm:CrossIndustryInvoice
              xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
              xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
              <rsm:ExchangedDocumentContext>
                <ram:GuidelineSpecifiedDocumentContextParameter>
                  <ram:ID>urn:factur-x.eu:1p0:basic</ram:ID>
                </ram:GuidelineSpecifiedDocumentContextParameter>
              </rsm:ExchangedDocumentContext>
            </rsm:CrossIndustryInvoice>
            """);
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText("Invoice", 40, 40, new PdfDrawTextOptions { Language = "en" });
        EInvoice.Attach(document, invoice, new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "BASIC"
        });
        var pdf = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });

        var trust = await new PdfTrustInspector().InspectAsync(new MemoryStream(pdf));

        Assert.Equal("PDF/A-3b", trust.PdfA.Profile);
        Assert.Equal(PdfTrustStatus.Valid, trust.PdfA.Status);
        Assert.NotNull(trust.Risk);
        Assert.Equal(1, trust.Risk!.EmbeddedFileCount);
    }
}
