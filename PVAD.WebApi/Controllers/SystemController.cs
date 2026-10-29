using Microsoft.AspNetCore.Mvc;
using PVAD.Core.ProjectFormats;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Route("api/system")]
[Route("api/pvad/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("brand")]
    [ProducesResponseType(typeof(ApiBrandingResponse), 200)]
    public IActionResult GetBranding()
    {
        return Ok(ApiBrandingResponse.Current);
    }
}

public sealed record ApiBrandingResponse(
    string ProductName,
    string DeveloperName,
    string CliName,
    string NativeFileExtension,
    string LegacyProductName,
    string LegacyNamespacePrefix,
    string[] CompatibilityNotes)
{
    public string[] NativeFileExtensions { get; init; } =
        [PvadProjectFormat.PvadpExtension, PvadProjectFormat.PvadsExtension];

    public static readonly ApiBrandingResponse Current = new(
        ProductName: "PVADE",
        DeveloperName: "PVAD",
        CliName: "pvad",
        NativeFileExtension: PvadProjectFormat.PvadpExtension,
        LegacyProductName: "PXA",
        LegacyNamespacePrefix: "PXA",
        CompatibilityNotes:
        [
            "Legacy /api routes remain compatible.",
            "Use PVAD.* namespaces for new code integrations.",
            "The primary PDF project extension is .pvadp; Spreadsheet projects use .pvads. Legacy .pxap and .pxas readers remain compatible.",
            "CANMIG diagnostic identifiers remain stable."
        ]);
}
