using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PXA.Generator;

namespace PXA.Generator.Tests;

/// <summary>
/// Asserts the generator against the shared parity corpus in
/// <c>test-assets/e-invoicing/totals-parity.json</c>. The Designer asserts the same file from
/// TypeScript, so a change to either calculation that is not mirrored in the other fails here.
/// </summary>
public sealed class EInvoiceTotalsParityTests
{
    private static readonly XNamespace Aggregate =
        "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var testCase in Corpus.Cases)
        {
            data.Add(testCase.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void Generated_xml_carries_the_pinned_totals(string caseId)
    {
        var testCase = Corpus.Cases.Single(item => item.Id == caseId);

        var result = EInvoice.GenerateXml(
            BuildInvoice(testCase),
            new PxaEInvoiceOptions
            {
                Standard = PxaEInvoiceStandard.FacturX,
                Version = EInvoice.CurrentFacturXVersion,
                Profile = "EN 16931"
            });

        Assert.True(
            result.IsGenerated,
            $"Case '{caseId}' failed to generate: " +
                string.Join(
                    "; ",
                    result.Diagnostics.Where(item => item.Severity == "error").Select(item => item.Code + " " + item.Message)));

        var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(result.Xml.Span));
        var expected = testCase.Expected;

        Assert.Equal(expected.LineNetAmounts, ReadLineNetAmounts(document));
        Assert.Equal(
            expected.Taxes.Select(tax => (tax.CategoryCode, tax.Rate, tax.Basis, tax.Amount)).ToArray(),
            ReadTaxGroups(document));
        Assert.Equal(expected.LineNetTotal, ReadSummation(document, "LineTotalAmount"));
        Assert.Equal(expected.TaxTotal, ReadSummation(document, "TaxTotalAmount"));
        Assert.Equal(expected.GrandTotal, ReadSummation(document, "GrandTotalAmount"));
        Assert.Equal(expected.GrandTotal, ReadSummation(document, "DuePayableAmount"));
        Assert.Equal(expected.LineNetTotal, ReadSummation(document, "TaxBasisTotalAmount"));
    }

    [Fact]
    public void Recognized_semantic_values_match_the_pinned_totals()
    {
        foreach (var testCase in Corpus.Cases)
        {
            var generated = EInvoice.GenerateXml(
                BuildInvoice(testCase),
                new PxaEInvoiceOptions
                {
                    Standard = PxaEInvoiceStandard.FacturX,
                    Version = EInvoice.CurrentFacturXVersion,
                    Profile = "EN 16931"
                });
            var semantic = generated.SemanticData;

            Assert.NotNull(semantic);
            Assert.Equal(
                testCase.Expected.LineNetTotal,
                Money(semantic!.LineNetTotal, testCase.CurrencyDecimalPlaces));
            Assert.Equal(
                testCase.Expected.TaxTotal,
                Money(semantic.TaxTotal, testCase.CurrencyDecimalPlaces));
            Assert.Equal(
                testCase.Expected.GrandTotal,
                Money(semantic.TaxInclusiveTotal, testCase.CurrencyDecimalPlaces));
            Assert.Equal(
                testCase.Expected.GrandTotal,
                Money(semantic.PayableAmount, testCase.CurrencyDecimalPlaces));
        }
    }

    /// <summary>Guards against a fixture that quietly loses the cases it is meant to pin.</summary>
    [Fact]
    public void Parity_corpus_covers_the_arithmetic_that_can_drift()
    {
        Assert.Equal("pxa.e-invoice.totals-parity/1", Corpus.Schema);
        Assert.True(Corpus.Cases.Count >= 9, "The parity corpus must keep every pinned case.");
        Assert.Contains(Corpus.Cases, item => item.Id == "line-net-rounds-half-away-from-zero");
        Assert.Contains(Corpus.Cases, item => item.Id == "multiple-rates-ordered-by-rate");
        Assert.Contains(Corpus.Cases, item => item.Id == "same-rate-different-category");
        Assert.Contains(Corpus.Cases, item => item.CurrencyDecimalPlaces == 0);
        Assert.Contains(Corpus.Cases, item => item.CurrencyDecimalPlaces == 3);
        Assert.Equal(
            Corpus.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count(),
            Corpus.Cases.Count);
        foreach (var testCase in Corpus.Cases)
        {
            Assert.Equal(testCase.Lines.Count, testCase.Expected.LineNetAmounts.Count);
        }
    }

    private static PxaEInvoiceDocument BuildInvoice(ParityCase testCase) => new()
    {
        InvoiceNumber = "PARITY-" + testCase.Id,
        IssueDate = new DateOnly(2026, 9, 27),
        Currency = "EUR",
        CurrencyDecimalPlaces = testCase.CurrencyDecimalPlaces,
        Seller = new("Seller GmbH", Address: new("Main Street 1", "Berlin", "10115", "DE")),
        Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
        Lines = testCase.Lines
            .Select(line => new PxaEInvoiceDocumentLine(
                line.Identifier,
                line.Description,
                line.Quantity,
                line.UnitCode,
                line.UnitPrice,
                line.TaxRate,
                line.TaxCategoryCode))
            .ToArray()
    };

    private static string[] ReadLineNetAmounts(XDocument document) => document
        .Descendants(Aggregate + "IncludedSupplyChainTradeLineItem")
        .Select(line => line
            .Descendants(Aggregate + "SpecifiedTradeSettlementLineMonetarySummation")
            .Descendants(Aggregate + "LineTotalAmount")
            .Single()
            .Value)
        .ToArray();

    private static (string CategoryCode, string Rate, string Basis, string Amount)[] ReadTaxGroups(
        XDocument document) => document
        .Descendants(Aggregate + "ApplicableHeaderTradeSettlement")
        .Elements(Aggregate + "ApplicableTradeTax")
        .Select(tax => (
            tax.Element(Aggregate + "CategoryCode")!.Value,
            tax.Element(Aggregate + "RateApplicablePercent")!.Value,
            tax.Element(Aggregate + "BasisAmount")!.Value,
            tax.Element(Aggregate + "CalculatedAmount")!.Value))
        .ToArray();

    private static string ReadSummation(XDocument document, string name) => document
        .Descendants(Aggregate + "SpecifiedTradeSettlementHeaderMonetarySummation")
        .Single()
        .Elements(Aggregate + name)
        .First()
        .Value;

    private static string Money(decimal? value, int decimalPlaces) =>
        value!.Value.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture);

    private static ParityCorpus Corpus { get; } = Load();

    private static ParityCorpus Load()
    {
        var path = ResolveCorpusPath();
        return JsonSerializer.Deserialize<ParityCorpus>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"The parity corpus at '{path}' could not be read.");
    }

    private static string ResolveCorpusPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "test-assets", "e-invoicing", "totals-parity.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("test-assets/e-invoicing/totals-parity.json was not found.");
    }

    private sealed record ParityCorpus(
        [property: JsonPropertyName("schema")] string Schema,
        [property: JsonPropertyName("cases")] IReadOnlyList<ParityCase> Cases);

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
        IReadOnlyList<ParityTax> Taxes,
        string LineNetTotal,
        string TaxTotal,
        string GrandTotal);

    private sealed record ParityTax(string CategoryCode, string Rate, string Basis, string Amount);
}
