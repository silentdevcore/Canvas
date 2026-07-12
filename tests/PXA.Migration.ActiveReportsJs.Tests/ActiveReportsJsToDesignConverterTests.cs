using PXA.Migration.ActiveReportsJs;

namespace PXA.Migration.ActiveReportsJs.Tests;

public sealed class ActiveReportsJsToDesignConverterTests
{
    private const string Sample = """
        {
          "reportType": "ActiveReportsJS",
          "name": "Invoice JS",
          "page": { "width": "8.5in", "height": "11in" },
          "body": {
            "reportItems": [
              {
                "type": "textbox",
                "name": "title",
                "left": "1in",
                "top": "0.5in",
                "width": "4in",
                "height": "0.4in",
                "value": "Invoice",
                "style": { "fontFamily": "Arial", "fontSize": 18, "bold": true, "textAlign": "Center", "color": "#0066CC" }
              },
              { "type": "textbox", "name": "customer", "left": 72, "top": 90, "width": 200, "height": 20, "value": "{Customers.Name}" },
              { "type": "line", "name": "rule", "left": 72, "top": 120, "width": 400, "height": 1, "style": { "color": "#808080", "strokeWidth": 2 } },
              { "type": "table", "name": "items", "left": 72, "top": 150, "width": 300, "height": 80,
                "columns": [{ "width": 160 }, { "width": 140 }],
                "rows": [["Item", "Amount"], ["Widget", "{Items.Amount}"]]
              },
              { "type": "chart", "name": "chart1", "left": 72, "top": 250, "width": 200, "height": 120 }
            ]
          }
        }
        """;

    private static ActiveReportsJsConvertResult Convert(string json) =>
        new ActiveReportsJsToDesignConverter().Convert(json);

    [Fact]
    public void LooksLikeActiveReportsJs_RequiresExplicitMarker()
    {
        Assert.True(ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(Sample));
        Assert.False(ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs("""{ "rows": [1, 2, 3] }"""));
        Assert.False(ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs("""<Report />"""));
    }

    [Fact]
    public void Convert_MapsBasicItemsAndPageSize()
    {
        var result = Convert(Sample);

        Assert.Equal("Invoice JS", result.Design.Name);
        Assert.Equal(612, result.Design.PageSettings!.Width, 1);
        Assert.Equal(792, result.Design.PageSettings.Height, 1);
        Assert.Equal(5, result.Design.Pages[0].Elements.Count);

        var title = result.Design.Pages[0].Elements.Single(e => e.Name == "title");
        Assert.Equal("text", title.Type);
        Assert.Equal(72, title.X, 1);
        Assert.Equal(36, title.Y, 1);
        Assert.Equal("bold", title.Style!["fontWeight"]);
        Assert.Equal("center", title.Style["textAlign"]);

        var customer = result.Design.Pages[0].Elements.Single(e => e.Name == "customer");
        Assert.Equal("Name", customer.Binding);
        Assert.Equal("{{Name}}", customer.Content);

        var table = result.Design.Pages[0].Elements.Single(e => e.Name == "items");
        Assert.Equal("table", table.Type);
        Assert.Equal(new[] { "Item", "Amount" }, table.CellData![0]);
        Assert.Equal(160, table.ColumnWidths![0]);

        var placeholder = result.Design.Pages[0].Elements.Single(e => e.Name == "chart1");
        Assert.Equal("text", placeholder.Type);
        Assert.Contains("chart", placeholder.Content);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGARJS011");
    }

    [Fact]
    public void Convert_InvalidMarker_Throws()
    {
        Assert.Throws<ArgumentException>(() => Convert("""{ "name": "Data only" }"""));
    }
}
