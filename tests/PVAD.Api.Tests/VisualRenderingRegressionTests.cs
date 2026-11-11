using System.Text;
using PVAD.Core.Contracts;
using PVAD.Pdf;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class VisualRenderingRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChoiceWidgets_HaveExplicitNormalAppearances_WithOrWithoutPackagedFonts(bool packaged)
    {
        var document = new PdfDocument { FontLoader = packaged ? new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts")) : null };
        var page = document.AddPage(300, 200);
        page.AddComboBox("department", 20, 100, 250, 48, ["Sales", "Finance"], "Finance", 11);
        page.AddListBox("country", 20, 20, 250, 70, ["Switzerland", "Germany"], ["Germany"], fontSize: 11);
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pdf, @"/AP << /N \d+ 0 R >>").Count);
        Assert.Contains("/BBox [0 0 250 48]", pdf);
        Assert.Contains("/BBox [0 0 250 70]", pdf);
        Assert.Contains("/ChoiceFont ", pdf);
        Assert.Contains("2 20.7 Td", pdf);
        Assert.Contains("2 58.1 Td", pdf);
        Assert.Contains("re W n", pdf);
        if (packaged) Assert.Contains("/BaseFont /LiberationSans", pdf);
        else Assert.Contains("(Finance) Tj", pdf);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChoiceValueUpdate_DiscardsThePreviousNormalAppearance(bool list)
    {
        var document = new PdfDocument();
        var page = document.AddPage(300, 200);
        if (list) page.AddListBox("choice", 20, 20, 200, 100, ["Alpha", "Beta"], ["Alpha"], multiSelect: true);
        else page.AddComboBox("choice", 20, 20, 200, 40, ["Alpha", "Beta"], "Alpha");
        var bytes = document.ToBytes();
        using var fields = System.Text.Json.JsonDocument.Parse("""[{"name":"choice","value":"Beta"}]""");
        var result = await new PVAD.WebApi.Services.PdfViewerFormExtractionService().FillAsync(new MemoryStream(bytes), fields.RootElement);
        var imported = await new PVAD.Importer.PdfImporter().LoadAsync(new MemoryStream(result));
        var choice = Assert.Single(imported.ObjectGraph.Objects.Values.Select(item => item.Value)
            .OfType<PVAD.Importer.Objects.PdfDictionary>().Where(field => field["FT"] is PVAD.Importer.Objects.PdfName { Value: "Ch" }));
        Assert.Null(choice["AP"]);
        Assert.Equal("Beta", Assert.IsType<PVAD.Importer.Objects.PdfString>(choice["V"]).ToLatin1String());
    }

    [Theory]
    [InlineData(PdfStandardFont.Helvetica, false, "Helvetica-Bold")]
    [InlineData(PdfStandardFont.TimesRoman, false, "Times-Bold")]
    [InlineData(PdfStandardFont.Courier, false, "Courier-Bold")]
    [InlineData(PdfStandardFont.HelveticaBold, true, "Helvetica-BoldOblique")]
    public void DefaultFamily_RetainsRequestedStyles(PdfStandardFont font, bool italic, string expected)
    {
        var document = new PdfDocument { DefaultFont = font };
        var page = document.AddPage(300, 200);
        page.DrawText("Styled text", 20, 150, new PdfDrawTextOptions { Bold = true, Italic = italic });
        page.DrawParagraph("Styled paragraph", 20, 100, 200,
            new PdfParagraphOptions { Bold = true, Italic = italic });
        Assert.Contains($"/BaseFont /{expected}", Encoding.Latin1.GetString(document.ToBytes()));
    }

    [Fact]
    public void RichText_PreservesBoldAndItalicFonts()
    {
        var pdf = Render(new ElementDto
        {
            Type = "richtext", HtmlContent = "<p><b>Bold</b> and <i>italic</i></p>"
        });
        Assert.Contains("/BaseFont /Helvetica-Bold", pdf);
        Assert.Contains("/BaseFont /Helvetica-Oblique", pdf);
    }

    [Fact]
    public void Arabic_UsesBundledFont_AndRetainsLogicalText()
    {
        var loader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        Assert.True(loader.TryLoad("ar", out var font));
        const string text = "\u0645\u0631\u062d\u0628\u0627 \u0628\u0643\u0645 \u0641\u064a \u0645\u0633\u062a\u0646\u062f \u0627\u0644\u0627\u062e\u062a\u0628\u0627\u0631";
        Assert.All(text.Where(character => character != ' '), character => Assert.NotEqual(0, font!.GetGlyphId(character)));
        var document = new PdfDocument { FontLoader = loader };
        document.AddPage(595, 842).DrawParagraph(text, 30, 700, 500,
            new PdfParagraphOptions { Language = "ar", TextDirection = "rtl", FontSize = 16 });
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Contains("NotoSansArabic", pdf);
        Assert.Contains($"/ActualText <FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>", pdf);
        Assert.DoesNotContain("<0000> Tj", pdf);
    }

    [Fact]
    public void Radio_UsesCirclesAndRequestedSelection_WithoutTextMarkers()
    {
        var pdf = Render(new ElementDto { Type = "radio", FieldName = "delivery", Options = ["Email", "Postal"], SelectedValue = "Postal" });
        Assert.DoesNotContain("(o Email)", pdf);
        Assert.DoesNotContain("(* Postal)", pdf);
        Assert.Contains("(Email) Tj", pdf);
        Assert.Contains("(Postal) Tj", pdf);
        Assert.Contains("/V /R2", pdf);
    }

    [Theory]
    [InlineData("checked")]
    [InlineData("cross")]
    [InlineData("dot")]
    [InlineData("empty")]
    public void Checkbox_HasExplicitOffAndOnAppearances(string state)
    {
        var pdf = Render(new ElementDto { Type = "checkbox", FieldName = "approval", CheckState = state });
        Assert.Contains("/AP << /N << /Off", pdf);
        Assert.Contains("/Subtype /Form /BBox [0 0 14 14]", pdf);
        Assert.Contains(state == "empty" ? "/V /Off /AS /Off" : "/V /Yes /AS /Yes", pdf);
    }

    [Fact]
    public void Watermark_PreservesWeightOpacityAndCssRotationDirection()
    {
        var pdf = Render(new ElementDto
        {
            Type = "watermark", Content = "Draft", Width = 150, Height = 150,
            Style = new() { ["fontWeight"] = "bold", ["fontSize"] = 28, ["rotation"] = -35, ["opacity"] = 0.35 }
        });
        Assert.Contains("/ca 0.35", pdf);
        Assert.Contains("/BaseFont /Helvetica-Bold", pdf);
        Assert.Contains("0.819 0.574 -0.574 0.819", pdf);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TextDecorations_UseTheTextColor(bool underline, bool strike)
    {
        var document = new PdfDocument();
        document.AddPage(300, 200).DrawText("Colored decoration", 20, 100, new PdfDrawTextOptions
        {
            Underline = underline, Strikethrough = strike, FillColor = PdfColor.FromRgb(29, 78, 216)
        });
        var pdf = Encoding.Latin1.GetString(document.ToBytes());
        Assert.Contains("0.114 0.306 0.847 rg", pdf);
        Assert.Contains("0.114 0.306 0.847 RG", pdf);
    }

    private static string Render(ElementDto element)
    {
        element.Id = "visual-regression";
        if (element.Width <= 0) element.Width = 250;
        if (element.Height <= 0) element.Height = 100;
        return Encoding.Latin1.GetString(DesignJsonMapper.MapToPdfDocument(new DesignExportDto
        {
            Pages = [new PageDto { Id = "page", Elements = [element] }]
        }).ToBytes());
    }
}
