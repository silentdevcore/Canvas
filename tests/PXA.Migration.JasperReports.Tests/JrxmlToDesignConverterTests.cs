using System.Text.Json;
using PXA.Core.Contracts;
using PXA.Migration.Abstractions;
using PXA.Migration.JasperReports;

namespace PXA.Migration.JasperReports.Tests;

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
    public void GroupResetSumVariable_TranslatesToGroupScopedAggregate()
    {
        var jrxml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Agg"
                pageWidth="595" pageHeight="842" leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
              <field name="amount" class="java.math.BigDecimal"/>
              <field name="country" class="java.lang.String"/>
              <variable name="totalAmount" class="java.math.BigDecimal" resetType="Group" resetGroup="byCountry" calculation="Sum">
                <variableExpression><![CDATA[$F{amount}]]></variableExpression>
              </variable>
              <group name="byCountry">
                <groupExpression><![CDATA[$F{country}]]></groupExpression>
                <groupFooter>
                  <band height="20">
                    <textField>
                      <reportElement key="grpTotal" x="0" y="0" width="200" height="20"/>
                      <textFieldExpression><![CDATA[$V{totalAmount}]]></textFieldExpression>
                    </textField>
                  </band>
                </groupFooter>
              </group>
              <detail>
                <band height="20">
                  <textField>
                    <reportElement key="amt" x="0" y="0" width="200" height="20"/>
                    <textFieldExpression><![CDATA[$F{amount}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        Assert.Equal("$sum($group, \"amount\")", El(Convert(jrxml).Design, "grpTotal").Expression);
    }

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
    public void Convert_GroupHeaderAndFooter_MapToRepeatMetadata()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Groups"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <group name="Store Group" isReprintHeaderOnEachPage="true">
                <groupExpression><![CDATA[$F{store_id}]]></groupExpression>
                <groupHeader>
                  <band height="20">
                    <textField>
                      <reportElement key="storeHeader" x="0" y="0" width="160" height="20"/>
                      <textFieldExpression><![CDATA[$F{store_name}]]></textFieldExpression>
                    </textField>
                  </band>
                </groupHeader>
                <groupFooter>
                  <band height="18">
                    <textField>
                      <reportElement key="storeFooter" x="0" y="0" width="160" height="18"/>
                      <textFieldExpression><![CDATA[$V{STORE_TOTAL}]]></textFieldExpression>
                    </textField>
                  </band>
                </groupFooter>
              </group>
              <detail>
                <band height="20">
                  <staticText>
                    <reportElement key="detailText" x="0" y="0" width="100" height="20"/>
                    <text><![CDATA[Detail]]></text>
                  </staticText>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var header = El(result.Design, "storeHeader");
        var detail = El(result.Design, "detailText");
        var footer = El(result.Design, "storeFooter");

        Assert.Equal(20, header.Y, 0.5);
        Assert.Equal(40, detail.Y, 0.5);
        Assert.Equal(60, footer.Y, 0.5);
        Assert.Equal("Store_Group", header.Repeat!.DataPath);
        Assert.Equal(header.Id, header.Repeat.TemplateId);
        Assert.Equal("Store_Group", footer.Repeat!.DataPath);

        var group = Assert.IsType<Dictionary<string, object>>(header.Style!["jrxmlGroup"]);
        Assert.Equal("Store Group", group["name"]);
        Assert.Equal("header", group["role"]);
        Assert.Equal("$F{store_id}", group["expression"]);
        Assert.Equal("[store_id]", group["normalizedExpression"]);
        var repeat = Assert.IsType<Dictionary<string, object>>(footer.Style!["jrxmlRepeat"]);
        Assert.Equal("jrxmlGroup", repeat["source"]);
        Assert.Equal("footer", repeat["role"]);

        var groupsJson = Assert.Single(result.Design.PageSettings!.CustomProperties!, p => p.Name == "jrxmlGroups").Value;
        var groups = JsonDocument.Parse(groupsJson).RootElement;
        Assert.Equal("Store Group", groups[0].GetProperty("Name").GetString());
        Assert.Equal("[store_id]", groups[0].GetProperty("NormalizedExpression").GetString());
        Assert.True(groups[0].GetProperty("IsReprintHeaderOnEachPage").GetString() == "true");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML017" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_MultipleDetailBands_MapToSharedRepeatMetadata()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="MultiDetail"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="15">
                  <textField>
                    <reportElement key="line1" x="0" y="0" width="160" height="15"/>
                    <textFieldExpression><![CDATA[$F{name}]]></textFieldExpression>
                  </textField>
                </band>
                <band height="12">
                  <textField>
                    <reportElement key="line2" x="0" y="0" width="160" height="12"/>
                    <textFieldExpression><![CDATA[$F{description}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var line1 = El(result.Design, "line1");
        var line2 = El(result.Design, "line2");

        Assert.Equal(20, line1.Y, 0.5);
        Assert.Equal(35, line2.Y, 0.5);
        Assert.Equal("DetailRows", line1.Repeat!.DataPath);
        Assert.Equal(line1.Id, line1.Repeat.TemplateId);
        Assert.Equal("DetailRows", line2.Repeat!.DataPath);

        var repeat1 = Assert.IsType<Dictionary<string, object>>(line1.Style!["jrxmlDetailRepeat"]);
        Assert.Equal("jrxmlDetail", repeat1["source"]);
        Assert.Equal(0, repeat1["bandIndex"]);
        Assert.Equal(2, repeat1["bandCount"]);
        var repeat2 = Assert.IsType<Dictionary<string, object>>(line2.Style!["jrxmlDetailRepeat"]);
        Assert.Equal(1, repeat2["bandIndex"]);

        var detailsJson = Assert.Single(result.Design.PageSettings!.CustomProperties!, p => p.Name == "jrxmlDetailBands").Value;
        var details = JsonDocument.Parse(detailsJson).RootElement;
        Assert.Equal(2, details.GetArrayLength());
        Assert.Equal("detail-0", details[0].GetProperty("Name").GetString());
        Assert.Equal("DetailRows", details[1].GetProperty("DataPath").GetString());
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML018" && d.Severity == MigrationDiagnosticSeverity.Warning);
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
    public void Convert_ChainedAndConditionalStyles_ArePreserved()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Styles"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <style name="Base" forecolor="#111111">
                <box><pen lineWidth="1" lineColor="#222222" lineStyle="Solid"/></box>
              </style>
              <style name="Child" style="Base" hAlign="Right">
                <font fontName="Arial" size="11" isBold="true"/>
                <conditionalStyle>
                  <conditionExpression><![CDATA[$F{amount} > 100]]></conditionExpression>
                  <style forecolor="#FF0000" backcolor="#FFFF00">
                    <font isItalic="true"/>
                    <box><bottomPen lineWidth="2" lineColor="#00FF00" lineStyle="Dashed"/></box>
                  </style>
                </conditionalStyle>
              </style>
              <detail>
                <band height="20">
                  <textField>
                    <reportElement key="amount" x="0" y="0" width="120" height="20" style="Child"/>
                    <textFieldExpression><![CDATA[$F{amount}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var amount = El(result.Design, "amount");

        Assert.Equal("#111111", amount.Style!["color"]);
        Assert.Equal(1d, amount.Style["borderWidth"]);
        Assert.Equal("#222222", amount.Style["borderColor"]);
        Assert.Equal("right", amount.Style["textAlign"]);
        Assert.Equal("Arial", amount.Style["fontFamily"]);
        Assert.Equal("bold", amount.Style["fontWeight"]);

        var conditionalStyles = Assert.IsType<Dictionary<string, object>[]>(amount.Style["jrxmlConditionalStyles"]);
        Assert.Single(conditionalStyles);
        Assert.Equal("$F{amount} > 100", conditionalStyles[0]["ConditionExpression"]);
        Assert.Equal("[amount] > 100", conditionalStyles[0]["NormalizedConditionExpression"]);
        var conditionalStyle = Assert.IsType<Dictionary<string, object>>(conditionalStyles[0]["Style"]);
        Assert.Equal("#FF0000", conditionalStyle["color"]);
        Assert.Equal("#FFFF00", conditionalStyle["backgroundColor"]);
        Assert.Equal("italic", conditionalStyle["fontStyle"]);
        Assert.Equal(2d, conditionalStyle["borderBottomWidth"]);
        Assert.Equal("#00FF00", conditionalStyle["borderBottomColor"]);
        Assert.Equal("dashed", conditionalStyle["borderBottomStyle"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML019" && d.Severity == MigrationDiagnosticSeverity.Warning);
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
    public void Convert_ParameterAndVariableExpressions_NormalizeToCanvasDialect()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Expressions"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="80">
                  <textField>
                    <reportElement key="month" x="0" y="0" width="160" height="20"/>
                    <textFieldExpression><![CDATA[$P{THE_MONTH}]]></textFieldExpression>
                  </textField>
                  <textField>
                    <reportElement key="total" x="0" y="20" width="160" height="20"/>
                    <textFieldExpression><![CDATA[$V{TOTAL_AMOUNT}]]></textFieldExpression>
                  </textField>
                  <textField>
                    <reportElement key="summary" x="0" y="40" width="220" height="20"/>
                    <textFieldExpression><![CDATA[$P{THE_MONTH} + " " + $V{TOTAL_AMOUNT} + " " + $F{store_name}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var month = El(result.Design, "month");
        var total = El(result.Design, "total");
        var summary = El(result.Design, "summary");

        Assert.Null(month.Binding);
        Assert.Equal("{{Parameters.THE_MONTH}}", month.Content);
        Assert.Equal("[Parameters.THE_MONTH]", month.Expression);
        Assert.Equal("{{Variables.TOTAL_AMOUNT}}", total.Content);
        Assert.Equal("[Variables.TOTAL_AMOUNT]", total.Expression);
        Assert.Equal("""[Parameters.THE_MONTH] + " " + [Variables.TOTAL_AMOUNT] + " " + [store_name]""", summary.Expression);
        Assert.Equal(summary.Expression, summary.Content);
        Assert.Equal("""$P{THE_MONTH} + " " + $V{TOTAL_AMOUNT} + " " + $F{store_name}""", summary.Style!["jrxmlExpression"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML010" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_DataDeclarations_ArePreservedAsCustomProperties()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Data"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <parameter name="CUSTOMER_ID" class="java.lang.Integer" isForPrompting="false">
                <defaultValueExpression><![CDATA[314]]></defaultValueExpression>
              </parameter>
              <queryString language="SQL"><![CDATA[select * from customers where id = $P{CUSTOMER_ID}]]></queryString>
              <field name="cust_name" class="java.lang.String"/>
              <variable name="TOTAL_AMOUNT" class="java.math.BigDecimal" calculation="Sum">
                <variableExpression><![CDATA[$F{price}]]></variableExpression>
              </variable>
              <subDataset name="Products">
                <parameter name="CUSTOMER_ID" class="java.lang.Integer"/>
                <queryString language="json"><![CDATA[products]]></queryString>
                <field name="product_name" class="java.lang.String"/>
              </subDataset>
              <detail><band height="20"/></detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var props = result.Design.PageSettings!.CustomProperties!;

        var parameters = JsonDocument.Parse(Assert.Single(props, p => p.Name == "jrxmlParameters").Value).RootElement;
        Assert.Equal("CUSTOMER_ID", parameters[0].GetProperty("Name").GetString());
        Assert.Equal("314", parameters[0].GetProperty("DefaultValueExpression").GetString());

        var fields = JsonDocument.Parse(Assert.Single(props, p => p.Name == "jrxmlFields").Value).RootElement;
        Assert.Equal("cust_name", fields[0].GetProperty("Name").GetString());

        var variables = JsonDocument.Parse(Assert.Single(props, p => p.Name == "jrxmlVariables").Value).RootElement;
        Assert.Equal("TOTAL_AMOUNT", variables[0].GetProperty("Name").GetString());
        Assert.Equal("Sum", variables[0].GetProperty("Calculation").GetString());

        var subDatasets = JsonDocument.Parse(Assert.Single(props, p => p.Name == "jrxmlSubDatasets").Value).RootElement;
        Assert.Equal("Products", subDatasets[0].GetProperty("Name").GetString());
        Assert.Equal("json", subDatasets[0].GetProperty("Query").GetProperty("Language").GetString());

        var query = JsonDocument.Parse(Assert.Single(props, p => p.Name == "jrxmlQuery").Value).RootElement;
        Assert.Equal("SQL", query.GetProperty("Language").GetString());
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML015" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_PrintWhenExpression_MapsToVisibility()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Visibility"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="40">
                  <staticText>
                    <reportElement key="alwaysHidden" x="0" y="0" width="100" height="20"/>
                    <printWhenExpression><![CDATA[false]]></printWhenExpression>
                    <text><![CDATA[Hidden]]></text>
                  </staticText>
                  <textField>
                    <reportElement key="levelThree" x="0" y="20" width="100" height="20"/>
                    <printWhenExpression><![CDATA[$F{level} == 3]]></printWhenExpression>
                    <textFieldExpression><![CDATA[$F{label}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var hidden = El(result.Design, "alwaysHidden");
        var conditional = El(result.Design, "levelThree");

        Assert.True(hidden.Hidden);
        Assert.Null(hidden.VisibleExpression);
        Assert.Equal("[level] == 3", conditional.VisibleExpression);
        Assert.Equal("$F{level} == 3", conditional.Style!["jrxmlPrintWhenExpression"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML016" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_HyperlinkAndAnchorExpressions_MapToNavigationMetadata()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Links"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="80">
                  <textField hyperlinkType="Reference" hyperlinkTarget="Blank">
                    <reportElement key="externalLink" x="0" y="0" width="180" height="20"/>
                    <textFieldExpression><![CDATA["Open customer"]]></textFieldExpression>
                    <hyperlinkReferenceExpression><![CDATA[$P{CustomerUrl}]]></hyperlinkReferenceExpression>
                    <hyperlinkTooltipExpression><![CDATA["Customer profile"]]></hyperlinkTooltipExpression>
                  </textField>
                  <staticText>
                    <reportElement key="bookmark" x="0" y="20" width="180" height="20"/>
                    <anchorNameExpression><![CDATA["customer-section"]]></anchorNameExpression>
                    <text><![CDATA[Customer section]]></text>
                  </staticText>
                  <textField hyperlinkType="LocalAnchor">
                    <reportElement key="anchorLink" x="0" y="40" width="180" height="20"/>
                    <textFieldExpression><![CDATA["Jump"]]></textFieldExpression>
                    <hyperlinkAnchorExpression><![CDATA["customer-section"]]></hyperlinkAnchorExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var externalLink = El(result.Design, "externalLink");
        var bookmark = El(result.Design, "bookmark");
        var anchorLink = El(result.Design, "anchorLink");

        Assert.Equal("link", externalLink.Type);
        Assert.Equal("Open customer", externalLink.Content);
        Assert.Equal("{{Parameters.CustomerUrl}}", externalLink.Href);
        Assert.Equal("_blank", externalLink.LinkTarget);
        var externalNavigation = Assert.IsType<Dictionary<string, object>>(externalLink.Style!["jrxmlNavigation"]);
        Assert.Equal("$P{CustomerUrl}", externalNavigation["HyperlinkReference"]);
        Assert.Equal("[Parameters.CustomerUrl]", externalNavigation["NormalizedHyperlinkReference"]);
        Assert.Equal("\"Customer profile\"", externalNavigation["HyperlinkTooltip"]);

        Assert.Equal("Customer section", bookmark.Content);
        Assert.Equal("customer-section", bookmark.BookmarkName);
        var bookmarkNavigation = Assert.IsType<Dictionary<string, object>>(bookmark.Style!["jrxmlNavigation"]);
        Assert.Equal("\"customer-section\"", bookmarkNavigation["AnchorName"]);

        Assert.Equal("link", anchorLink.Type);
        Assert.Equal("#customer-section", anchorLink.Href);
        Assert.Equal("_self", anchorLink.LinkTarget);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_ImageExpressions_EmbedResolvableSourcesAndPreserveUnresolvedMetadata()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), $"canvas-jrxml-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(imagePath, System.Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
        try
        {
            var escapedPath = imagePath.Replace("\\", "\\\\", StringComparison.Ordinal);
            var jrxml = $$"""
                <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Images"
                    pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
                  <detail>
                    <band height="80">
                      <image>
                        <reportElement key="embeddedDataUrl" x="0" y="0" width="20" height="20"/>
                        <imageExpression><![CDATA["data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="]]></imageExpression>
                      </image>
                      <image>
                        <reportElement key="localFile" x="0" y="20" width="20" height="20"/>
                        <imageExpression><![CDATA["{{escapedPath}}"]]></imageExpression>
                      </image>
                      <image>
                        <reportElement key="missingFile" x="0" y="40" width="20" height="20"/>
                        <imageExpression><![CDATA["missing/logo.png"]]></imageExpression>
                      </image>
                      <image>
                        <reportElement key="dynamicFile" x="0" y="60" width="20" height="20"/>
                        <imageExpression><![CDATA[$P{LogoPath}]]></imageExpression>
                      </image>
                    </band>
                  </detail>
                </jasperReport>
                """;

            var result = Convert(jrxml);
            var dataUrl = El(result.Design, "embeddedDataUrl");
            var localFile = El(result.Design, "localFile");
            var missingFile = El(result.Design, "missingFile");
            var dynamicFile = El(result.Design, "dynamicFile");

            Assert.StartsWith("data:image/png;base64,", dataUrl.Content);
            Assert.StartsWith("data:image/png;base64,", localFile.Content);
            var localSource = Assert.IsType<Dictionary<string, object>>(localFile.Style!["jrxmlImageSource"]);
            Assert.Equal(imagePath, localSource["Source"]);
            Assert.Equal(Path.GetFullPath(imagePath), localSource["ResolvedPath"]);
            Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML021" && d.Severity == MigrationDiagnosticSeverity.Info);

            Assert.Null(missingFile.Content);
            var missingSource = Assert.IsType<Dictionary<string, object>>(missingFile.Style!["jrxmlImageSource"]);
            Assert.Equal("missing/logo.png", missingSource["Source"]);
            Assert.Null(dynamicFile.Content);
            var dynamicSource = Assert.IsType<Dictionary<string, object>>(dynamicFile.Style!["jrxmlImageSource"]);
            Assert.Equal("$P{LogoPath}", dynamicSource["Expression"]);
            Assert.Equal("[Parameters.LogoPath]", dynamicSource["NormalizedExpression"]);
            Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML012" && d.Severity == MigrationDiagnosticSeverity.Warning);
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }
    }

    [Fact]
    public void Convert_PartReport_PreservesSubreportPartMetadata()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                xmlns:p="http://jasperreports.sourceforge.net/jasperreports/parts"
                name="Book" sectionType="Part" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <field name="store_id" class="java.lang.Integer"/>
              <group name="cover">
                <groupHeader>
                  <part evaluationTime="Report" uuid="part-cover">
                    <partNameExpression><![CDATA["Overview"]]></partNameExpression>
                    <p:subreportPart>
                      <subreportParameter name="THE_MONTH">
                        <subreportParameterExpression><![CDATA[$P{THE_MONTH}]]></subreportParameterExpression>
                      </subreportParameter>
                      <subreportExpression><![CDATA["Stores_Overview.jrxml"]]></subreportExpression>
                    </p:subreportPart>
                  </part>
                </groupHeader>
              </group>
              <detail>
                <part uuid="part-store">
                  <partNameExpression><![CDATA[$F{store_name}]]></partNameExpression>
                  <p:subreportPart>
                    <subreportParameter name="STORE_ID">
                      <subreportParameterExpression><![CDATA[$F{store_id}]]></subreportParameterExpression>
                    </subreportParameter>
                    <subreportExpression><![CDATA["Store.jrxml"]]></subreportExpression>
                  </p:subreportPart>
                </part>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);
        var partsJson = Assert.Single(result.Design.PageSettings!.CustomProperties!, p => p.Name == "jrxmlParts").Value;
        var parts = JsonDocument.Parse(partsJson).RootElement;

        Assert.Equal(2, parts.GetArrayLength());
        Assert.Equal("groupHeader", parts[0].GetProperty("Context").GetString());
        Assert.Equal("Report", parts[0].GetProperty("EvaluationTime").GetString());
        Assert.Equal("\"Overview\"", parts[0].GetProperty("PartNameExpression").GetString());
        Assert.Equal("\"Stores_Overview.jrxml\"", parts[0].GetProperty("SubreportPart").GetProperty("SubreportExpression").GetString());
        var parameters = parts[0].GetProperty("SubreportPart").GetProperty("Parameters");
        Assert.Equal("THE_MONTH", parameters[0].GetProperty("Name").GetString());
        Assert.Equal("$P{THE_MONTH}", parameters[0].GetProperty("Expression").GetString());
        Assert.Equal("[Parameters.THE_MONTH]", parameters[0].GetProperty("NormalizedExpression").GetString());

        Assert.Equal("detail", parts[1].GetProperty("Context").GetString());
        Assert.Equal("\"Store.jrxml\"", parts[1].GetProperty("SubreportPart").GetProperty("SubreportExpression").GetString());
    }

    [Fact]
    public void Convert_SectionTypePart_CreatesVisiblePartPlaceholders()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports"
                xmlns:p="http://jasperreports.sourceforge.net/jasperreports/parts"
                name="Book" sectionType="Part" pageWidth="595" pageHeight="842" leftMargin="20" topMargin="30">
              <group name="cover">
                <groupHeader>
                  <part uuid="cover-part">
                    <partNameExpression><![CDATA["Cover"]]></partNameExpression>
                    <p:subreportPart>
                      <subreportExpression><![CDATA["Cover.jrxml"]]></subreportExpression>
                    </p:subreportPart>
                  </part>
                </groupHeader>
              </group>
              <detail>
                <part uuid="store-part">
                  <partNameExpression><![CDATA[$F{store_name}]]></partNameExpression>
                  <p:subreportPart>
                    <subreportParameter name="STORE_ID">
                      <subreportParameterExpression><![CDATA[$F{store_id}]]></subreportParameterExpression>
                    </subreportParameter>
                    <subreportExpression><![CDATA["Store.jrxml"]]></subreportExpression>
                  </p:subreportPart>
                </part>
              </detail>
            </jasperReport>
            """;

        var result = Convert(jrxml);

        Assert.Equal(2, result.Design.Pages[0].Elements.Count);
        var cover = result.Design.Pages[0].Elements[0];
        var store = result.Design.Pages[0].Elements[1];
        Assert.Equal("text", cover.Type);
        Assert.Contains("Cover", cover.Content);
        Assert.Contains("{{store_name}}", store.Content);
        Assert.True(store.Y > cover.Y);
        var metadata = Assert.IsType<Dictionary<string, object>>(store.Style!["jrxmlPart"]);
        Assert.Equal(1, metadata["Order"]);
        Assert.Equal("detail", metadata["Context"]);
        var subreportPart = Assert.IsType<Dictionary<string, object>>(metadata["SubreportPart"]);
        Assert.Equal("\"Store.jrxml\"", subreportPart["SubreportExpression"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_SubreportResource_InlinesMatchingJrxml()
    {
        var master = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Json_Master"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <parameter name="JSON" class="java.lang.String"/>
              <title>
                <band height="211">
                  <subreport>
                    <reportElement key="sub" x="0" y="0" width="554" height="211"/>
                    <dataSourceExpression><![CDATA[new net.sf.jasperreports.engine.data.JsonDataSource(org.apache.commons.io.IOUtils.toInputStream($P{JSON}))]]></dataSourceExpression>
                    <subreportExpression><![CDATA["Json_Sub.jasper"]]></subreportExpression>
                  </subreport>
                </band>
              </title>
            </jasperReport>
            """;
        var subreport = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Json_Sub"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <field name="firstName" class="java.lang.String"/>
              <field name="lastName" class="java.lang.String"/>
              <field name="address.street" class="java.lang.String"/>
              <detail>
                <band height="50">
                  <textField>
                    <reportElement key="greeting" x="0" y="0" width="555" height="50"/>
                    <textFieldExpression><![CDATA["Hello, " + $F{firstName} + " " + $F{lastName} + " of " + $F{address.street}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """;

        var result = new JrxmlToDesignConverter().Convert(master, new Dictionary<string, string>
        {
            ["Json_Sub.jrxml"] = subreport
        });

        var element = Assert.Single(result.Design.Pages[0].Elements);
        Assert.Contains("sub.greeting", element.Name);
        Assert.Equal("text", element.Type);
        Assert.Equal(""" "Hello, " + [firstName] + " " + [lastName] + " of " + [address.street] """.Trim(), element.Content);
        Assert.True(element.X > 20);
        Assert.True(element.Y > 20);
        Assert.True(element.Style!.ContainsKey("jrxmlInlinedFromSubreport"));
        Assert.True(element.Style.ContainsKey("jrxmlParentSubreport"));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGJRXML023" && d.Severity == MigrationDiagnosticSeverity.Info);
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
    public void Convert_Subreport_PreservesMetadataOnPlaceholder()
    {
        var jrxml = """
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Subreports"
                pageWidth="595" pageHeight="842" leftMargin="20" topMargin="20">
              <detail>
                <band height="40">
                  <subreport>
                    <reportElement key="sub" x="0" y="0" width="200" height="30"/>
                    <subreportParameter name="STORE_ID">
                      <subreportParameterExpression><![CDATA[$F{store_id}]]></subreportParameterExpression>
                    </subreportParameter>
                    <connectionExpression><![CDATA[$P{REPORT_CONNECTION}]]></connectionExpression>
                    <subreportExpression><![CDATA["Store.jrxml"]]></subreportExpression>
                  </subreport>
                </band>
              </detail>
            </jasperReport>
            """;

        var r = Convert(jrxml);
        var sub = El(r.Design, "sub");
        Assert.Equal("text", sub.Type);
        Assert.Contains("Store.jrxml", sub.Content);
        Assert.Equal("subreport", sub.Style!["jrxmlComponentType"]);
        var metadata = Assert.IsType<Dictionary<string, object>>(sub.Style["jrxmlSubreport"]);
        Assert.Equal("\"Store.jrxml\"", metadata["SubreportExpression"]);
        Assert.Equal("$P{REPORT_CONNECTION}", metadata["ConnectionExpression"]);
        Assert.Equal("[Parameters.REPORT_CONNECTION]", metadata["NormalizedConnectionExpression"]);
        var parameters = Assert.IsType<Dictionary<string, object>[]>(metadata["Parameters"]);
        Assert.Equal("STORE_ID", parameters[0]["Name"]);
        Assert.Equal("$F{store_id}", parameters[0]["Expression"]);
        Assert.Equal("[store_id]", parameters[0]["NormalizedExpression"]);
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
        Assert.Contains(expressions, e => (string)e["Name"] == "categoryExpression" && (string)e["NormalizedValue"] == "[region]");
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
        Assert.Equal("""TEXT([price], "0.00")""", table.CellData[1][1]);
        var metadata = Assert.IsType<Dictionary<string, object>>(table.Style!["jrxmlTable"]);
        Assert.Equal("Products", metadata["DatasetName"]);
        Assert.Equal(2, metadata["ColumnCount"]);
        var parameters = Assert.IsType<Dictionary<string, object>[]>(metadata["Parameters"]);
        Assert.Equal("CUSTOMER_ID", parameters[0]["Name"]);
        Assert.Equal("$P{CUSTOMER_ID}", parameters[0]["Expression"]);
        Assert.Equal("[Parameters.CUSTOMER_ID]", parameters[0]["NormalizedExpression"]);
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
