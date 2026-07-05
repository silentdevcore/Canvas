using System.Text;
using PXA.FileImporter;
using SkiaSharp;

namespace PXA.FileImporter.Tests;

public sealed class FileImporterFacadeTests
{
    [Fact]
    public async Task SvgImporter_DelegatesToCanvasImporter()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">
              <rect x="10" y="20" width="80" height="40" fill="#336699" />
              <text x="12" y="80">PXA</text>
            </svg>
            """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));

        var design = await new SvgFileImporter().ImportAsync(stream, "sample");

        Assert.Equal("sample", design.Name);
        Assert.Equal(200, design.PageSettings!.Width);
        Assert.Equal(100, design.PageSettings.Height);
        Assert.NotEmpty(design.Pages[0].Elements);
    }

    [Fact]
    public async Task ImageImporter_DelegatesToCanvasImporter()
    {
        await using var stream = new MemoryStream(MakeImage(120, 80));

        var design = await new ImageFileImporter().ImportAsync(stream, "sample.png");

        Assert.Equal("sample", design.Name);
        Assert.Equal("landscape", design.PageSettings!.Orientation);
        Assert.Equal("image", design.Pages[0].Elements[0].Type);
    }

    private static byte[] MakeImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.CornflowerBlue };
            canvas.DrawRect(0, 0, width, height, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
