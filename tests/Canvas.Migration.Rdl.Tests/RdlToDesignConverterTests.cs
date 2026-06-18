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
        var sub = El(r.Design, "sub");                 // kept as a labeled placeholder, not dropped
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
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
        Assert.Equal("text", gauge.Type);
        Assert.Contains("{{Score}}", gauge.Content);
        Assert.Equal(0.5 * 72 + 2 * 72 + 0.2 * 72, gauge.X, 1);
        Assert.Equal(1 * 72 + 0.5 * 72 + 0.1 * 72, gauge.Y, 1);
        Assert.Equal("metrics", gauge.Style!["rdlParentTablix"]);
        Assert.Equal(1, gauge.Style["rdlParentTablixRow"]);
        Assert.Equal(1, gauge.Style["rdlParentTablixColumn"]);
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
}
