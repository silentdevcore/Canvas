using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PvadAccountPermissions.LicensesRead)]
[Route("api/pvad/v1/account/licenses")]
public sealed class AccountLicensesController : ControllerBase
{
    private readonly PvadDbContext dbContext;
    private readonly IPvadTenantContext tenantContext;
    private readonly PvadOfflineLicenseValidator licenseValidator;

    public AccountLicensesController(
        PvadDbContext dbContext,
        IPvadTenantContext tenantContext,
        PvadOfflineLicenseValidator licenseValidator)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.licenseValidator = licenseValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountLicenseResponse>>> GetLicenses(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();

        return Ok(await BuildQuery(dbContext.OfflineLicenses.AsNoTracking()
                .Where(value => value.OrganizationId == organizationId)
                .OrderByDescending(value => value.IssuedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("{licenseId:guid}")]
    public async Task<ActionResult<AccountLicenseResponse>> GetLicense(
        Guid licenseId, CancellationToken cancellationToken)
    {
        var license = await FindOwnLicenseAsync(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        return Ok(await BuildQuery(dbContext.OfflineLicenses.AsNoTracking().Where(value => value.Id == licenseId))
            .SingleAsync(cancellationToken));
    }

    [HttpGet("{licenseId:guid}/download")]
    public async Task<IActionResult> DownloadLicense(Guid licenseId, CancellationToken cancellationToken)
    {
        var license = await FindOwnLicenseAsync(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        return PvadLicenseDownload.Create(license);
    }

    [HttpGet("{licenseId:guid}/validate")]
    public async Task<ActionResult<AccountLicenseValidationResponse>> ValidateLicense(
        Guid licenseId,
        [FromQuery] string? productVersion,
        [FromQuery] string? deploymentId,
        [FromQuery] int activeInstances = 1,
        CancellationToken cancellationToken = default)
    {
        var license = await FindOwnLicenseAsync(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleAsync(value => value.Id == license.SubscriptionId, cancellationToken);
        var validation = licenseValidator.Validate(
            license, subscription, DateTimeOffset.UtcNow, productVersion, deploymentId, activeInstances);
        return Ok(new AccountLicenseValidationResponse(
            validation.Valid,
            license.Status.ToString(),
            license.ValidFrom,
            license.ValidUntil,
            validation.Code));
    }

    private IQueryable<AccountLicenseResponse> BuildQuery(IQueryable<OfflineLicense> query) =>
        from license in query
        join subscription in dbContext.OrganizationSubscriptions.AsNoTracking() on license.SubscriptionId equals subscription.Id
        select new AccountLicenseResponse(
            license.Id, license.LicenseNumber, subscription.Edition.ToString(), subscription.DeploymentMode.ToString(),
            license.Status.ToString(), license.ValidFrom, license.ValidUntil, license.InstanceLimit,
            license.IssuedAt, license.RevokedAt, license.RevocationReason);

    private async Task<OfflineLicense?> FindOwnLicenseAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return null;
        return await dbContext.OfflineLicenses.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == licenseId && value.OrganizationId == organizationId, cancellationToken);
    }

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");
}

public sealed record AccountLicenseResponse(
    Guid Id, string LicenseNumber, string Edition, string DeploymentMode, string Status,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, int InstanceLimit,
    DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt, string? RevocationReason);

public sealed record AccountLicenseValidationResponse(
    bool Valid, string Status, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string Code)
{
    public string? CanonicalCode => PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(Code);
}
