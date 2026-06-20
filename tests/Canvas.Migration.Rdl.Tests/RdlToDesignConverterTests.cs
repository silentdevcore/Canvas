using System.Text.Json;
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

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

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

    [Fact]
    public void Convert_TablixPercentColumnWidths_AreResolvedAgainstTableWidth()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="percentTable">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>400pt</Width>
                  <TablixBody>
                    <TablixColumns>
                      <TablixColumn><Width>25%</Width></TablixColumn>
                      <TablixColumn><Width>75%</Width></TablixColumn>
                    </TablixColumns>
                    <TablixRows><TablixRow><Height>0.25in</Height><TablixCells>
                      <TablixCell><CellContents><Textbox Name="a"><Value>A</Value></Textbox></CellContents></TablixCell>
                      <TablixCell><CellContents><Textbox Name="b"><Value>B</Value></Textbox></CellContents></TablixCell>
                    </TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var table = El(Convert(rdl).Design, "percentTable");

        Assert.Equal(new[] { 100.0, 300.0 }, table.ColumnWidths);
    }

    [Fact]
    public void Convert_TablixRelativeColumnWidths_ShareRemainingWidth()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="relativeTable">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>360pt</Width>
                  <TablixBody>
                    <TablixColumns>
                      <TablixColumn><Width>60pt</Width></TablixColumn>
                      <TablixColumn><Width>1*</Width></TablixColumn>
                      <TablixColumn><Width>2*</Width></TablixColumn>
                    </TablixColumns>
                    <TablixRows><TablixRow><Height>0.25in</Height><TablixCells>
                      <TablixCell><CellContents><Textbox Name="a"><Value>A</Value></Textbox></CellContents></TablixCell>
                      <TablixCell><CellContents><Textbox Name="b"><Value>B</Value></Textbox></CellContents></TablixCell>
                      <TablixCell><CellContents><Textbox Name="c"><Value>C</Value></Textbox></CellContents></TablixCell>
                    </TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var table = El(Convert(rdl).Design, "relativeTable");

        Assert.Equal(new[] { 60.0, 100.0, 200.0 }, table.ColumnWidths);
    }

    [Fact]
    public void Convert_TablixDetailOnlyHierarchy_DoesNotTreatFirstDataRowAsHeader()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="details">
                  <Top>0in</Top><Left>0in</Left><Height>0.5in</Height><Width>3in</Width>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>3in</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><Height>0.25in</Height><TablixCells>
                      <TablixCell><CellContents><Textbox Name="product"><Value>=Fields!Product.Value</Value></Textbox></CellContents></TablixCell>
                    </TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="Details" /></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var table = El(result.Design, "details");

        Assert.False(table.HeaderRow);
        Assert.Equal(new[] { "{{Product}}" }, table.CellData![0]);
        Assert.Equal(false, table.Style!["rdlHeaderRowFromHierarchy"]);
        var rowHierarchy = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlTablixRowHierarchy"]);
        Assert.Equal("Details", rowHierarchy[0]["groupName"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL029" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_TablixHierarchyHeaders_ArePreservedOnTableStyle()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="matrix">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                    <TablixRows>
                      <TablixRow><Height>0.25in</Height><TablixCells>
                        <TablixCell><CellContents><Textbox Name="h1"><Value>Product</Value></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="h2"><Value>Total</Value></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                      <TablixRow><Height>0.25in</Height><TablixCells>
                        <TablixCell><CellContents><Textbox Name="p"><Value>=Fields!Product.Value</Value></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="t"><Value>=Sum(Fields!Total.Value)</Value></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                    </TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers>
                    <TablixMember>
                      <Group Name="YearGroup"><GroupExpressions><GroupExpression>=Fields!Year.Value</GroupExpression></GroupExpressions></Group>
                      <SortExpressions><SortExpression><Value>=Fields!Year.Value</Value></SortExpression></SortExpressions>
                      <TablixHeader><Size>18pt</Size><CellContents><Textbox Name="yearHeader"><Value>=Fields!Year.Value</Value></Textbox></CellContents></TablixHeader>
                    </TablixMember>
                    <TablixMember />
                  </TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers>
                    <TablixMember><KeepWithGroup>After</KeepWithGroup><RepeatOnNewPage>true</RepeatOnNewPage></TablixMember>
                    <TablixMember>
                      <Group Name="ProductGroup"><GroupExpressions><GroupExpression>=Fields!Product.Value</GroupExpression></GroupExpressions></Group>
                      <TablixHeader><Size>1in</Size><CellContents><Textbox Name="productHeader"><Value>=Fields!Product.Value</Value></Textbox></CellContents></TablixHeader>
                    </TablixMember>
                  </TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var table = El(result.Design, "matrix");

        Assert.True(table.HeaderRow);
        Assert.Equal(true, table.Style!["rdlHeaderRowFromHierarchy"]);
        var rowHierarchy = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlTablixRowHierarchy"]);
        Assert.Equal(2, rowHierarchy.Length);
        Assert.Equal("After", rowHierarchy[0]["keepWithGroup"]);
        Assert.Equal("ProductGroup", rowHierarchy[1]["groupName"]);
        Assert.Equal("{{Product}}", rowHierarchy[1]["headerText"]);
        Assert.Equal(72.0, rowHierarchy[1]["headerSizePt"]);

        var columnHierarchy = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlTablixColumnHierarchy"]);
        Assert.Equal("YearGroup", columnHierarchy[0]["groupName"]);
        Assert.Equal(new[] { "=Fields!Year.Value" }, Assert.IsType<string[]>(columnHierarchy[0]["groupExpressions"]));
        Assert.Equal(new[] { "=Fields!Year.Value" }, Assert.IsType<string[]>(columnHierarchy[0]["sortExpressions"]));
        Assert.Equal("{{Year}}", columnHierarchy[0]["headerText"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL029" && d.Severity == MigrationDiagnosticSeverity.Warning);
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
    public void Convert_ExternalImage_PreservesReferenceWithWarning()
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
        var ext = El(r.Design, "ext");
        Assert.Equal("image", ext.Type);
        Assert.Equal("http://example.com/a.png", ext.Content);
        Assert.Equal("External", ext.Style!["rdlImageSource"]);
        Assert.True(Has(r.Diagnostics, "CANMIGRDL012"));
    }

    // 18 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_DatabaseImage_MapsFieldBindingWithWarning()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Image Name="photo"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <Source>Database</Source><Value>=Fields!ProductImage.Value</Value></Image>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var r = Convert(rdl);
        var photo = El(r.Design, "photo");
        Assert.Equal("image", photo.Type);
        Assert.Equal("ProductImage", photo.Binding);
        Assert.Equal("{{ProductImage}}", photo.Content);
        Assert.Equal("Database", photo.Style!["rdlImageSource"]);
        Assert.True(Has(r.Diagnostics, "CANMIGRDL012"));
    }

    // 19 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Subreport_PreservesMetadataOnPlaceholder()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Subreport Name="sub"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <ReportName>Detail</ReportName>
                  <Parameters>
                    <Parameter Name="OrderId"><Value>=Fields!OrderId.Value</Value></Parameter>
                    <Parameter Name="Region"><Value>=Parameters!Region.Value</Value></Parameter>
                  </Parameters>
                </Subreport>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        var r = Convert(rdl);
        var sub = El(r.Design, "sub");                 // kept as a labeled placeholder, not dropped
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report: Detail", sub.Content);
        Assert.Equal("Subreport", sub.Style!["rdlCustomItemType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(sub.Style["rdlSubreport"]);
        Assert.Equal("Detail", metadata["ReportName"]);
        var parameters = Assert.IsType<Dictionary<string, object>[]>(metadata["Parameters"]);
        Assert.Equal("OrderId", parameters[0]["Name"]);
        Assert.Equal("=Fields!OrderId.Value", parameters[0]["Value"]);
        Assert.Equal("Region", parameters[1]["Name"]);
        Assert.Equal("=Parameters!Region.Value", parameters[1]["Value"]);
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

    // 23 ──────────────────────────────────────────────────────────────────────────────────────────
    // ActiveReports / DsReport .rdlx is plain RDL XML (Microsoft RDL namespace) — it routes and
    // converts through the same pipeline, and serializes its barcode as an RDL <CustomReportItem>.
    [Fact]
    public void Convert_ActiveReportsRdlxBarcode_BecomesCanvasBarcode()
    {
        var rdlx = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition" Name="ARReport">
              <Body><ReportItems>
                <CustomReportItem Name="code"><Type>Barcode</Type>
                  <Top>0in</Top><Left>1in</Left><Height>0.5in</Height><Width>2in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>Symbology</Name><Value>Code128</Value></CustomProperty>
                    <CustomProperty><Name>Value</Name><Value>=Fields!Sku.Value</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
              <Page><PageWidth>8.5in</PageWidth><PageHeight>11in</PageHeight><LeftMargin>1in</LeftMargin></Page>
            </Report>
            """;
        Assert.True(RdlToDesignConverter.LooksLikeRdl(rdlx));
        var code = El(Convert(rdlx).Design, "code");
        Assert.Equal("barcode", code.Type);
        Assert.Equal("code128", code.BarcodeType);
        Assert.Equal("{{Sku}}", code.BarcodeValue);
    }

    // 24 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_QrCodeCustomItem_BecomesQrCode()
    {
        var rdlx = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2010/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="qr"><Type>Barcode</Type>
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>1in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>Symbology</Name><Value>QRCode</Value></CustomProperty>
                    <CustomProperty><Name>Value</Name><Value>https://example.com</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        var qr = El(Convert(rdlx).Design, "qr");
        Assert.Equal("qrcode", qr.Type);
        Assert.Equal("https://example.com", qr.QrValue);
    }

    // 25 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ChartCustomItem_BecomesCanvasChartPlaceholder()
    {
        var rdlx = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="chart"><Type>Chart</Type>
                  <Top>0in</Top><Left>0in</Left><Height>2in</Height><Width>3in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>Category</Name><Value>=Fields!Region.Value</Value></CustomProperty>
                    <CustomProperty><Name>Value</Name><Value>=Sum(Fields!Total.Value)</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;
        var r = Convert(rdlx);
        var chart = El(r.Design, "chart");
        Assert.Equal("chart", chart.Type);
        Assert.Equal("bar", chart.ChartType);
        Assert.NotNull(chart.ChartData);
        Assert.Equal("Chart", chart.Style!["rdlCustomItemType"]);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRDL017" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_GaugeCustomItem_PreservesValueRangeMetadata()
    {
        var rdlx = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="deliveryGauge"><Type>Gauge</Type>
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>2in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>GaugeType</Name><Value>Linear</Value></CustomProperty>
                    <CustomProperty><Name>Value</Name><Value>=Fields!DeliveredPercent.Value</Value></CustomProperty>
                    <CustomProperty><Name>MinimumValue</Name><Value>0</Value></CustomProperty>
                    <CustomProperty><Name>MaximumValue</Name><Value>100</Value></CustomProperty>
                    <CustomProperty><Name>TargetValue</Name><Value>95</Value></CustomProperty>
                    <CustomProperty><Name>DataSetName</Name><Value>DeliveryStats</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdlx);
        var gauge = El(result.Design, "deliveryGauge");

        Assert.Equal("text", gauge.Type);
        Assert.Contains("{{DeliveredPercent}} / 100", gauge.Content);
        Assert.Equal("Gauge", gauge.Style!["rdlCustomItemType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(gauge.Style["rdlGauge"]);
        Assert.Equal("Linear", metadata["GaugeType"]);
        Assert.Equal("=Fields!DeliveredPercent.Value", metadata["Value"]);
        Assert.Equal("0", metadata["MinimumValue"]);
        Assert.Equal("100", metadata["MaximumValue"]);
        Assert.Equal("95", metadata["TargetValue"]);
        Assert.Equal("DeliveryStats", metadata["DataSetName"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL018" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_SparklineCustomItem_BecomesCompactCanvasChart()
    {
        var rdlx = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="trend"><Type>Sparkline</Type>
                  <Top>0in</Top><Left>0in</Left><Height>0.35in</Height><Width>1.75in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>SparklineType</Name><Value>Line</Value></CustomProperty>
                    <CustomProperty><Name>Category</Name><Value>=Fields!Month.Value</Value></CustomProperty>
                    <CustomProperty><Name>Value</Name><Value>=Sum(Fields!Revenue.Value)</Value></CustomProperty>
                    <CustomProperty><Name>DataSetName</Name><Value>RevenueTrend</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdlx);
        var sparkline = El(result.Design, "trend");

        Assert.Equal("chart", sparkline.Type);
        Assert.Equal("line", sparkline.ChartType);
        Assert.Equal("Sparkline", sparkline.Style!["rdlCustomItemType"]);
        Assert.NotNull(sparkline.ChartData);
        Assert.True(Assert.IsType<bool>(sparkline.ChartData["rdlSparkline"]));
        Assert.Equal("=Fields!Month.Value", sparkline.ChartData["rdlCategoryExpression"]);
        Assert.Equal("=Sum(Fields!Revenue.Value)", sparkline.ChartData["rdlValueExpression"]);
        Assert.Equal("RevenueTrend", sparkline.ChartData["rdlDataSetName"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL017" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 26 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NativeChart_BecomesCanvasChartPlaceholder()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Chart Name="TopEmployeesChart">
                  <Top>0in</Top><Left>0in</Left><Height>2in</Height><Width>4in</Width>
                  <DataSetName>TopEmployees</DataSetName>
                  <ChartCategoryHierarchy><ChartMembers><ChartMember><Label>=Fields!FullName.Value</Label></ChartMember></ChartMembers></ChartCategoryHierarchy>
                  <ChartData><ChartSeriesCollection><ChartSeries Name="Series1">
                    <ChartDataPoints><ChartDataPoint><ChartDataPointValues><Y>=Round(Sum(Fields!SaleAmount.Value)/1000)</Y></ChartDataPointValues></ChartDataPoint></ChartDataPoints>
                    <Type>Bar</Type>
                  </ChartSeries></ChartSeriesCollection></ChartData>
                </Chart>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var chart = El(result.Design, "TopEmployeesChart");

        Assert.Equal("chart", chart.Type);
        Assert.Equal("bar", chart.ChartType);
        Assert.NotNull(chart.ChartData);
        Assert.Equal("Chart", chart.Style!["rdlCustomItemType"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL017" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 27 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NativeChart_PreservesMultipleSeriesAndAdvancedChartTypes()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Chart Name="AreaChart">
                  <Top>0in</Top><Left>0in</Left><Height>2in</Height><Width>4in</Width>
                  <DataSetName>SalesData</DataSetName>
                  <ChartCategoryHierarchy><ChartMembers><ChartMember><Label>=Fields!Month.Value</Label></ChartMember></ChartMembers></ChartCategoryHierarchy>
                  <ChartTitles><ChartTitle Name="Title"><Caption>Monthly Sales</Caption></ChartTitle></ChartTitles>
                  <ChartData><ChartSeriesCollection>
                    <ChartSeries Name="Sales"><ChartDataPoints><ChartDataPoint><ChartDataPointValues><Y>=Sum(Fields!Sales.Value)</Y></ChartDataPointValues></ChartDataPoint></ChartDataPoints><Type>Area</Type></ChartSeries>
                    <ChartSeries Name="Forecast"><ChartDataPoints><ChartDataPoint><ChartDataPointValues><Y>=Sum(Fields!Forecast.Value)</Y></ChartDataPointValues></ChartDataPoint></ChartDataPoints><Type>Line</Type></ChartSeries>
                  </ChartSeriesCollection></ChartData>
                </Chart>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var chart = El(result.Design, "AreaChart");

        Assert.Equal("chart", chart.Type);
        Assert.Equal("line", chart.ChartType);
        var datasets = Assert.IsType<Dictionary<string, object>[]>(chart.ChartData!["datasets"]);
        Assert.Equal(2, datasets.Length);
        Assert.Equal("Sales", datasets[0]["label"]);
        Assert.Equal("Forecast", datasets[1]["label"]);
        var series = Assert.IsType<Dictionary<string, object>[]>(chart.ChartData["rdlSeries"]);
        Assert.Equal("Area", series[0]["type"]);
        Assert.Equal("=Sum(Fields!Sales.Value)", series[0]["y"]);
        Assert.Equal("Line", series[1]["type"]);
        Assert.Equal("=Fields!Month.Value", chart.ChartData["rdlCategoryExpression"]);
        Assert.Equal("SalesData", chart.ChartData["rdlDataSetName"]);
        Assert.Equal("Monthly Sales", chart.ChartData["rdlTitle"]);
    }

    // 28 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NativeScatterChart_PreservesXAndSizeExpressions()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Chart Name="BubbleChart">
                  <Top>0in</Top><Left>0in</Left><Height>2in</Height><Width>4in</Width>
                  <ChartData><ChartSeriesCollection>
                    <ChartSeries Name="Growth">
                      <ChartDataPoints><ChartDataPoint><ChartDataPointValues>
                        <X>=Fields!Margin.Value</X>
                        <Y>=Fields!Growth.Value</Y>
                        <Size>=Fields!Revenue.Value</Size>
                      </ChartDataPointValues></ChartDataPoint></ChartDataPoints>
                      <Type>Scatter</Type>
                    </ChartSeries>
                  </ChartSeriesCollection></ChartData>
                </Chart>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var chart = El(Convert(rdl).Design, "BubbleChart");

        Assert.Equal("line", chart.ChartType);
        var series = Assert.IsType<Dictionary<string, object>[]>(chart.ChartData!["rdlSeries"]);
        Assert.Equal("Scatter", series[0]["type"]);
        Assert.Equal("=Fields!Margin.Value", series[0]["x"]);
        Assert.Equal("=Fields!Growth.Value", series[0]["y"]);
        Assert.Equal("=Fields!Revenue.Value", series[0]["size"]);
    }

    // 29 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ShapeCustomItem_BecomesCanvasShape()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="badge"><Type>Shape</Type>
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>2in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>ShapeType</Name><Value>Ellipse</Value></CustomProperty>
                    <CustomProperty><Name>FillColor</Name><Value>LightGray</Value></CustomProperty>
                    <CustomProperty><Name>LineColor</Name><Value>#336699</Value></CustomProperty>
                    <CustomProperty><Name>LineWidth</Name><Value>2pt</Value></CustomProperty>
                    <CustomProperty><Name>LineStyle</Name><Value>Dash</Value></CustomProperty>
                    <CustomProperty><Name>RotationAngle</Name><Value>15</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var badge = El(result.Design, "badge");

        Assert.Equal("circle", badge.Type);
        Assert.Equal("Shape", badge.Style!["rdlCustomItemType"]);
        Assert.Equal("Ellipse", badge.Style["rdlShapeType"]);
        Assert.Equal("#D3D3D3", badge.Style["backgroundColor"]);
        Assert.Equal("#336699", badge.Style["borderColor"]);
        Assert.Equal(2.0, badge.Style["borderWidth"]);
        Assert.Equal("dashed", badge.Style["dashStyle"]);
        Assert.Equal(15.0, badge.Style["rotation"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 28 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ArrowShapeCustomItem_BecomesCanvasArrow()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="nextArrow"><Type>Shape</Type>
                  <Top>0in</Top><Left>0in</Left><Height>0.5in</Height><Width>1in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>ShapeType</Name><Value>RightArrow</Value></CustomProperty>
                    <CustomProperty><Name>LineColor</Name><Value>Blue</Value></CustomProperty>
                    <CustomProperty><Name>LineWidth</Name><Value>3</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var arrow = El(result.Design, "nextArrow");

        Assert.Equal("arrow", arrow.Type);
        Assert.Equal("right", arrow.ArrowDirection);
        Assert.Equal("arrow", arrow.EndMarker);
        Assert.Equal("#0000FF", arrow.Style!["color"]);
        Assert.Equal(3.0, arrow.Style["strokeWidth"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL020");
    }

    [Fact]
    public void Convert_DocumentCustomItems_BecomePositionedPlaceholdersWithMetadata()
    {
        var embeddedPdf = new string('A', 600);
        var rdl = $$"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="htmlDoc"><Type>htmldocument</Type>
                  <Top>0.25in</Top><Left>0.5in</Left><Height>1in</Height><Width>2in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>Source</Name><Value>URL</Value></CustomProperty>
                    <CustomProperty><Name>Sizing</Name><Value>AutoSize</Value></CustomProperty>
                    <CustomProperty><Name>DocumentValue</Name><Value>https://example.com/report</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
                <CustomReportItem Name="pdfDoc"><Type>pdfdocument</Type>
                  <Top>1.5in</Top><Left>0.5in</Left><Height>2in</Height><Width>3in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>Source</Name><Value>Embedded</Value></CustomProperty>
                    <CustomProperty><Name>DocumentValue</Name><Value>{{embeddedPdf}}</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var html = El(result.Design, "htmlDoc");
        var pdf = El(result.Design, "pdfDoc");

        Assert.Equal("text", html.Type);
        Assert.Equal("HtmlDocument", html.Style!["rdlCustomItemType"]);
        Assert.Equal("html", html.Style["rdlDocumentKind"]);
        Assert.Equal("URL", html.Style["rdlDocumentSource"]);

        Assert.Equal("text", pdf.Type);
        Assert.Equal("PdfDocument", pdf.Style!["rdlCustomItemType"]);
        Assert.Equal("pdf", pdf.Style["rdlDocumentKind"]);
        var pdfProps = Assert.IsType<Dictionary<string, object>>(pdf.Style["rdlCustomProperties"]);
        Assert.Contains("[truncated 88 chars]", Assert.IsType<string>(pdfProps["DocumentValue"]));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL027" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_SignatureCustomItem_BecomesCanvasSignatureWithMetadata()
    {
        var signatureValue = new string('B', 540);
        var rdl = $$"""
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="signedBy"><Type>PDFSignature</Type>
                  <Top>0.25in</Top><Left>0.5in</Left><Height>0.75in</Height><Width>2in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>SignatureValue</Name><Value>{{signatureValue}}</Value></CustomProperty>
                    <CustomProperty><Name>CertificateFileName</Name><Value>/PDFSign.pfx</Value></CustomProperty>
                    <CustomProperty><Name>SignedName</Name><Value>false</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var signature = El(result.Design, "signedBy");

        Assert.Equal("signature", signature.Type);
        Assert.Equal("PDF Signature", signature.SignatureLabel);
        Assert.Equal("PDFSignature", signature.Style!["rdlCustomItemType"]);
        Assert.Equal("pdf", signature.Style["rdlSignatureKind"]);
        var props = Assert.IsType<Dictionary<string, object>>(signature.Style["rdlCustomProperties"]);
        Assert.Equal("/PDFSign.pfx", props["CertificateFileName"]);
        Assert.Contains("[truncated 28 chars]", Assert.IsType<string>(props["SignatureValue"]));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL028" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 29 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NativeGaugePanel_PreservesGaugeMetadataOnPlaceholder()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <GaugePanel Name="RevenueGauge">
                  <Top>0.5in</Top><Left>0.25in</Left><Height>2in</Height><Width>3in</Width>
                  <Style><BackgroundColor>White</BackgroundColor><Border><Color>LightGrey</Color><Style>Solid</Style></Border></Style>
                  <DataSetName>RevenueDataset</DataSetName>
                  <RadialGauges>
                    <RadialGauge Name="RadialGaugeSet">
                      <GaugeScales><RadialScale Name="RadialScale">
                        <MinimumValue><Value>0</Value></MinimumValue>
                        <MaximumValue><Value>=Fields!EstimatedRevenue.Value</Value></MaximumValue>
                        <Interval>=Fields!EstimatedRevenue.Value*0.5</Interval>
                        <GaugePointers><RadialPointer Name="ActualRevenue">
                          <Type>Needle</Type>
                          <GaugeInputValue><Value>=Fields!ActualRevenue.Value</Value></GaugeInputValue>
                        </RadialPointer></GaugePointers>
                        <ScaleRanges><ScaleRange Name="GoodRange">
                          <StartValue><Value>75</Value></StartValue><EndValue><Value>100</Value></EndValue>
                          <Style><BackgroundColor>Green</BackgroundColor></Style>
                        </ScaleRange></ScaleRanges>
                      </RadialScale></GaugeScales>
                    </RadialGauge>
                  </RadialGauges>
                </GaugePanel>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var gauge = El(result.Design, "RevenueGauge");

        Assert.Equal("text", gauge.Type);
        Assert.Contains("{{ActualRevenue}}", gauge.Content);
        Assert.Equal("GaugePanel", gauge.Style!["rdlCustomItemType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(gauge.Style["rdlGaugePanel"]);
        Assert.Equal("RevenueDataset", metadata["DataSetName"]);
        Assert.Equal("Radial", metadata["GaugeType"]);
        var gauges = Assert.IsType<Dictionary<string, object>[]>(metadata["Gauges"]);
        var scales = Assert.IsType<Dictionary<string, object>[]>(gauges[0]["Scales"]);
        var pointers = Assert.IsType<Dictionary<string, object>[]>(scales[0]["Pointers"]);
        Assert.Equal("=Fields!ActualRevenue.Value", pointers[0]["Value"]);
        var ranges = Assert.IsType<Dictionary<string, object>[]>(scales[0]["Ranges"]);
        Assert.Equal("Green", ranges[0]["BackgroundColor"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 30 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_NativeMap_PreservesMapMetadataOnPlaceholder()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Map Name="WorldMap">
                  <Top>0in</Top><Left>0in</Left><Height>3in</Height><Width>5in</Width>
                  <ToolTip>World Population Map</ToolTip>
                  <Style><BackgroundColor>White</BackgroundColor><Border><Color>LightGrey</Color><Width>1pt</Width></Border></Style>
                  <MapLayers>
                    <MapPolygonLayer Name="PolygonLayer1">
                      <MapDataRegionName>DataRegion</MapDataRegionName>
                      <MapBindingFieldPairs><MapBindingFieldPair>
                        <FieldName>name</FieldName>
                        <BindingExpression>=Fields!Country.Value</BindingExpression>
                      </MapBindingFieldPair></MapBindingFieldPairs>
                      <MapFieldDefinitions><MapFieldDefinition>
                        <Name>name</Name><DataType>String</DataType>
                      </MapFieldDefinition></MapFieldDefinitions>
                      <MapPolygonRules><MapColorRangeRule>
                        <DataValue>=Sum(Fields!Population.Value)</DataValue>
                      </MapColorRangeRule></MapPolygonRules>
                      <MapPolygons>
                        <MapPolygon><VectorData>abc</VectorData></MapPolygon>
                        <MapPolygon><VectorData>def</VectorData></MapPolygon>
                      </MapPolygons>
                    </MapPolygonLayer>
                  </MapLayers>
                  <MapDataRegions><MapDataRegion Name="DataRegion">
                    <DataSetName>PopulationDataset</DataSetName>
                    <MapMember><Group Name="CountryGroup" /></MapMember>
                  </MapDataRegion></MapDataRegions>
                  <MapViewport>
                    <MapCoordinateSystem>Geographic</MapCoordinateSystem>
                    <MapProjection>Mercator</MapProjection>
                    <MaximumZoom>4000000</MaximumZoom>
                    <MapCustomView><CenterX>50</CenterX><CenterY>50</CenterY><Zoom>125</Zoom></MapCustomView>
                  </MapViewport>
                  <MapLegends><MapLegend Name="Legend1" /></MapLegends>
                  <MapTitles><MapTitle Name="Title1"><Text>Population</Text></MapTitle></MapTitles>
                  <MapDistanceScale />
                  <MapColorScale />
                </Map>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var map = El(result.Design, "WorldMap");

        Assert.Equal("text", map.Type);
        Assert.Equal("Map", map.Style!["rdlCustomItemType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(map.Style["rdlMap"]);
        Assert.Equal("World Population Map", metadata["ToolTip"]);
        Assert.True((bool)metadata["HasDistanceScale"]);
        Assert.True((bool)metadata["HasColorScale"]);

        var layers = Assert.IsType<Dictionary<string, object>[]>(metadata["Layers"]);
        Assert.Equal("MapPolygonLayer", layers[0]["Kind"]);
        Assert.Equal(2, layers[0]["SpatialElementCount"]);
        Assert.Contains("MapColorRangeRule", Assert.IsType<string[]>(layers[0]["RuleKinds"]));
        var bindings = Assert.IsType<Dictionary<string, object>[]>(layers[0]["BindingFieldPairs"]);
        Assert.Equal("=Fields!Country.Value", bindings[0]["BindingExpression"]);

        var regions = Assert.IsType<Dictionary<string, object>[]>(metadata["DataRegions"]);
        Assert.Equal("PopulationDataset", regions[0]["DataSetName"]);
        Assert.Equal("CountryGroup", regions[0]["GroupName"]);
        var viewport = Assert.IsType<Dictionary<string, object>>(metadata["Viewport"]);
        Assert.Equal("Geographic", viewport["CoordinateSystem"]);
        Assert.Equal("Mercator", viewport["Projection"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_MapCustomItem_PreservesMapMetadataOnPlaceholder()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <CustomReportItem Name="customMap"><Type>Map</Type>
                  <Top>0in</Top><Left>0in</Left><Height>2in</Height><Width>3in</Width>
                  <CustomProperties>
                    <CustomProperty><Name>MapType</Name><Value>Polygon</Value></CustomProperty>
                    <CustomProperty><Name>DataSetName</Name><Value>PopulationDataset</Value></CustomProperty>
                    <CustomProperty><Name>FieldName</Name><Value>name</Value></CustomProperty>
                    <CustomProperty><Name>BindingExpression</Name><Value>=Fields!Country.Value</Value></CustomProperty>
                    <CustomProperty><Name>ValueExpression</Name><Value>=Sum(Fields!Population.Value)</Value></CustomProperty>
                    <CustomProperty><Name>LabelExpression</Name><Value>=Fields!CountryLabel.Value</Value></CustomProperty>
                    <CustomProperty><Name>Projection</Name><Value>Mercator</Value></CustomProperty>
                    <CustomProperty><Name>CoordinateSystem</Name><Value>Geographic</Value></CustomProperty>
                  </CustomProperties>
                </CustomReportItem>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var map = El(result.Design, "customMap");

        Assert.Equal("text", map.Type);
        Assert.Contains("[Map: Polygon]", map.Content);
        Assert.Equal("Map", map.Style!["rdlCustomItemType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(map.Style["rdlMap"]);
        Assert.Equal("Polygon", metadata["MapType"]);
        Assert.Equal("PopulationDataset", metadata["DataSetName"]);
        Assert.Equal("=Sum(Fields!Population.Value)", metadata["ValueExpression"]);
        Assert.Equal("=Fields!CountryLabel.Value", metadata["LabelExpression"]);
        var bindings = Assert.IsType<Dictionary<string, object>[]>(metadata["BindingFieldPairs"]);
        Assert.Equal("name", bindings[0]["FieldName"]);
        Assert.Equal("=Fields!Country.Value", bindings[0]["BindingExpression"]);
        var viewport = Assert.IsType<Dictionary<string, object>>(metadata["Viewport"]);
        Assert.Equal("Geographic", viewport["CoordinateSystem"]);
        Assert.Equal("Mercator", viewport["Projection"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 31 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TablixCellNestedGaugePanel_ExtractsPositionedElement()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="metrics">
                  <Top>1in</Top><Left>0.5in</Left><Height>2in</Height><Width>4in</Width>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                    <TablixRows>
                      <TablixRow><Height>0.5in</Height><TablixCells>
                        <TablixCell><CellContents><Textbox Name="h1"><Value>Name</Value></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="h2"><Value>Gauge</Value></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                      <TablixRow><Height>1in</Height><TablixCells>
                        <TablixCell><CellContents><Textbox Name="name"><Value>=Fields!Name.Value</Value></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents>
                          <GaugePanel Name="cellGauge">
                            <Top>0.1in</Top><Left>0.2in</Left><Height>0.8in</Height><Width>1.5in</Width>
                            <DataSetName>Metrics</DataSetName>
                            <LinearGauges><LinearGauge Name="LinearGaugeSet">
                              <GaugeScales><LinearScale Name="LinearScale">
                                <GaugePointers><LinearPointer Name="ScorePointer">
                                  <Type>Marker</Type>
                                  <GaugeInputValue><Value>=Fields!Score.Value</Value></GaugeInputValue>
                                </LinearPointer></GaugePointers>
                              </LinearScale></GaugeScales>
                            </LinearGauge></LinearGauges>
                          </GaugePanel>
                        </CellContents></TablixCell>
                      </TablixCells></TablixRow>
                    </TablixRows>
                  </TablixBody>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var table = El(result.Design, "metrics");
        var gauge = El(result.Design, "cellGauge");

        Assert.Equal("table", table.Type);
        Assert.Contains("cellGauge", Assert.IsType<string[]>(table.Style!["rdlExtractedCellItems"]));
        var layouts = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlExtractedCellItemLayouts"]);
        var layout = Assert.Single(layouts);
        Assert.Equal("cellGauge", layout["name"]);
        Assert.Equal("GaugePanel", layout["type"]);
        Assert.Equal(1, layout["row"]);
        Assert.Equal(1, layout["column"]);
        Assert.Equal(2 * 72 + 0.2 * 72, layout["x"]);
        Assert.Equal(0.5 * 72 + 0.1 * 72, layout["y"]);
        Assert.Equal(1.5 * 72, layout["width"]);
        Assert.Equal(0.8 * 72, layout["height"]);
        Assert.Equal("text", gauge.Type);
        Assert.Contains("{{Score}}", gauge.Content);
        Assert.Equal(0.5 * 72 + 2 * 72 + 0.2 * 72, gauge.X, 1);
        Assert.Equal(1 * 72 + 0.5 * 72 + 0.1 * 72, gauge.Y, 1);
        Assert.Equal("metrics", gauge.Style!["rdlParentTablix"]);
        Assert.Equal(1, gauge.Style["rdlParentTablixRow"]);
        Assert.Equal(1, gauge.Style["rdlParentTablixColumn"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL023" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_NestedTablixInGroupedCell_ExtractsTableWithRepeatScope()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="master">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <TablixBody>
                    <TablixColumns>
                      <TablixColumn><Width>2in</Width></TablixColumn>
                      <TablixColumn><Width>2in</Width></TablixColumn>
                    </TablixColumns>
                    <TablixRows>
                      <TablixRow><Height>0.25in</Height><TablixCells>
                        <TablixCell><CellContents><Textbox Name="category"><Value>=Fields!Category.Value</Value></Textbox></CellContents></TablixCell>
                        <TablixCell><CellContents><Textbox Name="summary"><Value>=Sum(Fields!Total.Value)</Value></Textbox></CellContents></TablixCell>
                      </TablixCells></TablixRow>
                      <TablixRow><Height>0.75in</Height><TablixCells>
                        <TablixCell><CellContents>
                          <Tablix Name="detail">
                            <Top>0.05in</Top><Left>0.1in</Left><Height>0.5in</Height><Width>3.5in</Width>
                            <TablixBody>
                              <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn><TablixColumn><Width>1.5in</Width></TablixColumn></TablixColumns>
                              <TablixRows>
                                <TablixRow><Height>0.2in</Height><TablixCells>
                                  <TablixCell><CellContents><Textbox Name="hProduct"><Value>Product</Value></Textbox></CellContents></TablixCell>
                                  <TablixCell><CellContents><Textbox Name="hQty"><Value>Qty</Value></Textbox></CellContents></TablixCell>
                                </TablixCells></TablixRow>
                                <TablixRow><Height>0.3in</Height><TablixCells>
                                  <TablixCell><CellContents><Textbox Name="dProduct"><Value>=Fields!Product.Value</Value></Textbox></CellContents></TablixCell>
                                  <TablixCell><CellContents><Textbox Name="dQty"><Value>=Fields!Quantity.Value</Value></Textbox></CellContents></TablixCell>
                                </TablixCells></TablixRow>
                              </TablixRows>
                            </TablixBody>
                            <TablixColumnHierarchy><TablixMembers><TablixMember /><TablixMember /></TablixMembers></TablixColumnHierarchy>
                            <TablixRowHierarchy><TablixMembers><TablixMember><KeepWithGroup>After</KeepWithGroup></TablixMember><TablixMember><Group Name="DetailRows" /></TablixMember></TablixMembers></TablixRowHierarchy>
                          </Tablix>
                          <ColSpan>2</ColSpan>
                        </CellContents></TablixCell>
                        <TablixCell />
                      </TablixCells></TablixRow>
                    </TablixRows>
                  </TablixBody>
                  <TablixColumnHierarchy><TablixMembers><TablixMember /><TablixMember /></TablixMembers></TablixColumnHierarchy>
                  <TablixRowHierarchy><TablixMembers>
                    <TablixMember>
                      <Group Name="CategoryGroup"><GroupExpressions><GroupExpression>=Fields!Category.Value</GroupExpression></GroupExpressions></Group>
                      <TablixMembers><TablixMember /><TablixMember /></TablixMembers>
                    </TablixMember>
                  </TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var master = El(result.Design, "master");
        var detail = El(result.Design, "detail");

        Assert.Equal("table", detail.Type);
        Assert.Contains("detail", Assert.IsType<string[]>(master.Style!["rdlExtractedCellItems"]));
        var layouts = Assert.IsType<Dictionary<string, object>[]>(master.Style["rdlExtractedCellItemLayouts"]);
        var layout = Assert.Single(layouts);
        Assert.Equal("detail", layout["name"]);
        Assert.Equal("Tablix", layout["type"]);
        Assert.Equal(1, layout["row"]);
        Assert.Equal(0, layout["column"]);
        Assert.Equal(2, layout["columnSpan"]);
        Assert.True(layout.ContainsKey("repeatScope"));
        Assert.True(layout.ContainsKey("repeat"));
        Assert.Equal(new[] { "Product", "Qty" }, detail.CellData![0]);
        Assert.Equal(new[] { "{{Product}}", "{{Quantity}}" }, detail.CellData![1]);
        Assert.Equal("master", detail.Style!["rdlParentTablix"]);
        Assert.Equal(1, detail.Style["rdlParentTablixRow"]);
        Assert.Equal(0, detail.Style["rdlParentTablixColumn"]);
        Assert.Equal(2, detail.Style["rdlParentTablixColumnSpan"]);
        Assert.NotNull(detail.Repeat);
        Assert.Equal("CategoryGroup", detail.Repeat!.DataPath);
        Assert.Equal(detail.Id, detail.Repeat.TemplateId);

        var scope = Assert.IsType<Dictionary<string, object>>(detail.Style["rdlParentTablixRepeatScope"]);
        var groups = Assert.IsType<Dictionary<string, object>[]>(scope["groups"]);
        Assert.Equal("CategoryGroup", groups[0]["name"]);
        Assert.Equal(new[] { "=Fields!Category.Value" }, Assert.IsType<string[]>(groups[0]["expressions"]));

        var repeat = Assert.IsType<Dictionary<string, object>>(detail.Style["rdlRepeat"]);
        Assert.Equal("rdlTablix", repeat["source"]);
        Assert.Equal("master", repeat["parent"]);
        Assert.Equal("CategoryGroup", repeat["dataPath"]);
        Assert.Equal("item", repeat["itemAlias"]);
        var repeatGroups = Assert.IsType<Dictionary<string, object>[]>(repeat["groups"]);
        Assert.Equal("CategoryGroup", repeatGroups[0]["name"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL023" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 32 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ReportParameters_ArePreservedAsCustomProperties()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems /><Height>5in</Height></Body>
              <ReportParameters>
                <ReportParameter Name="ProductCategory">
                  <DataType>String</DataType>
                  <Prompt>Category</Prompt>
                  <MultiValue>true</MultiValue>
                  <DefaultValue><Values><Value>Bikes</Value></Values></DefaultValue>
                  <ValidValues><DataSetReference><DataSetName>Categories</DataSetName><ValueField>CategoryID</ValueField><LabelField>Name</LabelField></DataSetReference></ValidValues>
                </ReportParameter>
              </ReportParameters>
              <ReportParametersLayout>
                <GridLayoutDefinition><NumberOfColumns>1</NumberOfColumns><NumberOfRows>1</NumberOfRows><CellDefinitions><CellDefinition><ColumnIndex>0</ColumnIndex><RowIndex>0</RowIndex><ParameterName>ProductCategory</ParameterName></CellDefinition></CellDefinitions></GridLayoutDefinition>
              </ReportParametersLayout>
            </Report>
            """;

        var result = Convert(rdl);
        var props = result.Design.PageSettings!.CustomProperties!;
        var parametersJson = Assert.Single(props, p => p.Name == "rdlReportParameters").Value;
        using var doc = JsonDocument.Parse(parametersJson);
        var parameter = doc.RootElement[0];

        Assert.Equal("ProductCategory", parameter.GetProperty("Name").GetString());
        Assert.Equal("String", parameter.GetProperty("DataType").GetString());
        Assert.Equal("true", parameter.GetProperty("MultiValue").GetString());
        Assert.Equal("Bikes", parameter.GetProperty("DefaultValue").GetString());
        Assert.Contains("DataSetReference:Categories|CategoryID|Name", parameter.GetProperty("ValidValues").GetString());
        Assert.Contains(props, p => p.Name == "rdlReportParametersLayout");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL024" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 33 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TablixFilters_ArePreservedOnTableStyle()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="filtered">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>2in</Width>
                  <Filters><Filter><FilterExpression>=Fields!Year.Value</FilterExpression><Operator>Equal</Operator><FilterValues><FilterValue>=Parameters!OrderYear.Value</FilterValue></FilterValues></Filter></Filters>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><Height>0.5in</Height><TablixCells><TablixCell><CellContents><Textbox Name="cell"><Value>Item</Value></Textbox></CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixRowHierarchy><TablixMembers><TablixMember><Group Name="YearGroup"><Filters><Filter><FilterExpression>=Fields!Category.Value</FilterExpression><Operator>Like</Operator><FilterValues><FilterValue>A*</FilterValue></FilterValues></Filter></Filters></Group></TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var table = El(result.Design, "filtered");
        var filters = Assert.IsType<Dictionary<string, object>[]>(table.Style!["rdlFilters"]);
        Assert.Equal("=Fields!Year.Value", filters[0]["FilterExpression"]);
        Assert.Equal("Equal", filters[0]["Operator"]);
        Assert.Contains("=Parameters!OrderYear.Value", Assert.IsType<string[]>(filters[0]["FilterValues"]));

        var groupFilters = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlTablixGroupFilters"]);
        Assert.Equal("YearGroup", groupFilters[0]["GroupName"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL025" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 34 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ActionInfoAndBookmark_ArePreservedOnElementStyle()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="link"><Top>0in</Top><Left>0in</Left><Height>0.3in</Height><Width>2in</Width>
                  <Bookmark>HomePage</Bookmark>
                  <ActionInfo><Actions><Action>
                    <Drillthrough><ReportName>/Reports/Detail</ReportName><Parameters><Parameter Name="ProductName"><Value>=Fields!Name.Value</Value></Parameter></Parameters></Drillthrough>
                  </Action><Action><BookmarkLink>HomePage</BookmarkLink></Action></Actions></ActionInfo>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Open detail</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var link = El(result.Design, "link");
        var navigation = Assert.IsType<Dictionary<string, object>>(link.Style!["rdlNavigation"]);

        Assert.Equal("link", link.Type);
        Assert.Equal("/Reports/Detail?ProductName={{Name}}", link.Href);
        Assert.Equal("_blank", link.LinkTarget);
        Assert.Equal(true, link.Style["rdlNavigationMappedToLink"]);
        Assert.Equal("HomePage", navigation["Bookmark"]);
        var actions = Assert.IsType<Dictionary<string, object>[]>(navigation["Actions"]);
        var drill = Assert.IsType<Dictionary<string, object>>(actions[0]["Drillthrough"]);
        Assert.Equal("/Reports/Detail", drill["ReportName"]);
        var parameters = Assert.IsType<Dictionary<string, object>[]>(drill["Parameters"]);
        Assert.Equal("ProductName", parameters[0]["Name"]);
        Assert.Equal("=Fields!Name.Value", parameters[0]["Value"]);
        Assert.Equal("HomePage", actions[1]["BookmarkLink"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL026" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 35 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TablixDocumentMapAndToggle_ArePreservedOnTableStyle()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Tablix Name="navTable">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>2in</Width>
                  <TablixBody>
                    <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                    <TablixRows><TablixRow><Height>0.5in</Height><TablixCells><TablixCell><CellContents><Textbox Name="cell"><Value>Item</Value></Textbox></CellContents></TablixCell></TablixCells></TablixRow></TablixRows>
                  </TablixBody>
                  <TablixRowHierarchy><TablixMembers><TablixMember>
                    <Visibility><Hidden>true</Hidden><ToggleItem>CategoryToggle</ToggleItem></Visibility>
                    <Group Name="Category"><DocumentMapLabel>=Fields!Category.Value</DocumentMapLabel></Group>
                  </TablixMember></TablixMembers></TablixRowHierarchy>
                </Tablix>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var table = El(result.Design, "navTable");
        var navigation = Assert.IsType<Dictionary<string, object>[]>(table.Style!["rdlTablixNavigation"]);

        Assert.Contains(navigation, item => item.TryGetValue("GroupName", out var name) && (string)name == "Category"
            && item.TryGetValue("DocumentMapLabel", out var label) && (string)label == "=Fields!Category.Value");
        Assert.Contains(navigation, item => item.TryGetValue("ToggleItem", out var toggle) && (string)toggle == "CategoryToggle");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL026" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 36 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ComprehensiveSyncfusionFixture_MapsCoreLayoutAndKnownPlaceholders()
    {
        var r = Convert(Fixture("ComprehensiveSyncfusionReport.rdl"));
        var d = r.Design;

        Assert.Equal("Comprehensive Syncfusion Sales Report", d.Name);
        Assert.Equal(595.28, d.PageSettings!.Width, 1);   // 21cm
        Assert.Equal(841.89, d.PageSettings!.Height, 1);  // 29.7cm
        Assert.Contains(d.SharedElements, e => e.Name == "brandLogo");
        Assert.Contains(d.SharedElements, e => e.Name == "reportTitle");
        Assert.Contains(d.SharedElements, e => e.Name == "pageNumber");

        var title = El(d, "reportTitle");
        Assert.Equal("text", title.Type);
        Assert.Equal("Arial", title.Style!["fontFamily"]);
        Assert.Equal("center", title.Style!["textAlign"]);
        Assert.Equal("underline", title.Style!["textDecoration"]);
        Assert.False(El(d, "confidentialNotice").Hidden);

        var customer = El(d, "customerName");
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
        var invoiceNo = El(d, "invoiceNo");
        Assert.Equal("richtext", invoiceNo.Type);
        Assert.Contains("<span style=\"font-weight:bold\">Invoice: </span>", invoiceNo.HtmlContent);
        Assert.Contains("{{InvoiceNo}}", invoiceNo.HtmlContent);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRDL016" && d.Severity == MigrationDiagnosticSeverity.Warning);

        var grandTotal = El(d, "grandTotal");
        Assert.Equal("=Sum(Fields!LineTotal.Value)", grandTotal.Expression);
        Assert.Equal("IIF([LineTotal] = 0, False, True)", grandTotal.VisibleExpression);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRDL015" && d.Severity == MigrationDiagnosticSeverity.Warning);

        var panel = El(d, "summaryPanel");
        Assert.Equal("rect", panel.Type);
        Assert.Equal("#F8FAFC", panel.Style!["backgroundColor"]);

        var line = El(d, "summaryRule");
        Assert.Equal("line", line.Type);
        Assert.Equal("dashed", line.Style!["dashStyle"]);

        var detailPhoto = El(d, "detailPhoto");
        Assert.Equal("image", detailPhoto.Type);
        Assert.StartsWith("data:image/png;base64,", detailPhoto.Content);
        var productPhoto = El(d, "productPhoto");
        Assert.Equal("image", productPhoto.Type);
        Assert.Equal("ProductImage", productPhoto.Binding);
        Assert.Equal("Database", productPhoto.Style!["rdlImageSource"]);

        var barcode = El(d, "shipmentBarcode");
        Assert.Equal("barcode", barcode.Type);
        Assert.Equal("code128", barcode.BarcodeType);
        Assert.Equal("{{Sku}}", barcode.BarcodeValue);

        var chart = El(d, "salesChart");
        var gauge = El(d, "deliveryGauge");
        var subreport = El(d, "detailSubreport");
        Assert.Equal("chart", chart.Type);
        Assert.Equal("bar", chart.ChartType);
        Assert.Equal("Chart", chart.Style!["rdlCustomItemType"]);
        Assert.NotNull(chart.ChartData);
        Assert.Equal("text", gauge.Type);
        Assert.Contains("Gauge", gauge.Content);
        Assert.Equal("Gauge", gauge.Style!["rdlCustomItemType"]);
        Assert.Equal("text", subreport.Type);
        Assert.Contains("Sub-report", subreport.Content);
        Assert.Equal("Subreport", subreport.Style!["rdlCustomItemType"]);
        Assert.True(subreport.Style.ContainsKey("rdlSubreport"));
        var subreportPagination = Assert.IsType<Dictionary<string, object>>(subreport.Style!["rdlPagination"]);
        Assert.Equal("End", subreportPagination["PageBreak.BreakLocation"]);
        Assert.True(Has(r.Diagnostics, "CANMIGRDL011"));
        Assert.True(Has(r.Diagnostics, "CANMIGRDL017"));
        Assert.True(Has(r.Diagnostics, "CANMIGRDL018"));
        Assert.True(Has(r.Diagnostics, "CANMIGRDL019"));
    }

    // 28 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ComprehensiveSyncfusionFixture_MapsTablixTableShape()
    {
        var result = Convert(Fixture("ComprehensiveSyncfusionReport.rdl"));
        var table = El(result.Design, "salesMatrix");

        Assert.Equal("table", table.Type);
        Assert.True(table.HeaderRow);
        Assert.Equal(3, table.CellData!.Length);
        Assert.Equal(new[] { "SKU", "Product", "Qty", "Total" }, table.CellData[0]);
        Assert.Equal(new[] { "{{Sku}}", "{{Product}}", "{{Quantity}}", "{{LineTotal}}" }, table.CellData[1]);
        Assert.Equal(new[] { "Total", "", "=Sum(Fields!Quantity.Value)", "=Sum(Fields!LineTotal.Value)" }, table.CellData[2]);
        Assert.Equal(new[] { 90.0, 244.8, 79.2, 115.2 }, table.ColumnWidths!.Select(w => Math.Round(w, 1)).ToArray());
        Assert.Equal(new[] { "left", "left", "right", "right" }, table.ColumnAlignments);
        Assert.True(table.Style!.ContainsKey("rdlTablixGroups"));
        var groups = Assert.IsType<Dictionary<string, object>[]>(table.Style["rdlTablixGroups"]);
        var group = Assert.Single(groups);
        Assert.Equal("RegionGroup", group["name"]);
        Assert.Equal(new[] { "=Fields!Region.Value" }, Assert.IsType<string[]>(group["expressions"]));
        Assert.Contains("=Fields!Product.Value", (string[])table.Style["rdlTablixSorts"]);
        Assert.Equal(new[] { "After", "Before" }, (string[])table.Style["rdlTablixKeepWithGroup"]);
        var pagination = Assert.IsType<Dictionary<string, object>>(table.Style["rdlPagination"]);
        Assert.Equal("Sales Matrix", pagination["PageName"]);
        Assert.Equal("true", pagination["KeepTogether"]);
        Assert.Equal(new[] { "true" }, Assert.IsType<string[]>(pagination["TablixMemberRepeatOnNewPage"]));
        Assert.Equal(new[] { "true" }, Assert.IsType<string[]>(pagination["TablixMemberFixedData"]));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL014" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL019" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 29 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_StaticVisibilityHidden_MapsToHidden()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="secret"><Top>0in</Top><Left>0in</Left><Height>0.3in</Height><Width>2in</Width>
                  <Visibility><Hidden>true</Hidden></Visibility>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Secret</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var secret = El(Convert(rdl).Design, "secret");

        Assert.True(secret.Hidden);
        Assert.Null(secret.VisibleExpression);
    }

    // 30 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_DynamicVisibilityHidden_MapsToInvertedVisibleExpression()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="conditional"><Top>0in</Top><Left>0in</Left><Height>0.3in</Height><Width>2in</Width>
                  <Visibility><Hidden>=Fields!Quantity.Value = 0</Hidden></Visibility>
                  <Paragraphs><Paragraph><TextRuns><TextRun><Value>Conditional</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var conditional = El(result.Design, "conditional");

        Assert.Null(conditional.Hidden);
        Assert.Equal("IIF([Quantity] = 0, False, True)", conditional.VisibleExpression);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL015" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 31 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_MultiRunTextbox_BecomesRichTextWithInlineStyles()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Textbox Name="mixed"><Top>0in</Top><Left>0in</Left><Height>0.4in</Height><Width>3in</Width>
                  <Paragraphs><Paragraph><Style><TextAlign>Center</TextAlign></Style><TextRuns>
                    <TextRun><Value>Total: </Value><Style><FontWeight>Bold</FontWeight></Style></TextRun>
                    <TextRun><Value>=Fields!Total.Value</Value><Style><Color>Green</Color><TextDecoration>Underline</TextDecoration></Style></TextRun>
                  </TextRuns></Paragraph></Paragraphs>
                </Textbox>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var mixed = El(result.Design, "mixed");

        Assert.Equal("richtext", mixed.Type);
        Assert.Equal("Total: {{Total}}", mixed.Content);
        Assert.Contains("""<p style="text-align:center">""", mixed.HtmlContent);
        Assert.Contains("""<span style="font-weight:bold">Total: </span>""", mixed.HtmlContent);
        Assert.Contains("""<span style="color:#008000;text-decoration:underline">{{Total}}</span>""", mixed.HtmlContent);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL016" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // 32 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_PageBreakAndRepeatMetadata_IsPreservedOnStyle()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition">
              <Body><ReportItems>
                <Rectangle Name="section"><Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>2in</Width>
                  <PageName>Section One</PageName>
                  <KeepTogether>true</KeepTogether>
                  <RepeatOnNewPage>true</RepeatOnNewPage>
                  <PageBreak><BreakLocation>StartAndEnd</BreakLocation><ResetPageNumber>true</ResetPageNumber></PageBreak>
                </Rectangle>
              </ReportItems><Height>5in</Height></Body>
            </Report>
            """;

        var result = Convert(rdl);
        var section = El(result.Design, "section");
        var pagination = Assert.IsType<Dictionary<string, object>>(section.Style!["rdlPagination"]);

        Assert.Equal("Section One", pagination["PageName"]);
        Assert.Equal("true", pagination["KeepTogether"]);
        Assert.Equal("true", pagination["RepeatOnNewPage"]);
        Assert.Equal("StartAndEnd", pagination["PageBreak.BreakLocation"]);
        Assert.Equal("true", pagination["PageBreak.ResetPageNumber"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGRDL019" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    // ── ActiveReports .rdlx (RDL-2005) fidelity ────────────────────────────────────────────────────

    [Fact]
    public void Convert_Table2005Footer_RowsAreIncludedInGrid()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition" Name="T">
              <Body><ReportItems>
                <Table Name="t">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <TableColumns><TableColumn><Width>2in</Width></TableColumn><TableColumn><Width>2in</Width></TableColumn></TableColumns>
                  <Header><TableRows><TableRow><TableCells>
                    <TableCell><ReportItems><Textbox Name="h1"><Value>Item</Value></Textbox></ReportItems></TableCell>
                    <TableCell><ReportItems><Textbox Name="h2"><Value>Amount</Value></Textbox></ReportItems></TableCell>
                  </TableCells></TableRow></TableRows></Header>
                  <Details><TableRows><TableRow><TableCells>
                    <TableCell><ReportItems><Textbox Name="d1"><Value>Widget</Value></Textbox></ReportItems></TableCell>
                    <TableCell><ReportItems><Textbox Name="d2"><Value>10</Value></Textbox></ReportItems></TableCell>
                  </TableCells></TableRow></TableRows></Details>
                  <Footer><TableRows><TableRow><TableCells>
                    <TableCell><ReportItems><Textbox Name="f1"><Value>Total</Value></Textbox></ReportItems></TableCell>
                    <TableCell><ReportItems><Textbox Name="f2"><Value>10</Value></Textbox></ReportItems></TableCell>
                  </TableCells></TableRow></TableRows></Footer>
                </Table>
              </ReportItems><Height>5in</Height></Body>
              <PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth>
            </Report>
            """;
        var result = Convert(rdl);
        var t = El(result.Design, "t");
        Assert.Equal("table", t.Type);
        Assert.Equal(3, t.CellData!.Length);                       // header + detail + footer
        Assert.Equal(new[] { "Total", "10" }, t.CellData![2]);     // footer row preserved
        Assert.True(Has(result.Diagnostics, "CANMIGRDL030"));
    }

    [Fact]
    public void Convert_ListRegion_ParsesNestedTableAndCarriesRepeatMetadata()
    {
        var rdl = """
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2005/01/reportdefinition" Name="T">
              <Body><ReportItems>
                <List Name="list1">
                  <Top>0in</Top><Left>0in</Left><Height>1in</Height><Width>4in</Width>
                  <DataSetName>Results</DataSetName>
                  <Grouping Name="list1_Group">
                    <GroupExpressions><GroupExpression>=Fields!TestGroup.Value</GroupExpression></GroupExpressions>
                  </Grouping>
                  <ReportItems>
                    <Table Name="nested">
                      <Top>0in</Top><Left>0in</Left><Height>0.5in</Height><Width>4in</Width>
                      <TableColumns><TableColumn><Width>4in</Width></TableColumn></TableColumns>
                      <Details><TableRows><TableRow><TableCells>
                        <TableCell><ReportItems><Textbox Name="c1"><Value>=Fields!Name.Value</Value></Textbox></ReportItems></TableCell>
                      </TableCells></TableRow></TableRows></Details>
                    </Table>
                  </ReportItems>
                </List>
              </ReportItems><Height>5in</Height></Body>
              <PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth>
            </Report>
            """;
        var result = Convert(rdl);

        // Nested table inside the List is parsed, not dropped.
        var nested = El(result.Design, "nested");
        Assert.Equal("table", nested.Type);
        Assert.Equal(new[] { "{{Name}}" }, nested.CellData![0]);

        // The List becomes a container carrying repeat/grouping metadata.
        var list = El(result.Design, "list1");
        Assert.Equal("rect", list.Type);
        Assert.Equal("Results", list.Repeat!.DataPath);
        var repeat = Assert.IsType<Dictionary<string, object>>(list.Style!["rdlList"]);
        Assert.Equal("rdlList", repeat["source"]);
        Assert.Equal(new[] { "=Fields!TestGroup.Value" }, Assert.IsType<string[]>(repeat["groupExpressions"]));
        Assert.True(Has(result.Diagnostics, "CANMIGRDL031"));
    }

    [Fact]
    public void Convert_ActiveReportsRdlxSamples_AllConvertWithoutDroppingRegions()
    {
        var dir = FindActiveReportsSamplesDir();
        var files = Directory.GetFiles(dir, "*.rdlx");
        Assert.Equal(8, files.Length);

        foreach (var file in files)
        {
            var xml = File.ReadAllText(file);
            Assert.True(RdlToDesignConverter.LooksLikeRdl(xml), $"{Path.GetFileName(file)} should be detected as RDL");

            var result = Convert(xml);
            var elements = result.Design.Pages[0].Elements.Concat(result.Design.SharedElements).ToList();
            Assert.NotEmpty(elements);
            // No top-level region should have been mapped to the unsupported-control placeholder.
            Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGRDL011" && d.Message.Contains("List"));
        }
    }

    private static string FindActiveReportsSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "designer-simples", "ActiveReports", "ReportSamples-master");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate designer-simples/ActiveReports/ReportSamples-master from the test output directory.");
    }
}
