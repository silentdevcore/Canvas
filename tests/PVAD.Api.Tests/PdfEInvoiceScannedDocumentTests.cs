using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Generator;
using PVAD.Pdf;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

/// <summary>
/// EINV-09: a PDF whose pages carry no extractable text - a scan, or a page drawn entirely as
/// graphics - must stay at `notEvaluated`. Treating "we could not read the page" as "the page
/// agrees with the XML" would be the most dangerous false positive this comparison can produce.
/// </summary>
public sealed class PdfEInvoiceScannedDocumentTests
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
          <rsm:ExchangedDocument><ram:ID>SCAN-1</ram:ID></rsm:ExchangedDocument>
          <rsm:SupplyChainTradeTransaction>
            <ram:ApplicableHeaderTradeAgreement><ram:SellerTradeParty><ram:Name>Seller GmbH</ram:Name></ram:SellerTradeParty><ram:BuyerTradeParty><ram:Name>Buyer AG</ram:Name></ram:BuyerTradeParty></ram:ApplicableHeaderTradeAgreement>
            <ram:ApplicableHeaderTradeSettlement><ram:InvoiceCurrencyCode>EUR</ram:InvoiceCurrencyCode><ram:TaxTotalAmount>19.00</ram:TaxTotalAmount><ram:SpecifiedTradeSettlementHeaderMonetarySummation><ram:TaxBasisTotalAmount>100.00</ram:TaxBasisTotalAmount><ram:GrandTotalAmount>119.00</ram:GrandTotalAmount><ram:DuePayableAmount>119.00</ram:DuePayableAmount></ram:SpecifiedTradeSettlementHeaderMonetarySummation></ram:ApplicableHeaderTradeSettlement>
          </rsm:SupplyChainTradeTransaction>
        </rsm:CrossIndustryInvoice>
        """);

    [Fact]
    public async Task Inspect_keeps_a_text_inaccessible_page_at_not_evaluated()
    {
        var pdf = TextFreeInvoicePdf();

        var inspected = await CreateController().Inspect(
            FormFile(pdf, "scan.pdf", "application/pdf"),
            CancellationToken.None);

        var summary = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
                Assert.IsType<OkObjectResult>(inspected.Result).Value));
        Assert.Equal("notEvaluated", summary.PdfConsistency.Status);
        Assert.All(
            summary.PdfConsistency.Findings,
            finding => Assert.Equal("notCheckable", finding.Status));
        Assert.DoesNotContain(summary.PdfConsistency.Findings, finding => finding.Status == "confirmed");

        // The embedded invoice is still read and reported; only the comparison is withheld.
        Assert.Equal("SCAN-1", summary.SemanticData?.InvoiceNumber);
        Assert.Equal(119.00m, summary.SemanticData?.PayableAmount);
        Assert.True(summary.IsStructurallyValid);
        Assert.Equal("notEvaluated", summary.ConformanceStatus);
    }

    [Fact]
    public async Task Attach_does_not_block_on_a_text_inaccessible_page_but_reports_it()
    {
        var controller = CreateController();

        var attached = await controller.Attach(
            FormFile(TextFreePdf(), "scan.pdf", "application/pdf"),
            FormFile(InvoiceXml, "factur-x.xml", "application/xml"),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            // Deliberately not approving an inconsistent attachment.
            allowInconsistentPdf: false,
            preserveUnrecognized: false,
            attachmentFileName: null,
            CancellationToken.None);

        // "Not evaluated" is not "inconsistent", so the operation proceeds - and says so.
        Assert.IsType<FileContentResult>(attached);
        Assert.Equal("notEvaluated", controller.Response.Headers["PXA-Invoice-Consistency"]);
    }

    [Fact]
    public async Task Attach_still_blocks_when_the_page_is_readable_and_contradicts_the_invoice()
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = Fonts;
        document.AddPage().DrawText(
            "Delivery note for order 55 - no amounts", 40, 40, new PdfDrawTextOptions { Language = "en" });
        var readable = document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });

        var result = await CreateController().Attach(
            FormFile(readable, "note.pdf", "application/pdf"),
            FormFile(InvoiceXml, "factur-x.xml", "application/xml"),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, false, false, null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("PXAEINVAPI021", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    /// <summary>A page drawn only as graphics: the closest deterministic stand-in for a scan.</summary>
    private static byte[] TextFreePdf()
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = Fonts;
        var page = document.AddPage();
        page.DrawRectangle(40, 40, 400, 200, 2, fill: true, fillColor: PdfColor.FromRgb(220, 220, 220));
        page.DrawRectangle(60, 260, 360, 120, 1);
        page.DrawLine(40, 420, 440, 420, 1);
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static byte[] TextFreeInvoicePdf()
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = Fonts;
        var page = document.AddPage();
        page.DrawRectangle(40, 40, 400, 200, 2, fill: true, fillColor: PdfColor.FromRgb(220, 220, 220));
        page.DrawLine(40, 300, 440, 300, 1);
        EInvoice.Attach(document, InvoiceXml, new PvadEInvoiceOptions
        {
            Standard = PvadEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "BASIC"
        });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static readonly PdfFontLoader Fonts =
        new(Path.Combine(AppContext.BaseDirectory, "fonts"));

    private static PdfEInvoicesController CreateController() => new(fontLoader: Fonts)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static FormFile FormFile(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}
