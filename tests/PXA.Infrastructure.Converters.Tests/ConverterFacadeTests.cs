using System.Text;
using PXA.Core.Contracts;
using PXA.Infrastructure.Converters;

namespace PXA.Infrastructure.Converters.Tests;

public sealed class ConverterFacadeTests
{
    [Theory]
    [MemberData(nameof(TextExporterCases))]
    public void TextExporters_ExportPxaCoreDesign(DocumentExporter exporter, string expectedKey, string expectedExtension, string expectedNeedle)
    {
        var bytes = exporter.Export(BuildDesign());
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Equal(expectedKey, exporter.FormatKey);
        Assert.Equal(expectedExtension, exporter.FileExtension);
        Assert.Contains(expectedNeedle, text);
    }

    public static IEnumerable<object[]> TextExporterCases()
    {
        yield return [new HtmlDocumentExporter(), "html", ".html", "Hello PXA"];
        yield return [new MarkdownDocumentExporter(), "md", ".md", "Hello PXA"];
        yield return [new CsvDocumentExporter(), "csv", ".csv", "Hello PXA"];
        yield return [new XmlDocumentExporter(), "xml", ".xml", "CanvasDocument"];
        yield return [new SvgDocumentExporter(), "svg", ".svg", "<svg"];
    }

    [Fact]
    public void ImageExporter_ExportsPngFromPxaCoreDesign()
    {
        var bytes = new ImageDocumentExporter().Export(BuildDesign(), new ExportOptions(Dpi: 72));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
    }

    [Fact]
    public void OdtExporter_ExportsOdtPackageFromPxaCoreDesign()
    {
        var exporter = new OdtDocumentExporter();

        var bytes = exporter.Export(BuildDesign());

        Assert.Equal("odt", exporter.FormatKey);
        Assert.Equal(".odt", exporter.FileExtension);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void ConverterCapabilities_MirrorCanvasCapabilities()
    {
        var capabilities = new ConverterRendererCapabilities();

        Assert.Equal("converter", capabilities.RendererKey);
        Assert.False(capabilities.SupportsBookmarks);
        Assert.False(capabilities.SupportsCompression);
    }

    [Fact]
    public void CsvCapabilities_ArePxaCapabilities()
    {
        var capabilities = new CsvDocumentExporter().Capabilities;

        Assert.False(capabilities.SupportsImages);
        Assert.False(capabilities.SupportsRichText);
        Assert.False(capabilities.SupportsFormFields);
    }

    private static DesignExportDto BuildDesign() => new()
    {
        Id = "design-1",
        Name = "Converter Design",
        PageSettings = new PageSettingsDto { Width = 300, Height = 180, Orientation = "landscape" },
        Pages =
        [
            new PageDto
            {
                Id = "page-1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "text-1",
                        Type = "text",
                        Content = "Hello PXA",
                        X = 20,
                        Y = 20,
                        Width = 140,
                        Height = 30,
                        Style = new Dictionary<string, object>
                        {
                            ["fontSize"] = 16,
                            ["fontWeight"] = "bold",
                            ["color"] = "#111827",
                        },
                    },
                    new ElementDto
                    {
                        Id = "table-1",
                        Type = "table",
                        Name = "Data",
                        X = 20,
                        Y = 60,
                        Width = 180,
                        Height = 60,
                        CellData =
                        [
                            ["Name", "Amount"],
                            ["A", "42"],
                        ],
                    },
                ],
            },
        ],
    };
}
