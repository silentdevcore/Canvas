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
