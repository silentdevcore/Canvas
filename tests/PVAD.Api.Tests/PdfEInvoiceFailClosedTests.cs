using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Generator;
using PVAD.Importer.EInvoicing;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

/// <summary>
/// The refusals PXA can prove without any official validation artifact.
/// </summary>
/// <remarks>
/// <para>
/// Official conformance needs the FeRD/FNFE and KoSIT artifact sets, whose licensing is unresolved,
/// so none of this claims an invoice is conformant. What it does prove is that every input PXA can
/// judge on its own - malformed XML, a DTD, a mismatched profile, an unsafe attachment, arithmetic
/// that does not add up, a payload that is too large, a filename or media type that is wrong - is
/// refused with a stable diagnostic rather than accepted quietly.
/// </para>
/// <para>
/// Every fixture here is synthetic and PXA-authored. No customer document appears in this file.
/// </para>
/// </remarks>
public sealed class PdfEInvoiceFailClosedTests
{
    // ── Malformed and hostile XML ────────────────────────────────────────────

    [Fact]
    public async Task Malformed_xml_is_refused_and_never_reported_as_recognized()
    {
        var result = await Validate("<rsm:CrossIndustryInvoice><unclosed>"u8.ToArray());

        Assert.False(result.IsStructurallyValid);
        Assert.False(result.IsConformanceValidated);
        Assert.Equal("notEvaluated", result.ConformanceStatus);
    }

