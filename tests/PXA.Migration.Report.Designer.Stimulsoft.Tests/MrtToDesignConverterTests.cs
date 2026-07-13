using PXA.Core.Contracts;
using PXA.Migration.Abstractions;
using PXA.Migration.Report.Designer.Stimulsoft;

namespace PXA.Migration.Report.Designer.Stimulsoft.Tests;

public sealed class MrtToDesignConverterTests
{
    // Mirrors a real Stimulsoft .mrt (StiSerializer XML): Pages → Page → Components are bands, each band's
    // nested Components are items with <ClientRectangle> (hundredths of an inch, band-relative), <Font>,
    // <TextBrush> [R:G:B], and <Text>{Source.Field}</Text> bindings.
    private const string SampleMrt = """
        <?xml version="1.0" encoding="utf-8"?>
        <StiSerializer version="1.02" type="Net" application="StiReport">
          <ReportName>Invoice</ReportName>
          <Pages isList="true" count="1">
            <Page1 Ref="4" type="Page" isKey="true">
              <PaperSize>A4</PaperSize>
              <Components isList="true" count="3">
                <ReportTitleBand1 Ref="7" type="ReportTitleBand" isKey="true">
                  <ClientRectangle>0,20,749,40</ClientRectangle>
                  <Components isList="true" count="1">
                    <Text1 Ref="8" type="Text" isKey="true">
                      <ClientRectangle>0,0,749,40</ClientRectangle>
                      <Font>Arial,20,Bold,Point,False,0</Font>
                      <HorAlignment>Center</HorAlignment>
                      <Text>INVOICE</Text>
                      <TextBrush>[0:102:204]</TextBrush>
                      <Name>Text1</Name>
                    </Text1>
                  </Components>
                  <Name>ReportTitleBand1</Name>
                </ReportTitleBand1>
                <DataBand1 Ref="9" type="DataBand" isKey="true">
                  <ClientRectangle>0,80,749,40</ClientRectangle>
                  <Components isList="true" count="3">
                    <Text2 Ref="10" type="Text" isKey="true">
                      <ClientRectangle>0,0,300,20</ClientRectangle>
                      <Text>{Customers.CompanyName}</Text>
                      <Name>Text2</Name>
                    </Text2>
                    <Line1 Ref="11" type="HorizontalLinePrimitive" isKey="true">
                      <ClientRectangle>0,19,749,1</ClientRectangle>
                      <Color>[128:128:128]</Color>
                      <Name>Line1</Name>
                    </Line1>
                    <Sub1 Ref="12" type="SubReport" isKey="true">
                      <ClientRectangle>0,25,300,10</ClientRectangle>
                      <Name>Sub1</Name>
                    </Sub1>
                  </Components>
                  <Name>DataBand1</Name>
                </DataBand1>
                <PageFooterBand1 Ref="5" type="PageFooterBand" isKey="true">
                  <ClientRectangle>0,1071,749,20</ClientRectangle>
                  <Components isList="true" count="1">
                    <Text3 Ref="6" type="Text" isKey="true">
                      <ClientRectangle>600,0,149,20</ClientRectangle>
                      <Text>{PageNofM}</Text>
                      <Name>Text3</Name>
                    </Text3>
                  </Components>
                  <Name>PageFooterBand1</Name>
                </PageFooterBand1>
              </Components>
              <Name>Page1</Name>
            </Page1>
          </Pages>
        </StiSerializer>
        """;

