using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PVAD.WebApi.Application.Compliance;
using PVAD.WebApi.Observability;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PvadRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pvad/v1/admin/system")]
public sealed class AdminSystemController(
    PvadSystemHealthService systemHealth,
    PvadDependencyComplianceCatalog dependencyCompliance) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(typeof(PvadSystemHealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PvadSystemHealthResponse>> GetHealth(
        CancellationToken cancellationToken) =>
        Ok(await systemHealth.GetAsync(cancellationToken));

    [HttpGet("dependency-compliance")]
    [ProducesResponseType(typeof(PvadDependencyComplianceResponse), StatusCodes.Status200OK)]
    public ActionResult<PvadDependencyComplianceResponse> GetDependencyCompliance() =>
        Ok(dependencyCompliance.Status);
}
