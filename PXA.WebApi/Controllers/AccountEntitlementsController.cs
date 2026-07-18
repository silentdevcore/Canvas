using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Entitlements;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/account/entitlements")]
public sealed class AccountEntitlementsController : ControllerBase
{
    private readonly IPxaTenantContext tenantContext;
    private readonly IPxaEntitlementService entitlementService;

    public AccountEntitlementsController(
        IPxaTenantContext tenantContext,
        IPxaEntitlementService entitlementService)
    {
        this.tenantContext = tenantContext;
        this.entitlementService = entitlementService;
    }

    [HttpGet("{capability}")]
    public async Task<ActionResult<PxaEntitlementDecision>> Evaluate(
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
