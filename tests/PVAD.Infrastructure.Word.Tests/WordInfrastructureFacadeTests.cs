using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Word;

namespace PVAD.Infrastructure.Word.Tests;

public sealed class WordInfrastructureFacadeTests
{
    [Fact]
    public void Exporter_ExportsDocxFromPvadCoreDesign()
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
    public void Capabilities_MirrorPvadWordRendererCapabilities()
    {
        var capabilities = new WordRendererCapabilities();

        Assert.Equal("word", capabilities.RendererKey);
        Assert.True(capabilities.SupportsBookmarks);
        Assert.True(capabilities.SupportsWatermarks);
        Assert.False(capabilities.SupportsCompression);
        Assert.False(capabilities.SupportsPageRotation);
    }

    [Theory]
    [InlineData(null, "PVADE")]
    [InlineData("PXA", "PXA")]
    [InlineData("Example Author", "Example Author")]
    public void Exporter_DefaultsNewCommentAndRevisionAuthorsWithoutRewritingExplicitAuthors(
        string? suppliedAuthor, string expectedAuthor)
    {
        var design = BuildDesign();
        var text = design.Pages[0].Elements[0];
        text.RevisionType = "insert";
        text.RevisionAuthor = suppliedAuthor;
        text.RevisionDate = "2026-01-01T00:00:00Z";
        design.Pages[0].Elements.Add(new ElementDto
        {
            Id = "comment-1", Type = "comment", X = 36, Y = 90,
            Width = 240, Height = 32, CommentText = "Example comment",
            CommentAuthor = suppliedAuthor, CommentDate = "2026-01-01T00:00:00Z",
        });

        var bytes = new WordDocumentExporter().Export(design);
        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var main = document.MainDocumentPart!;
        var comment = Assert.Single(main.WordprocessingCommentsPart!.Comments!.Elements<Comment>());
        Assert.Equal(expectedAuthor, comment.Author?.Value);
        var revision = Assert.Single(main.Document!.Descendants<InsertedRun>());
        Assert.Equal(expectedAuthor, revision.Author?.Value);
    }

    [Fact]
    public void DigitalSigningFacade_RejectsUnsignedCertificateBytesLikePvadService()
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
