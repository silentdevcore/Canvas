using System.Text;
using PVAD.Core.Contracts;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class OptionListRenderingTests
{
    [Fact]
    public void PlainList_HasNoInteractiveOverlay_AndUsesTheRequestedMarker()
    {
        var list = List();
        list.ListStyle = "decimal";
        list.StartNumber = 4;
        var pdf = Render(list);
        Assert.DoesNotContain("/FT /Ch", pdf);
        Assert.Contains("4. Alpha", pdf);
        Assert.Contains("5. Beta", pdf);
    }

    [Theory]
    [InlineData("fieldName")]
    [InlineData("fieldLabel")]
    [InlineData("selectedValue")]
    [InlineData("multiSelect")]
    [InlineData("accessibility")]
    public void FormList_RetainsItsChoiceWidget_WithoutDuplicatePageText(string intent)
    {
        var list = List();
        switch (intent)
        {
            case "fieldName": list.FieldName = "regions"; break;
            case "fieldLabel": list.FieldLabel = "Regions"; break;
            case "selectedValue": list.SelectedValue = "Alpha"; break;
            case "multiSelect": list.MultiSelect = false; break;
            case "accessibility": list.Accessibility = new() { Role = "Form" }; break;
        }
        var pdf = Render(list);
        Assert.Contains("/FT /Ch /Ff 0 /F 4", pdf);
        Assert.Contains("/Opt [", pdf);
        Assert.DoesNotContain("Alpha) Tj", pdf);
    }

    private static ElementDto List() => new()
    {
        Id = "choices", Type = "optionlist", X = 20, Y = 20, Width = 200, Height = 80,
        Options = ["Alpha", "Beta"]
    };

    private static string Render(ElementDto list) => Encoding.Latin1.GetString(
        DesignJsonMapper.MapToPdfDocument(new DesignExportDto
        {
            Pages = [new PageDto { Id = "page", Elements = [list] }]
        }).ToBytes());
}
