using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Pdf;
using PXA.WebApi.Controllers;

namespace PXA.Api.Tests;

/// <summary>
/// EINV-10: XML PXA does not recognize may be stored byte for byte, but only when the caller
/// asks for it, and never as a standards claim.
/// </summary>
public sealed class PdfEInvoicePreservedXmlTests
{
    /// <summary>Well-formed XML that is not a supported invoice syntax.</summary>
    private static readonly byte[] UnknownXml = Encoding.UTF8.GetBytes(
        """<vendor:Rechnung xmlns:vendor="urn:example:legacy:billing:1"><Nummer>LEGACY-5</Nummer><Betrag>119,00</Betrag></vendor:Rechnung>""");

    [Fact]
    public async Task Attach_RefusesUnknownXmlUnlessPreservationIsRequested()
    {
        var result = await Attach(preserve: false);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Equal("PXAEINVAPI020", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RefusesToMislabelARecognizedInvoiceAsUnknownXml()
    {
        var result = await Attach(preserve: true, xml: FacturXXml);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("PXAEINVAPI028", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_PreservesUnknownXmlByteForByteWhenRequested()
    {
        var controller = CreateController();

        var attached = Assert.IsType<FileContentResult>(await Attach(preserve: true, controller: controller));

        var extracted = await CreateController().Extract(
            Pdf(attached.FileContents), "preserved-invoice.xml", CancellationToken.None);
        Assert.Equal(UnknownXml, Assert.IsType<FileContentResult>(extracted).FileContents);
        Assert.Equal("PreservedUnknownXml", controller.Response.Headers["PXA-Invoice-Mode"]);
        Assert.Equal("unrecognized", controller.Response.Headers["PXA-Invoice-Recognition"]);
        Assert.Equal("false", controller.Response.Headers["PXA-Invoice-Editable"]);
        Assert.Equal("notEvaluated", controller.Response.Headers["PXA-Invoice-Conformance"]);
    }

    [Fact]
    public async Task Inspect_ReportsPreservedXmlAsUnrecognizedNonEditableAndNonConformant()
    {
        var attached = Assert.IsType<FileContentResult>(await Attach(preserve: true));

        var inspected = await CreateController().Inspect(
            Pdf(attached.FileContents), CancellationToken.None);

        var summaries = Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(inspected.Result).Value);
        var summary = Assert.Single(summaries, item => item.FileName == "preserved-invoice.xml");
        Assert.True(summary.IsXml);
        Assert.Equal("unrecognized", summary.RecognitionStatus);
        Assert.False(summary.IsEditable);
        Assert.False(summary.IsStructurallyValid);
        Assert.Equal("notEvaluated", summary.ConformanceStatus);
        Assert.Null(summary.Standard);
        Assert.Null(summary.Profile);
        Assert.Null(summary.SemanticData);
        // /Unspecified, not /Alternative: it is not an alternative rendering of this document.
        Assert.Equal("Unspecified", summary.Relationship);
        Assert.True(summary.SizeMatches);
        Assert.True(summary.ChecksumMatches);
    }

    [Fact]
    public async Task Attach_DoesNotWriteAnInvoiceExtensionSchemaForPreservedXml()
    {
        var attached = Assert.IsType<FileContentResult>(await Attach(preserve: true));

        var text = Encoding.UTF8.GetString(attached.FileContents);
        Assert.DoesNotContain("urn:factur-x:pdfa:CrossIndustryDocument", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<fx:DocumentType>", text, StringComparison.Ordinal);
        // The PDF/A-3 identification is still asserted, because that part is true.
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachPreflight_ReportsThePreservationModeAndItsNonEditableState()
    {
        var result = await CreateController().AttachPreflight(
            Pdf(CreateSourcePdf()), Xml(UnknownXml),
            standard: null, version: null, profile: null,
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            preserveUnrecognized: true,
            attachmentFileName: null,
            CancellationToken.None);

        var response = Assert.IsType<PdfEInvoiceAttachmentPreflightResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(PdfEInvoiceAttachmentMode.PreservedUnknownXml, response.Mode);
        Assert.False(response.IsEditable);
        Assert.Equal("preserved-invoice.xml", response.AttachmentFileName);
        Assert.False(response.Invoice.IsStructurallyValid);
        Assert.False(response.Invoice.IsConformanceValidated);
        Assert.Equal("notEvaluated", response.Invoice.ConformanceStatus);
    }

    [Theory]
    [InlineData("factur-x.xml")]
    [InlineData("ZUGFeRD-invoice.xml")]
    [InlineData("xrechnung.xml")]
    [InlineData("order-x.xml")]
    public async Task Attach_RefusesToStorePreservedXmlUnderAReservedInvoiceFileName(string fileName)
    {
        var result = await Attach(preserve: true, attachmentFileName: fileName);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("PXAEINVAPI026", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Theory]
    [InlineData("../escape.xml")]
    [InlineData("sub/escape.xml")]
    [InlineData("payload.exe")]
    [InlineData(".xml")]
    public async Task Attach_RefusesAnUnsafePreservedFileName(string fileName)
    {
        var result = await Attach(preserve: true, attachmentFileName: fileName);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("PXAEINVAPI025", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RefusesToPreserveXmlThatDeclaresADoctype()
    {
        var doctype = Encoding.UTF8.GetBytes(
            """<?xml version="1.0"?><!DOCTYPE root [<!ENTITY x "expanded">]><root>&x;</root>""");

        var result = await Attach(preserve: true, xml: doctype);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Equal("PXAEINVAPI027", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RefusesToPreserveMalformedXml()
    {
        var result = await Attach(preserve: true, xml: "<unclosed>"u8.ToArray());

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Equal("PXAEINVAPI027", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_RefusesToPreserveAnEmptyUpload()
    {
        var result = await CreateController().Attach(
            Pdf(CreateSourcePdf()),
            Xml([]),
            null, null, null, false, false, false, false,
            preserveUnrecognized: true,
            attachmentFileName: null,
            CancellationToken.None);

        Assert.Equal("PXAEINVAPI001", Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(result).Value).Extensions["code"]);
    }

    [Fact]
    public async Task Attach_KeepsARecognizedInvoiceAndPreservedXmlSideBySide()
    {
        var hybrid = Assert.IsType<FileContentResult>(await CreateController().Attach(
            Pdf(CreateSourcePdf()), Xml(FacturXXml),
            "Factur-X", EInvoice.CurrentFacturXVersion, "BASIC",
            false, false, false, allowInconsistentPdf: true,
            preserveUnrecognized: false, attachmentFileName: null,
            CancellationToken.None));

        var both = Assert.IsType<FileContentResult>(await CreateController().Attach(
            Pdf(hybrid.FileContents), Xml(UnknownXml),
            null, null, null, false, false, false, false,
            preserveUnrecognized: true, attachmentFileName: "legacy-billing.xml",
            CancellationToken.None));

        var inspected = await CreateController().Inspect(Pdf(both.FileContents), CancellationToken.None);
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(inspected.Result).Value);
        Assert.Equal("recognized", Assert.Single(summaries, item => item.FileName == "factur-x.xml").RecognitionStatus);
        var preserved = Assert.Single(summaries, item => item.FileName == "legacy-billing.xml");
        Assert.Equal("unrecognized", preserved.RecognitionStatus);
        Assert.False(preserved.IsEditable);
    }

    private static readonly byte[] FacturXXml = Encoding.UTF8.GetBytes("""
        <rsm:CrossIndustryInvoice
          xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"
          xmlns:ram="urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100">
          <rsm:ExchangedDocumentContext>
            <ram:GuidelineSpecifiedDocumentContextParameter>
              <ram:ID>urn:factur-x.eu:1p0:basic</ram:ID>
            </ram:GuidelineSpecifiedDocumentContextParameter>
          </rsm:ExchangedDocumentContext>
          <rsm:ExchangedDocument><ram:ID>PRESERVE-1</ram:ID></rsm:ExchangedDocument>
        </rsm:CrossIndustryInvoice>
        """);

    private static Task<IActionResult> Attach(
        bool preserve,
        PdfEInvoicesController? controller = null,
        string? attachmentFileName = null,
        byte[]? xml = null) =>
        (controller ?? CreateController()).Attach(
            Pdf(CreateSourcePdf()),
            Xml(xml ?? UnknownXml),
            standard: preserve ? null : "Factur-X",
            version: preserve ? null : EInvoice.CurrentFacturXVersion,
            profile: preserve ? null : "BASIC",
            allowArchiveConversion: false,
            allowSignatureInvalidation: false,
            replaceExistingInvoice: false,
            allowInconsistentPdf: true,
            preserveUnrecognized: preserve,
            attachmentFileName: attachmentFileName,
            CancellationToken.None);

    private static PdfEInvoicesController CreateController() => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static byte[] CreateSourcePdf()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        document.AddPage().DrawText(
            "Legacy statement LEGACY-5", 40, 40, new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static FormFile Pdf(byte[] bytes) => File(bytes, "statement.pdf", "application/pdf");

    private static FormFile Xml(byte[] bytes) => File(bytes, "legacy.xml", "application/xml");

    private static FormFile File(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}