    private static MrtConvertResult Convert(string mrt) => new MrtToDesignConverter().Convert(mrt);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    [Fact]
    public void GroupFooterAggregate_TranslatesToGroupScopedSum()
    {
        var mrt = """
            <?xml version="1.0" encoding="utf-8"?>
            <StiSerializer version="1.02" type="Net" application="StiReport">
              <ReportName>Agg</ReportName>
              <Pages isList="true" count="1">
                <Page1 Ref="1" type="Page" isKey="true">
                  <PaperSize>A4</PaperSize>
                  <Components isList="true" count="2">
                    <GroupHeaderBand1 Ref="2" type="GroupHeaderBand" isKey="true">
                      <ClientRectangle>0,0,749,20</ClientRectangle>
                      <Condition>{Customers.Country}</Condition>
                      <Name>GroupHeaderBand1</Name>
                    </GroupHeaderBand1>
                    <GroupFooterBand1 Ref="3" type="GroupFooterBand" isKey="true">
                      <ClientRectangle>0,40,749,20</ClientRectangle>
                      <Components isList="true" count="1">
                        <Total1 Ref="4" type="Text" isKey="true">
                          <ClientRectangle>0,0,200,20</ClientRectangle>
                          <Text>{Sum(Customers.Total)}</Text>
                          <Name>Total1</Name>
                        </Total1>
                      </Components>
                      <Name>GroupFooterBand1</Name>
                    </GroupFooterBand1>
                  </Components>
                  <Name>Page1</Name>
                </Page1>
              </Pages>
            </StiSerializer>
            """;

        Assert.Equal("$sum($group, \"Total\")", El(Convert(mrt).Design, "Total1").Expression);
    }

    [Fact]
    public void Convert_ParsesPagesBandsAndPaperSize()
    {
        var r = Convert(SampleMrt);
        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(595, r.Design.PageSettings!.Width, 0.5);    // A4
        Assert.Equal(842, r.Design.PageSettings!.Height, 0.5);
        Assert.Equal(4, r.Design.Pages[0].Elements.Count);        // Text1, Text2, Line1, Sub1
        Assert.Single(r.Design.SharedElements);                   // Text3 (page footer)
        Assert.True(Has(r.Diagnostics, "CANMIGMRT001"));
    }

    [Fact]
    public void Convert_HundredthsOfInch_ToPoints_BandRelative()
    {
        var d = Convert(SampleMrt).Design;
        var title = El(d, "Text1");                 // band(0,20) + item(0,0) = (0,20) × 0.72
        Assert.Equal(0, title.X, 0.5);
        Assert.Equal(14.4, title.Y, 0.5);
        Assert.Equal(539.28, title.Width, 0.5);     // 749 × 0.72
        Assert.Equal(57.6, El(d, "Text2").Y, 0.5);  // band(0,80) × 0.72
    }

    [Fact]
    public void Convert_TextStyle_FontColorAlign()
    {
        var title = El(Convert(SampleMrt).Design, "Text1");
        Assert.Equal("text", title.Type);
        Assert.Equal("Arial", title.Style!["fontFamily"]);
        Assert.Equal(20.0, title.Style!["fontSize"]);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("center", title.Style!["textAlign"]);
        Assert.Equal("#0066CC", title.Style!["color"]);    // TextBrush [0:102:204]
        Assert.Equal("INVOICE", title.Content);
    }

