using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Core.ProjectFormats;
using PVAD.Core.Primitives;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PvadAuthenticationSchemes.DesignerCookie)]
[EnableRateLimiting("designer-code")]
[Route("api/pvad/v1/designer/projects")]
public sealed class DesignerProjectsController(
    IPvadTenantContext tenantContext,
    IPvadDesignerProjectAuditWriter auditWriter) : ControllerBase
{
    private const long MaximumPackageBytes = PvadProjectReadLimits.DefaultMaximumCompressedBytes;

    [HttpPost("pdf/export")]
    [Authorize(Policy = PvadDesignerPermissions.PdfExport)]
    [Produces(PvadProjectFormat.PvadpMediaType)]
    public async Task<IActionResult> ExportPdf(
        [FromBody] DesignExportDto document,
        CancellationToken cancellationToken = default) =>
        await ExportPdfPackage(document, null, cancellationToken);

    [HttpPost("pdf/export-with-workspace")]
    [Authorize(Policy = PvadDesignerPermissions.PdfExport)]
    [Produces(PvadProjectFormat.PvadpMediaType)]
    public async Task<IActionResult> ExportPdfWithWorkspace(
        [FromBody] PvadPdfProjectExportRequest request,
        CancellationToken cancellationToken = default) =>
        await ExportPdfPackage(request.Document, request.CodeWorkspace, cancellationToken);

    private async Task<IActionResult> ExportPdfPackage(
        DesignExportDto document,
        PvadCodeWorkspaceProjectComponent? codeWorkspace,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        try
        {
            var assets = PvadProjectAssetMapper.ExtractPdfAssets(document);
            var package = PvadProjectArchive.WritePdf(
                document, new PvadProjectWriteOptions
                {
                    Assets = assets,
                    CodeWorkspace = codeWorkspace,
                }, cancellationToken);
            await WriteAudit(organizationId, userId, document.Id, "pxap", "export", "succeeded",
                package.LongLength, assets.Count, cancellationToken: cancellationToken);
            return File(package, PvadProjectFormat.PvadpMediaType,
                $"{ExportFileNameSanitizer.Sanitize(document.Name, "document")}{PvadProjectFormat.PvadpExtension}");
        }
        catch (PvadProjectFormatException exception)
        {
            await WriteAudit(organizationId, userId, document.Id, "pxap", "export", "failed",
                diagnosticCode: exception.Code, cancellationToken: cancellationToken);
            return ProjectProblem(exception);
        }
    }

    [HttpPost("spreadsheet/export")]
    [Authorize(Policy = PvadDesignerPermissions.SpreadsheetExport)]
    [Produces(PvadProjectFormat.PvadsMediaType)]
    public async Task<IActionResult> ExportSpreadsheet(
        [FromBody] SpreadsheetDto workbook,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        try
        {
            var assets = PvadProjectAssetMapper.ExtractSpreadsheetAssets(workbook);
            var package = PvadProjectArchive.WriteSpreadsheet(
                workbook, new PvadProjectWriteOptions { Assets = assets }, cancellationToken);
            await WriteAudit(organizationId, userId, workbook.Id, "pxas", "export", "succeeded",
                package.LongLength, assets.Count, cancellationToken: cancellationToken);
            return File(package, PvadProjectFormat.PvadsMediaType,
                $"{ExportFileNameSanitizer.Sanitize(workbook.Name, "workbook")}{PvadProjectFormat.PvadsExtension}");
        }
        catch (PvadProjectFormatException exception)
        {
            await WriteAudit(organizationId, userId, workbook.Id, "pxas", "export", "failed",
                diagnosticCode: exception.Code, cancellationToken: cancellationToken);
            return ProjectProblem(exception);
        }
    }

    [HttpPost("pdf/open")]
    [Authorize(Policy = PvadDesignerPermissions.PdfImport)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PvadProjectOpenResponse<DesignExportDto>>> OpenPdf(
        IFormFile? file,
        [FromForm] string? reservedIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PvadProjectFormat.PvadpExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxap", "open", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PvadProjectArchive.ReadPdf(stream, cancellationToken: cancellationToken);
                if (!TryParseReservedIds(reservedIds, out var targetIds, out var idFailure))
                {
                    await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "open", "failed",
                        file!.Length, package.Assets.Count, diagnosticCode: "PXAPROJECT014",
                        cancellationToken: cancellationToken);
                    return idFailure!;
                }
                var remap = PvadProjectIdCollisionResolver.Resolve(package.Document, targetIds);
                PvadProjectAssetMapper.HydratePdfAssets(package);
                var response = ToResponse(package, remap.Remaps);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "open", "succeeded",
                    file!.Length, package.Assets.Count, response.Compatibility.RequiresReview,
                    cancellationToken: cancellationToken);
                return Ok(response);
            }
            catch (PvadProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxap", "open", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("spreadsheet/open")]
    [Authorize(Policy = PvadDesignerPermissions.SpreadsheetImport)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PvadProjectOpenResponse<SpreadsheetDto>>> OpenSpreadsheet(
        IFormFile? file,
        [FromForm] string? reservedIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PvadProjectFormat.PvadsExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxas", "open", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PvadProjectArchive.ReadSpreadsheet(stream, cancellationToken: cancellationToken);
                if (!TryParseReservedIds(reservedIds, out var targetIds, out var idFailure))
                {
                    await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "open", "failed",
                        file!.Length, package.Assets.Count, diagnosticCode: "PXAPROJECT014",
                        cancellationToken: cancellationToken);
                    return idFailure!;
                }
                var remap = PvadProjectIdCollisionResolver.Resolve(package.Document, targetIds);
                PvadProjectAssetMapper.HydrateSpreadsheetAssets(package);
                var response = ToResponse(package, remap.Remaps);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "open", "succeeded",
                    file!.Length, package.Assets.Count, response.Compatibility.RequiresReview,
                    cancellationToken: cancellationToken);
                return Ok(response);
            }
            catch (PvadProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxas", "open", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("pdf/validate")]
    [Authorize(Policy = PvadDesignerPermissions.PdfTemplatesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PvadProjectValidationResponse>> ValidatePdf(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PvadProjectFormat.PvadpExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxap", "validate", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PvadProjectArchive.ReadPdf(stream, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxap", "validate", "succeeded",
                    file!.Length, package.Assets.Count, cancellationToken: cancellationToken);
                return Ok(new PvadProjectValidationResponse(true, package.Manifest, package.PackageSha256));
            }
            catch (PvadProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxap", "validate", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("spreadsheet/validate")]
    [Authorize(Policy = PvadDesignerPermissions.SpreadsheetRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public async Task<ActionResult<PvadProjectValidationResponse>> ValidateSpreadsheet(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        if (!TryOpenUpload(file, PvadProjectFormat.PvadsExtension, out var stream, out var failure, out var failureCode))
        {
            await WriteAudit(organizationId, userId, "unknown", "pxas", "validate", "failed",
                file?.Length, diagnosticCode: failureCode, cancellationToken: cancellationToken);
            return failure!;
        }
        using (stream)
        {
            try
            {
                var package = PvadProjectArchive.ReadSpreadsheet(stream, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, package.Manifest.DocumentId, "pxas", "validate", "succeeded",
                    file!.Length, package.Assets.Count, cancellationToken: cancellationToken);
                return Ok(new PvadProjectValidationResponse(true, package.Manifest, package.PackageSha256));
            }
            catch (PvadProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", "pxas", "validate", "failed",
                    file!.Length, diagnosticCode: exception.Code, cancellationToken: cancellationToken);
                return ProjectProblem(exception);
            }
        }
    }

    [HttpPost("pdf/inspect")]
    [Authorize(Policy = PvadDesignerPermissions.PdfTemplatesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public Task<ActionResult<PvadProjectManifest>> InspectPdf(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        InspectProject(file, PvadProjectKind.Pdf, PvadProjectFormat.PvadpExtension, cancellationToken);

    [HttpPost("spreadsheet/inspect")]
    [Authorize(Policy = PvadDesignerPermissions.SpreadsheetRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPackageBytes + 64 * 1024)]
    public Task<ActionResult<PvadProjectManifest>> InspectSpreadsheet(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        InspectProject(file, PvadProjectKind.Spreadsheet, PvadProjectFormat.PvadsExtension, cancellationToken);

    private async Task<ActionResult<PvadProjectManifest>> InspectProject(
        IFormFile? file,
        PvadProjectKind projectKind,
        string extension,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var organizationId, out var userId))
            return Unauthorized();
        // Keep audit event identifiers stable independently of the exported envelope.
        var formatId = PvadProjectFormat.LegacyFormatId(projectKind);
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
                var manifest = PvadProjectArchive.Inspect(
                    stream, projectKind, cancellationToken: cancellationToken);
                await WriteAudit(organizationId, userId, manifest.DocumentId, formatId, "inspect", "succeeded",
                    file!.Length, manifest.Assets.Count, cancellationToken: cancellationToken);
                return Ok(manifest);
            }
            catch (PvadProjectFormatException exception)
            {
                await WriteAudit(organizationId, userId, "unknown", formatId,
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
        auditWriter.WriteAsync(organizationId, userId, new PvadDesignerProjectAuditRecord(
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
        if (!PvadProjectFormat.AcceptsExtension(extension, Path.GetExtension(file.FileName)))
        {
            diagnosticCode = "PXAPROJECT002";
            failure = ProjectProblem(diagnosticCode, $"The uploaded file must use the {extension} extension.", 415);
            return false;
        }
        stream = file.OpenReadStream();
        return true;
    }

    private ObjectResult ProjectProblem(PvadProjectFormatException exception) =>
        ProjectProblem(exception.Code, exception.Message, 400);

    private ObjectResult ProjectProblem(string code, string detail, int status) =>
        StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = "PVAD project rejected",
            Detail = detail,
            Extensions =
            {
                ["code"] = code,
                ["canonicalCode"] = PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(code),
            },
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

    private static PvadProjectOpenResponse<TDocument> ToResponse<TDocument>(
        PvadProjectPackage<TDocument> package,
        IReadOnlyDictionary<string, string>? idRemaps = null)
    {
        var missingFeatures = package.Manifest.RequiredFeatures
            .Where(feature => !PvadProjectCapabilities.Supported.Contains(feature))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var diagnostics = package.Manifest.Diagnostics.ToList();
        if (package.Manifest.FormatId is "pxap" or "pxas")
            diagnostics.Add(new PvadProjectDiagnostic
            {
                Code = "PVADPROJECT_LEGACY_FORMAT", Severity = "warning",
                Message = "This legacy project will open without changing the original file. Save project downloads a new PVADP or PVADS copy with recalculated integrity hashes.",
            });
        var compatibility = new PvadProjectCompatibilityInfo(
            missingFeatures.Length > 0 || diagnostics.Any(diagnostic => diagnostic.Severity != "info"),
            missingFeatures,
            diagnostics.Select(diagnostic => new PvadProjectDiagnosticResponse(
                diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Source)).ToArray());
        return new(package.Manifest, package.Document, package.PackageSha256,
            package.Assets.Select(asset => new PvadProjectAssetInfo(
                asset.Id, asset.Kind, asset.FileName, asset.ContentType, asset.Content.LongLength)).ToArray(),
            compatibility, idRemaps ?? new Dictionary<string, string>(), package.UpgradeReport,
            package.CodeWorkspace);
    }
}

public sealed record PvadProjectOpenResponse<TDocument>(
    PvadProjectManifest Manifest,
    TDocument Document,
    string PackageSha256,
    IReadOnlyList<PvadProjectAssetInfo> Assets,
    PvadProjectCompatibilityInfo Compatibility,
    IReadOnlyDictionary<string, string> IdRemaps,
    PvadProjectUpgradeReport? UpgradeReport,
    PvadCodeWorkspaceProjectComponent? CodeWorkspace);

public sealed record PvadPdfProjectExportRequest(
    DesignExportDto Document,
    PvadCodeWorkspaceProjectComponent CodeWorkspace);

public sealed record PvadProjectCompatibilityInfo(
    bool RequiresReview,
    IReadOnlyList<string> MissingFeatures,
    IReadOnlyList<PvadProjectDiagnosticResponse> Diagnostics);

public sealed record PvadProjectDiagnosticResponse(string Code, string Severity, string Message, string? Source)
{
    public string? CanonicalCode => PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(Code);
}

public sealed record PvadProjectAssetInfo(
    string Id,
    string Kind,
    string FileName,
    string ContentType,
    long Length);

public sealed record PvadProjectValidationResponse(
    bool Valid,
    PvadProjectManifest Manifest,
    string PackageSha256);
