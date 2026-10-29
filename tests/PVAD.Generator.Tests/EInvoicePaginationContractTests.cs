using System.Text.Json;
using PVAD.Generator;

namespace PVAD.Generator.Tests;

/// <summary>
/// Holds the hybrid-invoice renderer to the shared pagination contract.
/// </summary>
/// <remarks>
/// PXA renders an invoice twice - the Designer projects it onto pages, and
/// <see cref="EInvoice.CreatePdf"/> renders the hybrid PDF - and the two must agree, or the document
/// approved on screen is not the document issued. They agree by each implementing
/// <c>test-assets/e-invoicing/pagination-contract.json</c> rather than by sharing code, because
/// PVAD.Generator must not depend on the Designer. The Designer asserts the same file from TypeScript
/// in <c>eInvoicePaginationContract.test.ts</c>; when both pass, the two renderers put the same
/// invoice content on the same number of pages.
/// </remarks>
public sealed class EInvoicePaginationContractTests
{
    [Fact]
    public void The_contract_is_the_version_this_renderer_implements()
    {
        Assert.Equal(EInvoicePageLayout.ContractVersion, Contract.GetProperty("schema").GetString());
        Assert.True(Contract.GetProperty("cases").GetArrayLength() >= 12);
    }

    [Fact]
    public void Page_and_table_geometry_match_the_contract()
    {
        var page = Contract.GetProperty("page");
        Assert.Equal(EInvoicePageLayout.PageWidth, page.GetProperty("width").GetDouble());
        Assert.Equal(EInvoicePageLayout.PageHeight, page.GetProperty("height").GetDouble());
        Assert.Equal(EInvoicePageLayout.Left, page.GetProperty("left").GetDouble());
        Assert.Equal(EInvoicePageLayout.Right, page.GetProperty("right").GetDouble());

        var table = Contract.GetProperty("table");
        Assert.Equal(EInvoicePageLayout.FirstPageTableTop, table.GetProperty("firstPageTop").GetDouble());
        Assert.Equal(EInvoicePageLayout.ContinuationTableTop, table.GetProperty("continuationTop").GetDouble());
        Assert.Equal(EInvoicePageLayout.ColumnHeaderOffset, table.GetProperty("columnHeaderOffset").GetDouble());
        Assert.Equal(EInvoicePageLayout.HeaderRuleOffset, table.GetProperty("headerRuleOffset").GetDouble());

        var row = Contract.GetProperty("row");
        Assert.Equal(EInvoicePageLayout.RowHeight, row.GetProperty("height").GetDouble());
        Assert.Equal(EInvoicePageLayout.LineHeight, row.GetProperty("lineHeight").GetDouble());
        Assert.Equal(EInvoicePageLayout.RowPadding, row.GetProperty("padding").GetDouble());
        Assert.Equal(EInvoicePageLayout.RowLimit, row.GetProperty("limit").GetDouble());
        Assert.Equal(EInvoicePageLayout.BodyFontSize, row.GetProperty("bodyFontSize").GetDouble());

        var totals = Contract.GetProperty("totals");
        Assert.Equal(EInvoicePageLayout.TotalsHeight, totals.GetProperty("height").GetDouble());
        Assert.Equal(EInvoicePageLayout.TotalsLimit, totals.GetProperty("limit").GetDouble());
        Assert.Equal(EInvoicePageLayout.TaxRowHeight, totals.GetProperty("taxRowHeight").GetDouble());
        Assert.Equal(EInvoicePageLayout.TotalsGap, totals.GetProperty("gap").GetDouble());
        Assert.Equal(EInvoicePageLayout.FooterTop, totals.GetProperty("footerTop").GetDouble());

        Assert.Equal(
            EInvoicePageLayout.MaximumDescriptionFragments,
            Contract.GetProperty("limits").GetProperty("maximumDescriptionFragments").GetInt32());
    }

