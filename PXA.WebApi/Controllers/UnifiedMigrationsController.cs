using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PXA.Migration.Abstractions;
using PXA.Migration.Pdf;
using PXA.Migration.Pdf.Code.Nutrient;
using PXA.Migration.Pdf.Viewer.Nutrient;
using PXA.WebApi.Security;
using PXA.WebApi.Services;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/migrations")]
public sealed class UnifiedMigrationsController(
    MigrationService migrationService,
    ReportMigrationService reportMigrationService,
    NutrientViewerDesignMigrationService nutrientViewerDesignMigrationService) : ControllerBase
{
    private static readonly JsonSerializerOptions PackageJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("catalog")]
    public IActionResult GetCatalog([FromQuery] string? domain = null, [FromQuery] string? kind = null)
    {
        if (domain is not null && !MigrationDomains.All.Contains(domain))
            return MigrationProblem(400, "Invalid migration domain", $"Unknown migration domain '{domain}'.", "PXAMIG001");
        if (kind is not null && !MigrationKinds.All.Contains(kind))
            return MigrationProblem(400, "Invalid migration kind", $"Unknown migration kind '{kind}'.", "PXAMIG002");

        var providers = MigrationProviderCatalog.All
            .Where(item => domain is null || string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .Where(item => kind is null || string.Equals(item.MigrationKind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(CanRead)
            .Select(ToCatalogResponse)
            .ToArray();
        return Ok(new { providers });
    }

    [HttpGet("{domain}/{kind}/{provider}")]
    public IActionResult GetProvider(string domain, string kind, string provider)
    {
        if (!TryResolve(domain, kind, provider, out var descriptor, out var error)) return error!;
        if (!CanRead(descriptor)) return PermissionDenied(ReadPermission(descriptor));
        return Ok(ToCatalogResponse(descriptor));
    }

    [HttpGet("{domain}/{kind}/{provider}/examples")]
    public IActionResult GetExamples(string domain, string kind, string provider)
    {
        if (!TryResolve(domain, kind, provider, out var descriptor, out var error)) return error!;
        if (!CanRead(descriptor)) return PermissionDenied(ReadPermission(descriptor));

        if (descriptor.Domain == MigrationDomains.Pdf
            && descriptor.ProviderKey == "nutrient-viewer"
            && descriptor.MigrationKind is MigrationKinds.Viewer or MigrationKinds.Designer)
        {
            return Ok(new
            {
                examples = new object[]
                {
                    new { id = "typescript", inputType = "typescript", source = NutrientViewerExamples.ComprehensiveTypeScript },
                    new { id = "instant-json", inputType = "instantJson", source = NutrientViewerExamples.ComprehensiveInstantJson, pages = NutrientViewerExamples.ComprehensivePageGeometry }
                }
            });
        }

        if (descriptor.Domain == MigrationDomains.Pdf
            && descriptor.MigrationKind == MigrationKinds.Code
            && descriptor.ProviderKey == "nutrient")
        {
            return Ok(new
            {
                examples = new object[]
                {
                    new { id = "comprehensive", inputType = "csharp", source = NutrientCodeExamples.ComprehensiveGdPictureCSharp }
                }
            });
        }

        if (descriptor.Domain == MigrationDomains.Pdf && descriptor.MigrationKind == MigrationKinds.Code)
        {
            try
            {
                var sample = PdfTrustMigrationExamples.Get(descriptor.ProviderKey);
                return Ok(new { examples = new object[] { new { id = "pdf-trust", inputType = "csharp", source = sample.SourceCode } } });
            }
            catch (KeyNotFoundException)
            {
                // The descriptor remains discoverable even when no backend-owned example exists yet.
            }
        }

        return NotFound(new ProblemDetails
        {
            Status = 404,
            Title = "Migration example unavailable",
            Detail = $"No backend-owned example is available for '{descriptor.ProviderKey}'.",
            Extensions = { ["code"] = "PXAMIG006" }
        });
    }

    [HttpPost("{domain}/{kind}/{provider}/convert")]
    public IActionResult Convert(
        string domain,
        string kind,
        string provider,
        [FromBody] UnifiedMigrationRequest request)
    {
        if (!TryResolve(domain, kind, provider, out var descriptor, out var error)) return error!;
        if (!CanExecute(descriptor)) return PermissionDenied(descriptor.PermissionKey);
        if (string.IsNullOrWhiteSpace(request.Source) && string.IsNullOrWhiteSpace(request.SourceBase64))
            return MigrationProblem(400, "Migration source required", "Provide source or sourceBase64.", "PXAMIG003");
        if (descriptor.MigrationKind is MigrationKinds.Code or MigrationKinds.Viewer && string.IsNullOrWhiteSpace(request.Source))
            return MigrationProblem(400, "Migration source required", "This migration provider requires text source.", "PXAMIG003");
        if (ExceedsInputLimit(request, descriptor.MaximumInputBytes))
            return MigrationProblem(413, "Migration source is too large", $"The provider accepts at most {descriptor.MaximumInputBytes} bytes.", "PXAMIG010");

        try
        {
            return descriptor.MigrationKind switch
            {
                MigrationKinds.Code => ConvertCode(descriptor, request),
                MigrationKinds.Viewer => ConvertViewer(descriptor, request),
                MigrationKinds.Designer => ConvertReport(descriptor, request),
                _ => MigrationProblem(400, "Unsupported migration kind", "No datasource migration provider is registered.", "PXAMIG004")
            };
        }
        catch (NutrientViewerSourceException exception)
        {
            return MigrationProblem(400, "Viewer source could not be converted", exception.Message, exception.Code);
        }
        catch (NutrientInstantJsonException exception)
        {
            return MigrationProblem(400, "Instant JSON could not be converted", exception.Message, exception.Code);
        }
        catch (ArgumentException exception)
        {
            return MigrationProblem(400, "Migration input is invalid", exception.Message, "PXAMIG005");
        }
    }

    [HttpPost("pdf/designer/nutrient-viewer/convert")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 64 * 1024 * 1024)]
    public async Task<IActionResult> ConvertNutrientViewerToDesign(
        [FromForm] NutrientViewerDesignMigrationRequest request,
        CancellationToken cancellationToken)
    {
        var descriptor = MigrationProviderCatalog.Get(MigrationDomains.Pdf, MigrationKinds.Designer, "nutrient-viewer");
        if (!CanExecute(descriptor)) return PermissionDenied(descriptor.PermissionKey);
        if (request.Pdf is null || request.Pdf.Length == 0)
            return MigrationProblem(400, "Source PDF required", "Select the original PDF that the Nutrient Viewer opens.", "CANMIGNUTDESIGN100");
        if (!request.Pdf.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && !request.Pdf.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return MigrationProblem(400, "Invalid source PDF", "The uploaded document must be a PDF.", "CANMIGNUTDESIGN101");
        if (request.Pdf.Length > 50 * 1024 * 1024)
            return MigrationProblem(413, "Source PDF is too large", "The Nutrient Designer migration accepts PDFs up to 50 MiB.", "CANMIGNUTDESIGN102");
        if (string.IsNullOrWhiteSpace(request.Source))
            return MigrationProblem(400, "Nutrient source required", "Provide Viewer JavaScript, TypeScript, or Instant JSON.", "PXAMIG003");
        if (Encoding.UTF8.GetByteCount(request.Source) > descriptor.MaximumInputBytes)
            return MigrationProblem(413, "Nutrient source is too large", $"The provider accepts at most {descriptor.MaximumInputBytes} bytes.", "PXAMIG010");

        try
        {
            IReadOnlyList<string>? compilerDiagnostics = null;
            if (!string.IsNullOrWhiteSpace(request.CompilerDiagnostics))
                compilerDiagnostics = JsonSerializer.Deserialize<string[]>(request.CompilerDiagnostics, PackageJsonOptions);
            await using var stream = request.Pdf.OpenReadStream();
            var result = await nutrientViewerDesignMigrationService.ConvertAsync(
                stream,
                request.Pdf.FileName,
                request.InputType ?? "instantJson",
                request.Source,
                request.TranspiledSource,
                compilerDiagnostics,
                cancellationToken);
            var diagnostics = result.Diagnostics
                .Select(static item => new MigrationDiagnosticResponse(item.Code, item.Severity, item.Message))
                .ToArray();
            var errors = diagnostics.Count(static item => item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
            var warnings = diagnostics.Count(static item => item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
            var elements = result.Design.Pages.Sum(static page => page.Elements.Count);

            return Ok(new UnifiedMigrationResponse(
                descriptor.ProviderKey,
                descriptor.ProviderFamily,
                descriptor.Domain,
                descriptor.MigrationKind,
                request.InputType ?? "instantJson",
                "completed",
                new MigrationSummaryResponse(elements, warnings, errors, diagnostics.Length),
                [new MigrationArtifactResponse("design", "application/json", ".json", result.Design)],
                diagnostics,
                result.DocumentFidelity,
                result.SourcePreservation,
                Metadata: new
                {
                    result.MappedAnnotations,
                    result.AppliedFormValues,
                    result.CapabilityReport
                }));
        }
        catch (NutrientViewerSourceException exception)
        {
            return MigrationProblem(400, "Viewer source could not be converted", exception.Message, exception.Code);
        }
        catch (NutrientInstantJsonException exception)
        {
            return MigrationProblem(400, "Instant JSON could not be converted", exception.Message, exception.Code);
        }
        catch (ArgumentException exception)
        {
            return MigrationProblem(400, "Migration input is invalid", exception.Message, "PXAMIG005");
        }
        catch (InvalidDataException exception)
        {
            return MigrationProblem(400, "PDF could not be imported", exception.Message, "CANMIGNUTDESIGN103");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return MigrationProblem(400, "PDF could not be imported", "The original PDF could not be parsed or converted.", "CANMIGNUTDESIGN103");
        }
    }

    [HttpPost("{domain}/{kind}/{provider}/preview")]
    public IActionResult Preview(
        string domain,
        string kind,
        string provider,
        [FromBody] UnifiedMigrationRequest request)
    {
        if (!TryResolve(domain, kind, provider, out var descriptor, out var error)) return error!;
        if (!CanExecute(descriptor)) return PermissionDenied(descriptor.PermissionKey);
        if (!descriptor.SupportsPreview || descriptor.MigrationKind != MigrationKinds.Code)
            return MigrationProblem(400, "Preview is not supported", $"'{descriptor.ProviderKey}' has no server preview.", "PXAMIG007");
        if (string.IsNullOrWhiteSpace(request.Source))
            return MigrationProblem(400, "Migration source required", "Provide source.", "PXAMIG003");
        if (ExceedsInputLimit(request, descriptor.MaximumInputBytes))
            return MigrationProblem(413, "Migration source is too large", $"The provider accepts at most {descriptor.MaximumInputBytes} bytes.", "PXAMIG010");

        try
        {
            var bytes = migrationService.GeneratePreview(descriptor.ProviderKey, request.Source);
            return descriptor.Domain == MigrationDomains.Spreadsheet
                ? File(bytes, "text/html; charset=utf-8")
                : File(bytes, "application/pdf", "migration-preview.pdf");
        }
        catch (ArgumentException exception)
        {
            return MigrationProblem(400, "Preview could not be generated", exception.Message, "PXAMIG008");
        }
    }

    private IActionResult ConvertCode(MigrationProviderDescriptor descriptor, UnifiedMigrationRequest request)
    {
        var result = migrationService.Convert(descriptor.ProviderKey, request.Source!);
        return Ok(new UnifiedMigrationResponse(
            descriptor.ProviderKey,
            descriptor.ProviderFamily,
            descriptor.Domain,
            descriptor.MigrationKind,
            request.InputType ?? "csharp",
            "completed",
            new MigrationSummaryResponse(
                result.Summary.ConvertedCount,
                result.Summary.WarningCount,
                result.Summary.ErrorCount,
                result.Summary.TotalDiagnostics),
            [new MigrationArtifactResponse(descriptor.Artifacts[0].Id, descriptor.Artifacts[0].MediaType, descriptor.Artifacts[0].FileExtension, result.PxaCode)],
            result.Diagnostics.Select(static item => new MigrationDiagnosticResponse(item.Code, item.Severity, item.Message)).ToArray(),
            result.Summary.ErrorCount == 0 ? "compatible" : "reviewRequired",
            "regenerated",
            PdfTrust: CreatePdfTrustResponse(result.PdfTrust)));
    }

    private IActionResult ConvertReport(MigrationProviderDescriptor descriptor, UnifiedMigrationRequest request)
    {
        var result = reportMigrationService.Convert(new ReportMigrationInput(
            descriptor.ProviderKey,
            request.Source,
            request.Resources,
            request.ResourceXml,
            request.SourceBase64));
        var diagnostics = result.Diagnostics
            .Select(static item => new MigrationDiagnosticResponse(item.Id, item.Severity.ToString(), item.Message))
            .ToArray();
        var errors = diagnostics.Count(static item => string.Equals(item.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        var warnings = diagnostics.Count(static item => string.Equals(item.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        var elements = result.Design.Pages.Sum(static page => page.Elements.Count);
        return Ok(new UnifiedMigrationResponse(
            result.Provider,
            descriptor.ProviderFamily,
            descriptor.Domain,
            descriptor.MigrationKind,
            request.InputType ?? "report",
            "completed",
            new MigrationSummaryResponse(elements, warnings, errors, diagnostics.Length),
            [new MigrationArtifactResponse("design", "application/json", ".json", result.Design)],
            diagnostics,
            errors == 0 && warnings == 0 ? "exact" : "reviewRequired",
            "regenerated"));
    }

    private IActionResult ConvertViewer(MigrationProviderDescriptor descriptor, UnifiedMigrationRequest request)
    {
        var inputType = request.InputType ?? "javascript";
        if (inputType.Equals("instantJson", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Pages is null || request.Pages.Count == 0)
                return MigrationProblem(400, "Page geometry required", "Instant JSON requires page geometry.", "CANMIGNUTJSON008");
            var result = new NutrientInstantJsonConverter().Convert(request.Source!, request.Pages, request.SourceName);
            return Ok(ViewerResponse(descriptor, inputType, result, request.Pages));
        }

        NutrientViewerSourceResult sourceResult;
        if (inputType.Equals("typescript", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.TranspiledSource))
                return MigrationProblem(400, "TypeScript compiler output required", "Provide transpiledSource.", "CANMIGNUTWEB007");
            sourceResult = new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(
                request.Source!, request.TranspiledSource, request.SourceName, request.CompilerDiagnostics);
        }
        else if (inputType.Equals("javascript", StringComparison.OrdinalIgnoreCase) || inputType.Equals("js", StringComparison.OrdinalIgnoreCase))
        {
            sourceResult = new NutrientViewerSourceConverter().Convert(request.Source!, "javascript", request.SourceName);
        }
        else
        {
            return MigrationProblem(400, "Unsupported Viewer input", "Use javascript, typescript, or instantJson.", "CANMIGNUTWEB003");
        }

        return Ok(ViewerResponse(descriptor, inputType, sourceResult, request.Pages));
    }

    private static UnifiedMigrationResponse ViewerResponse(
        MigrationProviderDescriptor descriptor,
        string inputType,
        NutrientViewerSourceResult result,
        IReadOnlyList<NutrientPageGeometry>? pages)
    {
        var package = CreateViewerPackage(
            inputType,
            JsonNode.Parse(result.NormalizedViewerSettingsJson),
            result.GeneratedIntegrationModule,
            null,
            null,
            pages);
        return new(
            descriptor.ProviderKey, descriptor.ProviderFamily, descriptor.Domain, descriptor.MigrationKind, inputType, "completed",
            CapabilitySummary(result.CapabilityReport),
            [new("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json", package)],
            result.Diagnostics.Select(static item => new MigrationDiagnosticResponse(item.Code, item.Severity, item.Message)).ToArray(),
            result.DocumentFidelity, result.SourcePreservation, result.SourceMap,
            new Dictionary<string, string>
            {
                ["source"] = result.SourceChecksum,
                ["normalized"] = result.NormalizedChecksum,
                ["generated"] = result.GeneratedChecksum
            }, result.CapabilityReport, CreatePdfTrustResponse(result.PdfTrust));
    }

    private static UnifiedMigrationResponse ViewerResponse(
        MigrationProviderDescriptor descriptor,
        string inputType,
        NutrientInstantJsonResult result,
        IReadOnlyList<NutrientPageGeometry>? pages)
    {
        var package = CreateViewerPackage(
            inputType,
            null,
            null,
            JsonNode.Parse(result.AnnotationSidecarJson),
            JsonNode.Parse(result.FormValuePayloadJson),
            pages);
        return new(
            descriptor.ProviderKey, descriptor.ProviderFamily, descriptor.Domain, descriptor.MigrationKind, inputType, "completed",
            CapabilitySummary(result.CapabilityReport),
            [new("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json", package)],
            result.Diagnostics.Select(static item => new MigrationDiagnosticResponse(item.Code, item.Severity, item.Message)).ToArray(),
            result.DocumentFidelity, result.SourcePreservation, null,
            new Dictionary<string, string>
            {
                ["source"] = result.SourceChecksum,
                ["normalized"] = result.NormalizedChecksum,
                ["generated"] = result.GeneratedChecksum
            }, result.CapabilityReport, CreatePdfTrustResponse(result.PdfTrust));
    }

    private static JsonObject CreateViewerPackage(
        string inputType,
        JsonNode? viewerSettings,
        string? integrationModule,
        JsonNode? annotationSidecar,
        JsonNode? formValues,
        IReadOnlyList<NutrientPageGeometry>? pages) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "pxa.pdf-viewer-package",
        ["provider"] = "nutrient",
        ["sourceType"] = inputType,
        ["viewerSettings"] = viewerSettings,
        ["integrationModule"] = integrationModule,
        ["annotationSidecar"] = annotationSidecar,
        ["formValues"] = formValues,
        ["pageGeometry"] = JsonSerializer.SerializeToNode(pages ?? [], PackageJsonOptions)
    };

    private static MigrationSummaryResponse CapabilitySummary(NutrientMigrationCapabilityReport report) =>
        new(report.Migrated, report.ReviewRequired + report.Approximated, report.Unsupported, report.Migrated + report.Approximated + report.Omitted + report.ReviewRequired + report.Unsupported);

    private static object? CreatePdfTrustResponse(PdfTrustMigrationReport? report)
    {
        if (report is null) return null;

        return new
        {
            providerKey = report.ProviderKey,
            migrationKind = report.MigrationKind,
            overallDisposition = report.OverallDisposition.ToString(),
            evidence = report.Evidence.Select(item => new
            {
                capability = item.Capability.ToString(),
                disposition = item.Disposition.ToString(),
                sourceOperation = item.SourceOperation,
                targetOperation = item.TargetOperation,
                sourceSemantics = item.SourceSemantics.ToString(),
                diagnosticId = item.DiagnosticId,
                documentationPath = item.DocumentationPath,
                providerOptions = item.ProviderOptions
            }),
            manualActions = report.ManualActions.Select(action => new
            {
                actionId = action.ActionId,
                capability = action.Capability.ToString(),
                disposition = action.Disposition.ToString(),
                sourceOperation = action.SourceOperation,
                targetOperation = action.TargetOperation,
                documentationPath = action.DocumentationPath
            })
        };
    }

    private bool TryResolve(
        string domain,
        string kind,
        string provider,
        out MigrationProviderDescriptor descriptor,
        out IActionResult? error)
    {
        if (!MigrationDomains.All.Contains(domain))
        {
            descriptor = null!;
            error = MigrationProblem(400, "Invalid migration domain", $"Unknown migration domain '{domain}'.", "PXAMIG001");
            return false;
        }
        if (!MigrationKinds.All.Contains(kind))
        {
            descriptor = null!;
            error = MigrationProblem(400, "Invalid migration kind", $"Unknown migration kind '{kind}'.", "PXAMIG002");
            return false;
        }
        if (!MigrationProviderCatalog.TryGet(domain, kind, provider, out descriptor!))
        {
            error = MigrationProblem(404, "Migration provider not found", $"No provider exists at '{domain}/{kind}/{provider}'.", "PXAMIG009");
            return false;
        }
        error = null;
        return true;
    }

    private bool CanRead(MigrationProviderDescriptor descriptor) =>
        !IsDesignerRequest() || HasPermission(ReadPermission(descriptor)) || CanExecute(descriptor);

    private bool CanExecute(MigrationProviderDescriptor descriptor)
    {
        if (!IsDesignerRequest()) return true;
        var permissions = PxaDesignerMigrationPermissions.CompatibilityPermissions(descriptor);
        var canonical = HasPermission(PxaDesignerMigrationPermissions.KindPermission(
                            descriptor.Domain, descriptor.MigrationKind, "execute")) &&
                        HasPermission(descriptor.PermissionKey);
        var legacy = permissions.Skip(1).All(HasPermission);
        return canonical || legacy;
    }

    private static string ReadPermission(MigrationProviderDescriptor descriptor) =>
        PxaDesignerMigrationPermissions.KindPermission(descriptor.Domain, descriptor.MigrationKind, "read");

    private static bool ExceedsInputLimit(UnifiedMigrationRequest request, long maximumBytes)
    {
        if (request.Source is not null && Encoding.UTF8.GetByteCount(request.Source) > maximumBytes)
            return true;

        // Reject oversized Base64 before decoding; exact validity is handled by the report service.
        return request.SourceBase64 is not null &&
               request.SourceBase64.Length > checked((maximumBytes * 4 / 3) + 4);
    }

    private bool IsDesignerRequest() => string.Equals(
        Request.Headers["X-PXA-Application"].ToString(), "designer", StringComparison.OrdinalIgnoreCase);

    private bool HasPermission(string permission) => User.HasClaim(PxaClaimTypes.Permission, permission);

    private IActionResult PermissionDenied(string permission) => MigrationProblem(
        403, "Designer migration permission required",
        "Your organization role does not allow this migration provider.",
        "PXA_DESIGNER_PERMISSION_REQUIRED", permission);

    private ObjectResult MigrationProblem(
        int status,
        string title,
        string detail,
        string code,
        string? requiredPermission = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (requiredPermission is not null) problem.Extensions["requiredPermission"] = requiredPermission;
        return StatusCode(status, problem);
    }

    private static object ToCatalogResponse(MigrationProviderDescriptor descriptor) => new
    {
        providerKey = descriptor.ProviderKey,
        providerFamily = descriptor.ProviderFamily,
        descriptor.DisplayName,
        descriptor.Domain,
        kind = descriptor.MigrationKind,
        descriptor.Status,
        descriptor.Description,
        descriptor.Aliases,
        descriptor.Inputs,
        descriptor.Artifacts,
        descriptor.SupportsPreview,
        descriptor.HasExamples,
        descriptor.DocumentationId,
        descriptor.FeatureId,
        descriptor.MaximumInputBytes,
        requiredPermission = descriptor.PermissionKey
    };
}

public sealed record UnifiedMigrationRequest(
    string? InputType,
    string? Source,
    string? SourceName = null,
    string? SourceBase64 = null,
    IReadOnlyDictionary<string, string>? Resources = null,
    string? ResourceXml = null,
    IReadOnlyList<NutrientPageGeometry>? Pages = null,
    string? TranspiledSource = null,
    IReadOnlyList<string>? CompilerDiagnostics = null,
    IReadOnlyDictionary<string, JsonElement>? Options = null);

public sealed class NutrientViewerDesignMigrationRequest
{
    public IFormFile? Pdf { get; set; }
    public string? InputType { get; set; }
    public string Source { get; set; } = "";
    public string? TranspiledSource { get; set; }
    public string? CompilerDiagnostics { get; set; }
}

public sealed record UnifiedMigrationResponse(
    string Provider,
    string ProviderFamily,
    string Domain,
    string Kind,
    string DetectedInput,
    string Status,
    MigrationSummaryResponse Summary,
    IReadOnlyList<MigrationArtifactResponse> Artifacts,
    IReadOnlyList<MigrationDiagnosticResponse> Diagnostics,
    string? DocumentFidelity = null,
    string? SourcePreservation = null,
    object? SourceMap = null,
    IReadOnlyDictionary<string, string>? Checksums = null,
    object? Metadata = null,
    object? PdfTrust = null);

public sealed record MigrationSummaryResponse(int ConvertedCount, int WarningCount, int ErrorCount, int TotalDiagnostics);
public sealed record MigrationArtifactResponse(string Id, string MediaType, string? FileExtension, object Content);
public sealed record MigrationDiagnosticResponse(string Code, string Severity, string Message);
