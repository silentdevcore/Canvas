using Canvas.Core.Contracts;
using Canvas.Migration.Abstractions;
using Canvas.Migration.FastReport;

namespace Canvas.Migration.FastReport.Tests;

public sealed class FrxToDesignConverterTests
{
    // Shape mirrors a real FastReport Demos/Reports/*.frx: dotted attributes (ReportInfo.Name,
    // Fill.Color, TextFill.Color, Border.Color), Font="…, 14pt, style=Bold", [Source.Column] bindings,
    // band element names ending in "Band", object geometry in pixels, page size defaulting to A4.
    private const string SampleFrx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report ScriptLanguage="CSharp" ReportInfo.Name="Invoice">
          <Dictionary>
            <TableDataSource Name="Items"><Column Name="Name" DataType="System.String"/></TableDataSource>
          </Dictionary>
          <ReportPage Name="Page1">
            <ReportTitleBand Name="ReportTitle1" Top="0" Width="718.2" Height="37.8">
              <TextObject Name="title" Left="0" Top="0" Width="718.2" Height="37.8" Text="EMPLOYEES" HorzAlign="Center" Font="Tahoma, 14pt, style=Bold, Underline" TextFill.Color="Blue" Fill.Color="WhiteSmoke"/>
            </ReportTitleBand>
            <PageHeaderBand Name="PageHeader1" Top="40" Width="718.2" Height="20">
              <TextObject Name="hdr" Left="0" Top="0" Width="100" Height="20" Text="Header"/>
            </PageHeaderBand>
            <DataBand Name="Data1" Top="64" Width="718.2" Height="20" DataSource="Items">
              <TextObject Name="name" Left="0" Top="0" Width="200" Height="20" Text="[Items.Name]"/>
              <LineObject Name="rule" Left="0" Top="19" Width="718.2" Height="0" Border.Color="Gray" Border.Width="2"/>
              <ShapeObject Name="box" Left="600" Top="0" Width="40" Height="40" Shape="Ellipse" Fill.Color="LightGray" Border.Color="Black"/>
              <BarcodeObject Name="bc" Left="300" Top="0" Width="100" Height="40" Text="12345" Barcode="Code128"/>
              <CheckBoxObject Name="chk" Left="450" Top="0" Width="20" Height="20" Checked="true"/>
              <SubreportObject Name="sub" Left="0" Top="40" Width="200" Height="30"/>
            </DataBand>
            <PageFooterBand Name="PageFooter1" Top="100" Width="718.2" Height="20">
              <TextObject Name="pageinfo" Left="600" Top="0" Width="100" Height="20" Text="Page [Page]"/>
            </PageFooterBand>
          </ReportPage>
        </Report>
        """;

    private static FrxConvertResult Convert(string frx) => new FrxToDesignConverter().Convert(frx);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    [Fact]
    public void Convert_ParsesReportAndDefaultsToA4()
    {
        var r = Convert(SampleFrx);
        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(595.3, r.Design.PageSettings!.Width, 0.5);   // 210mm
        Assert.Equal(841.9, r.Design.PageSettings!.Height, 0.5);  // 297mm
        Assert.Equal(7, r.Design.Pages[0].Elements.Count);       // title, name, rule, box, bc, chk, sub
        Assert.Equal(2, r.Design.SharedElements.Count);          // hdr + pageinfo (page header/footer)
        Assert.True(Has(r.Diagnostics, "CANMIGFRX001"));
    }

    [Fact]
    public void Convert_PixelsToPoints_FlattensBandTop()
    {
        var d = Convert(SampleFrx).Design;
        var title = El(d, "title");
        Assert.Equal(28.35, title.X, 0.5);     // left margin 10mm
        Assert.Equal(28.35, title.Y, 0.5);     // marginTop + bandTop(0) + objTop(0)
        Assert.Equal(538.65, title.Width, 0.5); // 718.2px × 0.75
        var name = El(d, "name");              // DataBand Top 64px(48pt) + marginTop
        Assert.Equal(76.35, name.Y, 0.5);
    }

    [Fact]
    public void Convert_TextStyle_FontColorAlignDecorationBackground()
    {
        var title = El(Convert(SampleFrx).Design, "title");
        Assert.Equal("text", title.Type);
        Assert.Equal("Tahoma", title.Style!["fontFamily"]);
        Assert.Equal(14.0, title.Style!["fontSize"]);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("underline", title.Style!["textDecoration"]);
        Assert.Equal("center", title.Style!["textAlign"]);
        Assert.Equal("#0000FF", title.Style!["color"]);          // TextFill.Color="Blue"
        Assert.Equal("#F5F5F5", title.Style!["backgroundColor"]); // Fill.Color="WhiteSmoke"
        Assert.Equal("EMPLOYEES", title.Content);
    }

    [Fact]
    public void Convert_FieldReference_BecomesBinding()
    {
        var r = Convert(SampleFrx);
        var name = El(r.Design, "name");                          // Text="[Items.Name]"
        Assert.Equal("Name", name.Binding);                      // last segment of Items.Name
        Assert.Equal("{{Name}}", name.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGFRX010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Convert_ComplexExpression_BecomesExpression()
    {
        var pageinfo = El(Convert(SampleFrx).Design, "pageinfo"); // Text="Page [Page]"
        Assert.Equal("Page [Page]", pageinfo.Expression);
    }

    [Fact]
    public void Convert_Line_MapsColorAndStroke()
    {
        var rule = El(Convert(SampleFrx).Design, "rule");
        Assert.Equal("line", rule.Type);
        Assert.Equal("#808080", rule.Style!["color"]);            // Border.Color="Gray"
        Assert.Equal(2.0, rule.Style!["strokeWidth"]);
    }

    [Fact]
    public void Convert_Shape_EllipseBecomesCircle()
    {
        var box = El(Convert(SampleFrx).Design, "box");
        Assert.Equal("circle", box.Type);
        Assert.Equal("#D3D3D3", box.Style!["backgroundColor"]);   // LightGray
        Assert.Equal("#000000", box.Style!["borderColor"]);       // Black
    }

    [Fact]
    public void Convert_Barcode_And_CheckBox()
    {
        var d = Convert(SampleFrx).Design;
        var bc = El(d, "bc");
        Assert.Equal("barcode", bc.Type);
        Assert.Equal("code128", bc.BarcodeType);
        Assert.Equal("12345", bc.BarcodeValue);
        Assert.Equal("checkmark", El(d, "chk").Type);
        Assert.Equal("checked", El(d, "chk").CheckState);
    }

    [Fact]
    public void Convert_Subreport_BecomesPlaceholder()
    {
        var r = Convert(SampleFrx);
        var sub = El(r.Design, "sub");
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
        Assert.True(Has(r.Diagnostics, "CANMIGFRX011"));
    }

    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeShared()
    {
        var d = Convert(SampleFrx).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "hdr");
        Assert.Contains(d.SharedElements, e => e.Name == "pageinfo");
        Assert.Equal(28.35, El(d, "hdr").Y, 0.5);                 // marginTop + objTop
        Assert.True(El(d, "pageinfo").Y > 700, "footer anchored near page bottom");
    }

    [Fact]
    public void Convert_ExplicitPageSizeAndLandscape()
    {
        // Landscape swaps the portrait PaperWidth/PaperHeight → 297×210 (A4 landscape).
        var frx = """
            <Report ReportInfo.Name="L"><ReportPage Name="Page1" PaperWidth="210" PaperHeight="297" Landscape="true" LeftMargin="20">
              <DataBand Name="Data1" Top="0" Width="100" Height="20"><TextObject Name="t" Left="0" Top="0" Width="50" Height="20" Text="Hi"/></DataBand>
            </ReportPage></Report>
            """;
        var d = Convert(frx).Design;
        Assert.Equal(841.9, d.PageSettings!.Width, 0.5);   // 297mm long side
        Assert.Equal(595.3, d.PageSettings!.Height, 0.5);
        Assert.Equal(56.7, El(d, "t").X, 0.5);             // left margin 20mm
    }

    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<Report><not closed"));

    [Fact]
    public void LooksLikeFrx_DetectsFrxVsRdlVsRpxVsRepx()
    {
        Assert.True(FrxToDesignConverter.LooksLikeFrx(SampleFrx));
        // RDL: reportdefinition namespace, no <ReportPage>
        Assert.False(FrxToDesignConverter.LooksLikeFrx("""<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"><Body /></Report>"""));
        // RPX: <Report> with <Sections>, no <ReportPage>
        Assert.False(FrxToDesignConverter.LooksLikeFrx("""<Report><Sections><Detail Height="1"><Controls /></Detail></Sections></Report>"""));
        // DevExpress .repx: root is not <Report>
        Assert.False(FrxToDesignConverter.LooksLikeFrx("""<XtraReportsLayoutSerializer Name="x" />"""));
        Assert.False(FrxToDesignConverter.LooksLikeFrx("public class Foo {}"));
    }

    [Fact]
    public void Convert_ArgbAndNumericColors()
    {
        var frx = """
            <Report><ReportPage Name="Page1">
              <DataBand Name="Data1" Top="0" Width="100" Height="20">
                <TextObject Name="a" Left="0" Top="0" Width="50" Height="20" Text="A" TextFill.Color="255, 0, 0"/>
                <TextObject Name="b" Left="0" Top="0" Width="50" Height="20" Text="B" TextFill.Color="lime"/>
              </DataBand>
            </ReportPage></Report>
            """;
        var d = Convert(frx).Design;
        Assert.Equal("#FF0000", El(d, "a").Style!["color"]);
        Assert.Equal("#00FF00", El(d, "b").Style!["color"]);   // case-insensitive named
    }
}
