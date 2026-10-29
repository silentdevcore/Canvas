using System.Text;
using System.IO.Compression;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Converters;
using SkiaSharp;
using System.Xml.Linq;

namespace PVAD.Infrastructure.Converters.Tests;

public sealed class ConverterFacadeTests
{
    [Theory]
    [MemberData(nameof(TextExporterCases))]
    public void TextExporters_ExportPvadCoreDesign(DocumentExporter exporter, string expectedKey, string expectedExtension, string expectedNeedle)
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
        yield return [new XmlDocumentExporter(), "xml", ".xml", "PvadDocument"];
        yield return [new SvgDocumentExporter(), "svg", ".svg", "<svg"];
    }

    [Fact]
    public void XmlExport_ChangesOnlyEnvelopeIdentityInExplicitLegacyMode()
    {
        var exporter = new XmlDocumentExporter();
        var design = BuildDesign();
        var canonical = XDocument.Load(new MemoryStream(exporter.Export(design)));
        var legacy = XDocument.Load(new MemoryStream(exporter.ExportLegacy(design)));
        Assert.Equal("PvadDocument", canonical.Root!.Name.LocalName);
        Assert.Equal("PxaDocument", legacy.Root!.Name.LocalName);
        Assert.Equal("1.0", canonical.Root.Attribute("version")?.Value);
        Assert.Contains("Hello PXA", canonical.ToString());
        legacy.Root.Name = canonical.Root.Name;
        Assert.True(XNode.DeepEquals(canonical, legacy));
    }

    [Fact]
    public void ImageExporter_ExportsPngFromPvadCoreDesign()
    {
        var bytes = new ImageDocumentExporter().Export(BuildDesign(), new ExportOptions(Dpi: 72));

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
    }

    [Fact]
    public void OdtExporter_ExportsOdtPackageFromPvadCoreDesign()
    {
        var exporter = new OdtDocumentExporter();

        var bytes = exporter.Export(BuildDesign());

        Assert.Equal("odt", exporter.FormatKey);
        Assert.Equal(".odt", exporter.FileExtension);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void PageOrientedExporters_KeepMixedPageDimensions()
    {
        var design = BuildMixedLayoutDesign();

        var html = Encoding.UTF8.GetString(new HtmlDocumentExporter().Export(design));
        Assert.Contains("width:595px; height:842px", html);
        Assert.Contains("width:842px; height:595px", html);

        var imageZip = new ImageDocumentExporter().Export(design, new ExportOptions(Dpi: 72));
        using (var archive = new ZipArchive(new MemoryStream(imageZip), ZipArchiveMode.Read))
        {
            Assert.Equal(2, archive.Entries.Count);
            using var first = SKBitmap.Decode(archive.Entries[0].Open());
            using var second = SKBitmap.Decode(archive.Entries[1].Open());
            Assert.Equal((595, 842), (first.Width, first.Height));
            Assert.Equal((842, 595), (second.Width, second.Height));
        }

        AssertRasterZipDimensions(
            new JpegDocumentExporter().Export(design, new ExportOptions(Dpi: 72)),
            ".jpg",
            bytes =>
            {
                using var bitmap = SKBitmap.Decode(bytes);
                return (bitmap.Width, bitmap.Height);
            });
        AssertRasterZipDimensions(
            new TiffDocumentExporter().Export(design, new ExportOptions(Dpi: 72)),
            ".tiff",
            ReadTiffDimensions);

        using (var archive = new ZipArchive(new MemoryStream(new SvgDocumentExporter().Export(design)), ZipArchiveMode.Read))
        {
            Assert.Equal(2, archive.Entries.Count);
            Assert.Equal((595d, 842d), ReadSvgDimensions(archive.Entries[0]));
            Assert.Equal((842d, 595d), ReadSvgDimensions(archive.Entries[1]));
        }

        var odt = new OdtDocumentExporter().Export(design);
        using (var archive = new ZipArchive(new MemoryStream(odt), ZipArchiveMode.Read))
        {
            using var reader = new StreamReader(archive.GetEntry("styles.xml")!.Open());
            var styles = reader.ReadToEnd();
            Assert.Contains("PageLayout1", styles);
            Assert.Contains("PageLayout2", styles);
            Assert.Contains("landscape", styles);
            using var content = archive.GetEntry("content.xml")!.Open();
            var contentXml = XDocument.Load(content);
            Assert.Equal(2, contentXml.Descendants().Count(element => element.Name.LocalName == "style" && ((string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "name"))?.StartsWith("PageStart", StringComparison.Ordinal) == true));
        }
    }

    private static void AssertRasterZipDimensions(
        byte[] bytes,
        string extension,
        Func<byte[], (int Width, int Height)> readDimensions)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(2, archive.Entries.Count);
        Assert.All(archive.Entries, entry => Assert.EndsWith(extension, entry.Name, StringComparison.Ordinal));
        Assert.Equal((595, 842), readDimensions(ReadEntry(archive.Entries[0])));
        Assert.Equal((842, 595), readDimensions(ReadEntry(archive.Entries[1])));
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }

    private static (int Width, int Height) ReadTiffDimensions(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal((byte)'I', reader.ReadByte());
        Assert.Equal((byte)'I', reader.ReadByte());
        Assert.Equal((ushort)42, reader.ReadUInt16());
        reader.BaseStream.Position = reader.ReadUInt32();
        var count = reader.ReadUInt16();
        int width = 0, height = 0;
        for (var index = 0; index < count; index++)
        {
            var tag = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt32();
            var value = reader.ReadUInt32();
            if (tag == 256) width = (int)value;
            if (tag == 257) height = (int)value;
        }
        return (width, height);
    }

    private static (double Width, double Height) ReadSvgDimensions(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        var root = XDocument.Load(source).Root!;
        return ((double)root.Attribute("width")!, (double)root.Attribute("height")!);
    }

    [Fact]
    public void ConverterCapabilities_MirrorPvadCapabilities()
    {
        var capabilities = new ConverterRendererCapabilities();

        Assert.Equal("converter", capabilities.RendererKey);
        Assert.False(capabilities.SupportsBookmarks);
        Assert.False(capabilities.SupportsCompression);
    }

    [Fact]
    public void CsvCapabilities_ArePvadCapabilities()
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

    private static DesignExportDto BuildMixedLayoutDesign() => new()
    {
        Id = "mixed-layout",
        Name = "Mixed layout",
        PageSettings = new PageSettingsDto { Width = 595, Height = 842, Orientation = "portrait" },
        Pages =
        [
            new PageDto { Id = "page-1" },
            new PageDto
            {
                Id = "page-2",
                Layout = new PageLayoutDto { Width = 842, Height = 595, Orientation = "landscape" },
            },
        ],
    };
}