    [Fact]
    public void Columns_match_the_contract_and_never_overlap()
    {
        var columns = Contract.GetProperty("columns");
        AssertColumn(columns, "position", EInvoicePageLayout.Position);
        AssertColumn(columns, "description", EInvoicePageLayout.Description);
        AssertColumn(columns, "quantity", EInvoicePageLayout.Quantity);
        AssertColumn(columns, "unitPrice", EInvoicePageLayout.UnitPrice);
        AssertColumn(columns, "taxRate", EInvoicePageLayout.TaxRate);
        AssertColumn(columns, "net", EInvoicePageLayout.Net);
        AssertColumn(columns, "currency", EInvoicePageLayout.Currency);
        AssertColumn(columns, "totalsLabel", EInvoicePageLayout.TotalsLabel);

        // Left to right, each table column ends before the next one starts. Overlapping boxes were a
        // real defect once: a totals label drew over the amount beside it.
        EInvoicePageLayout.Column[] table =
        [
            EInvoicePageLayout.Position, EInvoicePageLayout.Description, EInvoicePageLayout.Quantity,
            EInvoicePageLayout.UnitPrice, EInvoicePageLayout.TaxRate, EInvoicePageLayout.Net,
            EInvoicePageLayout.Currency
        ];
        for (var index = 1; index < table.Length; index++)
            Assert.True(table[index - 1].Right <= table[index].X,
                $"Column {index} starts at {table[index].X}, inside the previous column ending at {table[index - 1].Right}.");
        Assert.True(table[^1].Right <= EInvoicePageLayout.Right);
    }

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var element in Contract.GetProperty("cases").EnumerateArray())
            data.Add(element.GetProperty("id").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void The_planner_reproduces_every_pinned_case(string caseId)
    {
        var testCase = Contract.GetProperty("cases").EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == caseId);
        var descriptions = testCase.GetProperty("descriptions").EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        var taxGroups = testCase.GetProperty("taxGroups").GetInt32();
        var expected = testCase.GetProperty("expected");

        var plan = EInvoicePageLayout.Plan(descriptions, taxGroups);

        Assert.Equal(expected.GetProperty("pages").GetInt32(), plan.Pages.Count);
        AssertSequence(expected, "rowsPerPage", plan.Pages.Select(static page => page.Rows.Count));
        AssertSequence(expected, "distinctLinesPerPage",
            plan.Pages.Select(static page => page.Rows.Select(static row => row.LineIndex).Distinct().Count()));
        AssertSequence(expected, "totalsPages",
            plan.Pages.Where(static page => page.TotalsTop is not null).Select(static page => page.PageNumber));
        AssertSequence(expected, "taxGroupsPerPage", plan.Pages.Select(static page => page.TaxGroupCount));
        AssertSequence(expected, "netTotalPages",
            plan.Pages.Where(static page => page.ShowNetTotal).Select(static page => page.PageNumber));
        AssertSequence(expected, "payablePages",
            plan.Pages.Where(static page => page.ShowPayableTotal).Select(static page => page.PageNumber));
        AssertSequence(expected, "overflowingLines", plan.OverflowingLines);

        var fragments = plan.Pages
            .SelectMany(static page => page.Rows)
            .GroupBy(static row => row.LineIndex)
            .Where(static group => group.Count() > 1)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var expectedFragments = expected.GetProperty("fragmentedLines").EnumerateArray()
            .ToDictionary(
                static element => element.GetProperty("lineIndex").GetInt32(),
                static element => element.GetProperty("fragments").GetInt32());
        Assert.Equal(expectedFragments, fragments);
    }

    [Fact]
    public void Every_planned_row_stays_inside_the_page_and_in_canonical_order()
    {
        foreach (var element in Contract.GetProperty("cases").EnumerateArray())
        {
            var caseId = element.GetProperty("id").GetString();
            var descriptions = element.GetProperty("descriptions").EnumerateArray()
                .Select(static item => item.GetString())
                .ToArray();
            var plan = EInvoicePageLayout.Plan(descriptions, element.GetProperty("taxGroups").GetInt32());

            var order = new List<(int Line, int Fragment)>();
            foreach (var page in plan.Pages)
            {
                var previousBottom = double.MinValue;
                foreach (var row in page.Rows)
                {
                    Assert.True(row.Top >= previousBottom, $"{caseId}: rows overlap on page {page.PageNumber}.");
                    Assert.True(row.Top + row.Height <= EInvoicePageLayout.RowLimit,
                        $"{caseId}: a row reaches {row.Top + row.Height}, past the row limit.");
                    previousBottom = row.Top + row.Height;
                    order.Add((row.LineIndex, row.FragmentIndex));
                }
            }

            // Every line appears, in canonical order, with its fragments in sequence.
            Assert.Equal(
                Enumerable.Range(0, descriptions.Length).ToList(),
                order.Select(static entry => entry.Line).Distinct().ToList());
            foreach (var group in order.GroupBy(static entry => entry.Line))
                Assert.Equal(
                    Enumerable.Range(0, group.Count()).ToList(),
                    group.Select(static entry => entry.Fragment).ToList());
        }
    }

    [Fact]
    public void A_description_beyond_the_bound_is_reported_rather_than_rendered()
    {
        // Longer than MaximumDescriptionFragments pages of text. The public API cannot produce this
        // - PXAEINV109 caps a description at 4 096 characters - so the planner is driven directly.
        var words = Enumerable.Repeat("maintenance", 4_000);
        var plan = EInvoicePageLayout.Plan([string.Join(' ', words)], 1);

        Assert.Equal(new List<int> { 0 }, plan.OverflowingLines.ToList());
        // It is still planned rather than truncated, so nothing is silently dropped before the
        // caller decides to refuse it.
        Assert.Single(
            plan.Pages.SelectMany(static page => page.Rows).Select(static row => row.LineIndex).Distinct());
    }

    [Fact]
    public void A_maximum_length_token_is_split_without_clipping_or_losing_unicode_characters()
    {
        var description = new string('x', 4_094) + "🙂";
        var lines = EInvoicePageLayout.WrapText(
            description, EInvoicePageLayout.Description.Width, EInvoicePageLayout.BodyFontSize);

        Assert.True(lines.Count > 1);
        Assert.Equal(description, string.Concat(lines));
        Assert.DoesNotContain(lines, static line => line.Length == 0);

        var plan = EInvoicePageLayout.Plan([description], 1);
        var rendered = string.Concat(plan.Pages.SelectMany(static page => page.Rows)
            .Select(static row => string.Concat(row.Text.Where(static character => !char.IsWhiteSpace(character)))));
        Assert.Equal(description, rendered);
    }

    private static void AssertSequence(JsonElement expected, string name, IEnumerable<int> actual)
    {
        var pinned = expected.GetProperty(name).EnumerateArray()
            .Select(static element => element.GetInt32())
            .ToList();
        Assert.Equal(pinned, actual.ToList());
    }

    private static void AssertColumn(JsonElement columns, string name, EInvoicePageLayout.Column column)
    {
        var expected = columns.GetProperty(name);
        Assert.Equal(expected.GetProperty("x").GetDouble(), column.X);
        Assert.Equal(expected.GetProperty("width").GetDouble(), column.Width);
    }

    private static JsonElement Contract { get; } = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(
            ResolveRepositoryRoot(), "test-assets", "e-invoicing", "pagination-contract.json"))).RootElement;

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be located.");
    }
}
