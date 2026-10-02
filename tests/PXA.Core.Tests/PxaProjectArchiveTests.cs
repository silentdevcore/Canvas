using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PXA.Core.Contracts;
using PXA.Core.ProjectFormats;

namespace PXA.Core.Tests;

public sealed class PxaProjectArchiveTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static readonly string[] CurrentDesignerElementTypes =
    [
        "text", "richtext", "date", "number", "field", "textarea", "checkbox", "radio",
        "optionlist", "dropdown", "button", "signature", "table", "chart", "image", "qrcode",
        "barcode", "shape", "rect", "circle", "line", "arrow", "draw", "area", "subsection",
        "highlight", "checkmark", "link", "note", "watermark", "pagenumber", "pageboundary",
        "toc", "footnote", "endnote", "bookmark", "comment", "contentcontrol",
    ];

    [Fact]
    public void Upgrade_pipeline_applies_only_ordered_forward_steps_and_preserves_source_bytes()
    {
        var source = Encoding.UTF8.GetBytes("{\"id\":\"legacy\",\"name\":\"Original\"}");
        var original = source.ToArray();
        IPxaProjectUpgradeStep[] steps =
        [
            new TestUpgradeStep(PxaProjectKind.Pdf, "1.0", "2.0", document =>
            {
                document["schemaTwo"] = true;
                return new(document);
            }),
            new TestUpgradeStep(PxaProjectKind.Pdf, "2.0", "2.1", document =>
            {
                document["name"] = "Upgraded";
                return new(document, true,
                [
                    new PxaProjectDiagnostic
                    {
                        Code = "PXAPROJECT_TEST_LOSS",
                        Severity = "warning",
                        Message = "A synthetic legacy property was simplified.",
                    },
                ]);
            }),
        ];

        var result = PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Pdf, "1.0", "2.1", source, 32, steps);
        var upgraded = JsonNode.Parse(result.DocumentBytes)!.AsObject();

        Assert.Equal(original, source);
        Assert.Equal("Upgraded", upgraded["name"]!.GetValue<string>());
        Assert.True(upgraded["schemaTwo"]!.GetValue<bool>());
        Assert.NotNull(result.Report);
        Assert.True(result.Report.IsLossy);
        Assert.Equal(["1.0", "2.0"], result.Report.Steps.Select(step => step.FromVersion));
        Assert.Equal(["2.0", "2.1"], result.Report.Steps.Select(step => step.ToVersion));
        Assert.Contains(result.Report.Diagnostics, item => item.Code == "PXAPROJECT_UPGRADED");
        Assert.Contains(result.Report.Diagnostics, item => item.Code == "PXAPROJECT_TEST_LOSS");
    }

    [Theory]
    [InlineData("2.0", "1.0")]
    [InlineData("1.0", "3.0")]
    public void Upgrade_pipeline_rejects_downgrades_and_incomplete_paths(string source, string target)
    {
        IPxaProjectUpgradeStep[] steps =
        [
            new TestUpgradeStep(PxaProjectKind.Pdf, "1.0", "2.0", document => new(document)),
        ];

        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Pdf, source, target, "{}"u8, 32, steps));

        Assert.Equal("PXAPROJECT008", error.Code);
    }

    [Fact]
    public void Upgrade_pipeline_rejects_ambiguous_steps_and_keeps_current_documents_unchanged()
    {
        IPxaProjectUpgradeStep[] ambiguous =
        [
            new TestUpgradeStep(PxaProjectKind.Spreadsheet, "1.0", "2.0", document => new(document)),
            new TestUpgradeStep(PxaProjectKind.Spreadsheet, "1.0", "1.1", document => new(document)),
        ];
        var bytes = Encoding.UTF8.GetBytes("{\"id\":\"current\"}");

        Assert.Throws<PxaProjectFormatException>(() => PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Spreadsheet, "1.0", "2.0", bytes, 32, ambiguous));
        var current = PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Spreadsheet, "1.0", "1.0", bytes, 32, ambiguous);

        Assert.Equal(bytes, current.DocumentBytes);
        Assert.Null(current.Report);
    }

    [Fact]
    public void Upgrade_pipeline_rejects_non_deterministic_steps_with_a_stable_diagnostic()
    {
        var invocation = 0;
        IPxaProjectUpgradeStep[] steps =
        [
            new TestUpgradeStep(PxaProjectKind.Pdf, "1.0", "2.0", document =>
            {
                document["invocation"] = ++invocation;
                return new(document);
            }),
        ];

        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Pdf, "1.0", "2.0", "{}"u8, 32, steps));

        Assert.Equal("PXAPROJECT015", error.Code);
    }

    [Fact]
    public void Upgrade_pipeline_opens_older_compatible_minor_versions_without_rewriting_them()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"id\":\"compatible\"}");

        var result = PxaProjectUpgradePipeline.Upgrade(
            PxaProjectKind.Pdf, "1.1", "1.2", bytes, 32);

        Assert.Equal(bytes, result.DocumentBytes);
        Assert.Null(result.Report);
    }

    [Fact]
    public void Pdf_import_remaps_only_target_collisions_and_rewrites_element_references()
    {
        var document = new DesignExportDto
        {
            Id = "document-1",
            PageSettings = new PageSettingsDto
            {
                NamedStyles = [new NamedStyleDto { Id = "body", Name = "Body", BasedOn = "body" }],
            },
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto { Id = "header", Type = "text" },
                        new ElementDto
                        {
                            Id = "content", Type = "text",
                            Accessibility = new AccessibilityPropertiesDto
                            {
                                ParentElementId = "header", TableHeaderIds = ["header"],
                            },
                            Repeat = new RepeatDto { TemplateId = "header" },
                            BookmarkTarget = "header",
                        },
                    ],
                },
            ],
        };

        var result = PxaProjectIdCollisionResolver.Resolve(
            document, ["document-1", "page-1", "body", "header"]);

        Assert.Equal("document-1-import-2", document.Id);
        Assert.Equal("page-1-import-2", document.Pages[0].Id);
        Assert.Equal("body-import-2", document.PageSettings.NamedStyles[0].Id);
        Assert.Equal("body-import-2", document.PageSettings.NamedStyles[0].BasedOn);
        Assert.Equal("header-import-2", document.Pages[0].Elements[0].Id);
        Assert.Equal("content", document.Pages[0].Elements[1].Id);
        Assert.Equal("header-import-2", document.Pages[0].Elements[1].Accessibility!.ParentElementId);
        Assert.Equal(["header-import-2"], document.Pages[0].Elements[1].Accessibility!.TableHeaderIds);
        Assert.Equal("header-import-2", document.Pages[0].Elements[1].Repeat!.TemplateId);
        Assert.Equal("header-import-2", document.Pages[0].Elements[1].BookmarkTarget);
        Assert.Equal(4, result.Remaps.Count);
    }

    [Fact]
    public void Project_import_preserves_ids_when_the_target_has_no_collisions()
    {
        var pdf = PdfDesign();
        var workbook = CompleteSpreadsheet();
        var pdfJson = JsonSerializer.Serialize(pdf);
        var workbookJson = JsonSerializer.Serialize(workbook);

        var pdfResult = PxaProjectIdCollisionResolver.Resolve(pdf, ["unrelated"]);
        var workbookResult = PxaProjectIdCollisionResolver.Resolve(workbook, ["also-unrelated"]);

        Assert.Empty(pdfResult.Remaps);
        Assert.Empty(workbookResult.Remaps);
        Assert.Equal(pdfJson, JsonSerializer.Serialize(pdf));
        Assert.Equal(workbookJson, JsonSerializer.Serialize(workbook));
    }

    [Fact]
    public void Spreadsheet_import_remaps_workbook_sheet_and_image_collisions()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "workbook-1",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Images = [new SpreadsheetImageDto { Id = "image-1" }],
                },
            ],
        };

        var result = PxaProjectIdCollisionResolver.Resolve(
            workbook, ["workbook-1", "sheet-1", "image-1"]);

        Assert.Equal("workbook-1-import-2", workbook.Id);
        Assert.Equal("sheet-1-import-2", workbook.Sheets[0].Id);
        Assert.Equal("image-1-import-2", workbook.Sheets[0].Images[0].Id);
        Assert.Equal(3, result.Remaps.Count);
    }

    [Fact]
    public void Project_packages_reject_ambiguous_duplicate_editable_ids()
    {
        var pdf = PdfDesign();
        pdf.Pages[0].Elements =
        [
            new ElementDto { Id = "duplicate", Type = "text" },
            new ElementDto { Id = "duplicate", Type = "text" },
        ];
        var workbook = CompleteSpreadsheet();
        workbook.Sheets.Add(new SheetDto { Id = workbook.Sheets[0].Id, Name = "Duplicate" });

        var pdfError = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(pdf));
        var workbookError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.WriteSpreadsheet(workbook));

        Assert.Equal("PXAPROJECT004", pdfError.Code);
        Assert.Equal("PXAPROJECT004", workbookError.Code);
    }

    [Fact]
    public async Task Stream_helpers_write_deterministic_packages_without_closing_the_destination()
    {
        await using var pdfStream = new MemoryStream();
        await using var spreadsheetStream = new MemoryStream();
        var pdf = PdfDesign();
        var spreadsheet = CompleteSpreadsheet();

        await PxaProjectArchive.WritePdfAsync(pdfStream, pdf);
        await PxaProjectArchive.WriteSpreadsheetAsync(spreadsheetStream, spreadsheet);

        Assert.True(pdfStream.CanWrite);
        Assert.True(spreadsheetStream.CanWrite);
        Assert.Equal(PxaProjectArchive.WritePdf(pdf), pdfStream.ToArray());
        Assert.Equal(PxaProjectArchive.WriteSpreadsheet(spreadsheet), spreadsheetStream.ToArray());
    }

    [Theory]
    [InlineData("minimal.pxap", PxaProjectKind.Pdf, "pxap")]
    [InlineData("minimal.pxas", PxaProjectKind.Spreadsheet, "pxas")]
    public void Published_reference_fixtures_are_valid_synthetic_projects(
        string fileName,
        PxaProjectKind kind,
        string formatId)
    {
        var root = FindRepositoryRoot();
        var bytes = File.ReadAllBytes(Path.Combine(root, "docs", "fixtures", "native-projects", fileName));
        using var stream = new MemoryStream(bytes);

        var manifest = PxaProjectArchive.Inspect(stream, kind);

        Assert.Equal(formatId, manifest.FormatId);
        Assert.StartsWith("pxa", manifest.DocumentId, StringComparison.Ordinal);
        Assert.Equal(DateTimeOffset.UnixEpoch, manifest.CreatedAtUtc);
        using var packageStream = new MemoryStream(bytes);
        var regenerated = kind == PxaProjectKind.Pdf
            ? PxaProjectArchive.WritePdf(PxaProjectArchive.ReadPdf(packageStream).Document)
            : PxaProjectArchive.WriteSpreadsheet(PxaProjectArchive.ReadSpreadsheet(packageStream).Document);
        Assert.Equal(bytes, regenerated);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VERSION")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Theory]
    [InlineData("1.2", "1.2", PxaProjectVersionCompatibility.Current, true, false, false)]
    [InlineData("1.1", "1.2", PxaProjectVersionCompatibility.BackwardCompatible, true, false, false)]
    [InlineData("1.3", "1.2", PxaProjectVersionCompatibility.NewerApplicationRequired, false, false, false)]
    [InlineData("1.9", "2.0", PxaProjectVersionCompatibility.UpgradeRequired, true, true, true)]
    [InlineData("1.9", "2.0", PxaProjectVersionCompatibility.Unsupported, false, false, false)]
    public void Version_policy_distinguishes_compatible_upgrade_and_future_schemas(
        string source,
        string current,
        PxaProjectVersionCompatibility compatibility,
        bool canOpen,
        bool requiresUpgrade,
        bool hasMajorUpgradePath)
    {
        var result = PxaProjectVersionPolicy.AssessSchemaVersion(source, current, hasMajorUpgradePath);

        Assert.Equal(compatibility, result.Compatibility);
        Assert.Equal(canOpen, result.CanOpen);
        Assert.Equal(requiresUpgrade, result.RequiresUpgrade);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("01.0")]
    [InlineData("1.-1")]
    public void Schema_versions_must_use_canonical_major_minor_form(string value)
    {
        Assert.False(PxaProjectVersionPolicy.TryParseSchemaVersion(value, out _));
    }

    [Fact]
    public void Pxap_roundtrip_is_deterministic_and_preserves_editable_design()
    {
        var design = new DesignExportDto
        {
            Id = "pdf-project-1",
            Name = "Mixed layout",
            PageSettings = new PageSettingsDto { Width = 595, Height = 842, SystemLanguage = "en" },
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "title-1", Type = "text", Content = "Hello PXAP",
                            X = 36, Y = 42, Width = 220, Height = 30,
                        },
                    ],
                },
            ],
        };
        var options = new PxaProjectWriteOptions
        {
            CreatedAtUtc = Created,
            ModifiedAtUtc = Created,
            Locale = "en-CH",
            Assets =
            [
                new PxaProjectAsset(
                    "logo-primary",
                    "logo.png",
                    "image/png",
                    OnePixelPng),
            ],
        };

        var first = PxaProjectArchive.WritePdf(design, options);
        var second = PxaProjectArchive.WritePdf(design, options);
        var package = PxaProjectArchive.ReadPdf(new MemoryStream(first));

        Assert.Equal(first, second);
        Assert.Equal(PxaProjectFormat.PxapFormatId, package.Manifest.FormatId);
        Assert.Equal(PxaProjectFormat.PxapMediaType, package.Manifest.MediaType);
        Assert.Equal("pdf-project-1", package.Document.Id);
        Assert.Equal("Hello PXAP", package.Document.Pages[0].Elements[0].Content);
        Assert.Equal("en-CH", package.Manifest.Locale);
        Assert.Single(package.Assets);
        Assert.Equal("logo-primary", package.Assets[0].Id);
        Assert.Equal(64, package.Manifest.ContentHash.Length);
        Assert.Equal(64, package.PackageSha256.Length);

        using var zip = new ZipArchive(new MemoryStream(first), ZipArchiveMode.Read);
        Assert.Equal(PxaProjectFormat.MimeTypeEntry, zip.Entries[0].FullName);
        Assert.Equal(zip.Entries[0].Length, zip.Entries[0].CompressedLength);
    }

    [Fact]
    public void Pxap_roundtrip_preserves_the_complete_current_editable_contract()
    {
        var design = CompletePdfDesign();

        var package = PxaProjectArchive.ReadPdf(new MemoryStream(PxaProjectArchive.WritePdf(design)));

        AssertJsonEquivalent(design, package.Document);
    }

    [Fact]
    public void Pxap_roundtrip_preserves_every_current_designer_element_type()
    {
        var design = PdfDesign();
        design.Pages[0].Elements = CurrentDesignerElementTypes.Select((type, index) => new ElementDto
        {
            Id = $"element-{index}", Type = type, X = index, Y = index + 1, Width = 100, Height = 40,
            Content = type,
        }).ToList();

        var package = PxaProjectArchive.ReadPdf(new MemoryStream(PxaProjectArchive.WritePdf(design)));

        Assert.Equal(CurrentDesignerElementTypes, package.Document.Pages[0].Elements.Select(element => element.Type));
    }

    [Fact]
    public void Pxas_roundtrip_preserves_the_complete_current_workbook_contract()
    {
        var workbook = CompleteSpreadsheet();

        var package = PxaProjectArchive.ReadSpreadsheet(
            new MemoryStream(PxaProjectArchive.WriteSpreadsheet(workbook)));

        AssertJsonEquivalent(workbook, package.Document);
    }

    [Fact]
    public void Pdf_manifest_records_the_capabilities_required_by_the_document()
    {
        var package = PxaProjectArchive.ReadPdf(
            new MemoryStream(PxaProjectArchive.WritePdf(CompletePdfDesign())));

        Assert.Equal(new[]
        {
            PxaProjectCapabilities.PdfAccessibility,
            PxaProjectCapabilities.PdfBindings,
            PxaProjectCapabilities.PdfCharts,
            PxaProjectCapabilities.PdfDesigner,
            PxaProjectCapabilities.PdfLocalization,
            PxaProjectCapabilities.PdfPerPageLayouts,
            PxaProjectCapabilities.PdfTrustSettings,
        }, package.Manifest.RequiredFeatures);
    }

    [Fact]
    public void Spreadsheet_manifest_records_capabilities_and_review_diagnostics()
    {
        var workbook = CompleteSpreadsheet();
        workbook.ImportDiagnostics =
        [
            new ImportDiagnosticDto
            {
                Code = "PXASHEET_IMPORT_REVIEW",
                Severity = "warning",
                Message = "Imported workbook content requires review.",
                Source = "workbook",
            },
        ];
        workbook.Sheets[0].Cells.Add(new CellDto
        {
            Row = 10,
            Col = 0,
            Type = "formula",
            Formula = "='[External.xlsx]Sheet1'!A1",
        });
        workbook.Sheets[0].Extensions = new Dictionary<string, JsonElement>
        {
            ["vbaMetadata"] = Json("{\"moduleCount\":1}"),
            ["providerExtension"] = Json("{\"name\":\"synthetic\"}"),
        };

        var package = PxaProjectArchive.ReadSpreadsheet(
            new MemoryStream(PxaProjectArchive.WriteSpreadsheet(workbook)));

        Assert.Equal(new[]
        {
            PxaProjectCapabilities.SpreadsheetConditionalFormatting,
            PxaProjectCapabilities.SpreadsheetDesigner,
            PxaProjectCapabilities.SpreadsheetFormulas,
            PxaProjectCapabilities.SpreadsheetImages,
            PxaProjectCapabilities.SpreadsheetPrintLayout,
            PxaProjectCapabilities.SpreadsheetValidation,
        }, package.Manifest.RequiredFeatures);
        Assert.Contains(package.Manifest.Diagnostics, diagnostic =>
            diagnostic.Code == "PXASHEET_IMPORT_REVIEW" && diagnostic.Source == "workbook");
        Assert.Contains(package.Manifest.Diagnostics, diagnostic =>
            diagnostic.Code == "PXAPROJECT_EXTERNAL_LINK" && diagnostic.Source == "sheets[0]");
        Assert.Contains(package.Manifest.Diagnostics, diagnostic =>
            diagnostic.Code == "PXAPROJECT_MACRO_PRESERVED" && diagnostic.Source == "sheets[0]");
        Assert.Contains(package.Manifest.Diagnostics, diagnostic =>
            diagnostic.Code == "PXAPROJECT_PROVIDER_EXTENSION" && diagnostic.Source == "sheets[0]");
    }

    [Fact]
    public void Unknown_future_capabilities_are_preserved_for_the_opening_application()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        byte[] futureManifest;
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        using (var manifestStream = archive.GetEntry(PxaProjectFormat.ManifestEntry)!.Open())
        {
            var manifest = JsonSerializer.Deserialize<PxaProjectManifest>(manifestStream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            manifest.RequiredFeatures.Add("pdf.future-capability");
            futureManifest = JsonSerializer.SerializeToUtf8Bytes(manifest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        var package = PxaProjectArchive.ReadPdf(new MemoryStream(
            Rewrite(bytes, PxaProjectFormat.ManifestEntry, futureManifest)));

        Assert.Contains("pdf.future-capability", package.Manifest.RequiredFeatures);
        Assert.DoesNotContain("pdf.future-capability", PxaProjectCapabilities.Supported);
    }

    [Fact]
    public void Complete_roundtrip_fixtures_cover_all_nullable_editable_contract_properties()
    {
        var pdf = CompletePdfDesign();
        AssertAllNullablePropertiesRepresented([CompleteElement()]);
        AssertAllNullablePropertiesRepresented([pdf.PageSettings!]);
        AssertAllNullablePropertiesRepresented(pdf.Pages.Select(page => page.Layout!).Where(layout => layout is not null));

        var workbook = CompleteSpreadsheet();
        AssertAllNullablePropertiesRepresented(workbook.Sheets);
        AssertAllNullablePropertiesRepresented(workbook.Sheets.SelectMany(sheet => sheet.Cells));
        AssertAllNullablePropertiesRepresented(
            workbook.Sheets.SelectMany(sheet => sheet.Images), nameof(SpreadsheetImageDto.AssetId));
    }

    [Fact]
    public void Assets_use_typed_folders_and_preserve_their_declared_kind()
    {
        var assets = new PxaProjectAsset[]
        {
            new("image-1", "image.png", "image/png", OnePixelPng, PxaProjectAssetKinds.Image),
            new("font-1", "font.ttf", "font/ttf", [0x00, 0x01, 0x00, 0x00], PxaProjectAssetKinds.Font),
            new("preview-1", "preview.png", "image/png", OnePixelPng, PxaProjectAssetKinds.Preview),
            new("source-1", "source.pdf", "application/pdf", "%PDF-1.7\n"u8.ToArray(), PxaProjectAssetKinds.Source),
            new("data-1", "data.json", "application/json", "{\"value\":1}"u8.ToArray(), PxaProjectAssetKinds.Data),
        };

        var bytes = PxaProjectArchive.WritePdf(PdfDesign(), new PxaProjectWriteOptions { Assets = assets });
        var package = PxaProjectArchive.ReadPdf(new MemoryStream(bytes));

        Assert.Equal(assets.Select(asset => asset.Kind).Order(), package.Assets.Select(asset => asset.Kind).Order());
        Assert.Contains(package.Manifest.Assets, asset => asset.Path == "assets/images/image-1.png");
        Assert.Contains(package.Manifest.Assets, asset => asset.Path == "assets/fonts/font-1.ttf");
        Assert.Contains(package.Manifest.Assets, asset => asset.Path == "assets/previews/preview-1.png");
        Assert.Contains(package.Manifest.Assets, asset => asset.Path == "assets/sources/source-1.pdf");
        Assert.Contains(package.Manifest.Assets, asset => asset.Path == "assets/data/data-1.json");
    }

    [Fact]
    public void Asset_kind_must_match_the_sniffed_content_type()
    {
        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets =
                [
                    new PxaProjectAsset(
                        "not-a-font", "not-a-font.png", "image/png", OnePixelPng, PxaProjectAssetKinds.Font),
                ],
            }));

        Assert.Equal("PXAPROJECT010", error.Code);
    }

    [Fact]
    public void Asset_filenames_reject_invisible_format_characters()
    {
        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets =
                [
                    new PxaProjectAsset("image", "safe\u202Egnp.exe", "image/png", OnePixelPng),
                ],
            }));

        Assert.Equal("PXAPROJECT003", error.Code);
    }

    [Fact]
    public void Project_metadata_and_rich_text_are_bounded_and_reject_control_characters()
    {
        var metadata = PdfDesign();
        metadata.PageSettings = new PageSettingsDto
        {
            Metadata = new PdfMetadataDto { Title = "Unsafe\0title" },
        };
        var metadataError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.WritePdf(metadata));

        var richText = PdfDesign();
        richText.Pages[0].Elements =
        [
            new ElementDto
            {
                Id = "rich-text", Type = "richtext",
                HtmlContent = new string('a', PXA.Core.Security.PxaRichTextSanitizer.MaximumHtmlLength + 1),
            },
        ];
        var richTextError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.WritePdf(richText));

        Assert.Equal("PXAPROJECT008", metadataError.Code);
        Assert.Equal("PXAPROJECT008", richTextError.Code);
    }

    [Theory]
    [InlineData("application/x-msdownload", "executable")]
    [InlineData("application/zip", "external-package")]
    public void Executable_and_external_package_assets_are_rejected(string contentType, string id)
    {
        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets = [new PxaProjectAsset(id, $"{id}.bin", contentType, [1, 2, 3], PxaProjectAssetKinds.Data)],
            }));

        Assert.Equal("PXAPROJECT010", error.Code);
    }

    [Fact]
    public void Svg_assets_cannot_reference_network_content()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://example.test/a.png\" /></svg>"u8.ToArray();

        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets = [new PxaProjectAsset("svg", "svg.svg", "image/svg+xml", svg)],
            }));

        Assert.Equal("PXAPROJECT010", error.Code);
    }

    [Theory]
    [InlineData("accessToken", "\"opaque-value\"")]
    [InlineData("futureValue", "\"https://storage.example.test/signed\"")]
    [InlineData("futureValue", "\"/Users/example/private-file\"")]
    public void Extension_data_cannot_hide_secrets_or_external_locations(string name, string json)
    {
        var design = PdfDesign();
        design.Extensions = new Dictionary<string, JsonElement> { [name] = Json(json) };

        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(design));

        Assert.Equal("PXAPROJECT008", error.Code);
    }

    [Fact]
    public void Pxas_roundtrip_preserves_formulas_cached_values_and_layout()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "spreadsheet-project-1",
            Name = "Forecast",
            DefinedNames = [new DefinedNameDto { Name = "Revenue", RefersTo = "Plan!$A$1" }],
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Name = "Plan",
                    PageSetup = new PageSetupDto { Orientation = "landscape", PaperSize = "A4" },
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Type = "number", Value = 21d },
                        new CellDto { Row = 0, Col = 1, Type = "formula", Formula = "=A1*2", Value = 42d },
                    ],
                },
            ],
        };
        var bytes = PxaProjectArchive.WriteSpreadsheet(workbook, new PxaProjectWriteOptions
        {
            CreatedAtUtc = Created,
            ModifiedAtUtc = Created.AddMinutes(1),
        });

        var package = PxaProjectArchive.ReadSpreadsheet(new MemoryStream(bytes));

        Assert.Equal(PxaProjectFormat.PxasFormatId, package.Manifest.FormatId);
        Assert.Equal("=A1*2", package.Document.Sheets[0].Cells[1].Formula);
        Assert.Equal(42d, Assert.IsType<JsonElement>(package.Document.Sheets[0].Cells[1].Value).GetDouble());
        Assert.Equal("landscape", package.Document.Sheets[0].PageSetup?.Orientation);
        Assert.Equal("Plan!$A$1", package.Document.DefinedNames[0].RefersTo);
    }

    [Fact]
    public void Project_kinds_cannot_be_confused()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadSpreadsheet(new MemoryStream(bytes)));

        Assert.Equal("PXAPROJECT002", error.Code);
    }

    [Fact]
    public void Tampered_document_is_rejected_before_deserialization()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        var tampered = Rewrite(bytes, PxaProjectFormat.DocumentEntry, Encoding.UTF8.GetBytes("{}"));

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(tampered)));

        Assert.Equal("PXAPROJECT007", error.Code);
    }

    [Fact]
    public void Inspect_reads_envelope_metadata_without_expanding_or_trusting_document_content()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        var tampered = Rewrite(bytes, PxaProjectFormat.DocumentEntry, "{}"u8.ToArray());

        var manifest = PxaProjectArchive.Inspect(new MemoryStream(tampered), PxaProjectKind.Pdf);

        Assert.Equal(PxaProjectFormat.PxapFormatId, manifest.FormatId);
        var validationError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(tampered)));
        Assert.Equal("PXAPROJECT007", validationError.Code);
    }

    [Fact]
    public void Unsafe_and_duplicate_paths_are_rejected()
    {
        var traversal = ArchiveWithEntries(("../document.json", "{}"u8.ToArray()));
        var traversalError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(traversal)));
        Assert.Equal("PXAPROJECT003", traversalError.Code);

        var duplicate = ArchiveWithEntries(
            ("manifest.json", "{}"u8.ToArray()),
            ("Manifest.json", "{}"u8.ToArray()));
        var duplicateError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(duplicate)));
        Assert.Equal("PXAPROJECT004", duplicateError.Code);
    }

    [Fact]
    public void Invalid_utf8_json_is_rejected_with_a_stable_diagnostic()
    {
        var invalid = ArchiveWithEntries(
            (PxaProjectFormat.ManifestEntry, [0xff, 0xfe, 0xfd]),
            (PxaProjectFormat.DocumentEntry, "{}"u8.ToArray()));

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(invalid)));

        Assert.Equal("PXAPROJECT009", error.Code);
    }

    [Fact]
    public void Compression_bombs_and_nested_archives_are_rejected()
    {
        var bomb = ArchiveWithEntries(("document.json", new byte[2 * 1024 * 1024]));
        var bombError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(bomb), new PxaProjectReadLimits
            {
                MaximumCompressionRatio = 5,
            }));
        Assert.Equal("PXAPROJECT005", bombError.Code);

        var nested = ArchiveWithEntries(("assets/archive.png", [0x50, 0x4b, 0x03, 0x04, 0x00]));
        var nestedError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(nested)));
        Assert.Equal("PXAPROJECT011", nestedError.Code);
    }

    [Fact]
    public void Future_major_envelope_is_rejected_with_upgrade_diagnostic()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        byte[] futureManifest;
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        using (var manifestStream = archive.GetEntry(PxaProjectFormat.ManifestEntry)!.Open())
        {
            var manifest = JsonSerializer.Deserialize<PxaProjectManifest>(manifestStream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            manifest.EnvelopeVersion = "2.0";
            futureManifest = JsonSerializer.SerializeToUtf8Bytes(manifest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        var future = Rewrite(bytes, PxaProjectFormat.ManifestEntry, futureManifest);

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(future)));

        Assert.Equal("PXAPROJECT014", error.Code);
    }

    [Fact]
    public void Malformed_truncated_and_oversized_packages_have_stable_diagnostics()
    {
        var valid = PxaProjectArchive.WritePdf(PdfDesign());
        var malformedManifest = Rewrite(valid, PxaProjectFormat.ManifestEntry, "{"u8.ToArray());
        var truncated = valid[..^12];

        var malformedError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(malformedManifest)));
        var truncatedError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(truncated)));
        var oversizedError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(valid), new PxaProjectReadLimits
            {
                MaximumCompressedBytes = valid.Length - 1,
            }));

        Assert.Equal("PXAPROJECT009", malformedError.Code);
        Assert.Equal("PXAPROJECT001", truncatedError.Code);
        Assert.Equal("PXAPROJECT005", oversizedError.Code);
    }

    [Fact]
    public void Manifest_integrity_and_version_tampering_is_rejected_before_document_use()
    {
        var valid = PxaProjectArchive.WritePdf(PdfDesign());
        var invalidHash = RewriteManifest(valid, manifest => manifest.ContentHash = new string('0', 64));
        var oldUnsupportedSchema = RewriteManifest(valid, manifest => manifest.DocumentSchemaVersion = "0.9");
        var futureSchema = RewriteManifest(valid, manifest => manifest.DocumentSchemaVersion = "1.1");
        var futureApplication = RewriteManifest(valid, manifest => manifest.MinimumPxaVersion = "999.0.0");

        Assert.Equal("PXAPROJECT007", Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(invalidHash))).Code);
        Assert.Equal("PXAPROJECT008", Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(oldUnsupportedSchema))).Code);
        Assert.Equal("PXAPROJECT014", Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(futureSchema))).Code);
        var applicationError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(futureApplication)));
        Assert.Equal("PXAPROJECT014", applicationError.Code);
        Assert.Contains("999.0.0", applicationError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Current_pdf_and_spreadsheet_packages_declare_the_running_product_version()
    {
        var pdf = PxaProjectArchive.ReadPdf(new MemoryStream(PxaProjectArchive.WritePdf(PdfDesign())));
        var spreadsheet = PxaProjectArchive.ReadSpreadsheet(new MemoryStream(
            PxaProjectArchive.WriteSpreadsheet(CompleteSpreadsheet())));

        Assert.Equal(PxaProjectFormat.CurrentProductVersion, pdf.Manifest.MinimumPxaVersion);
        Assert.Equal(PxaProjectFormat.CurrentProductVersion, spreadsheet.Manifest.MinimumPxaVersion);
        Assert.True(PxaProjectVersionPolicy.IsProductVersionAtLeast(
            PxaProjectFormat.CurrentProductVersion, pdf.Manifest.MinimumPxaVersion));
    }

    [Fact]
    public void Passwords_are_never_written_to_portable_projects()
    {
        var pdf = PdfDesign();
        pdf.PageSettings = new PageSettingsDto
        {
            Encryption = new PdfEncryptionDto { Enabled = true, OwnerPassword = "must-not-leave-memory" },
        };
        var pdfError = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(pdf));
        Assert.Equal("PXAPROJECT012", pdfError.Code);

        var workbook = new SpreadsheetDto
        {
            Id = "protected-sheet",
            Name = "Protected",
            Sheets = [new SheetDto { Id = "sheet", Protection = new ProtectionDto { Protected = true, Password = "secret" } }],
        };
        var sheetError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.WriteSpreadsheet(workbook));
        Assert.Equal("PXAPROJECT012", sheetError.Code);
    }

    [Fact]
    public void Invalid_asset_type_is_rejected_instead_of_trusting_its_filename()
    {
        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets = [new PxaProjectAsset("fake-image", "looks-safe.png", "image/png", "not a png"u8.ToArray())],
            }));

        Assert.Equal("PXAPROJECT010", error.Code);
    }

    [Fact]
    public void Raster_assets_are_rejected_when_decoded_dimensions_exceed_the_pixel_limit()
    {
        var oversizedPng = OnePixelPng.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(oversizedPng.AsSpan(16, 4), 20_000);
        BinaryPrimitives.WriteUInt32BigEndian(oversizedPng.AsSpan(20, 4), 20_000);

        var error = Assert.Throws<PxaProjectFormatException>(() => PxaProjectArchive.WritePdf(
            PdfDesign(),
            new PxaProjectWriteOptions
            {
                Assets = [new PxaProjectAsset("oversized", "oversized.png", "image/png", oversizedPng)],
            }));

        Assert.Equal("PXAPROJECT005", error.Code);
    }

    [Fact]
    public void Read_write_and_inspect_honor_cancellation()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            PxaProjectArchive.WritePdf(PdfDesign(), cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(bytes), cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() =>
            PxaProjectArchive.Inspect(
                new MemoryStream(bytes), PxaProjectKind.Pdf, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Read_rejects_packages_that_exceed_the_processing_deadline()
    {
        var bytes = PxaProjectArchive.WritePdf(PdfDesign());
        using var slowStream = new SlowReadStream(bytes, TimeSpan.FromMilliseconds(20));

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(slowStream, new PxaProjectReadLimits
            {
                MaximumProcessingDuration = TimeSpan.FromMilliseconds(1),
            }));

        Assert.Equal("PXAPROJECT005", error.Code);
    }

    [Fact]
    public void Document_complexity_limits_are_enforced_on_write_and_read()
    {
        var design = PdfDesign();
        design.Pages.Add(new PageDto { Id = "page-2" });
        var bytes = PxaProjectArchive.WritePdf(design);

        var readError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(bytes), new PxaProjectReadLimits { MaximumPages = 1 }));
        Assert.Equal("PXAPROJECT005", readError.Code);

        var workbook = new SpreadsheetDto
        {
            Id = "large-workbook",
            Name = "Large",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet",
                    Cells =
                    [
                        new CellDto { Row = 0, Col = 0, Formula = "=1", Type = "formula" },
                        new CellDto { Row = 1, Col = 0, Formula = "=2", Type = "formula" },
                    ],
                },
            ],
        };
        var workbookBytes = PxaProjectArchive.WriteSpreadsheet(workbook);
        var formulaError = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadSpreadsheet(new MemoryStream(workbookBytes),
                new PxaProjectReadLimits { MaximumFormulas = 1 }));
        Assert.Equal("PXAPROJECT005", formulaError.Code);
    }

    [Fact]
    public void Unknown_pdf_and_spreadsheet_properties_survive_bounded_roundtrips()
    {
        var pdf = PdfDesign();
        pdf.Extensions = new Dictionary<string, JsonElement>
        {
            ["futureDocumentOption"] = Json("{\"enabled\":true}"),
        };
        pdf.Pages[0].Extensions = new Dictionary<string, JsonElement>
        {
            ["futurePageOption"] = Json("[1,2,3]"),
        };
        var pdfPackage = PxaProjectArchive.ReadPdf(new MemoryStream(PxaProjectArchive.WritePdf(
            pdf,
            new PxaProjectWriteOptions
            {
                ManifestExtensions = new Dictionary<string, JsonElement>
                {
                    ["futureManifestOption"] = Json("\"manifest-value\""),
                },
            })));

        var workbook = new SpreadsheetDto
        {
            Id = "future-workbook",
            Name = "Future workbook",
            Extensions = new Dictionary<string, JsonElement>
            {
                ["futureWorkbookOption"] = Json("\"preserved\""),
            },
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet",
                    Extensions = new Dictionary<string, JsonElement>
                    {
                        ["futureSheetOption"] = Json("42"),
                    },
                },
            ],
        };
        var spreadsheetPackage = PxaProjectArchive.ReadSpreadsheet(
            new MemoryStream(PxaProjectArchive.WriteSpreadsheet(workbook)));

        Assert.True(pdfPackage.Document.Extensions!["futureDocumentOption"].GetProperty("enabled").GetBoolean());
        Assert.Equal(3, pdfPackage.Document.Pages[0].Extensions!["futurePageOption"].GetArrayLength());
        Assert.Equal("manifest-value", pdfPackage.Manifest.Extensions!["futureManifestOption"].GetString());
        Assert.Equal("preserved", spreadsheetPackage.Document.Extensions!["futureWorkbookOption"].GetString());
        Assert.Equal(42, spreadsheetPackage.Document.Sheets[0].Extensions!["futureSheetOption"].GetInt32());
    }

    [Fact]
    public void Forward_compatible_extension_data_is_bounded()
    {
        var pdf = PdfDesign();
        pdf.Extensions = new Dictionary<string, JsonElement>
        {
            ["futureOne"] = Json("1"),
            ["futureTwo"] = Json("2"),
        };
        var bytes = PxaProjectArchive.WritePdf(pdf);

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(bytes), new PxaProjectReadLimits
            {
                MaximumExtensionProperties = 1,
            }));

        Assert.Equal("PXAPROJECT005", error.Code);
    }

    [Fact]
    public void Mimetype_must_be_the_first_uncompressed_entry()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (archive.CreateEntry("manifest.json").Open()) { }
            var mimetype = archive.CreateEntry(PxaProjectFormat.MimeTypeEntry, CompressionLevel.NoCompression);
            using var stream = mimetype.Open();
            stream.Write(Encoding.ASCII.GetBytes(PxaProjectFormat.PxapMediaType));
        }

        var error = Assert.Throws<PxaProjectFormatException>(() =>
            PxaProjectArchive.ReadPdf(new MemoryStream(output.ToArray())));
        Assert.Equal("PXAPROJECT002", error.Code);
    }

    private static DesignExportDto CompletePdfDesign() => new()
    {
        Id = "complete-pdf",
        Name = "Complete PDF project",
        Category = "contract-test",
        Description = "All currently supported editable fields",
        Pages =
        [
            new PageDto
            {
                Id = "page-portrait",
                Layout = new PageLayoutDto
                {
                    Width = 595,
                    Height = 842,
                    Orientation = "portrait",
                    BackgroundColor = "#ffffff",
                    BackgroundImage = "asset:background",
                    BackgroundImageFit = "cover",
                    Margins = new MarginsDto { Top = 24, Right = 25, Bottom = 26, Left = 27 },
                    BleedSize = 3,
                },
                Elements = [CompleteElement()],
                Extensions = new Dictionary<string, JsonElement> { ["futurePage"] = Json("{\"enabled\":true}") },
            },
            new PageDto
            {
                Id = "page-landscape",
                Layout = new PageLayoutDto { Width = 842, Height = 595, Orientation = "landscape" },
                Elements = [new ElementDto { Id = "second-page", Type = "text", Content = "Second page" }],
            },
        ],
        SharedElements = [new ElementDto { Id = "shared-header", Type = "text", Content = "Shared" }],
        PageSettings = new PageSettingsDto
        {
            Width = 595,
            Height = 842,
            Orientation = "portrait",
            Unit = "pt",
            BackgroundColor = "#f8fafc",
            BackgroundImage = "asset:default-background",
            BackgroundImageFit = "contain",
            Margins = new MarginsDto { Top = 36, Right = 37, Bottom = 38, Left = 39 },
            BleedSize = 4,
            PageNumbering = new PageNumberingDto
            {
                Format = "current-total", StartNumber = 2, Prefix = "Page ", Suffix = "!",
                ShowOnFirstPage = false, Placement = "bottom-right",
            },
            GlobalWatermark = new GlobalWatermarkDto
            {
                Mode = "text", Content = "DRAFT", Rotation = 35, Scale = 0.8,
                PageScope = "range", PageRange = "1-2", Color = "#64748b", FontSize = 42,
            },
            Metadata = new PdfMetadataDto
            {
                Title = "Metadata title", Author = "PXA", Subject = "Roundtrip", Keywords = "pxa,test",
            },
            NamedStyles =
            [
                new NamedStyleDto
                {
                    Id = "heading", Name = "Heading", Type = "paragraph", BasedOn = "normal",
                    NextStyle = "body", Style = new Dictionary<string, object> { ["fontSize"] = 18, ["bold"] = true },
                },
            ],
            Protection = new DocumentProtectionDto { Enabled = true, Mode = "formFields" },
            Encryption = new PdfEncryptionDto
            {
                Enabled = true,
                Algorithm = "Aes256",
                Permissions = new PdfEncryptionPermissionsDto
                {
                    Print = true, Modify = false, Copy = false, Annotate = true, FillForms = true,
                    ExtractAccessibility = true, Assemble = false, PrintHighResolution = true,
                },
            },
            ComplianceProfile = "PDF/A-2b",
            AccessibilityProfile = "PDF/UA-1",
            CustomProperties = [new CustomDocumentPropertyDto { Name = "Department", Value = "Engineering", Type = "text" }],
            TrackChanges = true,
            SystemLanguage = "en",
            ActiveLanguages = ["en", "de", "ar"],
            LocalizedProperties =
            [
                new LocalizedPropertyDto
                {
                    Key = "invoice.title", Scope = "global",
                    LocalizedValues = new Dictionary<string, string> { ["en"] = "Invoice", ["de"] = "Rechnung" },
                },
            ],
            TargetLanguage = "de",
            Extensions = new Dictionary<string, JsonElement> { ["futurePageSetting"] = Json("42") },
        },
        ImportDiagnostics =
        [
            new ImportDiagnosticDto { Code = "TEST001", Severity = "info", Message = "Synthetic", Source = "fixture" },
        ],
        Extensions = new Dictionary<string, JsonElement> { ["futureDocument"] = Json("[1,2,3]") },
    };

    private static ElementDto CompleteElement() => new()
    {
        Id = "complete-element",
        Type = "chart",
        Name = "Complete element",
        X = 10,
        Y = 20,
        Width = 300,
        Height = 180,
        Hidden = false,
        Locked = true,
        VisibleExpression = "{{showChart}}",
        Accessibility = new AccessibilityPropertiesDto
        {
            Role = "Figure", CustomRole = "Chart", NamespaceUri = "urn:pxa:test", IsArtifact = false,
            ParentElementId = "section-1", ReadingOrder = 3, Language = "en", AlternateText = "Sales chart",
            ActualText = "Sales increased", TableHeaderScope = "Column", TableHeaderIds = ["header-1"],
        },
        Content = "Visible content",
        Style = new Dictionary<string, object>
        {
            ["fontFamily"] = "Inter", ["fontSize"] = 12, ["color"] = "#111827", ["rotation"] = 5,
        },
        HtmlContent = "<p>Safe <strong>rich text</strong></p>",
        FieldLabel = "Customer",
        FieldName = "customerName",
        Placeholder = "Enter a name",
        Required = true,
        SignatureLabel = "Approved by",
        QrValue = "PXA-ROUNDTRIP",
        QrSize = 128,
        BarcodeValue = "1234567890",
        BarcodeType = "code128",
        CellData = [["A", "B"], ["1", "2"]],
        ColumnWidths = [120, 180],
        ColumnAlignments = ["left", "right"],
        HeaderRow = true,
        FooterRow = true,
        HeaderBgColor = "#e2e8f0",
        ZebraEnabled = true,
        ZebraColor = "#f8fafc",
        CellStyles =
        [
            new CellStyleDto
            {
                Row = 1, Col = 1, BackgroundColor = "#fff7ed", TextAlign = "right",
                BorderColor = "#0f172a", BorderWidth = 1,
                BorderTop = new CellBorderSideDto { Color = "#ef4444", Width = 2 },
                BorderRight = new CellBorderSideDto { Color = "#22c55e", Width = 2 },
                BorderBottom = new CellBorderSideDto { Color = "#3b82f6", Width = 2 },
                BorderLeft = new CellBorderSideDto { Color = "#a855f7", Width = 2 },
                Padding = 6, FontFamily = "Inter", FontSize = 11, Bold = true, Italic = true, Color = "#111827",
            },
        ],
        NoteTitle = "Review",
        NoteBody = "Check values",
        NoteAuthor = "Reviewer",
        NoteCollapsed = false,
        AssetId = "image-fixture",
        FitMode = "cover",
        CropX = 1,
        CropY = 2,
        CropWidth = 90,
        CropHeight = 80,
        FocalX = 0.4,
        FocalY = 0.6,
        WatermarkMode = "text",
        PageScope = "range",
        PageRange = "1-2",
        ArrowMode = "line",
        ArrowDirection = "right",
        ArrowRotation = 15,
        StartMarker = "circle",
        EndMarker = "arrow",
        DrawTool = "pen",
        PathData = "M0 0 L10 10",
        Language = "ar",
        TextDirection = "rtl",
        ElementLanguage = "ar",
        ElementGroup = "localized-chart",
        LangOverrides = new Dictionary<string, LangOverrideDto>
        {
            ["ar"] = new() { X = 30, Y = 40, Width = 320, Height = 190, Rotation = 2 },
        },
        DateMode = "dynamic",
        DateFormat = "yyyy-MM-dd",
        Locale = "de-CH",
        Timezone = "Europe/Zurich",
        FallbackText = "Unknown date",
        MarkMode = "rectangle",
        CheckState = "checked",
        PageBoundaryMode = "break-before",
        NumberingFormat = "current-total",
        StartNumber = 2,
        Prefix = "P",
        Suffix = "S",
        Options = ["One", "Two"],
        SelectedValue = "Two",
        MultiSelect = true,
        Ordered = true,
        ListStyle = "decimal",
        Chart = new ChartDefinitionDto
        {
            SchemaVersion = 2,
            Type = PxaChartTypes.Combo,
            Title = "Revenue",
            Subtitle = "Quarterly",
            Locale = "de-CH",
            Categories = ["Q1", "Q2"],
            Series =
            [
                new ChartSeriesDto
                {
                    Id = "revenue", Name = "Revenue", Type = PxaChartTypes.Bar,
                    Values = [12, null], Color = "#2563eb", StackGroup = "sales",
                    ValueAxisId = "primary", Fill = true, ShowMarkers = false,
                },
            ],
            CategoryAxis = new ChartAxisDto
            {
                Id = "category", Title = "Quarter", Minimum = 0, Maximum = 2,
                Scale = "linear", NumberFormat = "0", Visible = true, GridLines = false,
            },
            ValueAxes =
            [
                new ChartAxisDto
                {
                    Id = "primary", Title = "CHF", Minimum = -10, Maximum = 100,
                    Scale = "linear", NumberFormat = "CHF #,##0", Visible = true, GridLines = true,
                },
            ],
            Legend = new ChartLegendDto { Visible = true, Position = "right" },
            DataLabels = new ChartDataLabelsDto { Visible = true, Position = "outside", NumberFormat = "0.0" },
            Palette = ["#2563eb", "#16a34a"],
            Binding = new ChartBindingDto
            {
                DataPath = "sales", CategoryField = "quarter", SeriesField = "region",
                ValueField = "amount", Aggregation = "sum", Sort = "ascending",
            },
            Recognition = new ChartRecognitionDto
            {
                Status = "review", Confidence = 0.82, SourceKind = "vector",
                SourceAssetId = "chart-source", DiagnosticCode = "PXACHART001",
            },
        },
        ChartType = "combo",
        ChartData = new Dictionary<string, object> { ["legacy"] = true },
        Href = "#bookmark-target",
        LinkTarget = "document",
        ButtonAction = "submit",
        NumberValue = 1234.56,
        NumberStyle = "currency",
        NumberDecimals = 2,
        NumberCurrency = "CHF",
        NumberLocale = "de-CH",
        StyleName = "heading",
        CharacterStyle = "emphasis",
        FootnoteText = "Footnote",
        FootnoteRef = "fn-1",
        BookmarkName = "bookmark-source",
        BookmarkTarget = "bookmark-target",
        CommentAuthor = "Author",
        CommentDate = "2026-09-30T10:00:00Z",
        CommentText = "Comment",
        CommentId = "comment-1",
        ContentControlType = "text",
        ContentControlTag = "customer",
        ContentControlTitle = "Customer",
        ContentControlPlaceholder = "Enter customer",
        RevisionType = "insert",
        RevisionAuthor = "Editor",
        RevisionDate = "2026-09-30T10:00:00Z",
        RevisionId = "revision-1",
        AutoHyphenation = true,
        Binding = "customer.name",
        Expression = "{{customer.name}}",
        Formatter = "uppercase",
        Repeat = new RepeatDto { DataPath = "items", TemplateId = "row-template" },
        Extensions = new Dictionary<string, JsonElement> { ["futureElement"] = Json("{\"mode\":\"safe\"}") },
        PreserveAspectRatio = true,
        HeadingLevel = 2,
        TocTitle = "Contents",
        TocShowPageNumbers = true,
        TocShowLeaderDots = true,
        TocMinLevel = 1,
        TocMaxLevel = 3,
        TocPlacement = "beginning",
        TocEntries = [new TocEntryDto { Text = "Section", Level = 2, Page = 1 }],
    };

    private static SpreadsheetDto CompleteSpreadsheet() => new()
    {
        Schema = "https://powerdoxautomation.com/schemas/pxa-workbook.schema.json",
        SchemaVersion = SpreadsheetDto.CurrentSchemaVersion,
        Id = "complete-workbook",
        Name = "Complete workbook",
        DefinedNames = [new DefinedNameDto { Name = "Revenue", RefersTo = "Summary!$A$1:$A$3" }],
        Extensions = new Dictionary<string, JsonElement> { ["futureWorkbook"] = Json("true") },
        Sheets =
        [
            new SheetDto
            {
                Id = "summary",
                Name = "Summary",
                RowCount = 250,
                ColCount = 40,
                Columns = [new SheetColumnDto { Index = 0, Width = 22.5, Hidden = false, OutlineLevel = 1 }],
                Rows = [new SheetRowDto { Index = 0, Height = 24, Hidden = false, OutlineLevel = 1 }],
                Cells =
                [
                    new CellDto
                    {
                        Row = 0, Col = 0, Type = "text", Value = "Revenue", NumberFormat = "@",
                        Style = new CellStyleDto
                        {
                            BackgroundColor = "#dbeafe", TextAlign = "center", BorderColor = "#1e3a8a",
                            BorderWidth = 1, Padding = 4, FontFamily = "Inter", FontSize = 12,
                            Bold = true, Italic = false, Color = "#172554",
                        },
                        Comment = "Synthetic", Hyperlink = "Summary!A2",
                        Extensions = new Dictionary<string, JsonElement> { ["futureCell"] = Json("7") },
                    },
                    new CellDto { Row = 1, Col = 0, Type = "number", Value = 21.5, NumberFormat = "#,##0.00" },
                    new CellDto { Row = 2, Col = 0, Type = "formula", Formula = "=A2*2", Value = 43d },
                    new CellDto { Row = 3, Col = 0, Type = "boolean", Value = true },
                    new CellDto { Row = 4, Col = 0, Type = "date", Value = "2026-09-30" },
                ],
                Merges = ["A1:B1"],
                FrozenRows = 1,
                FrozenCols = 1,
                AutoFilterRange = "A1:D20",
                PageSetup = new PageSetupDto
                {
                    Orientation = "landscape", PaperSize = "A4", PrintArea = "A1:D20",
                    Header = "PXA", Footer = "Page &P", FitToWidth = 1, FitToHeight = 2,
                    Scale = 90, Margins = new MarginsDto { Top = 10, Right = 11, Bottom = 12, Left = 13 },
                    RowPageBreaks = [20], ColPageBreaks = [4],
                },
                Protection = new ProtectionDto { Protected = true },
                ConditionalFormats =
                [
                    new ConditionalFormatDto
                    {
                        Range = "A2:A20", Type = "cellIs", Operator = "greaterThan",
                        Value = "10", Value2 = "100", Color = "#22c55e",
                    },
                ],
                DataValidations =
                [
                    new DataValidationDto
                    {
                        Range = "B2:B20", Type = "list", Operator = "between",
                        Value1 = "1", Value2 = "10", ListSource = "Open,Closed",
                    },
                ],
                Images =
                [
                    new SpreadsheetImageDto
                    {
                        Id = "sheet-image", FileName = "image.png", ContentType = "image/png",
                        Data = $"data:image/png;base64,{Convert.ToBase64String(OnePixelPng)}",
                        Row = 5, Col = 2, Width = 200, Height = 100, AltText = "Logo",
                        Extensions = new Dictionary<string, JsonElement> { ["futureImage"] = Json("\"fit\"") },
                    },
                ],
                Extensions = new Dictionary<string, JsonElement> { ["futureSheet"] = Json("{\"rtl\":true}") },
            },
        ],
    };

    private static void AssertJsonEquivalent<T>(T expected, T actual)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var expectedNode = JsonSerializer.SerializeToNode(expected, options);
        var actualNode = JsonSerializer.SerializeToNode(actual, options);
        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode),
            $"Roundtrip mismatch.\nExpected: {expectedNode}\nActual: {actualNode}");
    }

    private static void AssertAllNullablePropertiesRepresented<T>(
        IEnumerable<T> instances,
        params string[] excludedProperties)
    {
        var values = instances.Cast<object>().ToArray();
        Assert.NotEmpty(values);
        var excluded = excludedProperties.ToHashSet(StringComparer.Ordinal);
        foreach (var property in typeof(T).GetProperties().Where(property =>
                     property.CanRead &&
                     !excluded.Contains(property.Name) &&
                     (!property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null)))
        {
            Assert.True(values.Any(instance => IsMeaningfullyPopulated(property.GetValue(instance))),
                $"The complete native-format fixture does not represent {typeof(T).Name}.{property.Name}.");
        }
    }

    private static bool IsMeaningfullyPopulated(object? value)
    {
        if (value is null)
            return false;
        if (value is string text)
            return !string.IsNullOrWhiteSpace(text);
        if (value is System.Collections.IEnumerable items)
        {
            var enumerator = items.GetEnumerator();
            try
            {
                return enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
        return true;
    }

    private static DesignExportDto PdfDesign() => new()
    {
        Id = "pdf-project",
        Name = "PDF project",
        Pages = [new PageDto { Id = "page-1" }],
    };

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static byte[] Rewrite(byte[] source, string replacementPath, byte[] replacement)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var next = target.CreateEntry(
                    entry.FullName,
                    entry.FullName == PxaProjectFormat.MimeTypeEntry
                        ? CompressionLevel.NoCompression
                        : CompressionLevel.Optimal);
                next.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var targetStream = next.Open();
                if (entry.FullName == replacementPath)
                    targetStream.Write(replacement);
                else
                {
                    using var sourceStream = entry.Open();
                    sourceStream.CopyTo(targetStream);
                }
            }
        }
        return output.ToArray();
    }

    private static byte[] RewriteManifest(byte[] source, Action<PxaProjectManifest> mutate)
    {
        using var archive = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
        using var manifestStream = archive.GetEntry(PxaProjectFormat.ManifestEntry)!.Open();
        var manifest = JsonSerializer.Deserialize<PxaProjectManifest>(manifestStream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        mutate(manifest);
        return Rewrite(source, PxaProjectFormat.ManifestEntry,
            JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static byte[] ArchiveWithEntries(params (string Path, byte[] Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var mimetype = archive.CreateEntry(PxaProjectFormat.MimeTypeEntry, CompressionLevel.NoCompression);
            using (var stream = mimetype.Open())
                stream.Write(Encoding.ASCII.GetBytes(PxaProjectFormat.PxapMediaType));
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Path, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(item.Content);
            }
        }
        return output.ToArray();
    }

    private sealed class SlowReadStream(byte[] content, TimeSpan delay) : MemoryStream(content)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(delay);
            return base.Read(buffer, offset, Math.Min(count, 32));
        }
    }

    private sealed class TestUpgradeStep(
        PxaProjectKind kind,
        string fromVersion,
        string toVersion,
        Func<JsonObject, PxaProjectUpgradeStepResult> apply) : IPxaProjectUpgradeStep
    {
        public PxaProjectKind Kind { get; } = kind;
        public string FromVersion { get; } = fromVersion;
        public string ToVersion { get; } = toVersion;
        public PxaProjectUpgradeStepResult Apply(JsonObject source) => apply(source);
    }
}
