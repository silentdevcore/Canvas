using System.Text;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Pdf;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class DateLayoutParityTests
{
    [Fact]
    public void DatesWrapAndUseParagraphLineHeightWithoutChangingTheSource()
    {
        var date = Date();
        var before = JsonSerializer.Serialize(date);
        var lines = Render(date).CreateDesignSnapshot().Pages[0].Elements.Where(element => element.Kind == "text").ToArray();
        Assert.Equal(new[] { "Friday 13 November", "2026" }, lines.Select(line => line.Text));
        Assert.Equal(14.4, lines[0].Y - lines[1].Y, 6);
        Assert.Equal(before, JsonSerializer.Serialize(date));
    }

    [Fact]
    public void CenterAlignmentUsesThePaddedContentBox()
    {
        var date = Date();
        date.Width = 180;
        date.Style = new() { ["fontSize"] = 12, ["paddingLeft"] = 24, ["paddingRight"] = 4,
            ["paddingTop"] = 10, ["textAlign"] = "center" };
        var lines = Render(date).CreateDesignSnapshot().Pages[0].Elements.Where(element => element.Kind == "text").ToArray();
        Assert.Equal(48.24, lines[0].X, 6);
        Assert.Equal(261.36, lines[0].Y, 6);
    }

    [Fact]
    public void RightAlignmentRespectsExplicitZeroPadding()
    {
        var date = Date();
        date.Width = 180;
        date.Content = "Friday";
        date.Style = new() { ["fontSize"] = 12, ["padding"] = 9, ["paddingRight"] = 0, ["textAlign"] = "right" };
        var line = Assert.Single(Render(date).CreateDesignSnapshot().Pages[0].Elements);
        Assert.Equal(162.56, line.X, 6);
    }

    [Theory]
    [InlineData(0, 14.4)]
    [InlineData(1.8, 21.6)]
    [InlineData(22, 22)]
    public void UnitlessAndAbsoluteLineHeightMatchTheUi(double requested, double expected)
    {
        var date = Date();
        date.Style = new() { ["fontSize"] = 12, ["lineHeight"] = requested };
        var lines = Render(date).CreateDesignSnapshot().Pages[0].Elements.Where(element => element.Kind == "text").ToArray();
        Assert.Equal(expected, lines[0].Y - lines[1].Y, 6);
    }

    [Fact]
    public void EmptyDatesStillPaintTheirBackgroundAndRoundedBorder()
    {
        var date = Date();
        date.Content = "";
        date.Style = new() { ["backgroundColor"] = "#e0f2fe", ["borderWidth"] = 2,
            ["borderColor"] = "#2563eb", ["borderRadius"] = 9 };
        var shapes = Render(date).CreateDesignSnapshot().Pages[0].Elements;
        Assert.Equal(2, shapes.Count);
        Assert.Contains(shapes, shape => shape.FillColor?.ToLowerInvariant() == "#e0f2fe");
        Assert.Contains(shapes, shape => shape.CornerRadius == 9 && shape.StrokeWidth == 2);
        Assert.DoesNotContain(shapes, shape => shape.Kind == "text");
    }

    [Fact]
    public void ExplicitZeroSideWidthsDoNotFallBackToTheUniformBorder()
    {
        var date = Date();
        date.Style = new() { ["borderWidth"] = 4, ["borderColor"] = "#dc2626",
            ["borderTopWidth"] = 0, ["borderRightWidth"] = 0, ["borderBottomWidth"] = 0, ["borderLeftWidth"] = 0 };
        Assert.All(Render(date).CreateDesignSnapshot().Pages[0].Elements, shape => Assert.Equal("text", shape.Kind));
    }

    [Fact]
    public void PerSideDashStylesAreRetained()
    {
        var date = Date();
        date.Style = new() { ["borderBottomWidth"] = 2, ["borderBottomStyle"] = "dashed", ["borderColor"] = "#15803d" };
        var document = Render(date);
        Assert.Single(document.CreateDesignSnapshot().Pages[0].Elements, shape => shape.Kind == "line");
        Assert.Contains("[8 4] 0 d", Encoding.Latin1.GetString(document.ToBytes()));
    }

    [Fact]
    public void ShortFieldsDoNotSilentlyClipOrResizeParagraphContent()
    {
        var date = Date();
        date.Height = 12;
        var lines = Render(date).CreateDesignSnapshot().Pages[0].Elements;
        Assert.Equal(2, lines.Count);
        Assert.True(lines[1].Y < 300 - date.Y - date.Height);
        Assert.Equal(12, date.Height);
    }

    private static ElementDto Date() => new() { Id = "date", Type = "date", DateMode = "static",
        X = 20, Y = 20, Width = 120, Height = 70, Content = "Friday 13 November 2026" };

    private static PdfDocument Render(ElementDto date) => DesignJsonMapper.MapToPdfDocument(new()
    {
        PageSettings = new() { Width = 400, Height = 300, Orientation = "landscape" },
        Pages = [new() { Id = "page", Elements = [date] }]
    });
}
