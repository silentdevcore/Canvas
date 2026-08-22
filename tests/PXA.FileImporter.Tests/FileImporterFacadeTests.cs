using System.IO.Compression;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using P = DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using PXA.FileImporter;
using PXA.Pdf;
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
    public async Task DocxImporter_PreservesSectionPageLayouts()
    {
        await using var stream = new MemoryStream(MakeMixedLayoutDocx());

        var design = await new DocxFileImporter().ImportAsync(stream, "mixed.docx");

        Assert.Equal(2, design.Pages.Count);
        Assert.Equal((595d, 842d), (design.Pages[0].Layout!.Width!.Value, design.Pages[0].Layout!.Height!.Value));
        Assert.Equal((842d, 595d), (design.Pages[1].Layout!.Width!.Value, design.Pages[1].Layout!.Height!.Value));
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
    public async Task OdtImporter_PreservesNamedPageLayouts()
    {
        await using var stream = new MemoryStream(MakeMixedLayoutOdt());

        var design = await new OdtFileImporter().ImportAsync(stream, "mixed.odt");

        Assert.Equal(2, design.Pages.Count);
        Assert.Equal("portrait", design.Pages[0].Layout!.Orientation);
        Assert.Equal("landscape", design.Pages[1].Layout!.Orientation);
        Assert.Equal(842, design.Pages[1].Layout!.Width!.Value, 0);
        Assert.Equal(595, design.Pages[1].Layout!.Height!.Value, 0);
    }

    [Fact]
    public async Task PdfImporter_PreservesEverySourcePageBox()
    {
#pragma warning disable PXA0001
        var pdf = new PdfDocument();
#pragma warning restore PXA0001
        pdf.AddPage(595, 842);
        var landscape = pdf.AddPage(842, 595);
        landscape.SetPageBoundary(PdfPageBoundary.CropBox, new PdfPoint(10, 20), new PdfPoint(810, 570));
        await using var stream = new MemoryStream(pdf.ToBytes());

        var design = await new PdfFileImporter().ImportAsync(stream, "mixed.pdf");

        Assert.Equal(2, design.Pages.Count);
        Assert.Equal((595d, 842d), (design.Pages[0].Layout!.Width!.Value, design.Pages[0].Layout!.Height!.Value));
        Assert.Equal((800d, 550d), (design.Pages[1].Layout!.Width!.Value, design.Pages[1].Layout!.Height!.Value));
        Assert.True(design.Pages[1].Extensions!.ContainsKey("pdfPageBoxes"));
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
                        new PageSize { Width = 11900U, Height = 16840U },
                        new PageMargin { Top = 720, Right = 720U, Bottom = 720, Left = 720U })));
        }

        return stream.ToArray();
    }

    private static byte[] MakeMixedLayoutDocx()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(
            stream,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
            autoSave: true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(
                    new ParagraphProperties(new SectionProperties(
                        new PageSize { Width = 11900U, Height = 16840U, Orient = PageOrientationValues.Portrait })),
                    new Run(new Text("Portrait"))),
                new Paragraph(new Run(new Text("Landscape"))),
                new SectionProperties(
                    new PageSize { Width = 16840U, Height = 11900U, Orient = PageOrientationValues.Landscape })));
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

    private static byte[] MakeMixedLayoutOdt()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("styles.xml").Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <office:document-styles xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0">
                      <office:automatic-styles>
                        <style:page-layout style:name="PL1"><style:page-layout-properties fo:page-width="20.99cm" fo:page-height="29.70cm" style:print-orientation="portrait"/></style:page-layout>
                        <style:page-layout style:name="PL2"><style:page-layout-properties fo:page-width="29.70cm" fo:page-height="20.99cm" style:print-orientation="landscape"/></style:page-layout>
                      </office:automatic-styles>
                      <office:master-styles>
                        <style:master-page style:name="P1" style:page-layout-name="PL1"/>
                        <style:master-page style:name="P2" style:page-layout-name="PL2"/>
                      </office:master-styles>
                    </office:document-styles>
                    """);
            }
            using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), Encoding.UTF8))
            {
                writer.Write("""
                    <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0">
                      <office:automatic-styles>
                        <style:style style:name="Start1" style:family="paragraph" style:master-page-name="P1"/>
                        <style:style style:name="Start2" style:family="paragraph" style:master-page-name="P2"><style:paragraph-properties fo:break-before="page"/></style:style>
                      </office:automatic-styles>
                      <office:body><office:text>
                        <text:p text:style-name="Start1"/><text:p>Portrait</text:p>
                        <text:p text:style-name="Start2"/><text:p>Landscape</text:p>
                      </office:text></office:body>
                    </office:document-content>
                    """);
            }
        }
        return stream.ToArray();
    }
}
