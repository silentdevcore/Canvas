using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Generator;
using PVAD.Importer.EInvoicing;
using PVAD.Pdf;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

/// <summary>
/// Covers the <c>attach</c> operation: embedding validated invoice XML into a PDF that already
/// exists, without replacing its visible pages.
/// </summary>
public sealed class PdfEInvoicesAttachControllerTests
{
    private const string InvoiceNumber = "ATTACH-77";

    private static readonly byte[] InvoiceXml = Encoding.UTF8.GetBytes($"""
        <rsm:CrossIndustryInvoice
          xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
          xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext>
            <ram:GuidelineSpecifiedDocumentContextParameter>
              <ram:ID>urn:factur-x.eu:1p0:basic</ram:ID>
            </ram:GuidelineSpecifiedDocumentContextParameter>
          </rsm:ExchangedDocumentContext>
          <rsm:ExchangedDocument><ram:ID>{InvoiceNumber}</ram:ID></rsm:ExchangedDocument>
          <rsm:SupplyChainTradeTransaction>
            <ram:ApplicableHeaderTradeAgreement><ram:SellerTradeParty><ram:Name>Seller GmbH</ram:Name></ram:SellerTradeParty><ram:BuyerTradeParty><ram:Name>Buyer AG</ram:Name></ram:BuyerTradeParty></ram:ApplicableHeaderTradeAgreement>
            <ram:ApplicableHeaderTradeSettlement><ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode><ram:TaxTotalAmount>19.00</ram:TaxTotalAmount><ram:SpecifiedTradeSettlementHeaderMonetarySummation><ram:TaxBasisTotalAmount>100.00</ram:TaxBasisTotalAmount><ram:GrandTotalAmount>119.00</ram:GrandTotalAmount><ram:DuePayableAmount>119.00</ram:DuePayableAmount></ram:SpecifiedTradeSettlementHeaderMonetarySummation></ram:ApplicableHeaderTradeSettlement>
          </rsm:SupplyChainTradeTransaction>
        </rsm:CrossIndustryInvoice>
        """);

    [Fact]
    public async Task Attach_EmbedsTheInvoiceAndKeepsTheSourcePagesByteIdentical()
    {
        var source = CreateSourcePdf(PdfComplianceProfile.PdfA3B);
        var controller = CreateController();

        var result = await controller.Attach(
            Pdf(source), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: false,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("statement-hybrid.pdf", file.FileDownloadName);
        Assert.Equal(source, file.FileContents[..source.Length]);
        Assert.Equal("IncrementalUpdate", controller.Response.Headers["PXA-Attachment-Strategy"]);
        Assert.Equal("true", controller.Response.Headers["PXA-Pages-Unchanged"]);
        Assert.Equal("PDF/A-3b", controller.Response.Headers["PXA-Compliance-Profile"]);
        Assert.Equal("consistent", controller.Response.Headers["PXA-Invoice-Consistency"]);
        // Structural validation alone never yields a conformance claim.
        Assert.Equal("notEvaluated", controller.Response.Headers["PXA-Invoice-Conformance"]);
    }

    [Fact]
    public async Task Attach_ProducesAPdfWhoseInvoiceInspectsAsAHybridDocument()
    {
        var controller = CreateController();
        var result = await AttachAsync(controller, CreateSourcePdf(PdfComplianceProfile.PdfA3B));

        var inspected = await CreateController().Inspect(
            Pdf(Assert.IsType<FileContentResult>(result).FileContents),
            CancellationToken.None);

        var summaries = Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(inspected.Result).Value);
        var summary = Assert.Single(summaries, item => item.FileName == "factur-x.xml");
        Assert.Equal("Factur-X / ZUGFeRD", summary.Standard);
        Assert.Equal("BASIC", summary.Profile);
        Assert.True(summary.IsStructurallyValid);
        Assert.Equal("notEvaluated", summary.ConformanceStatus);
        Assert.Equal(InvoiceNumber, summary.SemanticData?.InvoiceNumber);
        Assert.Equal("consistent", summary.PdfConsistency.Status);
    }

    [Fact]
    public async Task Attach_ReturnsTheInvoiceBytesUnchangedThroughExtract()
    {
        var attached = Assert.IsType<FileContentResult>(
            await AttachAsync(CreateController(), CreateSourcePdf(PdfComplianceProfile.PdfA3B)));

        var extracted = await CreateController().Extract(
            Pdf(attached.FileContents), "factur-x.xml", CancellationToken.None);

        Assert.Equal(InvoiceXml, Assert.IsType<FileContentResult>(extracted).FileContents);
    }

