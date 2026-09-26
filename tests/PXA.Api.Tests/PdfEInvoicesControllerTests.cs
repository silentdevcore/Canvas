using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PdfEInvoicesControllerTests
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
          <rsm:ExchangedDocument><ram:ID>INV-42</ram:ID></rsm:ExchangedDocument>
          <rsm:SupplyChainTradeTransaction>
            <ram:ApplicableHeaderTradeAgreement><ram:SellerTradeParty><ram:Name>Seller GmbH</ram:Name></ram:SellerTradeParty><ram:BuyerTradeParty><ram:Name>Buyer AG</ram:Name></ram:BuyerTradeParty></ram:ApplicableHeaderTradeAgreement>
            <ram:ApplicableHeaderTradeSettlement><ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode><ram:TaxTotalAmount>19.00</ram:TaxTotalAmount><ram:SpecifiedTradeSettlementHeaderMonetarySummation><ram:TaxBasisTotalAmount>100.00</ram:TaxBasisTotalAmount><ram:GrandTotalAmount>119.00</ram:GrandTotalAmount><ram:DuePayableAmount>119.00</ram:DuePayableAmount></ram:SpecifiedTradeSettlementHeaderMonetarySummation></ram:ApplicableHeaderTradeSettlement>
          </rsm:SupplyChainTradeTransaction>
        </rsm:CrossIndustryInvoice>
        """);

    [Fact]
    public async Task Validate_ReturnsStructuralReportWithoutConformanceClaim()
    {
        var result = await new PdfEInvoicesController().Validate(
            FormFile(InvoiceXml, "factur-x.xml", "application/xml"),
            "Factur-X",
            EInvoice.CurrentFacturXVersion,
            "BASIC",
            CancellationToken.None);

        var report = Assert.IsType<PxaEInvoiceValidationReport>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(report.IsStructurallyValid);
        Assert.False(report.IsConformanceValidated);
        Assert.Equal("CII", report.Syntax);
    }

    [Fact]
    public async Task Inspect_ReportsAssociatedInvoiceIntegrity()
    {
        var pdf = CreateInvoicePdf();

        var result = await new PdfEInvoicesController().Inspect(
            FormFile(pdf, "invoice.pdf", "application/pdf"),
            CancellationToken.None);

        var summaries = Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        var summary = Assert.Single(summaries);
        Assert.Equal("factur-x.xml", summary.FileName);
        Assert.True(summary.IsXml);
        Assert.True(summary.SizeMatches);
        Assert.True(summary.ChecksumMatches);
        Assert.Equal("Factur-X / ZUGFeRD", summary.Standard);
        Assert.Equal("CII", summary.Syntax);
        Assert.Equal("BASIC", summary.Profile);
        Assert.True(summary.IsStructurallyValid);
        Assert.Equal("notEvaluated", summary.ConformanceStatus);
        Assert.Null(summary.ValidatorArtifactSetId);
        Assert.Equal("INV-42", summary.SemanticData?.InvoiceNumber);
        Assert.Equal("Seller GmbH", summary.SemanticData?.Seller.Name);
        Assert.Equal(119.00m, summary.SemanticData?.PayableAmount);
        Assert.Equal("consistent", summary.PdfConsistency.Status);
    }

    [Fact]
    public async Task Extract_ReturnsOriginalInvoiceBytes()
    {
        var result = await new PdfEInvoicesController().Extract(
            FormFile(CreateInvoicePdf(), "invoice.pdf", "application/pdf"),
            "factur-x.xml",
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/xml", file.ContentType);
        Assert.Equal("factur-x.xml", file.FileDownloadName);
        Assert.Equal(InvoiceXml, file.FileContents);
    }

    [Fact]
    public void PdfConsistencyInspector_ReportsMissingVisibleValuesForReview()
    {
        var report = PxaEInvoicePdfConsistencyInspector.Inspect(
            "Invoice INV-42 Seller GmbH EUR 119.00",
            new PxaEInvoiceSemanticData(
                "INV-42",
                null,
                "EUR",
                new("Seller GmbH", null),
                new("Buyer AG", null),
                100m,
                100m,
                19m,
                119m,
                119m,
                []));

        Assert.Equal("reviewRequired", report.Status);
        Assert.Contains(report.Findings, finding =>
            finding.Field == "buyer" && finding.Status == "notFound");
        Assert.Contains(report.Findings, finding =>
            finding.Field == "invoiceNumber" && finding.Status == "confirmed");
    }

    [Fact]
    public void Generate_ReturnsTypedInvoiceXml()
    {
        var result = new PdfEInvoicesController().Generate(GenerationRequest());

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/xml", file.ContentType);
        Assert.Equal("factur-x.xml", file.FileDownloadName);
        var recognition = EInvoice.Recognize(file.FileContents);
        Assert.True(recognition.IsRecognized);
        Assert.Equal("API-INV-1", recognition.SemanticData?.InvoiceNumber);
        Assert.Equal(119m, recognition.SemanticData?.PayableAmount);
    }

    [Fact]
    public async Task Create_ReturnsPdfA3WithExtractableInvoiceXml()
    {
        var fonts = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        var controller = new PdfEInvoicesController(fontLoader: fonts);
        var result = controller.Create(GenerationRequest());

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("API-INV-1.pdf", file.FileDownloadName);
        Assert.Equal("%PDF"u8.ToArray(), file.FileContents[..4]);
        await using var stream = new MemoryStream(file.FileContents);
        var attachments = await new PdfAssociatedFileExtractor().ExtractAsync(stream);
        var invoice = Assert.Single(attachments);
        Assert.Equal("factur-x.xml", invoice.FileName);
        Assert.Equal("API-INV-1", EInvoice.Recognize(invoice.Content).SemanticData?.InvoiceNumber);

        var inspected = await controller.Inspect(
            FormFile(file.FileContents, file.FileDownloadName, "application/pdf"),
            CancellationToken.None);
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(inspected.Result).Value);
        Assert.Equal("consistent", Assert.Single(summaries).PdfConsistency.Status);
    }

    [Fact]
    public void Generate_RejectsInvalidTypedInvoiceWithStableProblemCode()
    {
        var request = GenerationRequest() with
        {
            Invoice = GenerationRequest().Invoice! with { Lines = [] }
        };

        var result = new PdfEInvoicesController().Generate(request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAEINV106", details.Extensions["code"]);
    }

    [Fact]
    public void Create_RejectsNullLinesWithoutThrowing()
    {
        var request = GenerationRequest() with
        {
            Invoice = GenerationRequest().Invoice! with { Lines = null! }
        };

        var result = new PdfEInvoicesController().Create(request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAEINV106", details.Extensions["code"]);
    }

    [Fact]
    public void Create_SanitizesInvoiceNumberForDownloadName()
    {
        var request = GenerationRequest() with
        {
            Invoice = GenerationRequest().Invoice! with { InvoiceNumber = "../API:INV/1" }
        };
        var fonts = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));

        var result = new PdfEInvoicesController(fontLoader: fonts).Create(request);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("API-INV-1.pdf", file.FileDownloadName);
    }

    private static byte[] CreateInvoicePdf()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText(
            "Invoice INV-42 Seller GmbH Buyer AG EUR 100.00 19.00 119.00",
            40,
            40,
            new PdfDrawTextOptions { Language = "en" });
        EInvoice.Attach(document, InvoiceXml, new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "BASIC"
        });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static FormFile FormFile(byte[] bytes, string name, string contentType)
    {
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return file;
    }

    private static PdfEInvoiceGenerationRequest GenerationRequest() => new(
        "FacturX",
        EInvoice.CurrentFacturXVersion,
        "EN 16931",
        new PxaEInvoiceDocument
        {
            InvoiceNumber = "API-INV-1",
            IssueDate = new DateOnly(2026, 9, 26),
            DueDate = new DateOnly(2026, 10, 26),
            Currency = "EUR",
            Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
            Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
            Lines = [new("1", "PXA service", 1, "C62", 100m, 19m)]
        });
}
