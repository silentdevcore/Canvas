using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Core.ProjectFormats;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class DesignerProjectsControllerTests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static readonly string PngDataUrl = $"data:image/png;base64,{Convert.ToBase64String(PngBytes)}";
    private readonly Guid organizationId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly RecordingAuditWriter auditWriter = new();
    private readonly DesignerProjectsController controller;

    public DesignerProjectsControllerTests()
    {
        controller = new DesignerProjectsController(
            new TestTenantContext(userId, organizationId), auditWriter);
    }

    [Fact]
    public async Task Pdf_project_export_and_open_roundtrip_the_editable_document()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-project",
            Name = "Annual Report",
            Pages = [new PageDto { Id = "page-1" }],
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportPdf(document));
        var opened = await OpenPdf(exported.FileContents, "annual-report.pxap");

        Assert.Equal(PxaProjectFormat.PxapMediaType, exported.ContentType);
        Assert.EndsWith(".pxap", exported.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal(document.Id, opened.Document.Id);
        Assert.Equal(document.Name, opened.Manifest.Title);
        Assert.Equal("pxap", opened.Manifest.FormatId);
    }

    [Fact]
    public async Task Pdf_project_export_optionally_preserves_inert_code_workspace()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-code-project",
            Name = "Code project",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var workspace = new PxaCodeWorkspaceProjectComponent
        {
            Revision = 3,
            BaseTemplateRevision = 2,
            JsonDraft = "{}",
            CSharpPdfDraft = "throw new Exception(\"not executed\");",
            CanonicalDesignJson = "{\"id\":\"pdf-code-project\"}",
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportPdfWithWorkspace(
            new PxaPdfProjectExportRequest(document, workspace)));
        var opened = await OpenPdf(exported.FileContents, "code-project.pxap");

        Assert.NotNull(opened.CodeWorkspace);
        Assert.Equal(3, opened.CodeWorkspace.Revision);
        Assert.Equal(workspace.CSharpPdfDraft, opened.CodeWorkspace.CSharpPdfDraft);
    }

    [Fact]
    public async Task Spreadsheet_project_export_and_open_roundtrip_formulas()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "spreadsheet-project",
            Name = "Forecast",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Cells = [new CellDto { Row = 0, Col = 0, Type = "formula", Formula = "=1+1", Value = 2 }],
                },
            ],
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportSpreadsheet(workbook));
        var opened = await OpenSpreadsheet(exported.FileContents, "forecast.pxas");

        Assert.Equal(PxaProjectFormat.PxasMediaType, exported.ContentType);
        Assert.EndsWith(".pxas", exported.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal("=1+1", opened.Document.Sheets[0].Cells[0].Formula);
        Assert.Equal("pxas", opened.Manifest.FormatId);
    }

    [Fact]
    public async Task Open_reports_diagnostics_that_require_review()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "spreadsheet-review",
            Name = "Review workbook",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Cells =
                    [
                        new CellDto
                        {
                            Row = 0, Col = 0, Type = "formula",
                            Formula = "='[External.xlsx]Sheet1'!A1",
                        },
                    ],
                },
            ],
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportSpreadsheet(workbook));
        var opened = await OpenSpreadsheet(exported.FileContents, "review.pxas");

        Assert.True(opened.Compatibility.RequiresReview);
        Assert.Empty(opened.Compatibility.MissingFeatures);
        Assert.Contains(opened.Compatibility.Diagnostics,
            diagnostic => diagnostic.Code == "PXAPROJECT_EXTERNAL_LINK");
    }

    [Fact]
    public async Task Open_reports_unknown_required_features_before_applying_the_document()
    {
        var document = new DesignExportDto
        {
            Id = "future-project",
            Name = "Future project",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var exported = Assert.IsType<FileContentResult>(await controller.ExportPdf(document));
        var package = RewriteManifest(exported.FileContents, manifest =>
            manifest.RequiredFeatures.Add("pdf.future-capability"));

        var opened = await OpenPdf(package, "future.pxap");

        Assert.True(opened.Compatibility.RequiresReview);
        Assert.Equal(["pdf.future-capability"], opened.Compatibility.MissingFeatures);
    }

    [Fact]
    public async Task Pdf_project_moves_embedded_images_into_package_assets()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-assets",
            Name = "Portable images",
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "image-1", Type = "image", Content = PngDataUrl,
                            Width = 100, Height = 80,
                        },
                    ],
                },
            ],
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportPdf(document));
        var package = PxaProjectArchive.ReadPdf(new MemoryStream(exported.FileContents));
        var packagedImage = package.Document.Pages[0].Elements[0];
        var opened = await OpenPdf(exported.FileContents, "portable.pxap");

        Assert.Single(package.Assets);
        Assert.Null(packagedImage.Content);
        Assert.Equal(package.Assets[0].Id, packagedImage.AssetId);
        Assert.Equal(PngBytes, package.Assets[0].Content);
        Assert.Equal(PxaProjectAssetKinds.Image, package.Assets[0].Kind);
        Assert.StartsWith("assets/images/", package.Manifest.Assets[0].Path, StringComparison.Ordinal);
        Assert.Equal(PxaProjectAssetKinds.Image, opened.Assets[0].Kind);
        Assert.Equal(PngDataUrl, opened.Document.Pages[0].Elements[0].Content);
    }

    [Fact]
    public async Task Spreadsheet_project_moves_embedded_images_into_package_assets()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "spreadsheet-assets",
            Name = "Portable workbook images",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Images = [new SpreadsheetImageDto { Id = "image-1", Data = PngDataUrl }],
                },
            ],
        };

        var exported = Assert.IsType<FileContentResult>(await controller.ExportSpreadsheet(workbook));
        var package = PxaProjectArchive.ReadSpreadsheet(new MemoryStream(exported.FileContents));
        var packagedImage = package.Document.Sheets[0].Images[0];
        var opened = await OpenSpreadsheet(exported.FileContents, "portable.pxas");

        Assert.Single(package.Assets);
        Assert.Null(packagedImage.Data);
        Assert.NotNull(packagedImage.AssetId);
        Assert.Equal(package.Assets[0].Id, packagedImage.AssetId.Value.ToString("N"));
        Assert.Equal(PngBytes, package.Assets[0].Content);
        Assert.Equal(PxaProjectAssetKinds.Image, package.Assets[0].Kind);
        Assert.StartsWith("assets/images/", package.Manifest.Assets[0].Path, StringComparison.Ordinal);
        Assert.Equal(PxaProjectAssetKinds.Image, opened.Assets[0].Kind);
        Assert.Equal(PngDataUrl, opened.Document.Sheets[0].Images[0].Data);
        Assert.Null(opened.Document.Sheets[0].Images[0].AssetId);
    }

    [Fact]
    public async Task Project_export_rejects_tenant_asset_references_and_external_image_urls()
    {
        var pdf = new DesignExportDto
        {
            Id = "tenant-bound-pdf",
            Name = "Tenant bound",
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "image-1", Type = "image", Content = "https://example.invalid/image.png",
                            AssetId = Guid.NewGuid().ToString(),
                        },
                    ],
                },
            ],
        };
        var spreadsheet = new SpreadsheetDto
        {
            Id = "tenant-bound-sheet",
            Name = "Tenant bound",
            Sheets =
            [
                new SheetDto
                {
                    Id = "sheet-1",
                    Images = [new SpreadsheetImageDto { Id = "image-1", AssetId = Guid.NewGuid() }],
                },
            ],
        };

        var pdfProblem = Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(await controller.ExportPdf(pdf)).Value);
        var spreadsheetProblem = Assert.IsType<ProblemDetails>(
            Assert.IsType<ObjectResult>(await controller.ExportSpreadsheet(spreadsheet)).Value);

        Assert.Equal("PXAPROJECT010", pdfProblem.Extensions["code"]);
        Assert.Equal("PXAPROJECT010", spreadsheetProblem.Extensions["code"]);
    }

    [Fact]
    public async Task Open_rejects_extension_and_package_type_confusion()
    {
        var pdf = new DesignExportDto
        {
            Id = "pdf-project",
            Name = "Document",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(pdf)).FileContents;

        var wrongExtension = await controller.OpenPdf(Form(bytes, "document.pxas"));
        var confused = await controller.OpenSpreadsheet(Form(bytes, "document.pxas"));

        Assert.Equal(415, Assert.IsType<ObjectResult>(wrongExtension.Result).StatusCode);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(confused.Result).Value);
        Assert.Equal("PXAPROJECT002", problem.Extensions["code"]);
    }

    [Fact]
    public async Task Open_remaps_ids_reserved_by_the_selected_target()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-project", Name = "Imported",
            Pages =
            [
                new PageDto
                {
                    Id = "page-1",
                    Elements = [new ElementDto { Id = "title", Type = "text" }],
                },
            ],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(document)).FileContents;

        var action = await controller.OpenPdf(
            Form(bytes, "import.pxap"), "[\"pdf-project\",\"page-1\",\"title\"]");
        var response = Assert.IsType<PxaProjectOpenResponse<DesignExportDto>>(
            Assert.IsType<OkObjectResult>(action.Result).Value);

        Assert.Equal("pdf-project-import-2", response.Document.Id);
        Assert.Equal("page-1-import-2", response.Document.Pages[0].Id);
        Assert.Equal("title-import-2", response.Document.Pages[0].Elements[0].Id);
        Assert.Equal(3, response.IdRemaps.Count);
    }

    [Fact]
    public async Task Open_rejects_invalid_reserved_id_contract()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-project", Name = "Imported",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(document)).FileContents;

        var action = await controller.OpenPdf(Form(bytes, "import.pxap"), "not-json");
        var result = Assert.IsType<ObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PXAPROJECT014", problem.Extensions["code"]);
    }

    [Fact]
    public async Task Inspect_returns_metadata_without_returning_the_document()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-project",
            Name = "Inspectable",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(document)).FileContents;

        var result = await controller.InspectPdf(Form(bytes, "inspectable.pxap"));
        var manifest = Assert.IsType<PxaProjectManifest>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("Inspectable", manifest.Title);
        Assert.Equal("pdf-project", manifest.DocumentId);
    }

    [Fact]
    public async Task Spreadsheet_inspect_uses_the_spreadsheet_contract()
    {
        var workbook = new SpreadsheetDto
        {
            Id = "spreadsheet-project",
            Name = "Inspectable workbook",
            Sheets = [new SheetDto { Id = "sheet-1" }],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportSpreadsheet(workbook)).FileContents;

        var result = await controller.InspectSpreadsheet(Form(bytes, "inspectable.pxas"));
        var manifest = Assert.IsType<PxaProjectManifest>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("Inspectable workbook", manifest.Title);
        Assert.Equal("spreadsheet-project", manifest.DocumentId);
        Assert.Equal("pxas", manifest.FormatId);
    }

    [Fact]
    public async Task Validate_reads_the_complete_document_and_rejects_tampering()
    {
        var document = new DesignExportDto
        {
            Id = "pdf-project",
            Name = "Validated",
            Pages = [new PageDto { Id = "page-1" }],
        };
        var bytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(document)).FileContents;
        var valid = await controller.ValidatePdf(Form(bytes, "validated.pxap"));
        bytes[^8] ^= 0x01;
        var invalid = await controller.ValidatePdf(Form(bytes, "tampered.pxap"));

        Assert.True(Assert.IsType<PxaProjectValidationResponse>(
            Assert.IsType<OkObjectResult>(valid.Result).Value).Valid);
        Assert.Equal(400, Assert.IsType<ObjectResult>(invalid.Result).StatusCode);
    }

    [Fact]
    public async Task Rejected_project_open_returns_a_stable_diagnostic_and_tenant_scoped_audit()
    {
        var valid = PxaProjectArchive.WritePdf(new DesignExportDto
        {
            Id = "rejected-project", Name = "Rejected",
            Pages = [new PageDto { Id = "page-1" }],
        });
        var truncated = valid[..^12];

        var action = await controller.OpenPdf(Form(truncated, "truncated.pxap"));
        var result = Assert.IsType<ObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        var audit = Assert.Single(auditWriter.Entries);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PXAPROJECT001", problem.Extensions["code"]);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("failed", audit.Record.Outcome);
        Assert.Equal("PXAPROJECT001", audit.Record.DiagnosticCode);
        Assert.DoesNotContain("truncated", JsonSerializer.Serialize(audit), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_project_operation_is_tenant_scoped_and_audited_without_document_content()
    {
        var secret = "PRIVATE-CUSTOMER-CONTENT";
        var pdf = new DesignExportDto
        {
            Id = "pdf-audit", Name = "Confidential customer title",
            Pages = [new PageDto { Id = "page", Elements = [new ElementDto { Id = "text", Content = secret }] }],
        };
        var spreadsheet = new SpreadsheetDto
        {
            Id = "sheet-audit", Name = "Confidential workbook",
            Sheets = [new SheetDto { Id = "sheet", Cells = [new CellDto { Row = 0, Col = 0, Value = secret }] }],
        };
        var pdfBytes = Assert.IsType<FileContentResult>(await controller.ExportPdf(pdf)).FileContents;
        var sheetBytes = Assert.IsType<FileContentResult>(await controller.ExportSpreadsheet(spreadsheet)).FileContents;

        await controller.OpenPdf(Form(pdfBytes, "private-name.pxap"));
        await controller.ValidatePdf(Form(pdfBytes, "private-name.pxap"));
        await controller.InspectPdf(Form(pdfBytes, "private-name.pxap"));
        await controller.OpenSpreadsheet(Form(sheetBytes, "private-sheet.pxas"));
        await controller.ValidateSpreadsheet(Form(sheetBytes, "private-sheet.pxas"));
        await controller.InspectSpreadsheet(Form(sheetBytes, "private-sheet.pxas"));

        Assert.Equal(8, auditWriter.Entries.Count);
        Assert.All(auditWriter.Entries, entry =>
        {
            Assert.Equal(organizationId, entry.OrganizationId);
            Assert.Equal(userId, entry.UserId);
            Assert.Equal("succeeded", entry.Record.Outcome);
        });
        Assert.Equal(new[]
        {
            "designer.projects.pxap.export", "designer.projects.pxap.inspect",
            "designer.projects.pxap.open", "designer.projects.pxap.validate",
            "designer.projects.pxas.export", "designer.projects.pxas.inspect",
            "designer.projects.pxas.open", "designer.projects.pxas.validate",
        }, auditWriter.Entries.Select(entry => entry.Record.Action).Order().ToArray());
        var serializedAudit = JsonSerializer.Serialize(auditWriter.Entries);
        Assert.DoesNotContain(secret, serializedAudit);
        Assert.DoesNotContain("Confidential", serializedAudit);
        Assert.DoesNotContain("private-name", serializedAudit);
    }

    [Fact]
    public async Task Missing_tenant_context_is_rejected_before_project_processing()
    {
        var audit = new RecordingAuditWriter();
        var tenantless = new DesignerProjectsController(new TestTenantContext(null, null), audit);

        var result = await tenantless.ExportPdf(new DesignExportDto
        {
            Id = "not-processed", Name = "Not processed", Pages = [new PageDto { Id = "page" }],
        });

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Project_audit_writer_persists_only_bounded_operational_metadata()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"project-audit-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new PxaDbContext(options);
        var writer = new PxaDesignerProjectAuditWriter(dbContext);

        await writer.WriteAsync(organizationId, userId, new PxaDesignerProjectAuditRecord(
            "designer.projects.pxap.open", "project-id", "pxap", "open", "succeeded",
            PackageBytes: 1234, AssetCount: 2, RequiresReview: true));

        var audit = await dbContext.AuditEvents.SingleAsync();
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(userId, audit.ActorUserId);
        Assert.Equal("designer_project", audit.TargetType);
        Assert.Equal("project-id", audit.TargetId);
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Contains("\"formatId\":\"pxap\"", audit.DetailsJson!);
        Assert.Contains("\"packageBytes\":1234", audit.DetailsJson!);
        Assert.DoesNotContain("fileName", audit.DetailsJson!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", audit.DetailsJson!, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PxaProjectOpenResponse<DesignExportDto>> OpenPdf(byte[] bytes, string fileName) =>
        Assert.IsType<PxaProjectOpenResponse<DesignExportDto>>(
            Assert.IsType<OkObjectResult>((await controller.OpenPdf(Form(bytes, fileName))).Result).Value);

    private async Task<PxaProjectOpenResponse<SpreadsheetDto>> OpenSpreadsheet(byte[] bytes, string fileName) =>
        Assert.IsType<PxaProjectOpenResponse<SpreadsheetDto>>(
            Assert.IsType<OkObjectResult>((await controller.OpenSpreadsheet(Form(bytes, fileName))).Result).Value);

    private static IFormFile Form(byte[] bytes, string fileName) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);

    private static byte[] RewriteManifest(byte[] source, Action<PxaProjectManifest> mutate)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in input.Entries)
            {
                var compression = entry.FullName == PxaProjectFormat.MimeTypeEntry
                    ? CompressionLevel.NoCompression
                    : CompressionLevel.Optimal;
                var target = archive.CreateEntry(entry.FullName, compression);
                using var targetStream = target.Open();
                if (entry.FullName == PxaProjectFormat.ManifestEntry)
                {
                    using var sourceStream = entry.Open();
                    var manifest = JsonSerializer.Deserialize<PxaProjectManifest>(sourceStream,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    mutate(manifest);
                    JsonSerializer.Serialize(targetStream, manifest,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                else
                {
                    using var sourceStream = entry.Open();
                    sourceStream.CopyTo(targetStream);
                }
            }
        }
        return output.ToArray();
    }

    private sealed record AuditEntry(
        Guid OrganizationId,
        Guid UserId,
        PxaDesignerProjectAuditRecord Record);

    private sealed class RecordingAuditWriter : IPxaDesignerProjectAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(
            Guid organizationId,
            Guid userId,
            PxaDesignerProjectAuditRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entries.Add(new AuditEntry(organizationId, userId, record));
            return Task.CompletedTask;
        }
    }

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;
}
