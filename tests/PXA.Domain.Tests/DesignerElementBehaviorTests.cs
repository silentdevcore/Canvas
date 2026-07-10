using PXA.Domain.ValueObjects;
using Xunit;

namespace PXA.Domain.Tests;

public sealed class DesignerElementBehaviorTests
{
    [Fact]
    public void MigratePropsToConfig_Text_MovesLegacyPropsIntoTypedConfig()
    {
        var element = new DesignerElement
        {
            Id = "text-1",
            Type = ElementType.Text,
            Props = new Dictionary<string, object>
            {
                ["FontFamily"] = "Inter",
                ["FontSize"] = 12d,
                ["Color"] = "#111111",
                ["Bold"] = true,
                ["MaxLines"] = 2,
            },
        };

        element.MigratePropsToConfig();

        Assert.NotNull(element.Text);
        Assert.Equal("Inter", element.Text.FontFamily);
        Assert.Equal(12d, element.Text.FontSize);
        Assert.Equal("#111111", element.Text.Color);
        Assert.True(element.Text.Bold);
        Assert.Equal(2, element.Text.MaxLines);
        Assert.Empty(element.Props);
    }

    [Fact]
    public void MigratePropsToConfig_PageNumber_ParsesCamelCaseAndPascalCaseProps()
    {
        var element = new DesignerElement
        {
            Id = "page-number-1",
            Type = ElementType.PageNumber,
            Props = new Dictionary<string, object>
            {
                ["format"] = "Page {page} of {total}",
                ["StartNumber"] = 3L,
                ["fontSize"] = "10.5",
                ["Color"] = "#333333",
            },
        };

        element.MigratePropsToConfig();

        Assert.NotNull(element.PageNumber);
        Assert.Equal("Page {page} of {total}", element.PageNumber.Format);
        Assert.Equal(3, element.PageNumber.StartNumber);
        Assert.Equal(10.5d, element.PageNumber.FontSize);
        Assert.Equal("#333333", element.PageNumber.Color);
        Assert.Empty(element.Props);
    }
}
