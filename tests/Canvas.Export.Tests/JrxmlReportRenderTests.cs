using System.Text;
using Canvas.Migration.JasperReports;
using PXA.WebApi.Infrastructure;

namespace Canvas.Export.Tests;

/// <summary>
/// End-to-end: a converted JasperReports .jrxml design must render to a valid PDF through the same
/// pipeline the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class JrxmlReportRenderTests
{
    private const string Jrxml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Invoice"
            pageWidth="595" pageHeight="842" columnWidth="555" leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
          <title>
            <band height="40">
              <staticText>
                <reportElement key="title" x="0" y="0" width="555" height="30" forecolor="#0066CC"/>
                <textElement textAlignment="Center"><font fontName="Arial" size="20" isBold="true"/></textElement>
                <text><![CDATA[INVOICE]]></text>
              </staticText>
            </band>
          </title>
          <detail>
            <band height="40">
              <textField>
                <reportElement key="customer" x="0" y="0" width="200" height="20"/>
                <textElement/>
                <textFieldExpression><![CDATA[$F{customerName}]]></textFieldExpression>
              </textField>
              <line>
                <reportElement key="rule" x="0" y="25" width="555" height="1" forecolor="#808080"/>
                <graphicElement><pen lineWidth="2"/></graphicElement>
              </line>
            </band>
          </detail>
          <pageFooter>
            <band height="20">
              <staticText><reportElement key="pageinfo" x="500" y="0" width="55" height="20"/><text><![CDATA[Page 1]]></text></staticText>
            </band>
          </pageFooter>
        </jasperReport>
        """;

    [Fact]
    public void ConvertedJrxmlReport_RendersToValidPdf()
    {
        var design = new JrxmlToDesignConverter().Convert(Jrxml).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
