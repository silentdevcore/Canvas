using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Application.Compliance;
using PXA.WebApi.Observability;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PxaRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/system")]
public sealed class AdminSystemController(
    PxaSystemHealthService systemHealth,
    PxaDependencyComplianceCatalog dependencyCompliance) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(typeof(PxaSystemHealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PxaSystemHealthResponse>> GetHealth(
        CancellationToken cancellationToken) =>
        Ok(await systemHealth.GetAsync(cancellationToken));

    [HttpGet("dependency-compliance")]
    [ProducesResponseType(typeof(PxaDependencyComplianceResponse), StatusCodes.Status200OK)]
    public ActionResult<PxaDependencyComplianceResponse> GetDependencyCompliance() =>
        Ok(dependencyCompliance.Status);
}
