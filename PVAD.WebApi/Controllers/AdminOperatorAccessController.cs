using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PvadRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pvad/v1/admin/operator")]
public sealed class AdminOperatorAccessController(
    UserManager<PvadIdentityUser> userManager,
    PvadSystemOperatorAccess operatorAccess) : ControllerBase
{
    [HttpGet("access")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetAccess()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !operatorAccess.IsAuthorized(user))
            return Forbid();

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            return Forbid();

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(subject));
        PVAD.WebApi.Infrastructure.PvadResponseHeaders.Set(Response, "Operator",
            $"pxa-{Convert.ToHexString(digest)[..24].ToLowerInvariant()}");
        return NoContent();
    }
}
