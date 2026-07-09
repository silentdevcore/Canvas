using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public sealed class PrimitiveAdaptersTests
{
    [Fact]
    public void PdfPoint_MapsToAndFromCanvasPoint()
    {
        var point = new PdfPoint(12.5, 34.75);

        var canvasPoint = point.ToCanvas();
        var roundTrip = canvasPoint.ToPxa();

        Assert.Equal(12.5, canvasPoint.X);
        Assert.Equal(34.75, canvasPoint.Y);
        Assert.Equal(point.X, roundTrip.X);
        Assert.Equal(point.Y, roundTrip.Y);
    }

    [Theory]
    [InlineData(PdfTextAlignment.Left)]
    [InlineData(PdfTextAlignment.Center)]
    [InlineData(PdfTextAlignment.Right)]
    [InlineData(PdfTextAlignment.Justify)]
    public void PdfTextAlignment_MapsByName(PdfTextAlignment alignment)
    {
        var canvasAlignment = alignment.ToCanvas();
        var roundTrip = canvasAlignment.ToPxa();

        Assert.Equal(alignment.ToString(), canvasAlignment.ToString());
        Assert.Equal(alignment, roundTrip);
    }

    [Theory]
    [InlineData(PdfVerticalAlignment.Top)]
    [InlineData(PdfVerticalAlignment.Middle)]
    [InlineData(PdfVerticalAlignment.Bottom)]
    public void PdfVerticalAlignment_MapsByName(PdfVerticalAlignment alignment)
    {
        var canvasAlignment = alignment.ToCanvas();
        var roundTrip = canvasAlignment.ToPxa();

        Assert.Equal(alignment.ToString(), canvasAlignment.ToString());
        Assert.Equal(alignment, roundTrip);
    }

    [Fact]
    public void ExportFormat_UsesCanvasCompatibleKeys()
    {
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Html, ExportFormat.Html);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Xml, ExportFormat.Xml);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Word, ExportFormat.Word);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Excel, ExportFormat.Excel);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Png, ExportFormat.Png);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Jpeg, ExportFormat.Jpeg);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Svg, ExportFormat.Svg);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Csv, ExportFormat.Csv);
        Assert.Equal(Canvas.Core.Primitives.ExportFormat.Markdown, ExportFormat.Markdown);
    }
}
