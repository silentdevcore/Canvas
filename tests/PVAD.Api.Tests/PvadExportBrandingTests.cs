using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using PVAD.Application.UseCases;
using PVAD.Importer;
using PVAD.Importer.Objects;
using PVAD.Infrastructure.Converters;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Infrastructure.Word;
using PVAD.WebApi.Controllers;
using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadExportBrandingTests
{
    [Theory]
    [InlineData("pdf", false)]
    [InlineData("pdf", true)]
    [InlineData("word", false)]
    [InlineData("word", true)]
    [InlineData("excel", false)]
    [InlineData("excel", true)]
    [InlineData("odt", false)]
    [InlineData("odt", true)]
    [InlineData("html", false)]
    [InlineData("html", true)]
    [InlineData("xml", false)]
    [InlineData("xml", true)]
    public async Task Generated_exports_keep_current_defaults_and_supplied_customer_names(string format, bool customer)
    {
        var design = CreateDesign();
        design.Name = customer ? "PXA customer report" : "PVADE Export Audit";
        var text = customer ? "PXA customer body" : "PVADE sample content";
        design.Pages[0].Elements[0].Content = text;
        var metadata = new PdfMetadataDto
        {
            Title = customer ? "PXA original title" : "PVADE sample title",
            Author = customer ? "PXA supplied author" : "Sample owner",
            Subject = customer ? "PXA supplied subject" : "Export naming review",
            Keywords = customer ? "PXA,original" : "sample,review",
        };
        design.PageSettings!.Metadata = metadata;
        var original = JsonSerializer.Serialize(design);
        var controller = new ExportController(new ExportDocumentUseCase([
            new WordDocumentExporter(), new ExcelDocumentExporter(), new OdtDocumentExporter(),
            new HtmlDocumentExporter(), new XmlDocumentExporter(),
        ]));

        var result = Assert.IsType<FileContentResult>(controller.Export(format, null, null, null, design));
        var (extension, mime) = format switch
        {
            "pdf" => ("pdf", "application/pdf"),
            "word" => ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            "excel" => ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            "odt" => ("odt", "application/vnd.oasis.opendocument.text"),
            "html" => ("html", "text/html; charset=utf-8"),
            _ => ("xml", "application/xml; charset=utf-8"),
        };
        Assert.Equal($"{(customer ? "PXA-customer-report" : "PVADE-Export-Audit")}.{extension}", result.FileDownloadName);
        Assert.Equal(mime, result.ContentType);
        Assert.Equal(original, JsonSerializer.Serialize(design));
        WriteGeneratedExport($"{(customer ? "customer" : "current")}.{extension}", result.FileContents);

        if (format == "pdf")
        {
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(result.FileContents, 0, 5));
            await using var input = new MemoryStream(result.FileContents);
            var imported = await new PdfImporter().LoadAsync(input);
            foreach (var (key, expected) in new[]
            {
                ("Title", metadata.Title), ("Author", metadata.Author),
                ("Subject", metadata.Subject), ("Keywords", metadata.Keywords), ("Creator", "PVAD PDF Renderer"),
            })
                Assert.Equal(expected, Assert.IsType<PdfString>(imported.Metadata[key]).ToLatin1String());
            if (!customer)
            {
                var values = imported.Metadata.Values.Values.OfType<PdfString>().ToArray();
                Assert.NotEmpty(values);
                foreach (var value in values)
                    Assert.DoesNotMatch("(?i)pxa|power[ _-]*dox", value.ToLatin1String());
            }
        }
        else if (format is "word" or "excel" or "odt")
        {
            using var input = new MemoryStream(result.FileContents);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                Assert.DoesNotMatch("(?i)pxa|power[ _-]*dox", entry.FullName);
                if (!customer && (entry.FullName.EndsWith(".xml", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith(".rels", StringComparison.Ordinal) ||
                    entry.FullName.EndsWith(".psmdcp", StringComparison.Ordinal)))
                {
                    using var reader = new StreamReader(entry.Open());
                    Assert.DoesNotMatch("(?i)pxa|power[ _-]*dox", reader.ReadToEnd());
                }
            }
            if (format == "word")
            {
                using var doc = WordprocessingDocument.Open(new MemoryStream(result.FileContents), false);
                Assert.Equal(metadata.Title, doc.PackageProperties.Title);
                Assert.Equal(metadata.Author, doc.PackageProperties.Creator);
                Assert.Equal(metadata.Subject, doc.PackageProperties.Subject);
                Assert.Contains(text, doc.MainDocumentPart!.Document!.InnerText);
            }
            else if (format == "excel")
            {
                using var workbook = new XLWorkbook(new MemoryStream(result.FileContents));
                Assert.Equal(text, workbook.Worksheet("Summary").Cell(2, 3).GetString());
            }
            else
            {
                using var meta = zip.GetEntry("meta.xml")!.Open();
                var xml = XDocument.Load(meta);
                XNamespace dc = "http://purl.org/dc/elements/1.1/";
                Assert.Equal(metadata.Title, xml.Descendants(dc + "title").Single().Value);
                Assert.Equal(metadata.Author, xml.Descendants(dc + "creator").Single().Value);
                using var content = zip.GetEntry("content.xml")!.Open();
                Assert.Contains(text, XDocument.Load(content).Root!.Value);
            }
        }
        else
        {
            var output = Encoding.UTF8.GetString(result.FileContents);
            Assert.Contains(text, output);
            if (format == "xml")
                Assert.Equal("PvadDocument", XDocument.Load(new MemoryStream(result.FileContents)).Root!.Name.LocalName);
            else Assert.Contains($"<title>{metadata.Title}</title>", output);
            if (!customer) Assert.DoesNotMatch("(?i)pxa|power[ _-]*dox", output);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spreadsheet_facade_retains_workbook_sheet_and_cell_names(bool customer)
    {
        var name = customer ? "PXA customer" : "Sample";
        var workbook = PVAD.Generator.Spreadsheet.CreateWorkbook(name);
        var sheet = workbook.AddSheet(name);
        sheet.Cell("A1").Value(name);
        var bytes = workbook.ToXlsx();
        WriteGeneratedExport($"{(customer ? "customer" : "current")}-workbook.xlsx", bytes);
        using var read = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(name, read.Worksheet(1).Name);
        Assert.Equal(name, read.Worksheet(1).Cell("A1").GetString());
        Assert.Equal(name, workbook.Name);
    }

    [Fact]
    public async Task Pdf_facade_preserves_explicit_customer_creator_and_producer()
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        document.AddPage(595, 842);
        document.Info.Title = "PXA original title";
        document.Info.Creator = "PXA customer creator";
        document.Info.Producer = "PXA customer producer";
        await using var input = new MemoryStream(document.ToBytes());
        var imported = await new PdfImporter().LoadAsync(input);
        Assert.Equal(document.Info.Title, Assert.IsType<PdfString>(imported.Metadata["Title"]).ToLatin1String());
        Assert.Equal(document.Info.Creator, Assert.IsType<PdfString>(imported.Metadata["Creator"]).ToLatin1String());
        Assert.Equal(document.Info.Producer, Assert.IsType<PdfString>(imported.Metadata["Producer"]).ToLatin1String());
    }

    private static void WriteGeneratedExport(string name, byte[] bytes)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PVAD.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "artifacts", "rename", "generated-document-output-naming", "exports");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    private static DesignExportDto CreateDesign() => new()
    {
        Id = "pvad-export",
        Name = "PVAD Export",
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
                        Content = "PVADE",
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
                            ["Product", "PVADE"],
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

    [Fact]
    public void New_chart_pdf_writes_only_the_canonical_metadata_key()
    {
        var design = new DesignExportDto
        {
            Name = "Sales",
            Pages = [new PageDto
            {
                Id = "page-1", Elements = [new ElementDto
                {
                    Id = "chart-1", Type = "chart", X = 10, Y = 10, Width = 300, Height = 180,
                    Chart = new ChartDefinitionDto
                    {
                        Categories = ["A"],
                        Series = [new ChartSeriesDto { Id = "sales", Name = "Sales", Values = [5] }],
                    },
                }],
            }],
        };

        var document = DesignJsonMapper.MapToPdfDocument(design);

        Assert.True(document.Info.CustomProperties.ContainsKey(ChartMetadataCodec.PdfInfoKey));
        Assert.False(document.Info.CustomProperties.ContainsKey(ChartMetadataCodec.LegacyPdfInfoKey));
        Assert.True(ChartMetadataCodec.TryDecode(
            document.Info.CustomProperties[ChartMetadataCodec.PdfInfoKey], out var metadata));
        Assert.Equal("chart-1", Assert.Single(metadata.Charts).ElementId);
    }

    [Fact]
    public void NewPdf_UsesCurrentCreatorWithoutRewritingCustomerMetadata()
    {
        var design = new DesignExportDto
        {
            Name = "Customer PXA document",
            PageSettings = new PageSettingsDto
            {
                Metadata = new PdfMetadataDto
                {
                    Title = "Canvas and PXA customer archive",
                    Author = "PXA customer",
                    Subject = "Original Canvas documents",
                    Keywords = "Canvas,PXA"
                }
            },
            Pages = [new PageDto { Id = "page-1", Elements = [] }]
        };

        var document = DesignJsonMapper.MapToPdfDocument(design);

        Assert.Equal("PVAD PDF Renderer", document.Info.Creator);
        Assert.Equal(design.PageSettings.Metadata.Title, document.Info.Title);
        Assert.Equal(design.PageSettings.Metadata.Author, document.Info.Author);
        Assert.Equal(design.PageSettings.Metadata.Subject, document.Info.Subject);
        Assert.Equal(design.PageSettings.Metadata.Keywords, document.Info.Keywords);
    }
}
