using PXA.Generator;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

/// <summary>
/// The PDF/XML comparison must not report a correct invoice as needing review. Two minor digits
/// are not universal, and a multi-rate invoice presents its tax as a breakdown.
/// </summary>
public sealed class PxaEInvoicePdfConsistencyInspectorTests
{
    [Theory]
    // A currency with three minor units, as Gulf currencies use.
    [InlineData("Invoice INV-1 Seller GmbH Buyer AG BHD 24.691 1.235 25.926", 3)]
    // A currency with no minor units, as JPY does.
    [InlineData("Invoice INV-1 Seller GmbH Buyer AG JPY 4502 450 4952", 0)]
    public void Amounts_outside_two_minor_digits_are_confirmed(string visibleText, int scale)
    {
        var amounts = scale switch
        {
            3 => (Net: 24.691m, Tax: 1.235m, Gross: 25.926m),
            _ => (Net: 4502m, Tax: 450m, Gross: 4952m)
        };

        var report = PxaEInvoicePdfConsistencyInspector.Inspect(
            visibleText,
            new PxaEInvoiceSemanticData(
                "INV-1",
                null,
                scale == 3 ? "BHD" : "JPY",
                new("Seller GmbH", null),
                new("Buyer AG", null),
                amounts.Net,
                amounts.Net,
                amounts.Tax,
                amounts.Gross,
                amounts.Gross,
                []));

        Assert.Equal("consistent", report.Status);
        Assert.DoesNotContain(report.Findings, finding => finding.Status == "notFound");
    }

    [Fact]
    public void An_amount_reports_the_scale_it_actually_has()
    {
        var report = PxaEInvoicePdfConsistencyInspector.Inspect(
            "Invoice INV-1 Seller S Buyer B EUR 24.691",
            new PxaEInvoiceSemanticData(
                "INV-1", null, "EUR", new("S", null), new("B", null),
                null, 24.691m, null, null, null, []));

        var finding = Assert.Single(report.Findings, item => item.Field == "taxExclusiveTotal");
        Assert.Equal("24.691", finding.ExpectedValue);
        Assert.Equal("confirmed", finding.Status);
    }

    [Fact]
    public void A_multi_rate_invoice_shows_and_confirms_its_aggregate_tax_total()
    {
        var invoice = new PxaEInvoiceDocument
        {
            InvoiceNumber = "MULTI-1",
            IssueDate = new DateOnly(2026, 9, 27),
            Currency = "EUR",
            Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
            Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
            Lines =
            [
                new("1", "Standard goods", 2, "C62", 50m, 19m),
                new("2", "Books", 3, "C62", 20m, 7m)
            ]
        };
        var fonts = new PXA.Pdf.PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));

        var created = EInvoice.CreatePdf(
            invoice,
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.FacturX,
                Version = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931"
            },
            fonts);

        Assert.True(created.IsGenerated);
        var text = PxaEInvoicePdfConsistencyInspector
            .ExtractVisibleTextAsync(created.Pdf, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.NotNull(text);
        // 19% of 100.00 plus 7% of 60.00 is 23.20; the page has to state it, not only the parts.
        Assert.Contains("23.20", text!, StringComparison.Ordinal);
        Assert.Contains("19.00", text!, StringComparison.Ordinal);
        Assert.Contains("4.20", text!, StringComparison.Ordinal);

        var report = PxaEInvoicePdfConsistencyInspector.Inspect(text, created.SemanticData);
        Assert.Equal("consistent", report.Status);
    }

    [Fact]
    public void A_single_rate_invoice_does_not_repeat_its_tax_row()
    {
        var invoice = new PxaEInvoiceDocument
        {
            InvoiceNumber = "SINGLE-1",
            IssueDate = new DateOnly(2026, 9, 27),
            Currency = "EUR",
            Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
            Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
            Lines = [new("1", "Consulting", 1, "C62", 100m, 19m)]
        };
        var fonts = new PXA.Pdf.PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));

        var created = EInvoice.CreatePdf(
            invoice,
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.FacturX,
                Version = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931"
            },
            fonts);
        var text = PxaEInvoicePdfConsistencyInspector
            .ExtractVisibleTextAsync(created.Pdf, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        // The group row already is the total, so the layout stays as it was.
        Assert.DoesNotContain("Tax total", text!, StringComparison.Ordinal);
        Assert.Contains("Tax 19%", text!, StringComparison.Ordinal);
        Assert.Equal(
            "consistent",
            PxaEInvoicePdfConsistencyInspector.Inspect(text, created.SemanticData).Status);
    }

    [Fact]
    public void A_contradicting_amount_is_still_reported_for_review()
    {
        var report = PxaEInvoicePdfConsistencyInspector.Inspect(
            "Invoice INV-1 Seller S Buyer B EUR 999.00",
            new PxaEInvoiceSemanticData(
                "INV-1", null, "EUR", new("S", null), new("B", null),
                100m, 100m, 19m, 119m, 119m, []));

        Assert.Equal("reviewRequired", report.Status);
        Assert.Contains(report.Findings, finding =>
            finding.Field == "payableAmount" && finding.Status == "notFound");
    }

    [Fact]
    public void Missing_page_text_stays_not_evaluated_rather_than_consistent()
    {
        var report = PxaEInvoicePdfConsistencyInspector.Inspect(
            null,
            new PxaEInvoiceSemanticData(
                "INV-1", null, "EUR", new("S", null), new("B", null),
                100m, 100m, 19m, 119m, 119m, []));

        Assert.Equal("notEvaluated", report.Status);
        Assert.Contains(report.Findings, finding => finding.Status == "notCheckable");
    }
}
