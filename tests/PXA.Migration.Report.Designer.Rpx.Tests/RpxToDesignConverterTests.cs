using PXA.Core.Contracts;
using PXA.Migration.Abstractions;
using PXA.Migration.Report.Designer.Rpx;
using System.Text.Json;

namespace PXA.Migration.Report.Designer.Rpx.Tests;

public sealed class RpxToDesignConverterTests
{
    // ActiveReports section report: PageHeader (title) + Detail (bound textbox, line, picture, barcode,
    // shape, checkbox) + PageFooter (page number). No <PageSettings> → Letter, zero margins.
    private const string SampleRpx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report Name="Invoice">
          <Sections>
            <PageHeader Name="PageHeader1" Height="1">
              <Controls>
                <Label Name="title" Left="1" Top="0.1" Width="5" Height="0.4" Text="Invoice 2024" Font-FamilyName="Arial" Font-Size="20" Font-Bold="True" Alignment="Center" ForeColor="0, 102, 204" />
              </Controls>
            </PageHeader>
            <Detail Name="Detail1" Height="2">
              <Controls>
                <TextBox Name="customer" Left="1" Top="0" Width="3" Height="0.3" DataField="CustomerName" />
                <Line Name="rule" X1="1" Y1="0.5" X2="6" Y2="0.5" LineWeight="2" LineColor="Gray" LineStyle="Dash" />
                <Picture Name="logo" Left="5" Top="0" Width="1" Height="0.5" Image="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=" />
                <Barcode Name="sku" Left="1" Top="1" Width="2" Height="0.5" DataField="Sku" Style="Code128" />
                <Shape Name="box" Left="4" Top="1" Width="1" Height="1" Style="Ellipse" BackColor="LightGray" />
                <CheckBox Name="paid" Left="1" Top="1.6" Width="1" Height="0.2" Text="Paid" Checked="True" />
              </Controls>
            </Detail>
            <PageFooter Name="PageFooter1" Height="0.5">
              <Controls>
                <Label Name="pageinfo" Left="5" Top="0.1" Width="1" Height="0.2" Text="Page 1" />
              </Controls>
            </PageFooter>
          </Sections>
        </Report>
        """;

    private static RpxConvertResult Convert(string rpx) => new RpxToDesignConverter().Convert(rpx);

    private static RpxConvertResult Convert(string rpx, IReadOnlyDictionary<string, string> resources) =>
        new RpxToDesignConverter().Convert(rpx, resources);

    private static ElementDto El(DesignExportDto d, string name) =>
        d.Pages[0].Elements.Concat(d.SharedElements).First(e => e.Name == name);

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    // 1 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_ParsesSectionsAndDefaultsToLetter()
    {
        var r = Convert(SampleRpx);
        Assert.Equal("Invoice", r.Design.Name);
        Assert.Equal(612, r.Design.PageSettings!.Width, 1);
        Assert.Equal(792, r.Design.PageSettings!.Height, 1);
        Assert.Equal(6, r.Design.Pages[0].Elements.Count);  // customer, rule, logo, sku, box, paid
        Assert.Equal(2, r.Design.SharedElements.Count);      // title + pageinfo
        Assert.True(Has(r.Diagnostics, "CANMIGRPX001"));
    }

    // 2 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_InchesToPoints_FlattensBandsToAbsolute()
    {
        var d = Convert(SampleRpx).Design;
        var customer = El(d, "customer");   // Detail band top = PageHeader height (1in=72) ; Top 0
        Assert.Equal(72, customer.X, 1);    // Left 1in
        Assert.Equal(72, customer.Y, 1);
        Assert.Equal(216, customer.Width, 1);  // 3in
        var paid = El(d, "paid");           // Detail top 72 + Top 1.6in(115.2)
        Assert.Equal(187.2, paid.Y, 1);
    }

    // 3 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_LabelStyle_MapsFontColorAlign()
    {
        var title = El(Convert(SampleRpx).Design, "title");
        Assert.Equal("text", title.Type);
        Assert.Equal("Arial", title.Style!["fontFamily"]);
        Assert.Equal(20.0, title.Style!["fontSize"]);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("center", title.Style!["textAlign"]);
        Assert.Equal("#0066CC", title.Style!["color"]);
    }

    // 4 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TextBoxDataField_BecomesBinding()
    {
        var r = Convert(SampleRpx);
        var customer = El(r.Design, "customer");
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX010" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    // 5 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Line_FromEndpointsWithStrokeAndDash()
    {
        var rule = El(Convert(SampleRpx).Design, "rule");
        Assert.Equal("line", rule.Type);
        Assert.Equal(72, rule.X, 1);     // min(X1,X2)=1in
        Assert.Equal(360, rule.Width, 1); // |6-1|in
        Assert.Equal("#808080", rule.Style!["color"]);
        Assert.Equal(2.0, rule.Style!["strokeWidth"]);
        Assert.Equal("dashed", rule.Style!["dashStyle"]);
    }

    // 6 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeShared()
    {
        var d = Convert(SampleRpx).Design;
        Assert.Contains(d.SharedElements, e => e.Name == "title");
        Assert.Contains(d.SharedElements, e => e.Name == "pageinfo");
        Assert.Equal(7.2, El(d, "title").Y, 1);          // top margin 0 + 0.1in
        Assert.True(El(d, "pageinfo").Y > 700, "footer anchored near page bottom");
    }

    // 7 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Picture_EmbeddedImageKeepsDataUrl()
    {
        var logo = El(Convert(SampleRpx).Design, "logo");
        Assert.Equal("image", logo.Type);
        Assert.StartsWith("data:image/png;base64,", logo.Content);
    }

    // 8 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Barcode_MapsValueAndType()
    {
        var sku = El(Convert(SampleRpx).Design, "sku");
        Assert.Equal("barcode", sku.Type);
        Assert.Equal("code128", sku.BarcodeType);
        Assert.Equal("{{Sku}}", sku.BarcodeValue);
    }

    // 9 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_Shape_EllipseBecomesCircle()
    {
        var box = El(Convert(SampleRpx).Design, "box");
        Assert.Equal("circle", box.Type);
        Assert.Equal("#D3D3D3", box.Style!["backgroundColor"]);
    }

    // 10 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_CheckBox_BecomesCheckmark()
    {
        var paid = El(Convert(SampleRpx).Design, "paid");
        Assert.Equal("checkmark", paid.Type);
        Assert.Equal("checked", paid.CheckState);
    }

    // 11 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_SubReport_EmitsManualMigrationDiagnostic()
    {
        var rpx = """
            <Report Name="R"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <SubReport Name="sub" Left="0" Top="0" Width="2" Height="1" />
            </Controls></Detail></Sections></Report>
            """;
        var r = Convert(rpx);
        var sub = El(r.Design, "sub");                 // kept as a labeled placeholder, not dropped
        Assert.Equal("text", sub.Type);
        Assert.Contains("Sub-report", sub.Content);
        Assert.True(Has(r.Diagnostics, "CANMIGRPX011"));
    }

    // 12 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_EmbeddedScript_EmitsWarning()
    {
        var rpx = """
            <Report Name="R"><Script Language="C#">public void Detail_Format(){}</Script>
              <Sections><Detail Name="Detail1" Height="1"><Controls>
                <Label Name="l" Left="0" Top="0" Width="2" Height="0.3" Text="Hi" />
              </Controls></Detail></Sections></Report>
            """;
        var r = Convert(rpx);
        Assert.True(Has(r.Diagnostics, "CANMIGRPX018"));

        var prop = Assert.Single(r.Design.PageSettings!.CustomProperties!);
        Assert.Equal("rpxScript", prop.Name);
        using var metadata = JsonDocument.Parse(prop.Value);
        Assert.Equal("C#", metadata.RootElement.GetProperty("language").GetString());
        Assert.Equal("public void Detail_Format(){}", metadata.RootElement.GetProperty("preview").GetString());
        Assert.True(metadata.RootElement.GetProperty("length").GetInt32() > 0);
        Assert.Equal(64, metadata.RootElement.GetProperty("sha256").GetString()!.Length);
    }

    // 13 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_PageSettings_PaperKindAndMarginsAndLandscape()
    {
        var rpx = """
            <Report Name="R">
              <PageSettings PaperKind="A4" Margins="1, 1, 1, 1" Orientation="Landscape" />
              <Sections><Detail Name="Detail1" Height="1"><Controls>
                <Label Name="l" Left="0" Top="0" Width="2" Height="0.3" Text="Hi" />
              </Controls></Detail></Sections>
            </Report>
            """;
        var d = Convert(rpx).Design;
        Assert.Equal(842, d.PageSettings!.Width, 1);   // A4 swapped (landscape)
        Assert.Equal(595, d.PageSettings!.Height, 1);
        Assert.Equal(72, El(d, "l").X, 1);             // left margin 1in
    }

    // 14 ──────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Blue", "#0000FF")]
    [InlineData("blue", "#0000FF")]          // case-insensitive named colour
    [InlineData("LightGray", "#D3D3D3")]
    [InlineData("0, 128, 0", "#008000")]
    [InlineData("0xFF112233", "#112233")]
    [InlineData("#abc", "#AABBCC")]
    public void Convert_ColorFormats_NormalizeToHex(string color, string expected)
    {
        var rpx = $"""
            <Report Name="R"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <Label Name="l" Left="0" Top="0" Width="2" Height="0.3" Text="Hi" ForeColor="{color}" />
            </Controls></Detail></Sections></Report>
            """;
        Assert.Equal(expected, El(Convert(rpx).Design, "l").Style!["color"]);
    }

    // 15 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_InvalidXml_Throws() =>
        Assert.Throws<ArgumentException>(() => Convert("<Report><not closed"));

    // 16 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void LooksLikeRpx_DetectsRpxVsRdlVsRepx()
    {
        Assert.True(RpxToDesignConverter.LooksLikeRpx(SampleRpx));
        // RDL: <Report> but reportdefinition namespace + no <Sections>
        Assert.False(RpxToDesignConverter.LooksLikeRpx("""<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"><Body /></Report>"""));
        Assert.False(RpxToDesignConverter.LooksLikeRpx("""<XtraReportsLayoutSerializer Name="x" />"""));
        Assert.False(RpxToDesignConverter.LooksLikeRpx("public class Foo {}"));
        // A leading XML comment must not defeat detection.
        Assert.True(RpxToDesignConverter.LooksLikeRpx("<!-- generated --><Report><Sections><Detail Height=\"1\"><Controls /></Detail></Sections></Report>"));
    }

    // 17 ──────────────────────────────────────────────────────────────────────────────────────────
    // Multiple same-type sections without a Name attribute must not collide (they'd both fall back to
    // the type name) — they keep unique band names and stack correctly instead of crashing.
    [Fact]
    public void Convert_RepeatedGroupSections_DoNotCollide()
    {
        var rpx = """
            <Report Name="Grouped"><Sections>
              <GroupHeader Height="0.5"><Controls><Label Name="g1" Left="0" Top="0" Width="2" Height="0.3" Text="G1" /></Controls></GroupHeader>
              <GroupHeader Height="0.5"><Controls><Label Name="g2" Left="0" Top="0" Width="2" Height="0.3" Text="G2" /></Controls></GroupHeader>
              <Detail Height="1"><Controls><Label Name="d" Left="0" Top="0" Width="2" Height="0.3" Text="D" /></Controls></Detail>
            </Sections></Report>
            """;
        var d = Convert(rpx).Design;
        Assert.Equal(3, d.Pages[0].Elements.Count);
        Assert.Equal(0, El(d, "g1").Y, 1);     // first GroupHeader at top margin (0)
        Assert.Equal(36, El(d, "g2").Y, 1);    // second GroupHeader stacked below (0.5in)
        Assert.Equal(72, El(d, "d").Y, 1);     // Detail below both group headers (1in)
    }

    // 18 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_GroupAndDetailSections_CarryRepeatMetadata()
    {
        var rpx = """
            <Report Name="Grouped"><Sections>
              <GroupHeader Name="CustomerGroup" Height="0.5"><Controls>
                <Label Name="groupTitle" Left="0" Top="0" Width="2" Height="0.3" Text="Customer" />
              </Controls></GroupHeader>
              <Detail Name="LineItems" Height="0.4"><Controls>
                <TextBox Name="item" Left="0" Top="0" Width="2" Height="0.3" DataField="ItemName" />
              </Controls></Detail>
              <GroupFooter Name="CustomerGroupFooter" Height="0.5"><Controls>
                <Label Name="groupTotal" Left="0" Top="0" Width="2" Height="0.3" Text="Total" />
              </Controls></GroupFooter>
            </Sections></Report>
            """;

        var r = Convert(rpx);
        var groupTitle = El(r.Design, "groupTitle");
        var item = El(r.Design, "item");
        var groupTotal = El(r.Design, "groupTotal");

        Assert.Equal("CustomerGroup", groupTitle.Repeat!.DataPath);
        Assert.Equal(groupTitle.Id, groupTitle.Repeat.TemplateId);
        Assert.Equal("LineItems", item.Repeat!.DataPath);
        Assert.Equal("CustomerGroupFooter", groupTotal.Repeat!.DataPath);
        Assert.True(groupTitle.Style!.ContainsKey("rpxGroupRepeat"));
        Assert.True(item.Style!.ContainsKey("rpxDetailRepeat"));
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX013");
    }

    // 19 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_TextDynamicSizingOutputFormatAndPageBreak_ArePreserved()
    {
        var rpx = """
            <Report Name="R"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <TextBox Name="amount" Left="0" Top="0" Width="2" Height="0.3" DataField="Amount"
                       CanGrow="True" CanShrink="True" OutputFormat="Currency" PageBreak="After" />
            </Controls></Detail></Sections></Report>
            """;

        var r = Convert(rpx);
        var amount = El(r.Design, "amount");

        Assert.Equal("visible", amount.Style!["overflow"]);
        Assert.Equal(true, amount.Style["rpxCanShrink"]);
        Assert.Equal("Currency", amount.Formatter);
        Assert.Equal("Currency", amount.Style["rpxOutputFormat"]);
        Assert.Equal("After", amount.Style["rpxPageBreak"]);
        var pageEnd = El(r.Design, "amount page end");
        Assert.Equal("pageboundary", pageEnd.Type);
        Assert.Equal("end", pageEnd.PageBoundaryMode);
        Assert.Equal("After", pageEnd.Style!["rpxPageBreak"]);
        Assert.Equal("amount", pageEnd.Style["rpxPageBreakFor"]);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX014");
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX015");
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX016");
    }

    // 20 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_CrossSectionControls_MapVisuallyAndKeepMetadata()
    {
        var rpx = """
            <Report Name="R"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <CrossSectionLine Name="vline" X1="0" Y1="0" X2="0" Y2="1" LineColor="Red" LineWeight="1" />
              <CrossSectionBox Name="box" Left="0.2" Top="0.1" Width="1" Height="0.5" BackColor="Yellow" />
            </Controls></Detail></Sections></Report>
            """;

        var r = Convert(rpx);
        var line = El(r.Design, "vline");
        var box = El(r.Design, "box");

        Assert.Equal("line", line.Type);
        Assert.Equal("#FF0000", line.Style!["color"]);
        Assert.Equal(true, line.Style["rpxCrossSection"]);
        Assert.Equal("CrossSectionLine", line.Style["rpxCrossSectionControl"]);
        Assert.Equal("rect", box.Type);
        Assert.Equal("#FFFF00", box.Style!["backgroundColor"]);
        Assert.Equal(true, box.Style["rpxCrossSection"]);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX016");
    }

    // 21 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_OleObject_StaysVisibleWithOleMetadata()
    {
        var rpx = """
            <Report Name="R"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <OleObject Name="ole" Left="0" Top="0" Width="2" Height="1" />
            </Controls></Detail></Sections></Report>
            """;

        var r = Convert(rpx);
        var ole = El(r.Design, "ole");

        Assert.Equal("text", ole.Type);
        Assert.Contains("OLE object", ole.Content);
        Assert.True(ole.Style!.ContainsKey("rpxOleObject"));
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX011");
    }

    // 22 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Convert_SubReportResource_InlinesMatchingRpx()
    {
        var master = """
            <Report Name="Master"><Sections><Detail Name="Detail1" Height="1"><Controls>
              <SubReport Name="sub" Left="1" Top="0.5" Width="3" Height="1" ReportName="InvoiceSub.rpx" />
            </Controls></Detail></Sections></Report>
            """;
        var subreport = """
            <Report Name="Sub"><Sections><Detail Name="SubDetail" Height="1"><Controls>
              <Label Name="subTitle" Left="0.25" Top="0.25" Width="2" Height="0.3" Text="Sub title" />
            </Controls></Detail></Sections></Report>
            """;

        var r = Convert(master, new Dictionary<string, string> { ["InvoiceSub.rpx"] = subreport });

        var subTitle = El(r.Design, "subTitle");
        Assert.DoesNotContain(r.Design.Pages[0].Elements, e => e.Name == "sub");
        Assert.Equal(90, subTitle.X, 1); // parent 1in + child .25in
        Assert.Equal(54, subTitle.Y, 1); // parent .5in + child .25in
        Assert.Equal("InvoiceSub.rpx", subTitle.Style!["rpxInlinedFromSubreport"]);
        Assert.Equal("sub", subTitle.Style["rpxParentSubreport"]);
        Assert.Contains(r.Diagnostics, d => d.Id == "CANMIGRPX017");
    }
}
