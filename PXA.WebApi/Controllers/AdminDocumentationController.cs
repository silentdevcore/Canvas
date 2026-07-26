using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Roles = $"{PxaRoles.SystemAdministrator},{PxaRoles.OrganizationAdministrator}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/documentation")]
public sealed class AdminDocumentationController(IWebHostEnvironment environment) : ControllerBase
{
    private readonly string documentationRoot =
        Path.Combine(environment.ContentRootPath, "AdminDocumentation");

    [HttpGet]
    public IActionResult GetHandbook()
    {
        var path = Path.Combine(documentationRoot, "admin-documentation.json");
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "application/json")
            : DocumentationUnavailable();
    }

    [HttpGet("images/{fileName}")]
    public IActionResult GetImage(string fileName)
    {
        if (!IsSafePngName(fileName))
            return NotFound();

        var path = Path.Combine(documentationRoot, "images", fileName);
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "image/png")
            : NotFound();
    }

    private static bool IsSafePngName(string fileName) =>
        fileName == Path.GetFileName(fileName) &&
        fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
        fileName.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private ObjectResult DocumentationUnavailable() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Admin documentation unavailable",
        detail: "The protected Admin handbook is not installed in this deployment.");
}
