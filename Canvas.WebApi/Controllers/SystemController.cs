using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/system")]
[Route("api/pxa/system")]
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
    public static readonly ApiBrandingResponse Current = new(
        ProductName: "Power Dox Automation",
        DeveloperName: "PXA",
        CliName: "pxa",
        NativeFileExtension: ".pxa",
        LegacyProductName: "Canvas",
        LegacyNamespacePrefix: "Canvas",
        CompatibilityNotes:
        [
            "Legacy /api routes remain compatible.",
            "Canvas.* namespaces remain available during the transition.",
            "CANMIG diagnostic identifiers remain stable."
        ]);
}
