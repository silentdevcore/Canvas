using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Telerik;

namespace Canvas.Migration.Telerik.Tests;

public sealed class TrdxToDesignConverterTests
{
    // Mirrors a real Telerik .trdx: telerik namespace, <Items> of *Section bands each with <Items>,
    // Unit-string geometry ("3in"), Value + StyleName, and a <StyleSheet> resolving the named "Header".
    private const string SampleTrdx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report Width="8.1in" Name="Invoice" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
          <PageSettings>
            <PaperKind>Letter</PaperKind>
            <Margins Left="1in" Right="1in" Top="1in" Bottom="1in"/>
          </PageSettings>
          <Items>
            <PageHeaderSection Height="0.5in" Name="pageHeaderSection1">
              <Items>
                <TextBox Width="3.5in" Height="0.3in" Left="0in" Top="0.1in" Value="INVOICE" Name="title" StyleName="Header">
                  <Style TextAlign="Center" Color="0, 102, 204"/>
                </TextBox>
              </Items>
            </PageHeaderSection>
            <DetailSection Height="1in" Name="detailSection1">
              <Items>
                <TextBox Width="3in" Height="0.3in" Left="0in" Top="0in" Value="=Fields.CustomerName" Name="customer"/>
                <Shape Width="0.5in" Height="0.5in" Left="4in" Top="0in" ShapeType="Ellipse" Name="box">
                  <Style BackgroundColor="LightGray" BorderColor="Black"/>
                </Shape>
                <Barcode Width="2in" Height="0.5in" Left="0in" Top="0.4in" Value="12345" Type="Code128" Name="bc"/>
                <SubReport Width="3in" Height="0.3in" Left="0in" Top="0.8in" Name="sub"/>
              </Items>
            </DetailSection>
            <PageFooterSection Height="0.4in" Name="pageFooterSection1">
              <Items>
                <TextBox Width="1in" Height="0.2in" Left="6in" Top="0in" Value="Page 1" Name="pageinfo"/>
              </Items>
            </PageFooterSection>
          </Items>
          <StyleSheet>
            <StyleRule>
              <Style><Font Name="Segoe UI" Size="25pt" Bold="True"/></Style>
              <Selectors><StyleSelector Type="ReportItemBase" StyleName="Header"/></Selectors>
            </StyleRule>
          </StyleSheet>
        </Report>
        """;

    private static TrdxConvertResult Convert(string trdx) => new TrdxToDesignConverter().Convert(trdx);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    [Fact]
    public void Convert_ParsesSectionsAndPaperKind()
    {
        var r = Convert(SampleTrdx);
        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(612, r.Design.PageSettings!.Width, 0.5);   // Letter
        Assert.Equal(792, r.Design.PageSettings!.Height, 0.5);
        Assert.Equal(4, r.Design.Pages[0].Elements.Count);       // customer, box, bc, sub
        Assert.Equal(2, r.Design.SharedElements.Count);          // title (header) + pageinfo (footer)
        Assert.True(Has(r.Diagnostics, "CANMIGTRDX001"));
    }

    [Fact]
    public void Convert_UnitStrings_FlattenSections()
    {
        var d = Convert(SampleTrdx).Design;
        var customer = El(d, "customer");   // Detail band top = marginTop(72) + PageHeader height(0.5in=36)
        Assert.Equal(72, customer.X, 0.5);  // left margin 1in
        Assert.Equal(108, customer.Y, 0.5);
        Assert.Equal(216, customer.Width, 0.5);  // 3in
        Assert.Equal(79.2, El(d, "title").Y, 0.5);  // page header: marginTop + 0.1in
    }

    [Fact]
    public void Convert_ResolvesNamedStyleAndInlineOverride()
    {
        var title = El(Convert(SampleTrdx).Design, "title");
        Assert.Equal("text", title.Type);
        Assert.Equal("Segoe UI", title.Style!["fontFamily"]);    // from StyleSheet "Header"
        Assert.Equal(25.0, title.Style!["fontSize"]);            // from StyleSheet
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("center", title.Style!["textAlign"]);       // inline override
        Assert.Equal("#0066CC", title.Style!["color"]);          // inline Color "0, 102, 204"
        Assert.Equal("INVOICE", title.Content);
    }

    [Fact]
    public void Convert_FieldsValue_BecomesBinding()
    {
        var r = Convert(SampleTrdx);
        var customer = El(r.Design, "customer");                 // Value="=Fields.CustomerName"
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGTRDX010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Convert_Shape_EllipseBecomesCircle()
    {
        var box = El(Convert(SampleTrdx).Design, "box");
        Assert.Equal("circle", box.Type);
        Assert.Equal("#D3D3D3", box.Style!["backgroundColor"]);   // LightGray
        Assert.Equal("#000000", box.Style!["borderColor"]);
    }

    [Fact]
    public void Convert_Barcode_MapsValueAndType()
    {
        var bc = El(Convert(SampleTrdx).Design, "bc");
        Assert.Equal("barcode", bc.Type);
        Assert.Equal("code128", bc.BarcodeType);
        Assert.Equal("12345", bc.BarcodeValue);
    }

    [Fact]
    public void Convert_SubReport_BecomesPlaceholder()
    {
        var r = Convert(SampleTrdx);
        var sub = El(r.Design, "sub");
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
        Assert.True(Has(r.Diagnostics, "CANMIGTRDX011"));
    }

    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeShared()
    {
        var d = Convert(SampleTrdx).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "title");
        Assert.Contains(d.SharedElements, e => e.Name == "pageinfo");
        Assert.True(El(d, "pageinfo").Y > 650, "footer anchored near page bottom");
    }

    [Fact]
    public void Convert_Panel_FlattensChildrenToAbsolute()
    {
        var trdx = """
            <Report Name="P" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
              <Items>
                <DetailSection Height="2in" Name="d">
                  <Items>
                    <Panel Width="3in" Height="2in" Left="1in" Top="0.5in" Name="panel">
                      <Items>
                        <TextBox Width="1in" Height="0.2in" Left="0.2in" Top="0.1in" Value="Inner" Name="inner"/>
                      </Items>
                    </Panel>
                  </Items>
                </DetailSection>
              </Items>
            </Report>
            """;
        var d = Convert(trdx).Design;
        Assert.Equal("rect", El(d, "panel").Type);
        // inner: marginLeft(0) + panelLeft(1in=72) + innerLeft(0.2in=14.4) = 86.4 ; sectionTop(0) + panelTop(36) + innerTop(7.2) = 43.2
        Assert.Equal(86.4, El(d, "inner").X, 0.5);
        Assert.Equal(43.2, El(d, "inner").Y, 0.5);
    }

    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<Report><not closed"));

    [Fact]
    public void LooksLikeTrdx_DetectsTrdxVsOthers()
    {
        Assert.True(TrdxToDesignConverter.LooksLikeTrdx(SampleTrdx));
        Assert.False(TrdxToDesignConverter.LooksLikeTrdx("""<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"><Body /></Report>"""));
        Assert.False(TrdxToDesignConverter.LooksLikeTrdx("""<Report><Sections /></Report>"""));
        Assert.False(TrdxToDesignConverter.LooksLikeTrdx("""<Report><ReportPage /></Report>"""));
        Assert.False(TrdxToDesignConverter.LooksLikeTrdx("public class Foo {}"));
    }

    [Fact]
    public void Convert_Table_AnchoredCells_BecomeCanvasTableGrid()
    {
        var trdx = """
            <Report Width="8.1in" Name="T" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
              <Items>
                <DetailSection Height="1in" Name="d1">
                  <Items>
                    <Table Name="table1" Left="0in" Top="0in" Width="4in" Height="0.6in">
                      <Body>
                        <TableBodyColumns>
                          <TableBodyColumn Width="2in"/>
                          <TableBodyColumn Width="2in"/>
                        </TableBodyColumns>
                        <TableBodyRows>
                          <TableBodyRow Height="0.3in"/>
                          <TableBodyRow Height="0.3in"/>
                        </TableBodyRows>
                      </Body>
                      <Items>
                        <TextBox Name="h1" Value="Name" Table.CellRowIndex="0" Table.CellColumnIndex="0"/>
                        <TextBox Name="h2" Value="Price" Table.CellRowIndex="0" Table.CellColumnIndex="1"/>
                        <TextBox Name="d1c" Value="=Fields.CustomerName" Table.CellRowIndex="1" Table.CellColumnIndex="0"/>
                        <TextBox Name="d2c" Value="=Fields.Total" Table.CellRowIndex="1" Table.CellColumnIndex="1"/>
                      </Items>
                    </Table>
                  </Items>
                </DetailSection>
              </Items>
            </Report>
            """;
        var r = Convert(trdx);
        var t = El(r.Design, "table1");

        Assert.Equal("table", t.Type);
        Assert.True(t.HeaderRow);
        Assert.Equal(new[] { "Name", "Price" }, t.CellData![0]);
        Assert.Equal(new[] { "{{CustomerName}}", "{{Total}}" }, t.CellData![1]);   // =Fields.X → binding token
        Assert.Equal(new[] { 144.0, 144.0 }, t.ColumnWidths);                       // 2in → 144pt
        Assert.True(Has(r.Diagnostics, "CANMIGTRDX013"));
    }

    [Fact]
    public void Convert_Table_WithoutAnchors_FallsBackToSequentialFill()
    {
        var trdx = """
            <Report Width="8.1in" Name="T" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
              <Items>
                <DetailSection Height="1in" Name="d1">
                  <Items>
                    <Table Name="table1" Left="0in" Top="0in" Width="4in" Height="0.6in">
                      <Body>
                        <TableBodyColumns>
                          <TableBodyColumn Width="2in"/>
                          <TableBodyColumn Width="2in"/>
                        </TableBodyColumns>
                      </Body>
                      <Items>
                        <TextBox Name="a" Value="A"/>
                        <TextBox Name="b" Value="B"/>
                        <TextBox Name="c" Value="C"/>
                        <TextBox Name="d" Value="D"/>
                      </Items>
                    </Table>
                  </Items>
                </DetailSection>
              </Items>
            </Report>
            """;
        var t = El(Convert(trdx).Design, "table1");
        Assert.Equal("table", t.Type);
        Assert.Equal(new[] { "A", "B" }, t.CellData![0]);   // 2 columns → sequential fill row-major
        Assert.Equal(new[] { "C", "D" }, t.CellData![1]);
    }

    [Fact]
    public void Convert_Table_CellStyles_FromItemStyle()
    {
        var trdx = """
            <Report Width="8.1in" Name="T" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
              <Items>
                <DetailSection Height="1in" Name="d1">
                  <Items>
                    <Table Name="table1" Left="0in" Top="0in" Width="4in" Height="0.6in">
                      <Body><TableBodyColumns><TableBodyColumn Width="2in"/></TableBodyColumns></Body>
                      <Items>
                        <TextBox Name="h1" Value="Name" Table.CellRowIndex="0" Table.CellColumnIndex="0">
                          <Style TextAlign="Center" BackgroundColor="#FFFF00" Color="#0000FF">
                            <Font Name="Verdana" Size="12pt" Bold="true"/>
                          </Style>
                        </TextBox>
                      </Items>
                    </Table>
                  </Items>
                </DetailSection>
              </Items>
            </Report>
            """;
        var t = El(Convert(trdx).Design, "table1");
        var cs = Assert.Single(t.CellStyles!);
        Assert.Equal("#FFFF00", cs.BackgroundColor);
        Assert.Equal("#0000FF", cs.Color);
        Assert.Equal("center", cs.TextAlign);
        Assert.Equal("Verdana", cs.FontFamily);
        Assert.Equal(12, cs.FontSize);
        Assert.True(cs.Bold);
    }
}
