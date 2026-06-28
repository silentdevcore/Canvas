using System.Globalization;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;
using Canvas.Infrastructure.Spreadsheet;

namespace Canvas.Export.Tests;

/// <summary>
/// Round-trips a SpreadsheetDto through the .xlsx exporter + importer and asserts that typed values,
/// formulas, number formats, styles, merges, column widths, and frozen panes survive.
/// </summary>
public sealed class SpreadsheetRoundTripTests
{
    private static CellDto? Cell(SheetDto s, int row, int col) =>
        s.Cells.FirstOrDefault(c => c.Row == row && c.Col == col);

    private static SpreadsheetDto RoundTrip(SpreadsheetDto wb, out byte[] bytes)
    {
        bytes = new ExcelWorkbookExporter().Export(wb);
        using var ms = new MemoryStream(bytes);
        return new ExcelWorkbookImporter().Import(ms, "roundtrip.xlsx");
    }

    [Fact]
    public void RoundTrip_PreservesValuesFormulasFormatsStylesAndMerges()
    {
        var date = new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Unspecified);
        var wb = new SpreadsheetDto
        {
            Id = "wb1", Name = "Test",
            Sheets =
            [
                new SheetDto
                {
                    Name = "Data", FrozenRows = 1,
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Type = "text", Value = "Hello",
                            Style = new CellStyleDto { Bold = true, BackgroundColor = "#FFFF00" } },
                        new CellDto { Row = 0, Col = 1, Type = "number", Value = 10d },
                        new CellDto { Row = 1, Col = 1, Type = "number", Value = 20d, NumberFormat = "#,##0.00" },
                        new CellDto { Row = 2, Col = 1, Type = "formula", Formula = "=SUM(B1:B2)" },
                        new CellDto { Row = 0, Col = 2, Type = "boolean", Value = true },
                        new CellDto { Row = 0, Col = 3, Type = "date", Value = date.ToString("o", CultureInfo.InvariantCulture) },
                    ],
                    Merges = ["D1:E1"],
                    Columns = [new SheetColumnDto { Index = 0, Width = 20 }],
                },
            ],
        };

        var result = RoundTrip(wb, out var bytes);

        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytes, 0, 2)); // .xlsx is a zip
        var sheet = Assert.Single(result.Sheets);
        Assert.Equal("Data", sheet.Name);

        var a1 = Cell(sheet, 0, 0)!;
        Assert.Equal("text", a1.Type);
        Assert.Equal("Hello", a1.Value!.ToString());
        Assert.True(a1.Style!.Bold);
        Assert.Equal("#FFFF00", a1.Style!.BackgroundColor);

        Assert.Equal(10d, Convert.ToDouble(Cell(sheet, 0, 1)!.Value, CultureInfo.InvariantCulture));
        Assert.Equal("#,##0.00", Cell(sheet, 1, 1)!.NumberFormat);

        var formula = Cell(sheet, 2, 1)!;
        Assert.Equal("formula", formula.Type);
        Assert.Equal("=SUM(B1:B2)", formula.Formula);
        Assert.Equal(30d, Convert.ToDouble(formula.Value, CultureInfo.InvariantCulture)); // cached computed value

        Assert.Equal("boolean", Cell(sheet, 0, 2)!.Type);
        Assert.True(Convert.ToBoolean(Cell(sheet, 0, 2)!.Value));

        var dateCell = Cell(sheet, 0, 3)!;
        Assert.Equal("date", dateCell.Type);
        Assert.Equal(date, DateTime.Parse(dateCell.Value!.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        Assert.Contains("D1:E1", sheet.Merges);
        Assert.Contains(sheet.Columns, c => c.Index == 0 && c.Width is > 19 and < 21);
        Assert.Equal(1, sheet.FrozenRows);
    }

    [Fact]
    public void FromRows_BuildsHeaderAndTypedRows()
    {
        var rows = new List<Dictionary<string, System.Text.Json.JsonElement>>
        {
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>("{\"Name\":\"Ann\",\"Age\":30}")!,
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>("{\"Name\":\"Bob\",\"Age\":25}")!,
        };
        var sheet = new SpreadsheetData().FromRows(rows, "People");
        Assert.Equal("Name", sheet.Cells.First(c => c.Row == 0 && c.Col == 0).Value);
        Assert.True(sheet.Cells.First(c => c.Row == 0 && c.Col == 0).Style!.Bold);
        Assert.Equal("Ann", sheet.Cells.First(c => c.Row == 1 && c.Col == 0).Value);
        Assert.Equal(30d, sheet.Cells.First(c => c.Row == 1 && c.Col == 1).Value);
    }

    [Fact]
    public void Fill_ReplacesTokens()
    {
        var wb = new SpreadsheetDto { Sheets = [new SheetDto { Cells = [new CellDto { Row = 0, Col = 0, Type = "text", Value = "Hello {{name}}" }] }] };
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>("{\"name\":\"World\"}")!;
        var count = new SpreadsheetData().Fill(wb, data);
        Assert.Equal(1, count);
        Assert.Equal("Hello World", wb.Sheets[0].Cells[0].Value);
    }

    [Fact]
    public void Xls_RoundTrip_PreservesValuesFormulasMerges()
    {
        var wb = new SpreadsheetDto
        {
            Sheets =
            [
                new SheetDto { Name = "Data", Merges = ["A1:A2"], Cells =
                [
                    new CellDto { Row = 0, Col = 0, Type = "text", Value = "Hi" },
                    new CellDto { Row = 0, Col = 1, Type = "number", Value = 10d },
                    new CellDto { Row = 1, Col = 1, Type = "formula", Formula = "=B1*2" },
                ] },
            ],
        };
        var io = new XlsWorkbookIo();
        using var ms = new MemoryStream(io.Export(wb));
        var s = io.Import(ms, "x.xls").Sheets[0];
        Assert.Equal("Hi", s.Cells.First(c => c.Row == 0 && c.Col == 0).Value);
        Assert.Equal(10d, Convert.ToDouble(s.Cells.First(c => c.Row == 0 && c.Col == 1).Value, CultureInfo.InvariantCulture));
        Assert.Equal("=B1*2", s.Cells.First(c => c.Row == 1 && c.Col == 1).Formula);
        Assert.Contains("A1:A2", s.Merges);
    }

    [Fact]
    public void Csv_RoundTrip_QuotesAndTypes()
    {
        var sheet = new SheetDto { Cells =
        [
            new CellDto { Row = 0, Col = 0, Type = "text", Value = "a,b" },
            new CellDto { Row = 0, Col = 1, Type = "number", Value = 5d },
        ] };
        var csv = CsvSheetIo.ToCsv(sheet);
        Assert.Equal("\"a,b\",5", csv);

        var back = CsvSheetIo.FromCsv(csv);
        Assert.Equal("a,b", back.Cells.First(c => c.Row == 0 && c.Col == 0).Value);
        Assert.Equal(5d, back.Cells.First(c => c.Row == 0 && c.Col == 1).Value);
    }

    [Fact]
    public void Tsv_RoundTrip_TabDelimited()
    {
        var sheet = new SheetDto { Cells =
        [
            new CellDto { Row = 0, Col = 0, Type = "text", Value = "a,b" }, // comma needs no quoting in TSV
            new CellDto { Row = 0, Col = 1, Type = "number", Value = 5d },
        ] };
        var tsv = CsvSheetIo.ToCsv(sheet, '\t');
        Assert.Equal("a,b\t5", tsv);

        var back = CsvSheetIo.FromCsv(tsv, "Sheet1", '\t');
        Assert.Equal("a,b", back.Cells.First(c => c.Row == 0 && c.Col == 0).Value);
        Assert.Equal(5d, back.Cells.First(c => c.Row == 0 && c.Col == 1).Value);
    }

    [Fact]
    public void ToDesign_Gridlines_AddsHeaderRowAndBorder()
    {
        var wb = new SpreadsheetDto { Sheets = [new SheetDto { Cells = [new CellDto { Row = 0, Col = 0, Type = "text", Value = "x" }] }] };
        var table = new SpreadsheetToDesignConverter().Convert(wb, 0, gridlines: true).Pages[0].Elements[0];
        Assert.True(table.HeaderRow);
        Assert.NotNull(table.Style);
        Assert.True(table.Style!.ContainsKey("borderColor"));
    }

    [Fact]
    public void RichFeatures_RoundTrip()
    {
        var wb = new SpreadsheetDto
        {
            Sheets =
            [
                new SheetDto
                {
                    Name = "Rich",
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Type = "text", Value = "Hdr", Comment = "a note", Hyperlink = "https://example.com" },
                        new CellDto { Row = 1, Col = 0, Type = "number", Value = 5d },
                    ],
                    AutoFilterRange = "A1:B2",
                    Columns = [new SheetColumnDto { Index = 0, OutlineLevel = 1 }],
                    PageSetup = new PageSetupDto { Orientation = "landscape", PrintArea = "A1:B2", Header = "Report" },
                    Protection = new ProtectionDto { Protected = true },
                    // export-only (round-trip just confirms they don't break the file):
                    DataValidations = [new DataValidationDto { Range = "A2", Type = "list", ListSource = "x,y,z" }],
                    ConditionalFormats = [new ConditionalFormatDto { Range = "A2", Type = "cellIs", Operator = "greaterThan", Value = "3", Color = "#FF0000" }],
                },
            ],
        };

        var s = RoundTrip(wb, out _).Sheets[0];
        var a1 = s.Cells.First(c => c.Row == 0 && c.Col == 0);
        Assert.Equal("a note", a1.Comment);
        Assert.StartsWith("https://example.com", a1.Hyperlink);
        Assert.NotNull(s.AutoFilterRange);
        Assert.Contains(s.Columns, c => c.Index == 0 && c.OutlineLevel == 1);
        Assert.Equal("landscape", s.PageSetup!.Orientation);
        Assert.Equal("Report", s.PageSetup!.Header);
        Assert.True(s.Protection!.Protected);
    }

    [Fact]
    public void SortRange_OrdersRowsByKeyColumn()
    {
        var sheet = new SheetDto
        {
            Cells =
            [
                new CellDto { Row = 0, Col = 0, Type = "text", Value = "b" }, new CellDto { Row = 0, Col = 1, Type = "number", Value = 2d },
                new CellDto { Row = 1, Col = 0, Type = "text", Value = "a" }, new CellDto { Row = 1, Col = 1, Type = "number", Value = 1d },
            ],
        };
        new SpreadsheetOperations().SortRange(sheet, "A1:B2", keyColumnOffset: 0, ascending: true);
        Assert.Equal("a", sheet.Cells.First(c => c.Row == 0 && c.Col == 0).Value);
        Assert.Equal(1d, sheet.Cells.First(c => c.Row == 0 && c.Col == 1).Value);
    }

    [Fact]
    public void FindReplace_ReplacesTextAndFormulas()
    {
        var wb = new SpreadsheetDto
        {
            Sheets =
            [
                new SheetDto { Cells =
                [
                    new CellDto { Row = 0, Col = 0, Type = "text", Value = "hello world" },
                    new CellDto { Row = 1, Col = 0, Type = "formula", Formula = "=A1&\" world\"" },
                ] },
            ],
        };
        var count = new SpreadsheetOperations().FindReplace(wb, "world", "there");
        Assert.Equal(2, count);
        Assert.Equal("hello there", wb.Sheets[0].Cells[0].Value);
        Assert.Equal("=A1&\" there\"", wb.Sheets[0].Cells[1].Formula);
    }

    [Fact]
    public void Calculate_ComputesFormulasServerSide()
    {
        var wb = new SpreadsheetDto
        {
            Sheets =
            [
                new SheetDto
                {
                    Name = "S",
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Type = "number", Value = 10d },
                        new CellDto { Row = 1, Col = 0, Type = "number", Value = 20d },
                        new CellDto { Row = 2, Col = 0, Type = "formula", Formula = "=SUM(A1:A2)" },
                        new CellDto { Row = 3, Col = 0, Type = "formula", Formula = "=IF(A3>25,\"big\",\"small\")" },
                        new CellDto { Row = 4, Col = 0, Type = "formula", Formula = "=A1/0" },
                    ],
                },
            ],
        };

        var cells = new SpreadsheetCalculator().Calculate(wb).Sheets[0].Cells;
        object? V(int r) => cells.First(c => c.Row == r && c.Col == 0).Value;

        Assert.Equal(30d, Convert.ToDouble(V(2), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("big", V(3));
        Assert.StartsWith("#", V(4)!.ToString()); // division error surfaced as #CODE
    }

    [Fact]
    public void ToDesign_MapsSheetToTableElement()
    {
        var wb = new SpreadsheetDto
        {
            Name = "Book",
            Sheets =
            [
                new SheetDto
                {
                    Name = "Data",
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Type = "text", Value = "Item", Style = new CellStyleDto { Bold = true } },
                        new CellDto { Row = 0, Col = 1, Type = "text", Value = "Qty" },
                        new CellDto { Row = 1, Col = 0, Type = "text", Value = "Coffee" },
                        new CellDto { Row = 1, Col = 1, Type = "formula", Formula = "=1+1", Value = 2d },
                    ],
                    Columns = [new SheetColumnDto { Index = 0, Width = 20 }],
                },
            ],
        };

        var design = new SpreadsheetToDesignConverter().Convert(wb);

        var table = Assert.Single(design.Pages[0].Elements);
        Assert.Equal("table", table.Type);
        Assert.Equal("Data", design.Name);
        Assert.Equal("Item", table.CellData![0][0]);
        Assert.Equal("2", table.CellData![1][1]); // the formula's cached computed value
        Assert.Contains(table.CellStyles!, s => s.Row == 0 && s.Col == 0 && s.Bold == true);
        Assert.Equal(140d, table.ColumnWidths![0]); // 20 char-units → points
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(27, "AB")]
    [InlineData(701, "ZZ")]
    public void A1_ColumnName_AndIndex_RoundTrip(int index, string name)
    {
        Assert.Equal(name, A1Reference.ColumnName(index));
        Assert.Equal(index, A1Reference.ColumnIndex(name));
    }

    [Fact]
    public void A1_ParseAndFormat()
    {
        Assert.Equal("C5", A1Reference.ToA1(4, 2));
        Assert.Equal((4, 2), A1Reference.Parse("C5"));
        Assert.Equal((4, 2), A1Reference.Parse("$C$5"));
    }
}
