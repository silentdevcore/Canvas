using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using PXA.Core.Contracts;
using PXA.Core.ProjectFormats;
using PXA.Core.Primitives;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[EnableRateLimiting("designer-code")]
[Route("api/pxa/v1/designer/projects")]
public sealed class DesignerProjectsController(
    IPxaTenantContext tenantContext,
    IPxaDesignerProjectAuditWriter auditWriter) : ControllerBase
{
    private const long MaximumPackageBytes = PxaProjectReadLimits.DefaultMaximumCompressedBytes;

    [HttpPost("pdf/export")]
    [Authorize(Policy = PxaDesignerPermissions.PdfExport)]
    [Produces(PxaProjectFormat.PxapMediaType)]
    public async Task<IActionResult> ExportPdf(
        [FromBody] DesignExportDto document,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        try
        {
            var assets = PxaProjectAssetMapper.ExtractPdfAssets(document);
            var package = PxaProjectArchive.WritePdf(
                document, new PxaProjectWriteOptions { Assets = assets }, cancellationToken);
            await WriteAudit(organizationId, userId, document.Id, "pxap", "export", "succeeded",
                package.LongLength, assets.Count, cancellationToken: cancellationToken);
            return File(package, PxaProjectFormat.PxapMediaType,
                $"{ExportFileNameSanitizer.Sanitize(document.Name, "document")}{PxaProjectFormat.PxapExtension}");
        }
        catch (PxaProjectFormatException exception)
        {
            await WriteAudit(organizationId, userId, document.Id, "pxap", "export", "failed",
                diagnosticCode: exception.Code, cancellationToken: cancellationToken);
            return ProjectProblem(exception);
        }
    }

    [HttpPost("spreadsheet/export")]
    [Authorize(Policy = PxaDesignerPermissions.SpreadsheetExport)]
    [Produces(PxaProjectFormat.PxasMediaType)]
    public async Task<IActionResult> ExportSpreadsheet(
        [FromBody] SpreadsheetDto workbook,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        try
        {
            var assets = PxaProjectAssetMapper.ExtractSpreadsheetAssets(workbook);
            var package = PxaProjectArchive.WriteSpreadsheet(
                workbook, new PxaProjectWriteOptions { Assets = assets }, cancellationToken);
            await WriteAudit(organizationId, userId, workbook.Id, "pxas", "export", "succeeded",
                package.LongLength, assets.Count, cancellationToken: cancellationToken);
            return File(package, PxaProjectFormat.PxasMediaType,
                $"{ExportFileNameSanitizer.Sanitize(workbook.Name, "workbook")}{PxaProjectFormat.PxasExtension}");
        }
        catch (PxaProjectFormatException exception)
        {
            await WriteAudit(organizationId, userId, workbook.Id, "pxas", "export", "failed",
                diagnosticCode: exception.Code, cancellationToken: cancellationToken);
            return ProjectProblem(exception);
        }
    }

    [HttpPost("pdf/open")]
    [Authorize(Policy = PxaDesignerPermissions.PdfImport)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PxaProjectOpenResponse<DesignExportDto>>> OpenPdf(
        IFormFile? file,
        [FromForm] string? reservedIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PxaProjectFormat.PxapExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxap", "open", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PxaProjectArchive.ReadPdf(stream, cancellationToken: cancellationToken);
                if (!TryParseReservedIds(reservedIds, out var targetIds, out var idFailure))
                {
                    await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "open", "failed",
                        file!.Length, package.Assets.Count, diagnosticCode: "PXAPROJECT014",
                        cancellationToken: cancellationToken);
                    return idFailure!;
                }
                var remap = PxaProjectIdCollisionResolver.Resolve(package.Document, targetIds);
                PxaProjectAssetMapper.HydratePdfAssets(package);
                var response = ToResponse(package, remap.Remaps);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "open", "succeeded",
                    file!.Length, package.Assets.Count, response.Compatibility.RequiresReview,
                    cancellationToken: cancellationToken);
                return Ok(response);
            }
            catch (PxaProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxap", "open", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("spreadsheet/open")]
    [Authorize(Policy = PxaDesignerPermissions.SpreadsheetImport)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PxaProjectOpenResponse<SpreadsheetDto>>> OpenSpreadsheet(
        IFormFile? file,
        [FromForm] string? reservedIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PxaProjectFormat.PxasExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxas", "open", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PxaProjectArchive.ReadSpreadsheet(stream, cancellationToken: cancellationToken);
                if (!TryParseReservedIds(reservedIds, out var targetIds, out var idFailure))
                {
                    await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "open", "failed",
                        file!.Length, package.Assets.Count, diagnosticCode: "PXAPROJECT014",
                        cancellationToken: cancellationToken);
                    return idFailure!;
                }
                var remap = PxaProjectIdCollisionResolver.Resolve(package.Document, targetIds);
                PxaProjectAssetMapper.HydrateSpreadsheetAssets(package);
                var response = ToResponse(package, remap.Remaps);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "open", "succeeded",
                    file!.Length, package.Assets.Count, response.Compatibility.RequiresReview,
                    cancellationToken: cancellationToken);
                return Ok(response);
            }
            catch (PxaProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxas", "open", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("pdf/validate")]
    [Authorize(Policy = PxaDesignerPermissions.PdfTemplatesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PxaProjectValidationResponse>> ValidatePdf(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PxaProjectFormat.PxapExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxap", "validate", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PxaProjectArchive.ReadPdf(stream, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "validate", "succeeded",
                    file!.Length, package.Assets.Count, cancellationToken: cancellationToken);
                return Ok(new PxaProjectValidationResponse(true, package.Manifest, package.PackageSha256));
            }
            catch (PxaProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxap", "validate", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("spreadsheet/validate")]
    [Authorize(Policy = PxaDesignerPermissions.SpreadsheetRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PxaProjectValidationResponse>> ValidateSpreadsheet(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PxaProjectFormat.PxasExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxas", "validate", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PxaProjectArchive.ReadSpreadsheet(stream, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "validate", "succeeded",
                    file!.Length, package.Assets.Count, cancellationToken: cancellationToken);
                return Ok(new PxaProjectValidationResponse(true, package.Manifest, package.PackageSha256));
            }
            catch (PxaProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxas", "validate", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("pdf/inspect")]
    [Authorize(Policy = PxaDesignerPermissions.PdfTemplatesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public Task<ActionResult<PxaProjectManifest>> InspectPdf(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        InspectProject(file, PxaProjectKind.Pdf, PxaProjectFormat.PxapExtension, cancellationToken);

    [HttpPost("spreadsheet/inspect")]
    [Authorize(Policy = PxaDesignerPermissions.SpreadsheetRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public Task<ActionResult<PxaProjectManifest>> InspectSpreadsheet(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        InspectProject(file, PxaProjectKind.Spreadsheet, PxaProjectFormat.PxasExtension, cancellationToken);

    private async Task<ActionResult<PxaProjectManifest>> InspectProject(
        IFormFile? file,
        PxaProjectKind projectKind,
        string extension,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        var formatId = PxaProjectFormat.FormatId(projectKind);
        if (!TryOpenUpload(file, extension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", formatId, "inspect", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var manifest = PxaProjectArchive.Inspect(
                    stream, projectKind, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, manifest.DocumentId, formatId, "inspect", "succeeded",
                    file!.Length, manifest.Assets.Count, cancellationToken: cancellationToken);
                return Ok(manifest);
            }
            catch (PxaProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", PxaProjectFormat.FormatId(projectKind),
                    "inspect", "failed", file!.Length, diagnosticCode: exception.Code,
                    cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    private bool TryGetActor(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private Task WriteAudit(
        Guid organizationId,
        Guid userId,
        string projectId,
        string formatId,
        string operation,
        string outcome,
        long? packageBytes = null,
        int? assetCount = null,
        bool? requiresReview = null,
        string? diagnosticCode = null,
        CancellationToken cancellationToken = default) =>
        auditWriter.WriteAsync(organizationId, userId, new PxaDesignerProjectAuditRecord(
            $"designer.projects.{formatId}.{operation}", projectId, formatId, operation, outcome,
            packageBytes, assetCount, requiresReview, diagnosticCode), cancellationToken);

    private bool TryOpenUpload(
        IFormFile? file,
        string extension,
        out Stream stream,
        out ActionResult? failure,
        out string? diagnosticCode)
    {
        stream = Stream.Null;
        failure = null;
        diagnosticCode = null;
        if (file is null || file.Length == 0)
        {
            diagnosticCode = "PXAPROJECT_REQUIRED";
            failure = ProjectProblem(diagnosticCode, $"A {extension} project file is required.", 400);
            return false;
        }
        if (file.Length > MaximumPackageBytes)
        {
            diagnosticCode = "PXAPROJECT005";
            failure = ProjectProblem(diagnosticCode, "The project package exceeds its compressed-size limit.", 413);
            return false;
        }
        if (!Path.GetExtension(file.FileName).Equals(extension, StringComparison.OrdinalIgnoreCase))
        {
            diagnosticCode = "PXAPROJECT002";
            failure = ProjectProblem(diagnosticCode, $"The uploaded file must use the {extension} extension.", 415);
            return false;
        }
        stream = file.OpenReadStream();
        return true;
    }

    private ObjectResult ProjectProblem(PxaProjectFormatException exception) =>
        ProjectProblem(exception.Code, exception.Message, 400);

    private ObjectResult ProjectProblem(string code, string detail, int status) =>
        StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = "PXA project rejected",
            Detail = detail,
            Extensions = { ["code"] = code },
        });

    private bool TryParseReservedIds(
        string? json,
        out IReadOnlyCollection<string> ids,
        out ActionResult? failure)
    {
        ids = [];
        failure = null;
        if (string.IsNullOrWhiteSpace(json))
            return true;
        if (json.Length > 1_000_000)
        {
            failure = ProjectProblem("PXAPROJECT014", "The reserved ID list exceeds its size limit.", 400);
            return false;
        }
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json) ?? [];
            if (values.Length > 100_000 || values.Any(value =>
                    string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl)))
            {
                failure = ProjectProblem("PXAPROJECT014", "The reserved ID list is invalid.", 400);
                return false;
            }
            ids = values.Distinct(StringComparer.Ordinal).ToArray();
            return true;
        }
        catch (JsonException)
        {
            failure = ProjectProblem("PXAPROJECT014", "The reserved ID list is invalid.", 400);
            return false;
        }
    }

    private static PxaProjectOpenResponse<TDocument> ToResponse<TDocument>(
        PxaProjectPackage<TDocument> package,
        IReadOnlyDictionary<string, string>? idRemaps = null)
    {
        var missingFeatures = package.Manifest.RequiredFeatures
            .Where(feature => !PxaProjectCapabilities.Supported.Contains(feature))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var compatibility = new PxaProjectCompatibilityInfo(
            missingFeatures.Length > 0 || package.Manifest.Diagnostics.Any(diagnostic => diagnostic.Severity != "info"),
            missingFeatures,
            package.Manifest.Diagnostics);
        return new(package.Manifest, package.Document, package.PackageSha256,
            package.Assets.Select(asset => new PxaProjectAssetInfo(
                asset.Id, asset.Kind, asset.FileName, asset.ContentType, asset.Content.LongLength)).ToArray(),
            compatibility, idRemaps ?? new Dictionary<string, string>(), package.UpgradeReport);
    }
}

public sealed record PxaProjectOpenResponse<TDocument>(
    PxaProjectManifest Manifest,
    TDocument Document,
    string PackageSha256,
    IReadOnlyList<PxaProjectAssetInfo> Assets,
    PxaProjectCompatibilityInfo Compatibility,
    IReadOnlyDictionary<string, string> IdRemaps,
    PxaProjectUpgradeReport? UpgradeReport);

public sealed record PxaProjectCompatibilityInfo(
    bool RequiresReview,
    IReadOnlyList<string> MissingFeatures,
    IReadOnlyList<PxaProjectDiagnostic> Diagnostics);

public sealed record PxaProjectAssetInfo(
    string Id,
    string Kind,
    string FileName,
    string ContentType,
    long Length);

public sealed record PxaProjectValidationResponse(
    bool Valid,
    PxaProjectManifest Manifest,
    string PackageSha256);
