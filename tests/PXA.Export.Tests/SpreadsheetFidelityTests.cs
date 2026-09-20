using System.Globalization;
using System.Text.Json;
using PXA.Core.Contracts;
using PXA.Infrastructure.Spreadsheet;

namespace PXA.Export.Tests;

public sealed class SpreadsheetFidelityTests
{
    private sealed record FeatureResult(string Name, bool Preserved, string? Detail = null);

    private sealed record SpreadsheetFidelityReport(
        string FixtureId,
        int SourceFeatures,
        int MatchedFeatures,
        IReadOnlyList<string> MissingFeatures,
        int DuplicateFeatures,
        bool SheetCountMatches,
        IReadOnlyList<FeatureResult> Features,
        IReadOnlyList<string> Limitations);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [Fact]
    public void CanonicalXlsxRoundTrip_ProducesMachineReadableFidelityEvidence()
    {
        var root = FindRepoRoot();
        var fixturePath = Path.Combine(root, "test-assets", "document-fidelity", "spreadsheet-roundtrip.json");
        var source = JsonSerializer.Deserialize<SpreadsheetDto>(File.ReadAllText(fixturePath), JsonOptions);
        Assert.NotNull(source);

        var bytes = new ExcelWorkbookExporter().Export(source!, recalculate: true);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
        using var stream = new MemoryStream(bytes);
        var actual = new ExcelWorkbookImporter().Import(stream, "spreadsheet-roundtrip.xlsx");

        var expectedSheet = source.Sheets[0];
        var actualSheet = actual.Sheets.First(sheet => sheet.Name == expectedSheet.Name);
        var features = new List<FeatureResult>();
        void Check(string name, bool preserved, string? detail = null) =>
            features.Add(new FeatureResult(name, preserved, preserved ? null : detail));

        Check("sheet-count", actual.Sheets.Count == source.Sheets.Count,
            $"expected {source.Sheets.Count}, received {actual.Sheets.Count}");
        Check("sheet-names", source.Sheets.Select(sheet => sheet.Name).SequenceEqual(actual.Sheets.Select(sheet => sheet.Name)),
            "sheet order or names changed");
        Check("typed-values", ExpectedCells(expectedSheet).All(expected => CellValueMatches(expected, FindCell(actualSheet, expected.Row, expected.Col))),
            "one or more text, number, boolean, or date values changed");
        Check("formulas", expectedSheet.Cells.Where(cell => cell.Type == "formula").All(expected =>
            string.Equals(expected.Formula, FindCell(actualSheet, expected.Row, expected.Col)?.Formula, StringComparison.Ordinal)),
            "one or more formulas changed");
        Check("number-formats", expectedSheet.Cells.Where(cell => cell.NumberFormat is not null).All(expected =>
            string.Equals(expected.NumberFormat, FindCell(actualSheet, expected.Row, expected.Col)?.NumberFormat, StringComparison.Ordinal)),
            "one or more number formats changed");
        Check("cell-styles", expectedSheet.Cells.Where(cell => cell.Style is not null).All(expected =>
            StyleMatches(expected.Style!, FindCell(actualSheet, expected.Row, expected.Col)?.Style)),
            "one or more cell styles changed");
        Check("comments", expectedSheet.Cells.Where(cell => cell.Comment is not null).All(expected =>
            string.Equals(expected.Comment, FindCell(actualSheet, expected.Row, expected.Col)?.Comment, StringComparison.Ordinal)),
            "one or more comments changed");
        Check("hyperlinks", expectedSheet.Cells.Where(cell => cell.Hyperlink is not null).All(expected =>
            (FindCell(actualSheet, expected.Row, expected.Col)?.Hyperlink ?? "").StartsWith(expected.Hyperlink!, StringComparison.Ordinal)),
            "one or more hyperlinks changed");
        Check("merged-ranges", expectedSheet.Merges.Order().SequenceEqual(actualSheet.Merges.Order()), "merged ranges changed");
        Check("columns", expectedSheet.Columns.All(expected => actualSheet.Columns.Any(actualColumn =>
            actualColumn.Index == expected.Index && actualColumn.Hidden == expected.Hidden &&
            actualColumn.OutlineLevel == expected.OutlineLevel && NullableClose(expected.Width, actualColumn.Width, 0.05))),
            $"expected [{DescribeColumns(expectedSheet.Columns)}], received [{DescribeColumns(actualSheet.Columns)}]");
        Check("rows", expectedSheet.Rows.All(expected => actualSheet.Rows.Any(actualRow =>
            actualRow.Index == expected.Index && actualRow.Hidden == expected.Hidden &&
            actualRow.OutlineLevel == expected.OutlineLevel && NullableClose(expected.Height, actualRow.Height, 0.05))),
            "row height, visibility, or outline changed");
        Check("frozen-panes", expectedSheet.FrozenRows == actualSheet.FrozenRows && expectedSheet.FrozenCols == actualSheet.FrozenCols,
            "frozen rows or columns changed");
        Check("auto-filter", RangeEquals(expectedSheet.AutoFilterRange, actualSheet.AutoFilterRange), "auto-filter range changed");
        Check("page-setup", PageSetupMatches(expectedSheet.PageSetup, actualSheet.PageSetup), "page setup was only partially preserved");
        Check("protection", expectedSheet.Protection?.Protected == actualSheet.Protection?.Protected, "sheet protection changed");
        Check("conditional-formatting", expectedSheet.ConditionalFormats.All(expected => actualSheet.ConditionalFormats.Any(actualFormat =>
                ConditionalFormatMatches(expected, actualFormat))),
            "conditional-format rules are not imported");
        Check("data-validation", expectedSheet.DataValidations.All(expected => actualSheet.DataValidations.Any(actualValidation =>
                DataValidationMatches(expected, actualValidation))),
            "data-validation rules are not imported");
        Check("defined-names", source.DefinedNames.All(expected => actual.DefinedNames.Any(value =>
            value.Name == expected.Name && NormalizeReference(value.RefersTo) == NormalizeReference(expected.RefersTo))),
            "one or more defined names changed");

        var missing = features.Where(feature => !feature.Preserved).Select(feature => feature.Name).ToArray();
        var report = new SpreadsheetFidelityReport(
            "spreadsheet-xlsx-roundtrip",
            features.Count,
            features.Count - missing.Length,
            missing,
            0,
            actual.Sheets.Count == source.Sheets.Count,
            features,
            new[] { "The current fidelity fixture is synthetic and does not establish real-world XLSX compatibility." }
                .Concat(missing.Select(name => $"The current XLSX roundtrip does not preserve {name}."))
                .ToArray());

        var output = Environment.GetEnvironmentVariable("PXA_SPREADSHEET_FIDELITY_ARTIFACTS") ??
            Path.Combine(root, "artifacts", "spreadsheet-fidelity");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "fidelity-report.json"), JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllBytes(Path.Combine(output, "spreadsheet-roundtrip.xlsx"), bytes);

