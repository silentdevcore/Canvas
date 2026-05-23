using Canvas.Application.UseCases;
using Canvas.Infrastructure.Converters;

namespace Canvas.Export.Tests;

public class ExportDocumentUseCaseTests
{
    private static DesignExportDto SimpleDesign() => new()
    {
        Id = "d1", Name = "Demo",
        Pages = [new PageDto
        {
            Id = "p1",
            Elements = [new ElementDto { Id = "e1", Type = "text", X = 0, Y = 0, Width = 100, Height = 20, Content = "Hi" }],
        }],
    };

    [Fact]
    public void Execute_ResolvesHtmlExporter_ByFormatKey()
    {
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);
        var result  = useCase.Execute(new ExportDocumentRequest(SimpleDesign(), "html"));

        Assert.NotEmpty(result.Data);
        Assert.Equal("text/html; charset=utf-8", result.MimeType);
        Assert.EndsWith(".html", result.FileName);
    }

    [Fact]
    public void Execute_IsCaseInsensitive_ForFormatKey()
    {
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);
        var result  = useCase.Execute(new ExportDocumentRequest(SimpleDesign(), "HTML"));

        Assert.Equal("text/html; charset=utf-8", result.MimeType);
    }

    [Fact]
    public void Execute_Throws_ForUnknownFormat()
    {
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);

        var ex = Assert.Throws<NotSupportedException>(
            () => useCase.Execute(new ExportDocumentRequest(SimpleDesign(), "pdf2")));

        Assert.Contains("pdf2", ex.Message);
        Assert.Contains("html", ex.Message);
    }

    [Fact]
    public void Execute_Throws_WhenRequestIsNull()
    {
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!));
    }

    [Fact]
    public void GetSupportedFormats_ReturnsAllRegisteredExporters()
    {
        var exporters = new Canvas.Core.Abstractions.IDocumentExporter[]
        {
            new HtmlDocumentExporter(),
            new XmlDocumentExporter(),
            new SvgDocumentExporter(),
            new CsvDocumentExporter(),
            new MarkdownDocumentExporter(),
            new ImageDocumentExporter(),
            new JpegDocumentExporter(),
            new Canvas.Infrastructure.Word.WordDocumentExporter(),
            new Canvas.Infrastructure.Sheet.ExcelDocumentExporter(),
        };
        var useCase = new ExportDocumentUseCase(exporters);
        var formats = useCase.GetSupportedFormats().ToList();

        Assert.Equal(9, formats.Count);
        Assert.Contains(formats, f => f.Key == "html");
        Assert.Contains(formats, f => f.Key == "xml");
        Assert.Contains(formats, f => f.Key == "svg");
        Assert.Contains(formats, f => f.Key == "csv");
        Assert.Contains(formats, f => f.Key == "md");
        Assert.Contains(formats, f => f.Key == "png");
        Assert.Contains(formats, f => f.Key == "jpeg");
        Assert.Contains(formats, f => f.Key == "word");
        Assert.Contains(formats, f => f.Key == "excel");
    }

    [Fact]
    public void GetSupportedFormats_IncludesCapabilities()
    {
        var useCase  = new ExportDocumentUseCase([new CsvDocumentExporter()]);
        var csvInfo  = useCase.GetSupportedFormats().Single();

        Assert.False(csvInfo.Capabilities.SupportsImages);
        Assert.False(csvInfo.Capabilities.SupportsRichText);
        Assert.False(csvInfo.Capabilities.SupportsFormFields);
        Assert.True(csvInfo.Capabilities.SupportsMultiPage);
    }

    [Fact]
    public void FileName_Uses_DesignName()
    {
        var design  = SimpleDesign();
        design.Name = "My Report";
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);
        var result  = useCase.Execute(new ExportDocumentRequest(design, "html"));

        Assert.StartsWith("My Report", result.FileName);
    }
}