    [Fact]
    public async Task AttachPreflight_ReportsStrategyValidationAndConsistencySeparately()
    {
        var result = await CreateController().AttachPreflight(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B)), Xml(),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, null, CancellationToken.None);

        var response = Assert.IsType<PdfEInvoiceAttachmentPreflightResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(PdfEInvoiceAttachmentStrategy.IncrementalUpdate, response.Attachment.Strategy);
        Assert.False(response.Attachment.RequiresArchiveConversion);
        Assert.True(response.Invoice.IsStructurallyValid);
        Assert.False(response.Invoice.IsConformanceValidated);
        Assert.Equal("notEvaluated", response.Invoice.ConformanceStatus);
        Assert.Equal("consistent", response.Consistency.Status);
    }

    [Fact]
    public async Task AttachPreflight_AnnouncesTheConversionANonArchivalSourceNeeds()
    {
        var result = await CreateController().AttachPreflight(
            Pdf(CreateSourcePdf(null)), Xml(),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, null, CancellationToken.None);

        var response = Assert.IsType<PdfEInvoiceAttachmentPreflightResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.Attachment.RequiresArchiveConversion);
        Assert.True(response.Attachment.RequiresApproval);
        Assert.Equal(PdfEInvoiceAttachmentStrategy.ArchiveConversion, response.Attachment.Strategy);
    }

    [Fact]
    public async Task Attach_BlocksAnUnapprovedConversionWithAStableProblemCode()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(null)), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: false,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAEINVAPI022", details.Extensions["code"]);
        Assert.NotNull(details.Extensions["preflight"]);
    }

    [Fact]
    public async Task Attach_ConvertsOnlyAfterTheCallerApprovesIt()
    {
        var controller = CreateController();

        var result = await controller.Attach(
            Pdf(CreateSourcePdf(null)), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: true,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: false,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        Assert.Equal("ArchiveConversion", controller.Response.Headers["PXA-Attachment-Strategy"]);
    }

    [Fact]
    public async Task Attach_RefusesAnInvoiceWhoseValuesAreMissingFromTheVisiblePdf()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B, "Delivery note without any invoice values")),
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: false,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("PXAEINVAPI021", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_AllowsAnInconsistentPdfOnlyWithExplicitApproval()
    {
        var controller = CreateController();

        var result = await controller.Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B, "Delivery note without any invoice values")),
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: true,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        Assert.Equal("reviewRequired", controller.Response.Headers["PXA-Invoice-Consistency"]);
    }

    [Fact]
    public async Task Attach_RejectsInvoiceXmlThatFailsStructuralValidation()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B)),
            Xml("<not-an-invoice/>"u8.ToArray()),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Equal("PXAEINVAPI020", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RejectsAProfileThatContradictsTheInvoiceXml()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B)), Xml(),
            "Factur-X", EInvoice.CurrentFacturXVersion, "EXTENDED",
            false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
    }

    [Fact]
    public async Task Attach_RejectsAnUnsupportedStandard()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B)), Xml(),
            "Peppol", "1.0", "BASIC", false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("PXAEINVAPI002", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RejectsAnUploadThatIsNotAPdf()
    {
        var result = await CreateController().Attach(
            new FormFile(new MemoryStream(InvoiceXml), 0, InvoiceXml.Length, "file", "invoice.xml")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/xml"
            },
            Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal("PXAEINVAPI010", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RejectsAMissingInvoiceUpload()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf(PdfComplianceProfile.PdfA3B)), null,
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal("PXAEINVAPI001", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_BlocksASecondInvoiceUntilReplacementIsApproved()
    {
        var first = Assert.IsType<FileContentResult>(
            await AttachAsync(CreateController(), CreateSourcePdf(PdfComplianceProfile.PdfA3B)));

        var blocked = await CreateController().Attach(
            Pdf(first.FileContents), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, replaceExistingInvoice: false, allowInconsistentPdf: false,
            preserveUnrecognized: false, attachmentFileName: null,
            cancellationToken: CancellationToken.None);
        var replaced = await CreateController().Attach(
            Pdf(first.FileContents), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, replaceExistingInvoice: true, allowInconsistentPdf: false,
            preserveUnrecognized: false, attachmentFileName: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(blocked).StatusCode);
        Assert.IsType<FileContentResult>(replaced);
    }

    private static async Task<IActionResult> AttachAsync(PdfEInvoicesController controller, byte[] source) =>
        await controller.Attach(
            Pdf(source), Xml(), "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, false, null, CancellationToken.None);

    private static PdfEInvoicesController CreateController() => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static byte[] CreateSourcePdf(
        PdfComplianceProfile? profile,
        string? text = null)
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.Info.Title = "Statement";
        document.AddPage().DrawText(
            text ?? $"Invoice {InvoiceNumber} Seller GmbH Buyer AG EUR 100.00 19.00 119.00",
            40,
            40,
            new PdfDrawTextOptions { Language = "en" });
        return profile is null
            ? document.ToBytes()
            : document.ToBytes(new PdfSaveOptions { ComplianceProfile = profile.Value });
    }

    private static FormFile Pdf(byte[] bytes) => File(bytes, "statement.pdf", "application/pdf");

    private static FormFile Xml(byte[]? bytes = null) =>
        File(bytes ?? InvoiceXml, "factur-x.xml", "application/xml");

    private static FormFile File(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}
