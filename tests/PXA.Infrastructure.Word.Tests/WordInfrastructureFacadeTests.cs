using DocumentFormat.OpenXml.Packaging;
using PXA.Core.Contracts;
using PXA.Infrastructure.Word;

namespace PXA.Infrastructure.Word.Tests;

public sealed class WordInfrastructureFacadeTests
{
    [Fact]
    public void Exporter_ExportsDocxFromPxaCoreDesign()
    {
        var exporter = new WordDocumentExporter();

        var bytes = exporter.Export(BuildDesign(), new ExportOptions(WordFidelityV2: true));

        Assert.True(bytes.Length > 0);
        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.NotNull(document.MainDocumentPart);
        Assert.Equal("word", exporter.FormatKey);
        Assert.Equal(".docx", exporter.FileExtension);
        Assert.True(exporter.Capabilities.SupportsImages);
    }

    [Fact]
    public void Capabilities_MirrorPxaWordRendererCapabilities()
    {
        var capabilities = new WordRendererCapabilities();

        Assert.Equal("word", capabilities.RendererKey);
        Assert.True(capabilities.SupportsBookmarks);
        Assert.True(capabilities.SupportsWatermarks);
        Assert.False(capabilities.SupportsCompression);
        Assert.False(capabilities.SupportsPageRotation);
    }

    [Fact]
    public void DigitalSigningFacade_RejectsUnsignedCertificateBytesLikePxaService()
    {
        using var stream = new MemoryStream(new WordDocumentExporter().Export(BuildDesign()));

        Assert.ThrowsAny<Exception>(() => DigitalSigningService.SignDocx(stream, [0x00, 0x01, 0x02]));
    }

    private static DesignExportDto BuildDesign() => new()
    {
        Id = "word-design",
        Name = "Word Design",
        PageSettings = new PageSettingsDto
        {
            Width = 595,
            Height = 842,
            Orientation = "portrait",
            Metadata = new PdfMetadataDto { Title = "Word Design" },
        },
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
                        Content = "Hello Word",
                        X = 36,
                        Y = 36,
                        Width = 240,
                        Height = 32,
                        Style = new Dictionary<string, object>
                        {
                            ["fontSize"] = 14,
                            ["color"] = "#111827",
                        },
                    }
                ],
            }
        ],
    };
}
