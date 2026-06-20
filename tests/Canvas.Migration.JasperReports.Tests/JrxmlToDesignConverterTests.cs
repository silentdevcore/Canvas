using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.JasperReports;

namespace Canvas.Migration.JasperReports.Tests;

public sealed class JrxmlToDesignConverterTests
{
    // Mirrors a real Jaspersoft .jrxml: jasperReport root in the JasperReports namespace, band sections
    // (title/pageHeader/detail/pageFooter) each wrapping <band>, <reportElement> geometry in points,
    // a named <style>, $F{} bindings, and graphicElement pens. reportElement key= gives stable names.
    private const string SampleJrxml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Invoice"
            pageWidth="595" pageHeight="842" columnWidth="555" leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
          <style name="Header" forecolor="#0066CC"><font fontName="Arial" size="20" isBold="true"/></style>
          <field name="customerName" class="java.lang.String"/>
          <title>
            <band height="40">
              <staticText>
                <reportElement key="title" x="0" y="0" width="555" height="30" style="Header"/>
                <textElement textAlignment="Center"/>
                <text><![CDATA[INVOICE]]></text>
              </staticText>
            </band>
          </title>
          <pageHeader>
            <band height="20">
              <staticText><reportElement key="hdr" x="0" y="0" width="100" height="20"/><text><![CDATA[Header]]></text></staticText>
            </band>
          </pageHeader>
          <detail>
            <band height="40">
              <textField>
                <reportElement key="customer" x="0" y="0" width="200" height="20"/>
                <textElement/>
                <textFieldExpression><![CDATA[$F{customerName}]]></textFieldExpression>
              </textField>
              <line>
                <reportElement key="rule" x="0" y="22" width="555" height="1" forecolor="#808080"/>
                <graphicElement><pen lineWidth="2"/></graphicElement>
              </line>
              <rectangle>
                <reportElement key="box" x="400" y="0" width="40" height="40" forecolor="#000000" backcolor="#D3D3D3" mode="Opaque"/>
              </rectangle>
              <subreport><reportElement key="sub" x="0" y="30" width="200" height="10"/></subreport>
            </band>
          </detail>
          <pageFooter>
            <band height="20">
              <staticText><reportElement key="pageinfo" x="500" y="0" width="55" height="20"/><text><![CDATA[Page 1]]></text></staticText>
            </band>
          </pageFooter>
        </jasperReport>
        """;

    private static JrxmlConvertResult Convert(string jrxml) => new JrxmlToDesignConverter().Convert(jrxml);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    [Fact]
    public void Convert_ParsesBandsAndPageInPoints()
    {
        var r = Convert(SampleJrxml);
        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(595, r.Design.PageSettings!.Width, 0.5);    // points, no scaling
        Assert.Equal(842, r.Design.PageSettings!.Height, 0.5);
        Assert.Equal(5, r.Design.Pages[0].Elements.Count);        // title, customer, rule, box, sub
        Assert.Equal(2, r.Design.SharedElements.Count);           // hdr + pageinfo
        Assert.True(Has(r.Diagnostics, "CANMIGJRXML001"));
    }

    [Fact]
    public void Convert_FlattensBands_MarginPlusAccumulatedHeights()
    {
        var d = Convert(SampleJrxml).Design;
        Assert.Equal(20, El(d, "title").X, 0.5);    // left margin
        Assert.Equal(20, El(d, "title").Y, 0.5);    // marginTop + title band top(0)
        // detail band top = marginTop(20) + title(40) + pageHeader(20) = 80
        Assert.Equal(80, El(d, "customer").Y, 0.5);
    }

    [Fact]
    public void Convert_NamedStyle_ResolvesFontAndColor()
    {
        var title = El(Convert(SampleJrxml).Design, "title");
        Assert.Equal("text", title.Type);
        Assert.Equal("Arial", title.Style!["fontFamily"]);    // from <style name="Header">
        Assert.Equal(20.0, title.Style!["fontSize"]);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("#0066CC", title.Style!["color"]);
        Assert.Equal("center", title.Style!["textAlign"]);    // textElement override
        Assert.Equal("INVOICE", title.Content);
    }

    [Fact]
    public void Convert_BoxPens_MapToPerSideBorderStyle()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Borders"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <style name="Boxed">
                <box><pen lineWidth="1" lineColor="#111111" lineStyle="Solid"/></box>
              </style>
              <detail>
                <band height="40">
                  <staticText>
                    <reportElement key="boxedText" x="0" y="0" width="160" height="30" style="Boxed"/>
                    <box>
                      <topPen lineWidth="2" lineColor="#FF0000" lineStyle="Dashed"/>
                      <rightPen lineWidth="0.5" lineColor="#0000FF" lineStyle="Dotted"/>
                    </box>
                    <text><![CDATA[Boxed]]></text>
                  </staticText>
                </band>
              </detail>
            </jasperReport>
            """;

        var boxed = El(Convert(jrxml).Design, "boxedText");

