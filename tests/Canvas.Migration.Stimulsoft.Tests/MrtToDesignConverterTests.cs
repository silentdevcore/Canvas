using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Stimulsoft;

namespace Canvas.Migration.Stimulsoft.Tests;

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
        Assert.False(MrtToDesignConverter.LooksLikeMrt("{ \"ReportVersion\": \"2023\" }"));   // JSON .mrt is V2
        Assert.False(MrtToDesignConverter.LooksLikeMrt("public class Foo {}"));
    }
}
