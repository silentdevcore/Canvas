using System.IO.Compression;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using PXA.FileImporter;
using SkiaSharp;

namespace PXA.FileImporter.Tests;

public sealed class FileImporterFacadeTests
{
    [Fact]
    public async Task DocxImporter_ImportsTextDesign()
    {
        await using var stream = new MemoryStream(MakeMinimalDocx("Hello DOCX"));

        var design = await new DocxFileImporter().ImportAsync(stream, "sample.docx");

        Assert.Equal("sample.docx", design.Name);
        Assert.Equal(595, design.PageSettings!.Width);
        Assert.Equal("text", design.Pages[0].Elements[0].Type);
        Assert.Equal("Hello DOCX", design.Pages[0].Elements[0].Content);
    }

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
    public async Task PptxImporter_ImportsSlideTextDesign()
    {
        await using var stream = new MemoryStream(MakeMinimalPptx("Hello PPTX"));

        var design = await new PptxFileImporter().ImportAsync(stream, "sample.pptx");

        Assert.Equal("sample.pptx", design.Name);
        Assert.Single(design.Pages);
        Assert.Equal("text", design.Pages[0].Elements[0].Type);
        Assert.Equal("Hello PPTX", design.Pages[0].Elements[0].Content);
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

    private static byte[] MakeMinimalDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
            stream,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
            autoSave: true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text(text))),
                    new SectionProperties(
                        new PageSize { Width = 8925U, Height = 12630U },
                        new PageMargin { Top = 720, Right = 720U, Bottom = 720, Left = 720U })));
        }

        return stream.ToArray();
    }

    private static byte[] MakeMinimalPptx(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = PresentationDocument.Create(
            stream,
            DocumentFormat.OpenXml.PresentationDocumentType.Presentation,
            autoSave: true))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation
            {
                SlideSize = new P.SlideSize { Cx = 9144000, Cy = 6858000 },
                SlideIdList = new P.SlideIdList()
            };

            var slidePart = presentationPart.AddNewPart<SlidePart>("rId1");
            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(
                    new P.ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.GroupShapeProperties(new A.TransformGroup()),
                        new P.Shape(
                            new P.NonVisualShapeProperties(
                                new P.NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                new P.ApplicationNonVisualDrawingProperties()),
                            new P.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 914400, Y = 914400 },
                                    new A.Extents { Cx = 3657600, Cy = 914400 }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                {
                                    Preset = A.ShapeTypeValues.Rectangle
                                }),
                            new P.TextBody(
                                new A.BodyProperties(),
                                new A.ListStyle(),
                                new A.Paragraph(new A.Run(new A.Text(text))))))));

            presentationPart.Presentation.SlideIdList.Append(
                new P.SlideId { Id = 256U, RelationshipId = "rId1" });
        }

        return stream.ToArray();
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
