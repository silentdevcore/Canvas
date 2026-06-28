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
