using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Licensing;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PxaAccountPermissions.LicensesRead)]
[Route("api/pxa/v1/account/licenses")]
public sealed class AccountLicensesController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly IPxaLicenseSigningService signingService;

    public AccountLicensesController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        IPxaLicenseSigningService signingService)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.signingService = signingService;
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
        using var envelope = JsonDocument.Parse(license.EnvelopeJson);
        var artifact = JsonSerializer.Serialize(new
        {
            envelope = envelope.RootElement,
            signature = license.Signature,
            keyId = license.KeyId,
            algorithm = license.Algorithm,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(artifact), "application/vnd.pxa.license+json",
            $"{license.LicenseNumber}.pxa-license.json");
    }

    [HttpGet("{licenseId:guid}/validate")]
    public async Task<ActionResult<AccountLicenseValidationResponse>> ValidateLicense(
        Guid licenseId, CancellationToken cancellationToken)
    {
        var license = await FindOwnLicenseAsync(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        var now = DateTimeOffset.UtcNow;
        var signatureValid = signingService.Verify(license.EnvelopeJson, license.Signature);
        var valid = signatureValid && license.Status == OfflineLicenseStatus.Active &&
                    license.ValidFrom <= now && license.ValidUntil > now;
        return Ok(new AccountLicenseValidationResponse(
            valid,
            license.Status.ToString(),
            license.ValidFrom,
            license.ValidUntil,
            valid ? "PXA_LICENSE_VALID" : GetValidationCode(license, signatureValid, now)));
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

    private static string GetValidationCode(OfflineLicense license, bool signatureValid, DateTimeOffset now) =>
        !signatureValid ? "PXA_LICENSE_SIGNATURE_INVALID" :
        license.Status == OfflineLicenseStatus.Revoked ? "PXA_LICENSE_REVOKED" :
        license.ValidFrom > now ? "PXA_LICENSE_NOT_YET_VALID" :
        license.ValidUntil <= now ? "PXA_LICENSE_EXPIRED" : "PXA_LICENSE_INACTIVE";

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
    bool Valid, string Status, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string Code);
