using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.WebApi.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Authorize(Policy = PvadPermissions.LicensesManage)]
[Authorize(Policy = PvadSecurityPolicies.RecentAuthentication)]
[Route("api/pvad/v1/admin/native-licenses")]
[RequestSizeLimit(64 * 1024)]
public sealed class AdminNativeLicensesController(PvadNativeLicenseIssuanceService issuer) : ControllerBase
{
    [HttpGet("capabilities")]
    public async Task<ActionResult<NativeLicenseCapabilities>> Capabilities(CancellationToken cancellationToken)
    {
        try { return Ok(await issuer.CapabilitiesAsync(User, cancellationToken)); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpGet]
    public async Task<ActionResult<NativeLicensePage>> List([FromQuery] Guid? organizationId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        try { return Ok(await issuer.ListAsync(User, organizationId, page, pageSize, cancellationToken)); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpGet("{licenseId:guid}/details")]
    public async Task<ActionResult<NativeLicenseDetails>> Details(Guid licenseId, CancellationToken cancellationToken)
    {
        try { return Ok(await issuer.DetailsAsync(User, licenseId, cancellationToken)); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpPost]
    [EnableRateLimiting("native-license-issuance")]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("native-licenses.issue")]
    public async Task<ActionResult<NativeLicenseResponse>> Issue(IssueNativeLicenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await issuer.IssueAsync(User, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { licenseId = result.Id }, result);
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpPost("{licenseId:guid}/renew")]
    [EnableRateLimiting("native-license-issuance")]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("native-licenses.renew")]
    public async Task<ActionResult<NativeLicenseResponse>> Renew(Guid licenseId, RenewNativeLicenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await issuer.RenewAsync(User, licenseId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { licenseId = result.Id }, result);
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpGet("{licenseId:guid}")]
    public async Task<ActionResult<NativeLicenseResponse>> Get(Guid licenseId, CancellationToken cancellationToken)
    {
        try { return Ok(PvadNativeLicenseIssuanceService.Summary(await issuer.ReadAsync(User, licenseId, cancellationToken))); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpGet("{licenseId:guid}/download")]
    public async Task<IActionResult> Download(Guid licenseId, CancellationToken cancellationToken)
    {
        try
        {
            var license = await issuer.ReadAsync(User, licenseId, cancellationToken);
            PvadNativeLicenseRecords.ReadStoredPayload(license);
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(Encoding.UTF8.GetBytes(license.ArtifactJson), "application/json", $"PVAD-native-{license.Id:N}.json");
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    private ObjectResult Failure(PvadNativeIssuanceException error) => Problem(statusCode: error.StatusCode,
        title: "Native license operation unavailable", detail: "The native license operation could not be completed.",
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
