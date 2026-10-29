using PVAD.Application.UseCases;
using PVAD.Core.Abstractions;
using PVAD.Infrastructure.Converters;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Infrastructure.Word;

namespace PVAD.Export.Tests;

public sealed class PvadExportCompositionTests
{
    [Fact]
    public void Execute_ResolvesPvadHtmlExporter_ByFormatKey()
    {
        var useCase = new ExportDocumentUseCase([new HtmlDocumentExporter()]);

        var result = useCase.Execute(new ExportDocumentRequest(CreateDesign(), "HTML"));

        Assert.NotEmpty(result.Data);
        Assert.Equal("text/html; charset=utf-8", result.MimeType);
        Assert.EndsWith(".html", result.FileName);
    }

    [Fact]
    public void GetSupportedFormats_ReturnsPvadExporterRegistrations()
    {
        IDocumentExporter[] exporters =
        [
            new HtmlDocumentExporter(),
            new XmlDocumentExporter(),
            new SvgDocumentExporter(),
            new CsvDocumentExporter(),
            new MarkdownDocumentExporter(),
            new ImageDocumentExporter(),
            new JpegDocumentExporter(),
            new WordDocumentExporter(),
            new ExcelDocumentExporter(),
        ];

        var formats = new ExportDocumentUseCase(exporters).GetSupportedFormats().ToList();

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
    public void PvadWordAndExcelExporters_RenderDesignBytes()
    {
        var design = CreateDesign();

        var wordBytes = new WordDocumentExporter().Export(design);
        var excelBytes = new ExcelDocumentExporter().Export(design);

        Assert.StartsWith("PK", System.Text.Encoding.ASCII.GetString(wordBytes, 0, 2));
        Assert.StartsWith("PK", System.Text.Encoding.ASCII.GetString(excelBytes, 0, 2));
    }

    private static DesignExportDto CreateDesign() => new()
    {
        Id = "pxa-export",
        Name = "PXA Export",
        Pages =
        [
            new PageDto
            {
                Id = "page-1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "title",
                        Type = "text",
                        X = 24,
                        Y = 24,
                        Width = 160,
                        Height = 32,
                        Content = "Power Dox Automation",
                    },
                    new ElementDto
                    {
                        Id = "table",
                        Type = "table",
                        X = 24,
                        Y = 72,
                        Width = 280,
                        Height = 80,
                        Name = "Data",
                        CellData =
                        [
                            ["Product", "PXA"],
                            ["Status", "Active"],
                        ],
                    },
                ],
            },
        ],
        PageSettings = new PageSettingsDto
        {
            Width = 595,
            Height = 842,
            Orientation = "portrait",
        },
    };
}