    [Fact]
    public async Task A_document_type_definition_is_refused()
    {
        var xml = Encoding.UTF8.GetBytes(
            "<!DOCTYPE invoice [<!ENTITY payload \"expanded\">]>"
            + "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\" />");

        var result = await Validate(xml);

        Assert.False(result.IsStructurallyValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public async Task An_external_entity_reference_is_refused()
    {
        var xml = Encoding.UTF8.GetBytes(
            "<!DOCTYPE invoice [<!ENTITY external SYSTEM \"file:///etc/passwd\">]>"
            + "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\">"
            + "<rsm:ExchangedDocument>&external;</rsm:ExchangedDocument></rsm:CrossIndustryInvoice>");

        var result = await Validate(xml);

        Assert.False(result.IsStructurallyValid);
    }

    [Fact]
    public async Task Xml_that_is_not_an_invoice_at_all_is_not_recognized()
    {
        var result = await Validate("<html><body>Not an invoice</body></html>"u8.ToArray());

        Assert.False(result.IsStructurallyValid);
        Assert.Equal("notEvaluated", result.ConformanceStatus);
    }

    // ── Ambiguous or mismatched declarations ─────────────────────────────────

    [Fact]
    public async Task A_syntax_that_contradicts_the_declared_standard_is_refused()
    {
        // UBL content declared as Factur-X, which is a CII syntax.
        var ubl = Encoding.UTF8.GetBytes(
            "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\">"
            + "<ID>INV-1</ID></Invoice>");

        var result = await Validate(ubl, "Factur-X", EInvoice.CurrentFacturXVersion, "EN 16931");

        Assert.False(result.IsConformanceValidated);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public void A_version_that_is_not_the_maintained_one_is_refused()
    {
        var generated = EInvoice.GenerateXml(Invoice(), new PvadEInvoiceOptions
        {
            Standard = PvadEInvoiceStandard.FacturX,
            Version = "0.9",
            Profile = "EN 16931"
        });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public void A_profile_the_standard_does_not_define_is_refused()
    {
        var generated = EInvoice.GenerateXml(Invoice(), new PvadEInvoiceOptions
        {
            Standard = PvadEInvoiceStandard.XRechnung,
            Version = EInvoice.CurrentXRechnungVersion,
            Profile = "EXTENDED"
        });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    // ── Business arithmetic PXA can check on its own ─────────────────────────

    [Theory]
    [InlineData(0, "PXAEINV113")]
    [InlineData(-3, "PXAEINV113")]
    public void A_non_positive_quantity_is_refused_with_its_own_code(decimal quantity, string code)
    {
        var generated = Generate(Invoice() with
        {
            Lines = [new("1", "Service", quantity, "C62", 100m, 19m)]
        });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void A_negative_unit_price_is_refused()
    {
        var generated = Generate(Invoice() with
        {
            Lines = [new("1", "Service", 1, "C62", -1m, 19m)]
        });

        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV114");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_tax_rate_outside_nought_to_a_hundred_is_refused(decimal rate)
    {
        var generated = Generate(Invoice() with
        {
            Lines = [new("1", "Service", 1, "C62", 100m, rate)]
        });

        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV115");
    }

    [Fact]
    public void A_duplicated_line_identifier_is_refused()
    {
        var generated = Generate(Invoice() with
        {
            Lines =
            [
                new("1", "First", 1, "C62", 100m, 19m),
                new("1", "Second", 1, "C62", 50m, 19m)
            ]
        });

        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV112");
    }

    [Fact]
    public void A_due_date_before_the_issue_date_is_refused()
    {
        var generated = Generate(Invoice() with { DueDate = new DateOnly(2020, 1, 1) });

        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV116");
    }

    [Fact]
    public void An_invoice_with_no_lines_is_refused()
    {
        var generated = Generate(Invoice() with { Lines = [] });

        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV106");
    }

    [Fact]
    public void The_generated_totals_are_the_ones_the_lines_add_up_to()
    {
        // Not a conformance claim - just that PXA's own arithmetic reaches the XML unaltered.
        var generated = Generate(Invoice() with
        {
            Lines =
            [
                new("1", "A", 3, "C62", 33.33m, 19m),
                new("2", "B", 1, "C62", 0.01m, 19m)
            ]
        });

        Assert.True(generated.IsGenerated);
        var xml = Encoding.UTF8.GetString(generated.Xml.Span);
        Assert.Contains("<ram:LineTotalAmount>100.00</ram:LineTotalAmount>", xml, StringComparison.Ordinal);
        Assert.Contains("<ram:GrandTotalAmount>119.00</ram:GrandTotalAmount>", xml, StringComparison.Ordinal);
    }

    // ── Oversized and malformed payloads ─────────────────────────────────────

    [Fact]
    public void More_lines_than_the_synchronous_limit_is_refused()
    {
        var lines = Enumerable.Range(1, 10_001)
            .Select(index => new PvadEInvoiceDocumentLine(index.ToString(), "Line", 1, "C62", 1m, 19m))
            .ToArray();

        var generated = Generate(Invoice() with { Lines = lines });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV107");
    }

    [Fact]
    public void A_description_beyond_its_length_limit_is_refused()
    {
        var generated = Generate(Invoice() with
        {
            Lines = [new("1", new string('x', 4_097), 1, "C62", 100m, 19m)]
        });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV109");
    }

    [Fact]
    public void A_party_name_beyond_its_length_limit_is_refused()
    {
        var generated = Generate(Invoice() with
        {
            Seller = new(new string('s', 201), Address: new("Street 1", "Berlin", "10115", "DE"))
        });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV104");
    }

    [Theory]
    [InlineData("EURO")]
    [InlineData("E1")]
    [InlineData("12")]
    public void A_currency_that_is_not_three_letters_is_refused(string currency)
    {
        var generated = Generate(Invoice() with { Currency = currency });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics,
            diagnostic => diagnostic.Code is "PXAEINV101" or "PXAEINV102");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Currency_minor_units_outside_the_supported_range_are_refused(int places)
    {
        var generated = Generate(Invoice() with { CurrencyDecimalPlaces = places });

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV103");
    }

    // ── Attachment safety ────────────────────────────────────────────────────

    [Fact]
    public async Task A_pdf_that_is_not_a_pdf_is_refused_by_inspection()
    {
        var result = await new PdfEInvoicesController().Inspect(
            FormFile("not a pdf at all"u8.ToArray(), "invoice.pdf", "application/pdf"),
            CancellationToken.None);

        Assert.IsNotType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task An_empty_upload_is_refused()
    {
        var result = await new PdfEInvoicesController().Inspect(
            FormFile([], "invoice.pdf", "application/pdf"),
            CancellationToken.None);

        Assert.IsNotType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task A_missing_upload_is_refused_rather_than_defaulted()
    {
        var result = await new PdfEInvoicesController().Validate(
            null, "Factur-X", EInvoice.CurrentFacturXVersion, "EN 16931", CancellationToken.None);

        Assert.IsNotType<OkObjectResult>(result.Result);
    }

    // ── PDF and XML disagreement ─────────────────────────────────────────────

    [Fact]
    public void Page_text_that_contradicts_the_embedded_xml_is_reported_for_review()
    {
        var report = PvadEInvoicePdfConsistencyInspector.Inspect(
            "Invoice INV-1 Seller S Buyer B EUR 1.00",
            new PvadEInvoiceSemanticData(
                "INV-1", null, "EUR", new("S", null), new("B", null),
                100m, 100m, 19m, 119m, 119m, []));

        Assert.Equal("reviewRequired", report.Status);
    }

    [Fact]
    public void A_document_with_no_extractable_text_is_never_called_consistent()
    {
        var report = PvadEInvoicePdfConsistencyInspector.Inspect(
            string.Empty,
            new PvadEInvoiceSemanticData(
                "INV-1", null, "EUR", new("S", null), new("B", null),
                100m, 100m, 19m, 119m, 119m, []));

        Assert.Equal("notEvaluated", report.Status);
        Assert.NotEqual("consistent", report.Status);
    }

    // ── Conformance is never asserted without evidence ───────────────────────

    [Fact]
    public async Task A_structurally_valid_invoice_is_still_not_conformance_validated()
    {
        var generated = Generate(Invoice());
        Assert.True(generated.IsGenerated);

        var result = await Validate(generated.Xml.ToArray());

        // Structural recognition and official conformance are different states, and PXA ships no
        // official artifact set, so the second must stay unevaluated however clean the first is.
        Assert.True(result.IsStructurallyValid);
        Assert.False(result.IsConformanceValidated);
        Assert.Equal("notEvaluated", result.ConformanceStatus);
        Assert.Null(result.ValidatorArtifactSetId);
    }

    [Fact]
    public async Task No_validation_path_can_report_valid_without_an_artifact_set()
    {
        foreach (var payload in new[]
        {
            "<html/>"u8.ToArray(),
            "<rsm:CrossIndustryInvoice/>"u8.ToArray(),
            Generate(Invoice()).Xml.ToArray()
        })
        {
            var result = await Validate(payload);
            Assert.NotEqual("valid", result.ConformanceStatus);
            if (result.ConformanceStatus == "notEvaluated") Assert.Null(result.ValidatorArtifactSetId);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<PvadEInvoiceValidationReport> Validate(
        byte[] xml,
        string standard = "Factur-X",
        string? version = null,
        string profile = "EN 16931")
    {
        var result = await new PdfEInvoicesController().Validate(
            FormFile(xml, "factur-x.xml", "application/xml"),
            standard,
            version ?? EInvoice.CurrentFacturXVersion,
            profile,
            CancellationToken.None);

        return result.Result switch
        {
            OkObjectResult ok => (PvadEInvoiceValidationReport)ok.Value!,
            ObjectResult problem when problem.Value is PvadEInvoiceValidationReport report => report,
            // Refused before a report was produced, which is itself a fail-closed outcome.
            _ => new PvadEInvoiceValidationReport(
                IsStructurallyValid: false,
                IsConformanceValidated: false,
                Syntax: null,
                GuidelineIdentifier: null,
                Diagnostics: [new PvadEInvoiceDiagnostic("PXAEINVTEST", "The request was refused before validation produced a report.", "error")])
        };
    }

    private static PvadEInvoiceGenerationResult Generate(PvadEInvoiceDocument invoice) =>
        EInvoice.GenerateXml(invoice, new PvadEInvoiceOptions
        {
            Standard = PvadEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "EN 16931"
        });

    private static PvadEInvoiceDocument Invoice() => new()
    {
        InvoiceNumber = "FAILCLOSED-1",
        IssueDate = new DateOnly(2026, 9, 27),
        Currency = "EUR",
        Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
        Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
        Lines = [new("1", "Consulting", 1, "C62", 100m, 19m)]
    };

    private static FormFile FormFile(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}
