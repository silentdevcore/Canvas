using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Migration.Pdf.Code.Nutrient;
using PXA.Migration.Pdf.Viewer.Nutrient;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/migrations/pdf/viewer")]
[Authorize(Policy = PxaDesignerPermissions.MigrationPdfExecute)]
[Authorize(Policy = "designer.migration.pdf.provider.nutrient-viewer.execute")]
[EnableRateLimiting("designer-code")]
public sealed class NutrientViewerMigrationController : ControllerBase
{
    [HttpGet("examples/comprehensive")]
    [ProducesResponseType(typeof(NutrientComprehensiveExample), StatusCodes.Status200OK)]
    public ActionResult<NutrientComprehensiveExample> GetComprehensiveExample() => Ok(
        new NutrientComprehensiveExample(
            NutrientViewerExamples.ComprehensiveTypeScript,
            NutrientViewerExamples.ComprehensiveInstantJson,
            NutrientCodeExamples.ComprehensiveGdPictureCSharp,
            NutrientViewerExamples.ComprehensivePageGeometry));

    [HttpPost("convert")]
    [RequestSizeLimit(NutrientInstantJsonConverter.MaximumPayloadBytes + 64 * 1024)]
    [ProducesResponseType(typeof(NutrientInstantJsonResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NutrientViewerSourceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<object> Convert([FromBody] NutrientViewerMigrationRequest request)
    {
        if (string.Equals(request.InputType, "javascript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InputType, "js", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return Ok(new NutrientViewerSourceConverter().Convert(request.Source, "javascript", request.SourceName));
            }
            catch (NutrientViewerSourceException exception)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Nutrient Viewer source could not be converted",
                    detail: exception.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
            }
        }

        if (string.Equals(request.InputType, "typescript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InputType, "ts", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.TranspiledSource))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "TypeScript compiler output is required",
                    detail: "Compile the TypeScript source with the PXA Designer TypeScript compiler before submitting it.",
                    extensions: new Dictionary<string, object?> { ["code"] = "CANMIGNUTWEB007" });
            }

            try
            {
                return Ok(new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(
                    request.Source,
                    request.TranspiledSource,
                    request.SourceName,
                    request.CompilerDiagnostics));
            }
            catch (NutrientViewerSourceException exception)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Nutrient Viewer TypeScript could not be converted",
                    detail: exception.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
            }
        }

        if (!string.Equals(request.InputType, "instantJson", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unsupported Nutrient Viewer migration input",
                detail: "Use inputType 'javascript', 'typescript', or 'instantJson'.",
                extensions: new Dictionary<string, object?> { ["code"] = "CANMIGNUTWEB003" });
        }

        if (request.Pages is null || request.Pages.Count == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Page geometry is required",
                detail: "Provide the width and height of every source PDF page used by the annotations.",
                extensions: new Dictionary<string, object?> { ["code"] = "CANMIGNUTJSON008" });
        }

        try
        {
            return Ok(new NutrientInstantJsonConverter().Convert(
                request.Source,
                request.Pages,
                request.SourceName));
        }
        catch (NutrientInstantJsonException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nutrient Instant JSON could not be converted",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
        catch (ArgumentException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nutrient Viewer migration input is invalid",
                detail: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "CANMIGNUTJSON009" });
        }
    }
}

public sealed record NutrientViewerMigrationRequest(
    string InputType,
    string Source,
    IReadOnlyList<NutrientPageGeometry>? Pages = null,
    string? SourceName = null,
    string? TranspiledSource = null,
    IReadOnlyList<string>? CompilerDiagnostics = null);

public sealed record NutrientComprehensiveExample(
    string TypeScript,
    string InstantJson,
    string GdPictureCSharp,
    IReadOnlyList<NutrientPageGeometry> Pages);
