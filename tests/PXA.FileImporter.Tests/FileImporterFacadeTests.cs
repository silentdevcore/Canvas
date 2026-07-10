using System.IO.Compression;
using System.Text;
using PXA.FileImporter;
using SkiaSharp;

namespace PXA.FileImporter.Tests;

public sealed class FileImporterFacadeTests
{
    [Fact]
    public async Task DocImporter_RejectsInvalidDoc()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a compound document"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new DocFileImporter().ImportAsync(stream, "bad.doc"));
    }

    [Fact]
    public async Task OdtImporter_ImportsTextDesign()
    {
        await using var stream = new MemoryStream(MakeMinimalOdt("Hello PXA"));

        var design = await new OdtFileImporter().ImportAsync(stream, "sample.odt");

        Assert.Equal("sample.odt", design.Name);
        Assert.Equal(595, design.PageSettings!.Width);
        Assert.Equal("text", design.Pages[0].Elements[0].Type);
        Assert.Equal("Hello PXA", design.Pages[0].Elements[0].Content);
    }

    [Fact]
    public async Task SvgImporter_ImportsSvgDesign()
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
    public async Task ImageImporter_ImportsImageDesign()
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

    private static byte[] MakeMinimalOdt(string text)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var content = zip.CreateEntry("content.xml");
            using var writer = new StreamWriter(content.Open(), Encoding.UTF8);
            writer.Write($$"""
                <?xml version="1.0" encoding="UTF-8"?>
                <document-content xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <text:body>
                    <text:text>
                      <text:p>{{System.Security.SecurityElement.Escape(text)}}</text:p>
                    </text:text>
                  </text:body>
                </document-content>
                """);
        }

        return stream.ToArray();
    }
}
