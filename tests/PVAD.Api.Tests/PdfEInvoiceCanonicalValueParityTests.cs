using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Generator;
using PVAD.Pdf;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

/// <summary>
/// EINV-05: closes the loop across all four surfaces for the shared parity corpus.
///
/// The Designer asserts the same corpus from TypeScript
/// (<c>pvad-designer/src/__tests__/eInvoiceTotalsParity.test.ts</c>) and the generator asserts it
/// from C# (<c>EInvoiceTotalsParityTests</c>). Here the same numbers are put through the real
/// endpoints: <c>create</c> renders the visible PDF and embeds the XML, and <c>inspect</c> reads
/// the values back out of the finished document. When all three pass, the Designer preview, the
/// generated XML, the visible PDF and a reopened Viewer inspection carry one set of values.
/// </summary>
public sealed class PdfEInvoiceCanonicalValueParityTests
{
    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Corpus)
        {
            data.Add(testCase.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public async Task Created_pdf_reports_the_same_canonical_values_it_was_given(string caseId)
    {
        var testCase = Corpus.Single(item => item.Id == caseId);
        var controller = CreateController();

        var created = controller.Create(RequestFor(testCase));

        var pdf = Assert.IsType<FileContentResult>(created);
        var inspected = await CreateController().Inspect(
            FormFile(pdf.FileContents, "invoice.pdf", "application/pdf"),
            CancellationToken.None);
        var summary = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
                Assert.IsType<OkObjectResult>(inspected.Result).Value));
        var semantic = summary.SemanticData;

        Assert.NotNull(semantic);
        Assert.Equal("PARITY-" + testCase.Id, semantic!.InvoiceNumber);
        Assert.Equal(
            testCase.Expected.LineNetTotal,
            Money(semantic.LineNetTotal, testCase.CurrencyDecimalPlaces));
        Assert.Equal(
            testCase.Expected.TaxTotal,
            Money(semantic.TaxTotal, testCase.CurrencyDecimalPlaces));
        Assert.Equal(
            testCase.Expected.GrandTotal,
            Money(semantic.TaxInclusiveTotal, testCase.CurrencyDecimalPlaces));
        Assert.Equal(
            testCase.Expected.GrandTotal,
            Money(semantic.PayableAmount, testCase.CurrencyDecimalPlaces));
        Assert.Equal(
            testCase.Expected.LineNetAmounts,
            semantic.Lines
                .Select(line => Money(line.NetAmount, testCase.CurrencyDecimalPlaces))
                .ToArray());

        // The rendered page must show the same figures the embedded XML carries, otherwise the
        // hybrid invoice would contradict itself.
        Assert.Equal(
            "consistent",
            summary.PdfConsistency.Status);
        Assert.DoesNotContain(summary.PdfConsistency.Findings, finding => finding.Status == "notFound");
        Assert.True(summary.IsStructurallyValid);
        // Still never a conformance claim.
        Assert.Equal("notEvaluated", summary.ConformanceStatus);
    }

    [Fact]
    public async Task Generated_xml_and_inspected_pdf_agree_on_parties_dates_and_currency()
    {
        var testCase = Corpus.Single(item => item.Id == "multiple-rates-ordered-by-rate");
        var controller = CreateController();

        var xml = Assert.IsType<FileContentResult>(controller.Generate(RequestFor(testCase)));
        var pdf = Assert.IsType<FileContentResult>(CreateController().Create(RequestFor(testCase)));

        var fromXml = EInvoice.Recognize(xml.FileContents).SemanticData;
        var inspected = await CreateController().Inspect(
            FormFile(pdf.FileContents, "invoice.pdf", "application/pdf"),
            CancellationToken.None);
        var fromPdf = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
                Assert.IsType<OkObjectResult>(inspected.Result).Value)).SemanticData;

        Assert.NotNull(fromXml);
        Assert.NotNull(fromPdf);
        Assert.Equal(fromXml!.InvoiceNumber, fromPdf!.InvoiceNumber);
        Assert.Equal(fromXml.Currency, fromPdf.Currency);
        Assert.Equal(fromXml.IssueDate, fromPdf.IssueDate);
        Assert.Equal(fromXml.Seller.Name, fromPdf.Seller.Name);
        Assert.Equal(fromXml.Buyer.Name, fromPdf.Buyer.Name);
        Assert.Equal(fromXml.PayableAmount, fromPdf.PayableAmount);
        Assert.Equal(
            fromXml.Lines.Select(line => (line.Identifier, line.Description, line.NetAmount)),
            fromPdf.Lines.Select(line => (line.Identifier, line.Description, line.NetAmount)));
    }

    [Fact]
    public async Task Attaching_the_generated_xml_to_the_generated_pdf_keeps_the_same_values()
    {
        var testCase = Corpus.Single(item => item.Id == "single-standard-rate");
        var request = RequestFor(testCase);
        var xml = Assert.IsType<FileContentResult>(CreateController().Generate(request));
        // A visible invoice page without an embedded invoice yet.
        var visible = VisiblePdfFor(testCase);
        var controller = CreateController();

        var attached = await controller.Attach(
            FormFile(visible, "invoice.pdf", "application/pdf"),
            FormFile(xml.FileContents, "factur-x.xml", "application/xml"),
            "Factur-X", EInvoice.CurrentFacturXVersion, "EN 16931",
            false, false, false, false, false, null, CancellationToken.None);

        var result = Assert.IsType<FileContentResult>(attached);
        Assert.Equal("true", controller.Response.Headers["PXA-Pages-Unchanged"]);
        var inspected = await CreateController().Inspect(
            FormFile(result.FileContents, "invoice.pdf", "application/pdf"),
            CancellationToken.None);
        var summary = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
                Assert.IsType<OkObjectResult>(inspected.Result).Value));

        Assert.Equal(
            testCase.Expected.GrandTotal,
            Money(summary.SemanticData?.PayableAmount, testCase.CurrencyDecimalPlaces));
        Assert.Equal("consistent", summary.PdfConsistency.Status);
        Assert.Equal("recognized", summary.RecognitionStatus);
        Assert.True(summary.IsEditable);
    }

    /// <summary>
    /// A visible, already archival invoice page that carries no invoice XML yet - the realistic
    /// starting point for `attach`. It shows every value the embedded XML will declare, including
    /// the issue date in the CII "102" form the XML uses.
    /// </summary>
    private static byte[] VisiblePdfFor(ParityCase testCase)
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.FontLoader = Fonts;
        var totals = testCase.Expected;
        document.AddPage().DrawText(
            $"Invoice PARITY-{testCase.Id} 20260927 Seller GmbH Buyer AG EUR " +
                $"{string.Join(' ', totals.LineNetAmounts)} {totals.LineNetTotal} " +
                $"{totals.TaxTotal} {totals.GrandTotal} " +
                string.Join(' ', testCase.Lines.Select(line => $"{line.Identifier} {line.Description}")),
            40,
            40,
            new PdfDrawTextOptions { Language = "en" });
        return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA3B });
    }

    private static PdfEInvoiceGenerationRequest RequestFor(ParityCase testCase) => new(
        "FacturX",
        EInvoice.CurrentFacturXVersion,
        "EN 16931",
        new PvadEInvoiceDocument
        {
            InvoiceNumber = "PARITY-" + testCase.Id,
            IssueDate = new DateOnly(2026, 9, 27),
            Currency = "EUR",
            CurrencyDecimalPlaces = testCase.CurrencyDecimalPlaces,
            Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
            Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
            Lines = testCase.Lines
                .Select(line => new PvadEInvoiceDocumentLine(
                    line.Identifier,
                    line.Description,
                    line.Quantity,
                    line.UnitCode,
                    line.UnitPrice,
                    line.TaxRate,
                    line.TaxCategoryCode))
                .ToArray()
        });

    private static readonly PdfFontLoader Fonts =
        new(Path.Combine(AppContext.BaseDirectory, "fonts"));

    private static PdfEInvoicesController CreateController() => new(fontLoader: Fonts)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static string Money(decimal? value, int decimalPlaces) =>
        value?.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture) ?? "<null>";

    private static FormFile FormFile(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static IReadOnlyList<ParityCase> Corpus { get; } = LoadCorpus();

    private static IReadOnlyList<ParityCase> LoadCorpus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "test-assets", "e-invoicing", "totals-parity.json");
            if (File.Exists(candidate))
            {
                return JsonSerializer.Deserialize<ParityCorpus>(
                    File.ReadAllText(candidate),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Cases;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("test-assets/e-invoicing/totals-parity.json was not found.");
    }

    private sealed record ParityCorpus(string Schema, IReadOnlyList<ParityCase> Cases);

    private sealed record ParityCase(
        string Id,
        string Description,
        int CurrencyDecimalPlaces,
        IReadOnlyList<ParityLine> Lines,
        ParityExpectation Expected);

    private sealed record ParityLine(
        string Identifier,
        string Description,
        decimal Quantity,
        string UnitCode,
        decimal UnitPrice,
        decimal TaxRate,
        string TaxCategoryCode);

    private sealed record ParityExpectation(
        IReadOnlyList<string> LineNetAmounts,
        string LineNetTotal,
        string TaxTotal,
        string GrandTotal);
}
