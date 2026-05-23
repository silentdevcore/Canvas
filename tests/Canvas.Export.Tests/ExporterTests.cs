using System.Text;
using Canvas.Infrastructure.Converters;
using Canvas.Infrastructure.Sheet;
using Canvas.Infrastructure.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Canvas.Export.Tests;

public class ExporterTests
{
    // ─── Shared fixture ───────────────────────────────────────────────────────

    private static DesignExportDto MinimalDesign() => new()
    {
        Id   = "test-id",
        Name = "Test Design",
        Pages =
        [
            new PageDto
            {
                Id = "p1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "e1", Type = "text",
                        X = 10, Y = 10, Width = 200, Height = 30,
                        Content = "Hello Export",
                        Style   = new() { ["fontSize"] = 14, ["color"] = "#111827" },
                    },
                    new ElementDto
                    {
                        Id = "e2", Type = "table",
                        X = 10, Y = 60, Width = 400, Height = 120,
                        HeaderRow    = true,
                        HeaderBgColor = "#f1f5f9",
                        CellData     = [["Name", "Value"], ["Row1", "Data1"], ["Row2", "Data2"]],
                        Style        = new() { ["borderWidth"] = 1, ["borderColor"] = "#000000" },
                    },
                ],
            },
        ],
        PageSettings = new PageSettingsDto { Width = 595, Height = 842 },
    };

    // ─── HTML ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Html_Export_ReturnsNonEmptyBytes()
    {
        var result = new HtmlDocumentExporter().Export(MinimalDesign());
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Html_Export_HasCorrectMimeType()
    {
        var exporter = new HtmlDocumentExporter();
        Assert.Equal("text/html; charset=utf-8", exporter.MimeType);
        Assert.Equal(".html", exporter.FileExtension);
    }

    [Fact]
    public void Html_Export_ContainsTextContent()
    {
        var html = Encoding.UTF8.GetString(new HtmlDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Hello Export", html);
    }

    [Fact]
    public void Html_Export_ContainsTableHeader()
    {
        var html = Encoding.UTF8.GetString(new HtmlDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Name", html);
        Assert.Contains("<table", html);
    }

    // ─── XML ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Xml_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new XmlDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Xml_Export_HasCorrectMimeType()
    {
        var exporter = new XmlDocumentExporter();
        Assert.Equal("application/xml; charset=utf-8", exporter.MimeType);
        Assert.Equal(".xml", exporter.FileExtension);
    }

    [Fact]
    public void Xml_Export_ContainsTextContent()
    {
        var xml = Encoding.UTF8.GetString(new XmlDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Hello Export", xml);
        Assert.Contains("CanvasDocument", xml);
    }

    [Fact]
    public void Xml_Export_ContainsTableData()
    {
        var xml = Encoding.UTF8.GetString(new XmlDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Name", xml);
        Assert.Contains("CellData", xml);
    }

    // ─── SVG ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Svg_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new SvgDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Svg_Export_HasCorrectMimeType()
    {
        var exporter = new SvgDocumentExporter();
        Assert.Equal("image/svg+xml", exporter.MimeType);
        Assert.Equal(".svg", exporter.FileExtension);
    }

    [Fact]
    public void Svg_Export_ContainsTextContent()
    {
        var svg = Encoding.UTF8.GetString(new SvgDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Hello Export", svg);
        Assert.Contains("<svg", svg);
    }

    // ─── CSV ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Csv_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new CsvDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Csv_Export_HasCorrectMimeType()
    {
        var exporter = new CsvDocumentExporter();
        Assert.Equal("text/csv; charset=utf-8", exporter.MimeType);
        Assert.Equal(".csv", exporter.FileExtension);
    }

    [Fact]
    public void Csv_Export_ContainsTableData()
    {
        var csv = Encoding.UTF8.GetString(new CsvDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Name", csv);
        Assert.Contains("Row1", csv);
    }

    [Fact]
    public void Csv_Export_HasUtf8Bom()
    {
        var bytes = new CsvDocumentExporter().Export(MinimalDesign());
        // UTF-8 BOM: EF BB BF
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "CSV output should start with UTF-8 BOM");
    }

    // ─── Markdown ─────────────────────────────────────────────────────────────

    [Fact]
    public void Markdown_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new MarkdownDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Markdown_Export_HasCorrectMimeType()
    {
        var exporter = new MarkdownDocumentExporter();
        Assert.Equal("text/markdown; charset=utf-8", exporter.MimeType);
        Assert.Equal(".md", exporter.FileExtension);
    }

    [Fact]
    public void Markdown_Export_ContainsTextContent()
    {
        var md = Encoding.UTF8.GetString(new MarkdownDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("Hello Export", md);
    }

    [Fact]
    public void Markdown_Export_ContainsGfmTable()
    {
        var md = Encoding.UTF8.GetString(new MarkdownDocumentExporter().Export(MinimalDesign()));
        Assert.Contains("|", md);
        Assert.Contains("Name", md);
    }

    // ─── Word ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Word_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new WordDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Word_Export_HasCorrectMimeType()
    {
        var exporter = new WordDocumentExporter();
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", exporter.MimeType);
        Assert.Equal(".docx", exporter.FileExtension);
    }

    [Fact]
    public void Word_Export_ProducesValidDocxSignature()
    {
        var bytes = new WordDocumentExporter().Export(MinimalDesign());
        // ZIP/OOXML magic bytes: 50 4B 03 04
        Assert.True(bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B,
            "DOCX should start with ZIP magic bytes 50 4B");
    }

    [Fact]
    public void Word_Export_DoesNotThrow_WhenLinkHrefIsRelative()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements.Add(new ElementDto
        {
            Id = "link-relative",
            Type = "link",
            Content = "Open details",
            Href = "/details/123"
        });

        var bytes = new WordDocumentExporter().Export(design);

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B);
    }

    [Fact]
    public void Word_Export_CreatesHyperlinkRelationship_ForExternalLinks()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements.Add(new ElementDto
        {
            Id = "link-external",
            Type = "link",
            Content = "Open site",
            Href = "https://example.com"
        });

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.NotEmpty(doc.MainDocumentPart!.HyperlinkRelationships);
    }

    [Fact]
    public void Word_Export_DoesNotCreateHyperlinkRelationship_ForRelativeLinks()
    {
        var design = new DesignExportDto
        {
            Id = "d1",
            Name = "relative-only",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "link-relative-only",
                            Type = "link",
                            Content = "Internal",
                            Href = "/internal/page"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.Empty(doc.MainDocumentPart!.HyperlinkRelationships);
    }

    [Fact]
    public void Word_Export_DoesNotThrow_WhenPagesAndElementsAreNull()
    {
        var design = new DesignExportDto
        {
            Id = "null-shape",
            Name = "Null-safe",
            Pages = null!,
            SharedElements =
            [
                new ElementDto
                {
                    Id = "t1",
                    Type = "text",
                    Content = "Fallback page",
                    Style = new() { ["color"] = "not-a-color" }
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Word_Export_MapsFrontendFontFamily_ToDeterministicWordFallback()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements[0].Style!["fontFamily"] = "Inter, sans-serif";

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var runFonts = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Run>()
            .First(r => r.RunProperties != null)
            .RunProperties!
            .RunFonts;

        Assert.NotNull(runFonts);
        Assert.Equal("Calibri", runFonts!.Ascii?.Value);
    }

    [Fact]
    public void Word_Export_AppliesParagraphLineHeight_FromStyle()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements[0].Style!["fontSize"] = 12;
        design.Pages[0].Elements[0].Style!["lineHeight"] = 1.5;

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var spacing = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Paragraph>()
            .First(p => p.ParagraphProperties?.SpacingBetweenLines != null)
            .ParagraphProperties!
            .SpacingBetweenLines;

        Assert.NotNull(spacing);
        Assert.Equal("360", spacing!.Line?.Value); // 12pt * 1.5 * 20 twips
    }

    [Fact]
    public void Word_Export_SupportsCombinedTextDecorations()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements[0].Style!["textDecoration"] = "underline line-through";

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var runProps = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Run>()
            .First(r => r.RunProperties != null)
            .RunProperties;

        Assert.NotNull(runProps);
        Assert.NotNull(runProps!.Underline);
        Assert.NotNull(runProps.Strike);
    }

    [Fact]
    public void Word_Export_RichText_PreservesInlineSpanStyles()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements[0] = new ElementDto
        {
            Id = "rt1",
            Type = "richtext",
            HtmlContent = "<p>Base <strong>Bold</strong> <em>Italic</em> <span style=\"color:#ff0000;font-size:20\"><u><s>Marked</s></u></span></p>",
            Style = new()
            {
                ["fontSize"] = 12,
                ["color"] = "#111827"
            }
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var runs = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Run>()
            .Where(r => r.RunProperties != null)
            .ToList();

        var boldRun = runs.First(r => r.InnerText.Contains("Bold", StringComparison.Ordinal));
        var italicRun = runs.First(r => r.InnerText.Contains("Italic", StringComparison.Ordinal));
        var markedRun = runs.First(r => r.InnerText.Contains("Marked", StringComparison.Ordinal));

        Assert.NotNull(boldRun.RunProperties?.Bold);
        Assert.NotNull(italicRun.RunProperties?.Italic);

        Assert.NotNull(markedRun.RunProperties?.Underline);
        Assert.NotNull(markedRun.RunProperties?.Strike);
        Assert.Equal("FF0000", markedRun.RunProperties?.Color?.Val?.Value);
        Assert.Equal("40", markedRun.RunProperties?.FontSize?.Val?.Value);
    }

    [Fact]
    public void Word_Export_FormLikeElements_UseStyleColor()
    {
        var design = new DesignExportDto
        {
            Id = "form-style",
            Name = "Form style",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "sig1",
                            Type = "signature",
                            SignatureLabel = "Signer",
                            Style = new() { ["color"] = "#123abc", ["fontSize"] = 13 }
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var signatureRun = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Run>()
            .First(r => r.InnerText.Contains("Signer", StringComparison.Ordinal));

        Assert.Equal("123ABC", signatureRun.RunProperties?.Color?.Val?.Value);
    }

    [Fact]
    public void Word_Export_RendersPlaceholder_WhenImageCannotBeLoaded()
    {
        var design = new DesignExportDto
        {
            Id = "img-fallback-1",
            Name = "Image fallback",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-missing",
                            Type = "image",
                            X = 20,
                            Y = 30,
                            Width = 100,
                            Height = 60,
                            Content = "data:image/png;base64,not-valid-base64"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("[image unavailable]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_Export_RendersPlaceholder_WhenRemoteImageFetchFails()
    {
        var design = new DesignExportDto
        {
            Id = "img-fallback-remote-1",
            Name = "Image remote fallback",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-remote-missing",
                            Type = "image",
                            X = 10,
                            Y = 10,
                            Width = 120,
                            Height = 80,
                            Content = "http://127.0.0.1:1/does-not-exist.png"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("[image unavailable]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_Export_PageNumber_PageOfTotal_UsesPageAndNumPagesFields()
    {
        var design = MinimalDesign();
        design.Pages[0].Elements.Add(new ElementDto
        {
            Id = "pg1",
            Type = "pagenumber",
            NumberingFormat = "pageOfTotal",
            Prefix = "Page ",
            Suffix = " end"
        });

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var fieldCodes = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<FieldCode>()
            .Select(f => f.InnerText)
            .ToList();

        Assert.Contains(fieldCodes, c => c.Contains("PAGE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fieldCodes, c => c.Contains("NUMPAGES", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Word_Export_UsesDeterministicOrder_ForElementsWithSameCoordinates()
    {
        var design = new DesignExportDto
        {
            Id = "stable-order-1",
            Name = "Stable order",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "b",
                            Type = "text",
                            X = 20,
                            Y = 20,
                            Content = "Second"
                        },
                        new ElementDto
                        {
                            Id = "a",
                            Type = "text",
                            X = 20,
                            Y = 20,
                            Content = "First"
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var orderedText = doc.MainDocumentPart!
            .Document!
            .Body!
            .Descendants<Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => t is "First" or "Second")
            .ToList();

        Assert.Equal(["First", "Second"], orderedText);
    }

    [Fact]
    public void Word_Export_RendersFallbackAnnotation_ForTrulyUnsupportedTypes()
    {
        // "rect"/"circle"/"line" now render as real DrawingML shapes in v2 mode.
        // Elements like "draw" (freehand) have no DrawingML equivalent and still produce annotations.
        var design = new DesignExportDto
        {
            Id = "unsupported-1",
            Name = "Unsupported",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "draw1",
                            Type = "draw",
                            X = 40,
                            Y = 50,
                            Width = 100,
                            Height = 60
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.Contains("[draw]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_Export_RendersAnchoredShape_ForRectInFidelityV2Mode()
    {
        var design = new DesignExportDto
        {
            Id = "rect-shape-1",
            Name = "Rect shape",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "rect1",
                            Type = "rect",
                            X = 40,
                            Y = 50,
                            Width = 100,
                            Height = 60,
                            Style = new() { ["backgroundColor"] = "#FF0000", ["borderColor"] = "#000000" }
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
        var anchor = doc.MainDocumentPart!.Document!.Body!.Descendants<DW.Anchor>().FirstOrDefault();

        Assert.DoesNotContain("[rect]", text, StringComparison.Ordinal);
        Assert.NotNull(anchor);
    }

    [Fact]
    public void Word_Export_WritesWarnings_ToPackageDescription_WhenFallbacksOccur()
    {
        var design = new DesignExportDto
        {
            Id = "warnings-1",
            Name = "Warnings",
            Description = "Base description",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "link1",
                            Type = "link",
                            Content = "Internal",
                            Href = "/internal/path"
                        },
                        new ElementDto
                        {
                            Id = "draw1",
                            Type = "draw",
                            X = 10,
                            Y = 30,
                            Width = 100,
                            Height = 50
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var description = doc.PackageProperties.Description;

        Assert.NotNull(description);
        Assert.Contains("Base description", description, StringComparison.Ordinal);
        Assert.Contains("ExportWarnings:", description, StringComparison.Ordinal);
        Assert.Contains("Link fallback", description, StringComparison.Ordinal);
        Assert.Contains("Unsupported element", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_Export_ThrowsOperationCanceled_WhenTokenIsPreCancelled()
    {
        var design = MinimalDesign();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new WordDocumentExporter().Export(design, new ExportOptions(CancellationToken: cts.Token)));
    }

    [Fact]
    public void Word_Export_ThrowsOperationCanceled_DuringRemoteImageFetch()
    {
        var design = new DesignExportDto
        {
            Id = "cancel-remote-1",
            Name = "Cancel remote image",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "img-remote-cancel",
                            Type = "image",
                            X = 10,
                            Y = 10,
                            Width = 120,
                            Height = 80,
                            Content = "http://127.0.0.1:1/slow-or-missing.png"
                        }
                    ]
                }
            ]
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new WordDocumentExporter().Export(design, new ExportOptions(CancellationToken: cts.Token)));
    }

    [Fact]
    public void Word_Export_FeatureFlag_DisablesFidelityV2FallbackAnnotations()
    {
        var design = new DesignExportDto
        {
            Id = "flag-off-1",
            Name = "Fidelity flag off",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "rect1",
                            Type = "rect",
                            X = 20,
                            Y = 30,
                            Width = 120,
                            Height = 80
                        }
                    ]
                }
            ]
        };

        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: false));

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var text = doc.MainDocumentPart!.Document!.Body!.InnerText;

        Assert.DoesNotContain("[rect]", text, StringComparison.Ordinal);
    }

    // ─── Excel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Excel_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new ExcelDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Excel_Export_HasCorrectMimeType()
    {
        var exporter = new ExcelDocumentExporter();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", exporter.MimeType);
        Assert.Equal(".xlsx", exporter.FileExtension);
    }

    [Fact]
    public void Excel_Export_ProducesValidXlsxSignature()
    {
        var bytes = new ExcelDocumentExporter().Export(MinimalDesign());
        Assert.True(bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B,
            "XLSX should start with ZIP magic bytes 50 4B");
    }

    // ─── PNG ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Png_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new ImageDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Png_Export_HasCorrectMimeType()
    {
        var exporter = new ImageDocumentExporter();
        Assert.Equal("image/png", exporter.MimeType);
        Assert.Equal(".png", exporter.FileExtension);
    }

    [Fact]
    public void Png_Export_ProducesValidPngSignature()
    {
        var bytes = new ImageDocumentExporter().Export(MinimalDesign());
        // PNG signature: 89 50 4E 47
        Assert.True(bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50,
            "PNG output should start with PNG magic bytes");
    }

    // ─── JPEG ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Jpeg_Export_ReturnsNonEmptyBytes()
        => Assert.NotEmpty(new JpegDocumentExporter().Export(MinimalDesign()));

    [Fact]
    public void Jpeg_Export_HasCorrectMimeType()
    {
        var exporter = new JpegDocumentExporter();
        Assert.Equal("image/jpeg", exporter.MimeType);
        Assert.Equal(".jpg", exporter.FileExtension);
    }

    [Fact]
    public void Jpeg_Export_ProducesValidJpegSignature()
    {
        var bytes = new JpegDocumentExporter().Export(MinimalDesign());
        // JPEG SOI marker: FF D8
        Assert.True(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8,
            "JPEG output should start with FF D8");
    }

    // ─── Capabilities ─────────────────────────────────────────────────────────

    [Fact]
    public void Csv_Capabilities_HasCorrectFlags()
    {
        var caps = new CsvDocumentExporter().Capabilities;
        Assert.True(caps.SupportsMultiPage);
        Assert.False(caps.SupportsImages);
        Assert.False(caps.SupportsRichText);
        Assert.False(caps.SupportsFormFields);
    }

    [Fact]
    public void Excel_Capabilities_HasCorrectFlags()
    {
        var caps = new ExcelDocumentExporter().Capabilities;
        Assert.True(caps.SupportsMultiPage);
        Assert.True(caps.SupportsImages);
        Assert.False(caps.SupportsRichText);
        Assert.False(caps.SupportsFormFields);
    }

    [Fact]
    public void Html_Capabilities_AllTrue()
    {
        var caps = new HtmlDocumentExporter().Capabilities;
        Assert.True(caps.SupportsMultiPage);
        Assert.True(caps.SupportsImages);
        Assert.True(caps.SupportsRichText);
        Assert.True(caps.SupportsFormFields);
    }

    [Fact]
    public void Word_Capabilities_ReportImageSupport()
    {
        var caps = new WordDocumentExporter().Capabilities;
        Assert.True(caps.SupportsImages);
    }
}
