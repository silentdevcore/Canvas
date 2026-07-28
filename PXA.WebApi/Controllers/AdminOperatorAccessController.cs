using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PxaRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/operator")]
public sealed class AdminOperatorAccessController : ControllerBase
{
    [HttpGet("access")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetAccess()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            return Forbid();

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(subject));
        Response.Headers["X-PXA-Operator"] =
            $"pxa-{Convert.ToHexString(digest)[..24].ToLowerInvariant()}";
        return NoContent();
    }
}
