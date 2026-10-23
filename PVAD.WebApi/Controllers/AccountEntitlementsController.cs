using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pvad/v1/account/entitlements")]
public sealed class AccountEntitlementsController : ControllerBase
{
    private readonly IPvadTenantContext tenantContext;
    private readonly IPvadEntitlementService entitlementService;

    public AccountEntitlementsController(
        IPvadTenantContext tenantContext,
        IPvadEntitlementService entitlementService)
    {
        this.tenantContext = tenantContext;
        this.entitlementService = entitlementService;
    }

    [HttpGet("{capability}")]
    public async Task<ActionResult<PvadEntitlementDecision>> Evaluate(
        string capability,
        [FromQuery] long quantity = 0,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Problem(statusCode: 403, title: "Organization context required");
        return Ok(await entitlementService.EvaluateAsync(
            organizationId,
            capability,
            quantity,
            cancellationToken));
    }
}
