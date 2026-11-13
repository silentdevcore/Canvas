using System.Text;
using System.Text.RegularExpressions;
using PVAD.Core.Contracts;
using PVAD.Pdf;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class StyledTableRenderingTests
{
    [Fact]
    public async Task Mapper_UsesCellColorsTypographyAndPaddingInsteadOfDiscardingStyles()
    {
        var design = new DesignExportDto { Pages = [new PageDto { Id = "page", Elements = [new ElementDto
        {
            Id = "table", Type = "table", X = 20, Y = 20, Width = 200, Height = 40, HeaderRow = true,
            CellData = [["Styled cell", "Other"]],
            CellStyles = [new CellStyleDto { Row = 0, Col = 0, FontSize = 18, Bold = false,
                Italic = true, Padding = 12, BackgroundColor = "#00ff00", Color = "#ff0000" }]
        }] }] };
        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();
        var pdf = Encoding.Latin1.GetString(bytes);
        Assert.Contains("/BaseFont /Helvetica-Oblique", pdf);
        Assert.Contains("1 0 0 rg", pdf);
        Assert.Contains("0 1 0 rg", pdf);
        Assert.Contains(" 18 Tf", pdf);
        using var stream = new MemoryStream(bytes);
        var imported = await new PVAD.Importer.PdfImporter().LoadAsync(stream);
        var text = Assert.Single(imported.Pages.SelectMany(page => page.TextObjects), text => text.Text == "Styled");
        Assert.Equal(32, text.Transform.E, 3);
        Assert.Equal(18, text.FontSize);
        Assert.True(text.Italic);
        Assert.False(text.Bold);
    }

    [Fact]
    public void ZeroBorder_SuppressesTheAdjacentDefaultBorder_WithoutDuplicateGridLines()
    {
        var document = new PdfDocument();
        document.AddPage(300, 200).DrawSimpleTable(20, 150, 200, [new[] { "A", "B" }],
            new PdfTableOptions { HasHeaderRow = false,
                CellStyles = [new() { Row = 0, Column = 0, BorderRight = new() { Width = 0 } }] });
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Equal(3, Regex.Matches(pdf, "\nS\n").Count);
    }

    [Theory]
    [InlineData(2, "0 0 1 RG")]
    [InlineData(3, "1 0 0 RG")]
    public void SharedBordersPreferGreaterWidth_ThenTheRightCellOnATie(double leftWidth, string color)
    {
        var document = new PdfDocument();
        document.AddPage(300, 200).DrawSimpleTable(20, 150, 200, [new[] { "A", "B" }], new PdfTableOptions
        {
            HasHeaderRow = false,
            CellStyles = [new() { Row = 0, Column = 0, BorderRight = new() { Width = leftWidth, Color = PdfColor.FromRgb(255, 0, 0) } },
                new() { Row = 0, Column = 1, BorderLeft = new() { Width = 2, Color = PdfColor.FromRgb(0, 0, 255) } }]
        });
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Equal(1, Regex.Matches(pdf, "\nS\n").Count);
        Assert.Contains(color, pdf);
        Assert.Contains("120 150 m\n120 126 l", pdf);
    }

    [Fact]
    public void FirstStyleWins_AndOutOfRangeEntriesAreIgnoredBeforeValidation()
    {
        var document = new PdfDocument();
        var result = document.AddPage(300, 200).DrawSimpleTable(20, 150, 200, [new[] { "A" }], new PdfTableOptions
        {
            AutoRowHeight = true,
            CellStyles = [new() { Row = 0, Column = 0, Padding = 0 },
                new() { Row = 0, Column = 0, Padding = double.NaN }, new() { Row = 99, Column = 0, FontSize = -1 }]
        });
        Assert.Equal(24, result.Height);
    }

    [Theory]
    [InlineData(PdfFontFamily.Times, "Times-Italic")]
    [InlineData(PdfFontFamily.Courier, "Courier-Oblique")]
    public void RequestedStandardFamily_IsNotSilentlyReplacedByThePackagedSansFont(PdfFontFamily family, string name)
    {
        var document = new PdfDocument { FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) };
        document.AddPage(300, 200).DrawSimpleTable(20, 150, 200, [new[] { "Styled", "Default" }],
            new PdfTableOptions { HasHeaderRow = false, CellStyles = [new() { Row = 0, Column = 0, FontFamily = family, Italic = true }] });
        Assert.Contains($"/BaseFont /{name}", Encoding.Latin1.GetString(document.ToBytes()));
    }

    [Fact]
    public void AutoHeightUsesEachCellsFontAndPadding_AndHonorsExplicitFalse()
    {
        var document = new PdfDocument();
        var result = document.AddPage(300, 400).DrawSimpleTable(20, 350, 200,
            [new[] { "A long styled cell with multiple wrapped lines", "B" }], new PdfTableOptions
            {
                AutoRowHeight = true, RowHeight = 16,
                CellStyles = [new() { Row = 0, Column = 0, FontSize = 18, Padding = 12, Bold = false }]
            });
        Assert.True(result.Height > 80);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidCellDimensionsFailBeforeRendering(double value)
    {
        var document = new PdfDocument();
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddPage(300, 200).DrawSimpleTable(20, 150, 200,
            [new[] { "A" }], new PdfTableOptions { CellStyles = [new() { Row = 0, Column = 0, Padding = value }] }));
    }

    [Fact]
    public void EveryCanonicalCellStyleFieldHasAnExplicitMappingContract()
    {
        var fields = new[] { "Row", "Col", "BackgroundColor", "TextAlign", "BorderColor", "BorderWidth",
            "BorderTop", "BorderRight", "BorderBottom", "BorderLeft", "Padding", "FontFamily", "FontSize", "Bold", "Italic", "Color" };
        Assert.Equal(fields.Order(), typeof(CellStyleDto).GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public void FlowPaginationRetainsCellStyles()
    {
        var document = new PdfDocument();
        document.CreateFlow().AddSimpleTable([new[] { "A" }],
            new PdfTableOptions { CellStyles = [new() { Row = 0, Column = 0, Italic = true }] });
        Assert.Contains("/BaseFont /Helvetica-BoldOblique", Encoding.Latin1.GetString(document.ToBytes()));
    }
}
