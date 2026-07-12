using PXA.Core.Contracts;
using PXA.Migration.Abstractions;
using PXA.Migration.FastReport;

namespace PXA.Migration.FastReport.Tests;

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

    [Fact]
    public void Convert_TableObject_BecomesCanvasTableWithCellsWidthsAndBindings()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="400" Height="80">
                  <TableObject Name="grid" Left="0" Top="0" Width="300" Height="80">
                    <TableColumn Name="c1" Width="96"/>
                    <TableColumn Name="c2" Width="96"/>
                    <TableRow Name="r1" Height="20">
                      <TableCell Name="h1" Text="Name" HorzAlign="Center"/>
                      <TableCell Name="h2" Text="Price" HorzAlign="Right"/>
                    </TableRow>
                    <TableRow Name="r2" Height="20">
                      <TableCell Name="d1" Text="[Items.Name]"/>
                      <TableCell Name="d2" Text="[Items.Price]"/>
                    </TableRow>
                  </TableObject>
                </DataBand>
              </ReportPage></Report>
            """;
        var r = Convert(frx);
        var grid = El(r.Design, "grid");

        Assert.Equal("table", grid.Type);
        Assert.True(grid.HeaderRow);
        Assert.Equal(new[] { "Name", "Price" }, grid.CellData![0]);
        Assert.Equal(new[] { "{{Name}}", "{{Price}}" }, grid.CellData![1]);   // [Items.X] → binding token
        Assert.Equal(new[] { 72.0, 72.0 }, grid.ColumnWidths);                // 96px → 72pt each
        Assert.Equal(new[] { "center", "right" }, grid.ColumnAlignments);
        Assert.True(Has(r.Diagnostics, "CANMIGFRX013"));
    }

    [Fact]
    public void Convert_TableObject_ColSpanPadsColumnsToKeepAlignment()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="400" Height="80">
                  <TableObject Name="grid" Left="0" Top="0" Width="300" Height="80">
                    <TableColumn Name="c1" Width="96"/>
                    <TableColumn Name="c2" Width="96"/>
                    <TableRow Name="r1" Height="20">
                      <TableCell Name="title" Text="Summary" ColSpan="2"/>
                    </TableRow>
                    <TableRow Name="r2" Height="20">
                      <TableCell Name="d1" Text="A"/>
                      <TableCell Name="d2" Text="B"/>
                    </TableRow>
                  </TableObject>
                </DataBand>
              </ReportPage></Report>
            """;
        var grid = El(Convert(frx).Design, "grid");
        Assert.Equal(new[] { "Summary", "" }, grid.CellData![0]);   // ColSpan=2 → value + 1 empty
        Assert.Equal(new[] { "A", "B" }, grid.CellData![1]);
    }

    [Fact]
    public void Convert_MultipleReportPages_BecomeMultipleCanvasPages()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <ReportTitleBand Name="Title1" Top="0" Width="700" Height="30">
                  <TextObject Name="onPage1" Left="0" Top="0" Width="200" Height="20" Text="Cover"/>
                </ReportTitleBand>
              </ReportPage>
              <ReportPage Name="Page2">
                <ReportTitleBand Name="Title2" Top="0" Width="700" Height="30">
                  <TextObject Name="onPage2" Left="0" Top="0" Width="200" Height="20" Text="Content"/>
                </ReportTitleBand>
              </ReportPage></Report>
            """;
        var r = Convert(frx);
        Assert.Equal(2, r.Design.Pages.Count);
        Assert.Contains(r.Design.Pages[0].Elements, e => e.Name == "onPage1");
        Assert.Contains(r.Design.Pages[1].Elements, e => e.Name == "onPage2");
        Assert.True(Has(r.Diagnostics, "CANMIGFRX015"));
    }

    [Fact]
    public void Convert_PictureObject_SniffsJpegMime()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="700" Height="60">
                  <PictureObject Name="pic" Left="0" Top="0" Width="40" Height="40" Image="/9j/4AAQSkZJRgABAQAA"/>
                </DataBand>
              </ReportPage></Report>
            """;
        var pic = El(Convert(frx).Design, "pic");
        Assert.Equal("image", pic.Type);
        Assert.StartsWith("data:image/jpeg;base64,", pic.Content);   // JPEG magic FF D8 FF
    }

    [Fact]
    public void Convert_RichObject_ExtractsRtfText()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="700" Height="60">
                  <RichObject Name="rich" Left="0" Top="0" Width="300" Height="50" Text="{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs20 Hello\par World}"/>
                </DataBand>
              </ReportPage></Report>
            """;
        var rich = El(Convert(frx).Design, "rich");
        Assert.Equal("richtext", rich.Type);
        Assert.Contains("<p>Hello</p>", rich.HtmlContent);
        Assert.Contains("<p>World</p>", rich.HtmlContent);
        Assert.DoesNotContain("Arial", rich.HtmlContent);          // font table skipped
        Assert.DoesNotContain("rtf", rich.HtmlContent);            // control words stripped
    }

    [Fact]
    public void Convert_TextObject_PerSideBorder()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="700" Height="30">
                  <TextObject Name="t" Left="0" Top="0" Width="200" Height="20" Text="Hi" Border.Lines="Bottom" Border.Color="Red" Border.Width="2"/>
                </DataBand>
              </ReportPage></Report>
            """;
        var t = El(Convert(frx).Design, "t");
        Assert.Equal("#FF0000", t.Style!["borderBottomColor"]);
        Assert.Equal(2.0, t.Style!["borderBottomWidth"]);
        Assert.False(t.Style!.ContainsKey("borderColor"));   // only the listed side, not uniform
    }

    [Fact]
    public void Convert_MultiColumnBand_EmitsDiagnostic()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="700" Height="30" Columns.Count="3">
                  <TextObject Name="t" Left="0" Top="0" Width="200" Height="20" Text="Hi"/>
                </DataBand>
              </ReportPage></Report>
            """;
        Assert.True(Has(Convert(frx).Diagnostics, "CANMIGFRX016"));
    }

    [Fact]
    public void Convert_GroupBands_AddRepeatAndGroupMetadata()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <GroupHeaderBand Name="GroupHeader1" Top="0" Width="700" Height="20" Condition="[Items.Country]">
                  <TextObject Name="countryHdr" Left="0" Top="0" Width="200" Height="20" Text="[Items.Country]"/>
                </GroupHeaderBand>
                <DataBand Name="Data1" Top="22" Width="700" Height="20">
                  <TextObject Name="name" Left="0" Top="0" Width="200" Height="20" Text="[Items.Name]"/>
                </DataBand>
                <GroupFooterBand Name="GroupFooter1" Top="44" Width="700" Height="20">
                  <TextObject Name="countTotal" Left="0" Top="0" Width="200" Height="20" Text="Total"/>
                </GroupFooterBand>
              </ReportPage></Report>
            """;
        var r = Convert(frx);

        var hdr = El(r.Design, "countryHdr");
        Assert.NotNull(hdr.Repeat);
        Assert.Equal("Country", hdr.Repeat!.DataPath);           // [Items.Country] → Country
        Assert.Equal(hdr.Id, hdr.Repeat.TemplateId);
        var hdrGroup = Assert.IsType<Dictionary<string, object>>(hdr.Style!["frxGroup"]);
        Assert.Equal("header", hdrGroup["role"]);
        Assert.Equal("[Items.Country]", hdrGroup["condition"]);

        var ftr = El(r.Design, "countTotal");
        var ftrGroup = Assert.IsType<Dictionary<string, object>>(ftr.Style!["frxGroup"]);
        Assert.Equal("footer", ftrGroup["role"]);
        Assert.Equal("Country", ftr.Repeat!.DataPath);           // footer inherits the header's group key

        // A plain DataBand control gets no group repeat metadata.
        Assert.Null(El(r.Design, "name").Repeat);
        Assert.True(Has(r.Diagnostics, "CANMIGFRX014"));
    }

    [Fact]
    public void Convert_TableObject_CellStyles_FillBorderFontAlign()
    {
        var frx = """
            <Report ReportInfo.Name="T">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="400" Height="40">
                  <TableObject Name="grid" Left="0" Top="0" Width="200" Height="40">
                    <TableColumn Name="c1" Width="100"/>
                    <TableRow Name="r1" Height="20">
                      <TableCell Name="c" Text="Total" Fill.Color="Yellow" TextFill.Color="Red" HorzAlign="Center"
                                 Font="Verdana, 12pt, style=Bold" Border.Lines="Bottom" Border.Color="Black" Border.Width="2"/>
                    </TableRow>
                  </TableObject>
                </DataBand>
              </ReportPage></Report>
            """;
        var grid = El(Convert(frx).Design, "grid");
        var cs = Assert.Single(grid.CellStyles!);
        Assert.Equal(0, cs.Row);
        Assert.Equal("#FFFF00", cs.BackgroundColor);
        Assert.Equal("#FF0000", cs.Color);
        Assert.Equal("center", cs.TextAlign);
        Assert.Equal("Verdana", cs.FontFamily);
        Assert.True(cs.Bold);
        Assert.NotNull(cs.BorderBottom);
        Assert.Equal(2, cs.BorderBottom!.Width);
        Assert.Null(cs.BorderTop);                 // only the listed side
    }

    // A group-footer aggregate (Sum) scopes to the current group ($group) and translates to the
    // executable Canvas helper; a plain arithmetic expression translates to bare field identifiers.
    [Fact]
    public void GroupFooterAggregate_TranslatesToGroupScopedSum()
    {
        var frx = """
            <?xml version="1.0" encoding="utf-8"?>
            <Report>
              <Dictionary><TableDataSource Name="Items"><Column Name="Total"/></TableDataSource></Dictionary>
              <ReportPage Name="Page1">
                <GroupHeaderBand Name="GroupHeader1" Top="0" Width="700" Height="20" Condition="[Items.Country]">
                  <TextObject Name="grp" Left="0" Top="0" Width="200" Height="20" Text="[Items.Country]"/>
                </GroupHeaderBand>
                <DataBand Name="Data1" Top="24" Width="700" Height="20" DataSource="Items">
                  <TextObject Name="amount" Left="0" Top="0" Width="200" Height="20" Text="[[Items.Qty] * [Items.Price]]"/>
                </DataBand>
                <GroupFooterBand Name="GroupFooter1" Top="48" Width="700" Height="20">
                  <TextObject Name="grpTotal" Left="0" Top="0" Width="200" Height="20" Text="[Sum([Items.Total])]"/>
                </GroupFooterBand>
              </ReportPage>
            </Report>
            """;

        var d = Convert(frx).Design;

        Assert.Equal("$sum($group, \"Total\")", El(d, "grpTotal").Expression);
        Assert.Equal("Qty * Price", El(d, "amount").Expression);
    }
}