        Assert.Equal("text", boxed.Type);
        Assert.Equal(1d, boxed.Style!["borderWidth"]);
        Assert.Equal("#111111", boxed.Style["borderColor"]);
        Assert.Equal("solid", boxed.Style["borderStyle"]);
        Assert.Equal(2d, boxed.Style["borderTopWidth"]);
        Assert.Equal("#FF0000", boxed.Style["borderTopColor"]);
        Assert.Equal("dashed", boxed.Style["borderTopStyle"]);
        Assert.Equal(0.5d, boxed.Style["borderRightWidth"]);
        Assert.Equal("#0000FF", boxed.Style["borderRightColor"]);
        Assert.Equal("dotted", boxed.Style["borderRightStyle"]);
    }

    [Fact]
    public void Convert_TextFieldExpression_BecomesBinding()
    {
        var r = Convert(SampleJrxml);
        var customer = El(r.Design, "customer");              // $F{customerName}
        Assert.Equal("customerName", customer.Binding);
        Assert.Equal("{{customerName}}", customer.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGJRXML010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Convert_Line_FromGraphicElementPen()
    {
        var rule = El(Convert(SampleJrxml).Design, "rule");
        Assert.Equal("line", rule.Type);
        Assert.Equal("#808080", rule.Style!["color"]);
        Assert.Equal(2.0, rule.Style!["strokeWidth"]);
    }

    [Fact]
    public void Convert_Rectangle_BorderAndFill()
    {
        var box = El(Convert(SampleJrxml).Design, "box");
        Assert.Equal("rect", box.Type);
        Assert.Equal("#000000", box.Style!["borderColor"]);
        Assert.Equal("#D3D3D3", box.Style!["backgroundColor"]);
    }

    [Fact]
    public void Convert_Subreport_BecomesPlaceholder()
    {
        var r = Convert(SampleJrxml);
        var sub = El(r.Design, "sub");
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
        Assert.True(Has(r.Diagnostics, "CANMIGJRXML011"));
    }

    [Fact]
    public void Convert_ComponentElementBarcode_BecomesCanvasBarcode()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                xmlns:jr="http://jasperreports.sourceforge.net/jasperreports/components"
                name="BarcodeReport" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="50">
                  <componentElement>
                    <reportElement key="skuBarcode" x="0" y="0" width="200" height="40"/>
                    <jr:barbecue type="Code128" drawText="true">
                      <jr:codeExpression><![CDATA[$F{sku}]]></jr:codeExpression>
                    </jr:barbecue>
                  </componentElement>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var barcode = El(result.Design, "skuBarcode");

        Assert.Equal("barcode", barcode.Type);
        Assert.Equal("code128", barcode.BarcodeType);
        Assert.Equal("{{sku}}", barcode.BarcodeValue);
        Assert.Equal("Code128", barcode.Style!["jrxmlComponentType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(barcode.Style["jrxmlComponent"]);
        Assert.Equal("barbecue", metadata["Component"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML013" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Convert_ComponentElementChart_PreservesMetadataOnPlaceholder()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                xmlns:jr="http://jasperreports.sourceforge.net/jasperreports/components"
                name="ChartReport" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="120">
                  <componentElement>
                    <reportElement key="salesChart" x="0" y="0" width="240" height="100"/>
                    <jr:barChart>
                      <jr:datasetRun subDataset="SalesData"/>
                      <jr:titleExpression><![CDATA["Sales by Region"]]></jr:titleExpression>
                      <jr:categoryExpression><![CDATA[$F{region}]]></jr:categoryExpression>
                      <jr:valueExpression><![CDATA[$F{total}]]></jr:valueExpression>
                    </jr:barChart>
                  </componentElement>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var chart = El(result.Design, "salesChart");

        Assert.Equal("text", chart.Type);
        Assert.Contains("Chart", chart.Content);
        Assert.Equal("barChart", chart.Style!["jrxmlComponentType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(chart.Style["jrxmlComponent"]);
        Assert.Equal("barChart", metadata["Component"]);
        Assert.Equal("SalesData", metadata["DatasetName"]);
        Assert.Equal("\"Sales by Region\"", metadata["Caption"]);
        var expressions = Assert.IsType<Dictionary<string, object>[]>(metadata["Expressions"]);
        Assert.Contains(expressions, e => (string)e["Name"] == "categoryExpression" && (string)e["Value"] == "$F{region}");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML011" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_ComponentElementTable_BecomesCanvasTableWithMetadata()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                xmlns:jr="http://jasperreports.sourceforge.net/jasperreports/components"
                name="TableReport" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="100">
                  <componentElement>
                    <reportElement key="productsTable" x="0" y="0" width="250" height="60"/>
                    <jr:table>
                      <datasetRun subDataset="Products">
                        <datasetParameter name="CUSTOMER_ID">
                          <datasetParameterExpression><![CDATA[$P{CUSTOMER_ID}]]></datasetParameterExpression>
                        </datasetParameter>
                        <connectionExpression><![CDATA[$P{REPORT_CONNECTION}]]></connectionExpression>
                      </datasetRun>
                      <jr:column width="100">
                        <jr:columnHeader height="20">
                          <staticText><reportElement x="0" y="0" width="100" height="20"/><text><![CDATA[Product]]></text></staticText>
                        </jr:columnHeader>
                        <jr:detailCell height="20">
                          <textField><reportElement x="0" y="0" width="100" height="20"/><textFieldExpression><![CDATA[$F{product_name}]]></textFieldExpression></textField>
                        </jr:detailCell>
                      </jr:column>
                      <jr:column width="80">
                        <jr:columnHeader height="20">
                          <staticText><reportElement x="0" y="0" width="80" height="20"/><text><![CDATA[Price]]></text></staticText>
                        </jr:columnHeader>
                        <jr:detailCell height="20">
                          <textField><reportElement x="0" y="0" width="80" height="20"/><textFieldExpression><![CDATA[TEXT($F{price}, "0.00")]]></textFieldExpression></textField>
                        </jr:detailCell>
                      </jr:column>
                    </jr:table>
                  </componentElement>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var table = El(result.Design, "productsTable");

        Assert.Equal("table", table.Type);
        Assert.True(table.HeaderRow);
        Assert.Equal(new[] { 100d, 80d }, table.ColumnWidths);
        Assert.Equal("Product", table.CellData![0][0]);
        Assert.Equal("Price", table.CellData[0][1]);
        Assert.Equal("{{product_name}}", table.CellData[1][0]);
        Assert.Equal("""TEXT($F{price}, "0.00")""", table.CellData[1][1]);
        var metadata = Assert.IsType<Dictionary<string, object>>(table.Style!["jrxmlTable"]);
        Assert.Equal("Products", metadata["DatasetName"]);
        Assert.Equal(2, metadata["ColumnCount"]);
        var parameters = Assert.IsType<Dictionary<string, object>[]>(metadata["Parameters"]);
        Assert.Equal("CUSTOMER_ID", parameters[0]["Name"]);
        Assert.Equal("$P{CUSTOMER_ID}", parameters[0]["Expression"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML014" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_Crosstab_PreservesMetadataOnPlaceholder()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                name="CrosstabReport" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <summary>
                <band height="160">
                  <crosstab>
                    <reportElement key="salesCrosstab" x="0" y="0" width="300" height="140"/>
                    <rowGroup name="RegionGroup" width="80"/>
                    <columnGroup name="MonthGroup" height="20"/>
                    <measure name="TotalSales" class="java.math.BigDecimal">
                      <measureExpression><![CDATA[$F{total}]]></measureExpression>
                    </measure>
                  </crosstab>
                </band>
              </summary>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var crosstab = El(result.Design, "salesCrosstab");

        Assert.Equal("text", crosstab.Type);
        Assert.Contains("Crosstab", crosstab.Content);
        Assert.Equal("crosstab", crosstab.Style!["jrxmlComponentType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(crosstab.Style["jrxmlComponent"]);
        Assert.Equal(1, metadata["RowGroupCount"]);
        Assert.Equal(1, metadata["ColumnGroupCount"]);
        Assert.Equal(1, metadata["MeasureCount"]);
        var expressions = Assert.IsType<Dictionary<string, object>[]>(metadata["Expressions"]);
        Assert.Contains(expressions, e => (string)e["Name"] == "measureExpression" && (string)e["Value"] == "$F{total}");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML011" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeShared()
    {
        var d = Convert(SampleJrxml).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "hdr");
        Assert.Contains(d.SharedElements, e => e.Name == "pageinfo");
        Assert.Equal(20, El(d, "hdr").Y, 0.5);                // marginTop + 0
        Assert.True(El(d, "pageinfo").Y > 780, "footer anchored near page bottom");
    }

    [Fact]
    public void Convert_Landscape_SwapsPageDimensions()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="L"
                pageWidth="595" pageHeight="842" orientation="Landscape" leftMargin="10">
              <detail><band height="20"><staticText><reportElement key="t" x="0" y="0" width="50" height="20"/><text><![CDATA[Hi]]></text></staticText></band></detail>
            </jasperReport>
            """;
        var d = Convert(jrxml).Design;
        Assert.Equal(842, d.PageSettings!.Width, 0.5);
        Assert.Equal(595, d.PageSettings!.Height, 0.5);
        Assert.Equal(10, El(d, "t").X, 0.5);
    }

    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<jasperReport><not closed"));

    [Fact]
    public void LooksLikeJrxml_DetectsJrxmlVsOthers()
    {
        Assert.True(JrxmlToDesignConverter.LooksLikeJrxml(SampleJrxml));
        Assert.False(JrxmlToDesignConverter.LooksLikeJrxml("""<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"><Body /></Report>"""));
        Assert.False(JrxmlToDesignConverter.LooksLikeJrxml("""<Report><ReportPage /></Report>"""));
        Assert.False(JrxmlToDesignConverter.LooksLikeJrxml("public class Foo {}"));
    }
}