        Assert.NotEmpty(features);
        Assert.Empty(report.MissingFeatures);
    }

    [Fact]
    public void PortraitPageSetup_WithOnlyMarginsAndScale_IsPreserved()
    {
        var source = new SpreadsheetDto
        {
            Id = "portrait-page-setup",
            Name = "Portrait page setup",
            Sheets =
            {
                new SheetDto
                {
                    Id = "sheet-1",
                    Name = "Portrait",
                    Cells = { new CellDto { Row = 0, Col = 0, Type = "text", Value = "Page setup" } },
                    PageSetup = new PageSetupDto
                    {
                        Orientation = "portrait",
                        PaperSize = "A4",
                        Scale = 85,
                        Margins = new MarginsDto { Top = 0.45, Bottom = 0.55, Left = 0.35, Right = 0.4 }
                    }
                }
            }
        };

        var bytes = new ExcelWorkbookExporter().Export(source, recalculate: false);
        using var stream = new MemoryStream(bytes);
        var actual = new ExcelWorkbookImporter().Import(stream, "portrait-page-setup.xlsx");

        Assert.True(PageSetupMatches(source.Sheets[0].PageSetup, actual.Sheets[0].PageSetup));
        Assert.Equal(85, actual.Sheets[0].PageSetup?.Scale);
    }

    private static IEnumerable<CellDto> ExpectedCells(SheetDto sheet) =>
        sheet.Cells.Where(cell => cell.Type != "formula");

    private static CellDto? FindCell(SheetDto sheet, int row, int col) =>
        sheet.Cells.FirstOrDefault(cell => cell.Row == row && cell.Col == col);

    private static bool CellValueMatches(CellDto expected, CellDto? actual)
    {
        if (actual is null || expected.Type != actual.Type) return false;
        if (expected.Type == "number")
            return Math.Abs(ToDouble(expected.Value) - ToDouble(actual.Value)) <= 0.000001;
        if (expected.Type == "date")
            return DateTime.TryParse(expected.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expectedDate) &&
                   DateTime.TryParse(actual.Value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var actualDate) &&
                   expectedDate == actualDate;
        return string.Equals(expected.Value?.ToString(), actual.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static double ToDouble(object? value) => value switch
    {
        JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetDouble(),
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };

    private static bool StyleMatches(CellStyleDto expected, CellStyleDto? actual) =>
        actual is not null && expected.Bold == actual.Bold &&
        ColorEquals(expected.BackgroundColor, actual.BackgroundColor) && ColorEquals(expected.Color, actual.Color);

    private static bool ColorEquals(string? expected, string? actual) =>
        string.Equals(expected?.ToUpperInvariant(), actual?.ToUpperInvariant(), StringComparison.Ordinal);

    private static bool NullableClose(double? expected, double? actual, double tolerance) =>
        expected is null || actual is not null && Math.Abs(expected.Value - actual.Value) <= tolerance;

    private static string DescribeColumns(IEnumerable<SheetColumnDto> columns) => string.Join("; ", columns.Select(column =>
        $"{column.Index}:width={column.Width?.ToString("0.###", CultureInfo.InvariantCulture) ?? "null"},hidden={column.Hidden},outline={column.OutlineLevel}"));

    private static bool RangeEquals(string? expected, string? actual) =>
        NormalizeReference(expected) == NormalizeReference(actual);

    private static string NormalizeReference(string? value) =>
        (value ?? "").Replace("'", "", StringComparison.Ordinal).Replace("$", "", StringComparison.Ordinal);

    private static bool PageSetupMatches(PageSetupDto? expected, PageSetupDto? actual) =>
        expected is not null && actual is not null &&
        string.Equals(expected.Orientation, actual.Orientation, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.PaperSize, actual.PaperSize, StringComparison.OrdinalIgnoreCase) &&
        RangeEquals(expected.PrintArea, actual.PrintArea) &&
        string.Equals(expected.Header, actual.Header, StringComparison.Ordinal) &&
        string.Equals(expected.Footer, actual.Footer, StringComparison.Ordinal) &&
        expected.FitToWidth == actual.FitToWidth && expected.FitToHeight == actual.FitToHeight &&
        NullableClose(expected.Margins?.Top, actual.Margins?.Top, 0.001) &&
        NullableClose(expected.Margins?.Bottom, actual.Margins?.Bottom, 0.001) &&
        NullableClose(expected.Margins?.Left, actual.Margins?.Left, 0.001) &&
        NullableClose(expected.Margins?.Right, actual.Margins?.Right, 0.001) &&
        expected.RowPageBreaks.SequenceEqual(actual.RowPageBreaks) &&
        expected.ColPageBreaks.SequenceEqual(actual.ColPageBreaks);

    private static bool DataValidationMatches(DataValidationDto expected, DataValidationDto actual) =>
        RangeEquals(expected.Range, actual.Range) && expected.Type == actual.Type &&
        (expected.Type == "list"
            ? expected.ListSource == actual.ListSource
            : expected.Operator == actual.Operator && expected.Value1 == actual.Value1 && expected.Value2 == actual.Value2);

    private static bool ConditionalFormatMatches(ConditionalFormatDto expected, ConditionalFormatDto actual) =>
        RangeEquals(expected.Range, actual.Range) && expected.Type == actual.Type &&
        expected.Operator == actual.Operator && expected.Value == actual.Value && expected.Value2 == actual.Value2 &&
        ColorEquals(expected.Color, actual.Color);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PXA.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find PXA repository root.");
    }
}