    [Fact]
    public void Convert_TextField_BecomesBinding()
    {
        var r = Convert(SampleMrt);
        var text2 = El(r.Design, "Text2");                 // {Customers.CompanyName}
        Assert.Equal("CompanyName", text2.Binding);
        Assert.Equal("{{CompanyName}}", text2.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGMRT010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Convert_SystemVariable_BecomesExpression()
    {
        var text3 = El(Convert(SampleMrt).Design, "Text3");  // {PageNofM}
        Assert.Equal("{PageNofM}", text3.Expression);
    }

    [Fact]
    public void Convert_LinePrimitive_MapsColor()
    {
        var line = El(Convert(SampleMrt).Design, "Line1");
        Assert.Equal("line", line.Type);
        Assert.Equal("#808080", line.Style!["color"]);     // Color [128:128:128]
    }

    [Fact]
    public void Convert_SubReport_BecomesPlaceholder()
    {
        var r = Convert(SampleMrt);
        var sub = El(r.Design, "Sub1");
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
        Assert.True(Has(r.Diagnostics, "CANMIGMRT011"));
    }

    [Fact]
    public void Convert_PageFooterBand_BecomesShared_NearBottom()
    {
        var d = Convert(SampleMrt).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "Text3");
        Assert.True(El(d, "Text3").Y > 700, "footer band position near page bottom");
        Assert.Equal(432, El(d, "Text3").X, 0.5);          // 600 × 0.72
    }

    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<StiSerializer><not closed"));

    [Fact]
    public void LooksLikeMrt_DetectsMrtVsOthers()
    {
        Assert.True(MrtToDesignConverter.LooksLikeMrt(SampleMrt));
        Assert.False(MrtToDesignConverter.LooksLikeMrt("""<jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" />"""));
        Assert.False(MrtToDesignConverter.LooksLikeMrt("""<Report><ReportPage /></Report>"""));
        Assert.True(MrtToDesignConverter.LooksLikeMrt("{ \"ReportVersion\": \"2023\", \"Pages\": {} }"));   // JSON .mrt now supported
        Assert.False(MrtToDesignConverter.LooksLikeMrt("{ \"some\": \"object\" }"));   // plain JSON isn't a Stimulsoft report
        Assert.False(MrtToDesignConverter.LooksLikeMrt("public class Foo {}"));
    }

    [Fact]
    public void Convert_GroupBands_AddRepeatAndGroupMetadata()
    {
        var mrt = """
            <?xml version="1.0" encoding="utf-8"?>
            <StiSerializer version="1.02" type="Net" application="StiReport">
              <ReportName>Grouped</ReportName>
              <Pages isList="true" count="1">
                <Page1 Ref="1" type="Page" isKey="true">
                  <PaperSize>A4</PaperSize>
                  <Components isList="true" count="3">
                    <GroupHeaderBand1 Ref="2" type="GroupHeaderBand" isKey="true">
                      <ClientRectangle>0,0,749,20</ClientRectangle>
                      <Condition>{Customers.Country}</Condition>
                      <Components isList="true" count="1">
                        <Text1 Ref="3" type="Text" isKey="true">
                          <ClientRectangle>0,0,300,20</ClientRectangle>
                          <Text>{Customers.Country}</Text>
                          <Name>country</Name>
                        </Text1>
                      </Components>
                      <Name>GroupHeaderBand1</Name>
                    </GroupHeaderBand1>
                    <DataBand1 Ref="4" type="DataBand" isKey="true">
                      <ClientRectangle>0,30,749,20</ClientRectangle>
                      <Components isList="true" count="1">
                        <Text2 Ref="5" type="Text" isKey="true">
                          <ClientRectangle>0,0,300,20</ClientRectangle>
                          <Text>{Customers.Name}</Text>
                          <Name>rowName</Name>
                        </Text2>
                      </Components>
                      <Name>DataBand1</Name>
                    </DataBand1>
                    <GroupFooterBand1 Ref="6" type="GroupFooterBand" isKey="true">
                      <ClientRectangle>0,60,749,20</ClientRectangle>
                      <Components isList="true" count="1">
                        <Text3 Ref="7" type="Text" isKey="true">
                          <ClientRectangle>0,0,300,20</ClientRectangle>
                          <Text>Total</Text>
                          <Name>groupTotal</Name>
                        </Text3>
                      </Components>
                      <Name>GroupFooterBand1</Name>
                    </GroupFooterBand1>
                  </Components>
                  <Name>Page1</Name>
                </Page1>
              </Pages>
            </StiSerializer>
            """;
        var r = Convert(mrt);

        var hdr = El(r.Design, "country");
        Assert.NotNull(hdr.Repeat);
        Assert.Equal("Country", hdr.Repeat!.DataPath);           // {Customers.Country} → Country
        Assert.Equal(hdr.Id, hdr.Repeat.TemplateId);
        var hdrGroup = Assert.IsType<Dictionary<string, object>>(hdr.Style!["mrtGroup"]);
        Assert.Equal("header", hdrGroup["role"]);

        var ftr = El(r.Design, "groupTotal");
        var ftrGroup = Assert.IsType<Dictionary<string, object>>(ftr.Style!["mrtGroup"]);
        Assert.Equal("footer", ftrGroup["role"]);
        Assert.Equal("Country", ftr.Repeat!.DataPath);           // footer inherits the header's group key

        Assert.Null(El(r.Design, "rowName").Repeat);             // plain DataBand control: no group repeat
        Assert.True(Has(r.Diagnostics, "CANMIGMRT013"));
    }

    [Fact]
    public void Convert_CompoundExpression_NormalizesFieldReferences()
    {
        var item = ItemMrt("""<Text>{Customers.First} - {Customers.Last}</Text>""", "expr");
        var el = El(Convert(item).Design, "expr");
        Assert.Contains("{{First}}", el.Content);
        Assert.Contains("{{Last}}", el.Content);
        Assert.Equal("{Customers.First} - {Customers.Last}", el.Style!["mrtExpression"]);
    }

    [Fact]
    public void Convert_PerSideBorder_FromStiBorderString()
    {
        var perSide = El(Convert(ItemMrt("""<Text>Hi</Text><Border>Bottom;[255:0:0];2;Solid</Border>""", "b1")).Design, "b1");
        Assert.Equal("#FF0000", perSide.Style!["borderBottomColor"]);
        Assert.Equal(2.0, perSide.Style!["borderBottomWidth"]);
        Assert.False(perSide.Style!.ContainsKey("borderColor"));   // only the listed side

        var uniform = El(Convert(ItemMrt("""<Text>Hi</Text><Border>All;[0:0:255];1;Solid</Border>""", "b2")).Design, "b2");
        Assert.Equal("#0000FF", uniform.Style!["borderColor"]);
        Assert.Equal(1.0, uniform.Style!["borderWidth"]);
    }

    [Fact]
    public void Convert_Chart_BecomesPlaceholder()
    {
        var item = ItemMrt("", "myChart", type: "Chart");
        var r = Convert(item);
        Assert.True(Has(r.Diagnostics, "CANMIGMRT014"));
        Assert.Contains("[Chart:", El(r.Design, "myChart").Content);
    }

    [Fact]
    public void Convert_NamedComponentStyle_SuppliesDefaults()
    {
        var mrt = """
            <?xml version="1.0" encoding="utf-8"?>
            <StiSerializer version="1.02" type="Net" application="StiReport">
              <ReportName>Styled</ReportName>
              <Styles isList="true" count="1">
                <Style1 Ref="1" type="StiStyle" isKey="true"><Name>Accent</Name><TextBrush>[255:0:0]</TextBrush></Style1>
              </Styles>
              <Pages isList="true" count="1">
                <Page1 Ref="2" type="Page" isKey="true">
                  <PaperSize>A4</PaperSize>
                  <Components isList="true" count="1">
                    <DataBand1 Ref="3" type="DataBand" isKey="true">
                      <ClientRectangle>0,0,749,20</ClientRectangle>
                      <Components isList="true" count="1">
                        <Text1 Ref="4" type="Text" isKey="true">
                          <ClientRectangle>0,0,300,20</ClientRectangle>
                          <Text>Hi</Text>
                          <ComponentStyle>Accent</ComponentStyle>
                          <Name>styled</Name>
                        </Text1>
                      </Components>
                      <Name>DataBand1</Name>
                    </DataBand1>
                  </Components>
                  <Name>Page1</Name>
                </Page1>
              </Pages>
            </StiSerializer>
            """;
        Assert.Equal("#FF0000", El(Convert(mrt).Design, "styled").Style!["color"]);
    }

    [Fact]
    public void Convert_ExplicitPageWidthHeight_WhenNoPaperSize()
    {
        var mrt = """
            <?xml version="1.0" encoding="utf-8"?>
            <StiSerializer version="1.02" type="Net" application="StiReport">
              <ReportName>Custom</ReportName>
              <Pages isList="true" count="1">
                <Page1 Ref="1" type="Page" isKey="true">
                  <PageWidth>827</PageWidth><PageHeight>1169</PageHeight>
                  <Components isList="true" count="1">
                    <DataBand1 Ref="2" type="DataBand" isKey="true">
                      <ClientRectangle>0,0,749,20</ClientRectangle>
                      <Components isList="true" count="1">
                        <Text1 Ref="3" type="Text" isKey="true"><ClientRectangle>0,0,300,20</ClientRectangle><Text>Hi</Text><Name>t</Name></Text1>
                      </Components>
                      <Name>DataBand1</Name>
                    </DataBand1>
                  </Components>
                  <Name>Page1</Name>
                </Page1>
              </Pages>
            </StiSerializer>
            """;
        var ps = Convert(mrt).Design.PageSettings!;
        Assert.Equal(595, ps.Width, 0);    // 827 hundredths-inch × 0.72 ≈ 595
        Assert.Equal(842, ps.Height, 0);   // 1169 × 0.72 ≈ 842
    }

    [Fact]
    public void Convert_JsonMrt_ParsesPageBandsItemsWithReportUnitGeometry()
    {
        var json = """
            {
              "ReportVersion": "2023.1",
              "ReportName": "JsonReport",
              "ReportUnit": "Centimeters",
              "Pages": {
                "0": {
                  "Ident": "StiPage",
                  "Name": "Page1",
                  "PageWidth": 21.0,
                  "PageHeight": 29.7,
                  "Components": {
                    "0": {
                      "Ident": "StiDataBand",
                      "Name": "DataBand1",
                      "ClientRectangle": "0,1,21,1",
                      "Components": {
                        "0": {
                          "Ident": "StiText",
                          "Name": "name",
                          "ClientRectangle": "0,0,5,0.5",
                          "Text": "{Customers.Name}",
                          "TextBrush": "[255:0:0]",
                          "Border": "All;[0:0:0];1;Solid"
                        }
                      }
                    }
                  }
                }
              }
            }
            """;
        Assert.True(MrtToDesignConverter.LooksLikeMrt(json));
        var r = Convert(json);

        Assert.Equal("JsonReport", r.Design.Name);
        Assert.Equal(595, r.Design.PageSettings!.Width, 0);   // 21cm × 72/2.54 ≈ 595

        var name = El(r.Design, "name");
        Assert.Equal("text", name.Type);
        Assert.Equal("{{Name}}", name.Content);                // {Customers.Name} → binding
        Assert.Equal("#FF0000", name.Style!["color"]);
        Assert.Equal("#000000", name.Style!["borderColor"]);   // StiBorder All
        Assert.Equal(28.0, name.Y, 0);                         // band y=1cm folded in (1 × 28.35 ≈ 28)
    }

    // Wraps a single component (default a Text) in a minimal A4 .mrt DataBand.
    private static string ItemMrt(string body, string name, string type = "Text") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <StiSerializer version="1.02" type="Net" application="StiReport">
          <ReportName>R</ReportName>
          <Pages isList="true" count="1">
            <Page1 Ref="1" type="Page" isKey="true">
              <PaperSize>A4</PaperSize>
              <Components isList="true" count="1">
                <DataBand1 Ref="2" type="DataBand" isKey="true">
                  <ClientRectangle>0,0,749,20</ClientRectangle>
                  <Components isList="true" count="1">
                    <Item1 Ref="3" type="{type}" isKey="true"><ClientRectangle>0,0,300,20</ClientRectangle>{body}<Name>{name}</Name></Item1>
                  </Components>
                  <Name>DataBand1</Name>
                </DataBand1>
              </Components>
              <Name>Page1</Name>
            </Page1>
          </Pages>
        </StiSerializer>
        """;
}
