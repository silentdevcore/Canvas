using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using PVAD.Core.Contracts;

namespace PVAD.Infrastructure.Spreadsheet.Tests;

public sealed class ClosedXmlCompatibilityTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-CH")]
    [InlineData("ar-SA")]
    public void Json_workbooks_preserve_typed_values_names_and_cross_sheet_formulas(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var source = JsonSerializer.Deserialize<SpreadsheetDto>(
                JsonSerializer.Serialize(BuildWorkbook(), options), options)!;

            var bytes = new ExcelWorkbookExporter().Export(source, recalculate: true);
            AssertValidXlsx(bytes);
            using var stream = new MemoryStream(bytes);
            var imported = new ExcelWorkbookImporter().Import(stream, "compatibility.xlsx");

            Assert.Equal(new[] { "Data", "Totals" }, imported.Sheets.Select(sheet => sheet.Name));
            var data = imported.Sheets[0];
            Assert.Equal("Gr\u00fcsse", Cell(data, 1, 0).Value);
            Assert.Equal(1234.5, Convert.ToDouble(Cell(data, 1, 1).Value, CultureInfo.InvariantCulture));
            Assert.Equal("#,##0.00", Cell(data, 1, 1).NumberFormat);
            Assert.Equal(true, Cell(data, 1, 2).Value);
            Assert.Equal(new DateTime(2026, 10, 1), DateTime.Parse(
                Assert.IsType<string>(Cell(data, 1, 3).Value), CultureInfo.InvariantCulture));
            Assert.Contains("A4:B4", data.Merges);
            Assert.Equal(1, data.FrozenRows);
            Assert.Equal(1, data.FrozenCols);
            Assert.True(Cell(data, 0, 0).Style!.Bold);
            var name = Assert.Single(imported.DefinedNames);
            Assert.Equal("Revenue", name.Name);
            Assert.Contains("$B$2", name.RefersTo);
            var formula = Cell(imported.Sheets[1], 0, 0);
            Assert.Equal("=Revenue*2", formula.Formula);
            Assert.Equal(2469d, Convert.ToDouble(formula.Value, CultureInfo.InvariantCulture));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void Export_preserves_print_settings_validation_and_protection_in_the_actual_file()
    {
        var source = BuildWorkbook();
        var data = source.Sheets[0];
        data.PageSetup = new PageSetupDto
        {
            Orientation = "landscape", PaperSize = "A4", PrintArea = "A1:D4",
            Header = "Compatibility report", Footer = "Synthetic data",
            FitToWidth = 1, FitToHeight = 1,
            Margins = new MarginsDto { Left = 0.5, Right = 0.5, Top = 0.75, Bottom = 0.75 },
        };
        data.Protection = new ProtectionDto { Protected = true };
        data.DataValidations.Add(new DataValidationDto { Range = "B2:B3", Type = "decimal", Value1 = "0", Value2 = "5000" });
        data.ConditionalFormats.Add(new ConditionalFormatDto { Range = "B2:B3", Value = "100", Color = "#FF0000" });

        var bytes = new ExcelWorkbookExporter().Export(source);
        AssertValidXlsx(bytes);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var stored = workbook.Worksheet("Data");

        Assert.Equal(XLPageOrientation.Landscape, stored.PageSetup.PageOrientation);
        Assert.Equal(XLPaperSize.A4Paper, stored.PageSetup.PaperSize);
        Assert.Equal("A1:D4", Assert.Single(stored.PageSetup.PrintAreas).RangeAddress.ToStringRelative());
        Assert.Equal(1, stored.PageSetup.PagesWide);
        Assert.Equal(1, stored.PageSetup.PagesTall);
        Assert.Equal(0.5, stored.PageSetup.Margins.Left);
        Assert.True(stored.IsProtected);
        Assert.Single(stored.DataValidations);
        Assert.Single(stored.ConditionalFormats);
    }

    [Fact]
    public void Design_table_and_summary_export_exercise_font_measurement_and_valid_xlsx()
    {
        var bytes = new ExcelDocumentExporter().Export(new DesignExportDto
        {
            Id = "compatibility-design", Name = "Compatibility",
            Pages = [new PageDto
            {
                Id = "page-1", Elements =
                [
                    new ElementDto { Id = "text-1", Type = "text", Name = "Title", Content = "Budget report" },
                    new ElementDto
                    {
                        Id = "table-1", Type = "table", Name = "Amounts", HeaderRow = true,
                        CellData = [["Item", "Amount"], ["Synthetic item", "42"]],
                    },
                ],
            }],
        });

        AssertValidXlsx(bytes);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(2, workbook.Worksheets.Count);
        var table = workbook.Worksheet("Amounts");
        Assert.Equal("Synthetic item", table.Cell("A2").GetString());
        Assert.True(table.Cell("A1").Style.Font.Bold);
        Assert.True(double.IsFinite(table.Column(1).Width) && table.Column(1).Width > 0);
        Assert.Equal("Budget report", workbook.Worksheet("Summary").Cell("C2").GetString());
    }

    [Fact]
    public void Runtime_graph_matches_the_declared_workbook_version_and_supported_font_major()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName,
            "src/Infrastructure/PVAD.Infrastructure.Spreadsheet/PVAD.Infrastructure.Spreadsheet.csproj"));
        var reference = Assert.Single(project.Descendants("PackageReference"),
            value => (string?)value.Attribute("Include") == "ClosedXML");
        using var dependencies = JsonDocument.Parse(File.ReadAllText(
            Path.ChangeExtension(typeof(ClosedXmlCompatibilityTests).Assembly.Location, ".deps.json")));
        var libraries = dependencies.RootElement.GetProperty("libraries");
        Assert.True(libraries.TryGetProperty($"ClosedXML/{(string?)reference.Attribute("Version")}", out _));
        var fonts = Assert.Single(libraries.EnumerateObject(), value => value.Name.StartsWith("SixLabors.Fonts/", StringComparison.Ordinal));
        var version = Version.Parse(fonts.Name["SixLabors.Fonts/".Length..]);
        Assert.True(version >= new Version(1, 0, 0) && version < new Version(3, 0, 0), fonts.Name);
    }

    private static CellDto Cell(SheetDto sheet, int row, int column) =>
        Assert.Single(sheet.Cells, value => value.Row == row && value.Col == column);

    private static void AssertValidXlsx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = SpreadsheetDocument.Open(stream, false);
        Assert.NotNull(document.WorkbookPart);
        Assert.Empty(new OpenXmlValidator().Validate(document));
    }

    private static SpreadsheetDto BuildWorkbook() => new()
    {
        Id = "compatibility-workbook", Name = "Compatibility",
        DefinedNames = [new DefinedNameDto { Name = "Revenue", RefersTo = "Data!$B$2" }],
        Sheets =
        [
            new SheetDto
            {
                Id = "data", Name = "Data", FrozenRows = 1, FrozenCols = 1,
                Merges = ["A4:B4"], Columns = [new SheetColumnDto { Index = 0, Width = 24 }],
                Cells =
                [
                    new CellDto { Row = 0, Col = 0, Type = "text", Value = "Heading", Style = new CellStyleDto { Bold = true } },
                    new CellDto { Row = 1, Col = 0, Type = "text", Value = "Gr\u00fcsse" },
                    new CellDto { Row = 1, Col = 1, Type = "number", Value = 1234.5, NumberFormat = "#,##0.00" },
                    new CellDto { Row = 1, Col = 2, Type = "boolean", Value = true },
                    new CellDto { Row = 1, Col = 3, Type = "date", Value = "2026-10-01T00:00:00" },
                    new CellDto { Row = 3, Col = 0, Type = "text", Value = "Merged title" },
                ],
            },
            new SheetDto
            {
                Id = "totals", Name = "Totals",
                Cells = [new CellDto { Row = 0, Col = 0, Type = "formula", Formula = "=Revenue*2" }],
            },
        ],
    };
}
