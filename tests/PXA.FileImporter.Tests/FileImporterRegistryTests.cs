using PXA.FileImporter;

namespace PXA.FileImporter.Tests;

public sealed class FileImporterRegistryTests
{
    public static IEnumerable<object[]> ImporterKeys()
    {
        yield return [FileImporterKeys.Doc, typeof(DocFileImporter)];
        yield return [FileImporterKeys.Docx, typeof(DocxFileImporter)];
        yield return [FileImporterKeys.Image, typeof(ImageFileImporter)];
        yield return [FileImporterKeys.Markdown, typeof(MarkdownFileImporter)];
        yield return [FileImporterKeys.Odt, typeof(OdtFileImporter)];
        yield return [FileImporterKeys.Pdf, typeof(PdfFileImporter)];
        yield return [FileImporterKeys.Pptx, typeof(PptxFileImporter)];
        yield return [FileImporterKeys.Svg, typeof(SvgFileImporter)];
    }

    [Theory]
    [MemberData(nameof(ImporterKeys))]
    public void Create_ReturnsExpectedImporter(string key, Type expectedType)
    {
        var importer = FileImporterRegistry.Create(key);

        Assert.IsType(expectedType, importer);
    }

    [Fact]
    public void Keys_ReturnsRegisteredImporterKeys()
    {
        Assert.Equal(
            [
                FileImporterKeys.Doc,
                FileImporterKeys.Docx,
                FileImporterKeys.Image,
                FileImporterKeys.Markdown,
                FileImporterKeys.Odt,
                FileImporterKeys.Pdf,
                FileImporterKeys.Pptx,
                FileImporterKeys.Svg,
            ],
            FileImporterRegistry.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void TryCreate_IsCaseInsensitiveAndAcceptsLeadingDot()
    {
        var created = FileImporterRegistry.TryCreate(".SVG", out var importer);

        Assert.True(created);
        Assert.IsType<SvgFileImporter>(importer);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData(".png")]
    [InlineData("TIFF")]
    public void Create_MapsImageExtensionsToImageImporter(string key)
    {
        var importer = FileImporterRegistry.Create(key);

        Assert.IsType<ImageFileImporter>(importer);
    }

    [Fact]
    public void Create_RejectsUnknownKey()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileImporterRegistry.Create("unknown"));
    }
}
