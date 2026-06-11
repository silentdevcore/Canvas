using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Rdl;

namespace Canvas.Migration.Rdl.Tests;

public sealed class RdlToDesignConverterTests
{
    // 2016-schema sample: PageHeader (centered bold title) + Body (bound textbox, complex expression,
    // a dashed line, a 2-column tablix with one header row) + PageFooter (page number).
    private const string SampleRdl = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" Name="Invoice">
          <Body>
            <ReportItems>
              <Textbox Name="customer">
                <Top>0in</Top><Left>1in</Left><Height>0.3in</Height><Width>3in</Width>
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!CustomerName.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
              </Textbox>
              <Textbox Name="total">
                <Top>0.3in</Top><Left>1in</Left><Height>0.3in</Height><Width>3in</Width>
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Sum(Fields!Total.Value)</Value></TextRun></TextRuns></Paragraph></Paragraphs>
              </Textbox>
              <Line Name="rule">
                <Top>0.5in</Top><Left>1in</Left><Height>0in</Height><Width>5in</Width>
                <Style><Border><Color>Gray</Color><Width>3pt</Width><Style>Dashed</Style></Border></Style>
              </Line>
              <Tablix Name="items">
                <Top>1in</Top><Left>1in</Left><Height>1in</Height><Width>4in</Width>
                <TablixBody>
                  <TablixColumns>
                    <TablixColumn><Width>2in</Width></TablixColumn>
                    <TablixColumn><Width>2in</Width></TablixColumn>
                  </TablixColumns>
                  <TablixRows>
                    <TablixRow>
                      <TablixCells>
                        <TablixCell><CellContents><Textbox Name="h1"><Paragraphs><Paragraph><Style><TextAlign>Left</TextAlign></Style><TextRuns><TextRun><Value>Name</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="h2"><Paragraphs><Paragraph><Style><TextAlign>Right</TextAlign></Style><TextRuns><TextRun><Value>Price</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells>
                    </TablixRow>
                    <TablixRow>
                      <TablixCells>
                        <TablixCell><CellContents><Textbox Name="c1"><Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!ItemName.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="c2"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Widget</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      </TablixCells>
                    </TablixRow>
                  </TablixRows>
                </TablixBody>
              </Tablix>
            </ReportItems>
            <Height>5in</Height>
          </Body>
          <Page>
            <PageHeader>
              <Height>1in</Height>
              <ReportItems>
                <Textbox Name="title">
                  <Top>0.1in</Top><Left>1in</Left><Height>0.4in</Height><Width>5in</Width>
                  <Paragraphs><Paragraph><Style><TextAlign>Center</TextAlign></Style><TextRuns><TextRun><Value>Invoice 2024</Value><Style><FontFamily>Arial</FontFamily><FontSize>20pt</FontSize><FontWeight>Bold</FontWeight><Color>#0066CC</Color><TextDecoration>Underline</TextDecoration></Style></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems>
            </PageHeader>
            <PageFooter>
              <Height>0.5in</Height>
              <ReportItems>
                <Textbox Name="pageinfo">
                  <Top>0.1in</Top><Left>4in</Left><Height>0.3in</Height><Width>1in</Width>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Page 1</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems>
            </PageFooter>
            <PageHeight>11in</PageHeight>
            <PageWidth>8.5in</PageWidth>
            <LeftMargin>1in</LeftMargin>
            <RightMargin>1in</RightMargin>
            <TopMargin>1in</TopMargin>
            <BottomMargin>1in</BottomMargin>
          </Page>
        </Report>
        """;

    private static RdlConvertResult Convert(string rdl) => new RdlToDesignConverter().Convert(rdl);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    // 1 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ParsesPageBodyAndItems()
    {
        var r = Convert(SampleRdl);

        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(612, r.Design.PageSettings!.Width, 1);   // 8.5in
        Assert.Equal(792, r.Design.PageSettings!.Height, 1);  // 11in
        Assert.Equal("pt", r.Design.PageSettings!.Unit);
        Assert.Equal(4, r.Design.Pages[0].Elements.Count);    // customer, total, rule, tablix
        Assert.Equal(2, r.Design.SharedElements.Count);       // header title + footer page number
        Assert.True(Has(r.Diagnostics, "CANMIGRDL001"));
    }

    // 2 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_LengthsToPoints_PositionBodyItemsAbsolutely()
    {
        var customer = El(Convert(SampleRdl).Design, "customer");
        // x = LeftMargin(72) + Left(1in=72); y = TopMargin(72) + PageHeaderHeight(1in=72) + Top(0) = 144
        Assert.Equal(144, customer.X, 1);
        Assert.Equal(144, customer.Y, 1);
        Assert.Equal(216, customer.Width, 1);  // 3in
    }

    // 3 ───────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("8.5in", 612)]
    [InlineData("21cm", 595.28)]
    [InlineData("297mm", 841.89)]
    [InlineData("612pt", 612)]
    [InlineData("816px", 612)]
    public void Convert_LengthUnits_AllParseToPoints(string width, double expected)
    {
        var rdl = $"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems /><Height>5in</Height></Body>
              <Page><PageWidth>{width}</PageWidth><PageHeight>11in</PageHeight></Page>
            </Report>
            """;
        Assert.Equal(expected, Convert(rdl).Design.PageSettings!.Width, 1);
    }

    // 4 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TextboxStyle_MapsFontColorAlignDecoration()
    {
        var title = El(Convert(SampleRdl).Design, "title");
        Assert.Equal("text", title.Type);
        Assert.Equal("Arial", title.Style!["fontFamily"]);
        Assert.Equal(20.0, title.Style!["fontSize"]);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("#0066CC", title.Style!["color"]);
        Assert.Equal("center", title.Style!["textAlign"]);
        Assert.Equal("underline", title.Style!["textDecoration"]);
    }

    // 5 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NamedColor_NormalizesToHex()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="t"><Top>0in</Top><Left>0in</Left><Height>0.3in</Height><Width>2in</Width>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Hi</Value><Style><Color>Blue</Color></Style></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        Assert.Equal("#0000FF", El(Convert(rdl).Design, "t").Style!["color"]);
    }

    // 6 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_FieldsValueBinding_BecomesCanvasBinding()
    {
        var r = Convert(SampleRdl);
        var customer = El(r.Design, "customer");
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRDL010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    // 7 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ComplexExpression_BecomesExpressionWithWarning()
    {
        var r = Convert(SampleRdl);
        var total = El(r.Design, "total");
        Assert.Equal("=Sum(Fields!Total.Value)", total.Expression);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRDL010" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 8 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_LiteralValue_BecomesPlainText()
    {
        var title = El(Convert(SampleRdl).Design, "title");
        Assert.Equal("Invoice 2024", title.Content);
        Assert.Null(title.Binding);
        Assert.Null(title.Expression);
    }

    // 9 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Tablix2016_BecomesTable()
    {
        var items = El(Convert(SampleRdl).Design, "items");
        Assert.Equal("table", items.Type);
        Assert.True(items.HeaderRow);
        Assert.Equal(new[] { "Name", "Price" }, items.CellData![0]);
        Assert.Equal(new[] { "{{ItemName}}", "Widget" }, items.CellData![1]);
    }

    // 10 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Table2008Schema_BecomesTable()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition" Name="T">
              <Body><ReportItems>
                <Table Name="t">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <TableColumns><TableColumn><Width>2in</Width></TableColumn><TableColumn><Width>2in</Width></TableColumn></TableColumns>
                  <Header><TableRows><TableRow><TableCells>
                    <TableCell><ReportItems><Textbox Name="h1"><Value>Name</Value><Style><TextAlign>Left</TextAlign></Style></Textbox></ReportItems></TableCell>
                    <TableCell><ReportItems><Textbox Name="h2"><Value>Price</Value><Style><TextAlign>Right</TextAlign></Style></Textbox></ReportItems></TableCell>
                  </TableCells></TableRow></TableRows></Header>
                  <Details><TableRows><TableRow><TableCells>
                    <TableCell><ReportItems><Textbox Name="d1"><Value>Widget</Value></Textbox></ReportItems></TableCell>
                    <TableCell><ReportItems><Textbox Name="d2"><Value>=Fields!Price.Value</Value></Textbox></ReportItems></TableCell>
                  </TableCells></TableRow></TableRows></Details>
                </Table>
              </ReportItems><Height>5in</Height></Body>
              <PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth>
            </Report>
            """;
        var t = El(Convert(rdl).Design, "t");
        Assert.Equal("table", t.Type);
        Assert.Equal(new[] { "Name", "Price" }, t.CellData![0]);
        Assert.Equal(new[] { "Widget", "{{Price}}" }, t.CellData![1]);
    }

    // 11 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TablixHeaderAlignments_BecomeColumnAlignments()
    {
        var items = El(Convert(SampleRdl).Design, "items");
        Assert.Equal(new[] { "left", "right" }, items.ColumnAlignments);
    }

    // 12 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TablixColumnWidths_BecomeColumnWidths()
    {
        var items = El(Convert(SampleRdl).Design, "items");
        Assert.Equal(new[] { 144.0, 144.0 }, items.ColumnWidths);  // 2in each
    }

    // 13 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeSharedElements()
    {
        var d = Convert(SampleRdl).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "title");
        Assert.Contains(d.SharedElements, e => e.Name == "pageinfo");

        var title = El(d, "title");
        var footer = El(d, "pageinfo");
        Assert.Equal(79.2, title.Y, 1);     // TopMargin(72) + 0.1in
        Assert.True(footer.Y > 600, "footer should be anchored near the page bottom");
    }

    // 14 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Rectangle_FlattensNestedItemsToAbsolute()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Rectangle Name="box">
                  <Top>1in</Top><Left>1in</Left><Height>2in</Height><Width>3in</Width>
                  <Style><BackgroundColor>LightGray</BackgroundColor></Style>
                  <ReportItems>
                    <Textbox Name="inner"><Top>0.1in</Top><Left>0.2in</Left><Height>0.3in</Height><Width>1in</Width>
                      <Paragraphs><Paragraph><TextRuns><TextRun><Value>Hi</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                    </Textbox>
                  </ReportItems>
                </Rectangle>
              </ReportItems><Height>5in</Height></Body>
              <Page><PageWidth>8.5in</PageWidth><PageHeight>11in</PageHeight>
                <LeftMargin>1in</LeftMargin><TopMargin>1in</TopMargin></Page>
            </Report>
            """;
        var d = Convert(rdl).Design;
        var box = El(d, "box");
        var inner = El(d, "inner");
        Assert.Equal("rect", box.Type);
        Assert.Equal("#D3D3D3", box.Style!["backgroundColor"]);
        // inner: LeftMargin(72) + Left(72) + innerLeft(0.2in=14.4) = 158.4 ; TopMargin(72) + Top(72) + innerTop(0.1in=7.2) = 151.2
        Assert.Equal(158.4, inner.X, 1);
        Assert.Equal(151.2, inner.Y, 1);
    }

    // 15 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Line_MapsStrokeAndDash()
    {
        var rule = El(Convert(SampleRdl).Design, "rule");
        Assert.Equal("line", rule.Type);
        Assert.Equal("#808080", rule.Style!["color"]);
        Assert.Equal(3.0, rule.Style!["strokeWidth"]);
        Assert.Equal("dashed", rule.Style!["dashStyle"]);
    }

    // 16 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_EmbeddedImage_KeepsDataUrl()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Image Name="logo"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <Source>Embedded</Source><Value>brand</Value></Image>
              </ReportItems><Height>5in</Height></Body>
              <EmbeddedImages><EmbeddedImage Name="brand"><MIMEType>image/png</MIMEType>
                <ImageData>iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=</ImageData>
              </EmbeddedImage></EmbeddedImages>
            </Report>
            """;
        var r = Convert(rdl);
        var logo = El(r.Design, "logo");
        Assert.Equal("image", logo.Type);
        Assert.StartsWith("data:image/png;base64,", logo.Content);
        Assert.False(Has(r.Diagnostics, "CANMIGRDL012"));
    }

    // 17 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ExternalImage_EmitsPlaceholderWarning()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Image Name="ext"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <Source>External</Source><Value>http://example.com/a.png</Value></Image>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        var r = Convert(rdl);
        Assert.Equal("image", El(r.Design, "ext").Type);
        Assert.True(Has(r.Diagnostics, "CANMIGRDL012"));
    }

    // 18 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Subreport_EmitsManualMigrationDiagnostic()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Subreport Name="sub"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <ReportName>Detail</ReportName></Subreport>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        var r = Convert(rdl);
        Assert.DoesNotContain(r.Design.Pages[0].Elements, e => e.Name == "sub");
        Assert.True(Has(r.Diagnostics, "CANMIGRDL011"));
    }

    // 19 ──────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition")]
    [InlineData("http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition")]
    [InlineData("http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition")]
    public void Convert_NamespaceVariants_AllDetected(string ns)
    {
        var rdl = $"""
            <Report xmlns="{ns}">
              <Body><ReportItems>
                <Textbox Name="t"><Top>0in</Top><Left>0in</Left><Height>0.3in</Height><Width>2in</Width>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Hi</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        Assert.True(RdlToDesignConverter.LooksLikeRdl(rdl));
        Assert.Single(Convert(rdl).Design.Pages[0].Elements);
    }

    // 20 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<Report><not closed"));

    // 21 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void LooksLikeRdl_DetectsRdlVsNonRdl()
    {
        Assert.True(RdlToDesignConverter.LooksLikeRdl(SampleRdl));
        Assert.False(RdlToDesignConverter.LooksLikeRdl("""<XtraReportsLayoutSerializer Name="x" />"""));
        Assert.False(RdlToDesignConverter.LooksLikeRdl("""<Report xmlns="http://example.com/other">x</Report>"""));
        Assert.False(RdlToDesignConverter.LooksLikeRdl("public class Foo {}"));
    }

    // 22 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_DefaultsToA4_WhenPageSizeAbsent()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems /><Height>5in</Height></Body>
            </Report>
            """;
        var ps = Convert(rdl).Design.PageSettings!;
        Assert.Equal(595, ps.Width, 1);
        Assert.Equal(842, ps.Height, 1);
    }
}
