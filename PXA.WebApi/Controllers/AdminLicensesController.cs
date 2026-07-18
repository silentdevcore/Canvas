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
[Authorize]
[Route("api/pxa/v1/admin/licenses")]
public sealed class AdminLicensesController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly IPxaLicenseSigningService signingService;

    public AdminLicensesController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        IPxaLicenseSigningService signingService)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.signingService = signingService;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.LicensesRead)]
    public async Task<ActionResult<IReadOnlyList<AdminLicenseResponse>>> GetLicenses(CancellationToken cancellationToken)
    {
        var query = dbContext.OfflineLicenses.AsNoTracking();
        if (!IsSystemAdministrator())
        {
            if (tenantContext.OrganizationId is not { } organizationId)
                return Problem(statusCode: 403, title: "Organization context required");
            query = query.Where(value => value.OrganizationId == organizationId);
        }
        return Ok(await BuildQuery(query.OrderByDescending(value => value.IssuedAt)).ToListAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PxaPermissions.LicensesManage)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminLicenseResponse>> IssueLicense(
        IssueOfflineLicenseRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        if (tenantContext.UserId is not { } actorUserId)
            return Unauthorized();
        if (request.ValidFrom >= request.ValidUntil || request.ValidUntil <= DateTimeOffset.UtcNow ||
            request.InstanceLimit is < 1 or > 1000)
            return ValidationProblem("Validity and instance limit are invalid.");
        var subscription = await dbContext.OrganizationSubscriptions.SingleOrDefaultAsync(
            value => value.Id == request.SubscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound();
        if (subscription.Edition != SubscriptionEdition.Enterprise ||
            subscription.DeploymentMode is not (SubscriptionDeploymentMode.OnPremise or SubscriptionDeploymentMode.Hybrid) ||
            subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.GracePeriod))
            return ConflictProblem("Offline licenses require an active Enterprise On-Premise or Hybrid subscription.");
        var contractEnd = new[]
            {
                subscription.CurrentPeriodEndsAt,
                subscription.CancellationEffectiveAt,
                subscription.GracePeriodEndsAt,
            }
            .Where(value => value is not null)
            .Min();
        if (contractEnd is { } maximumValidUntil && request.ValidUntil > maximumValidUntil)
            return ConflictProblem("The offline license cannot outlive the current subscription entitlement period.");
        var organizationName = await dbContext.Organizations.Where(value => value.Id == subscription.OrganizationId)
            .Select(value => value.Name).SingleAsync(cancellationToken);
        var entitlements = await dbContext.SubscriptionEntitlements.AsNoTracking()
            .Where(value => value.SubscriptionId == subscription.Id)
            .OrderBy(value => value.Capability)
            .Select(value => new PxaOfflineLicenseEntitlement(
                value.Capability, value.Enabled, value.Limit, value.Unit, value.ExpiresAt))
            .ToListAsync(cancellationToken);
        var license = new OfflineLicense
        {
            OrganizationId = subscription.OrganizationId,
            SubscriptionId = subscription.Id,
            LicenseNumber = $"PXA-{DateTimeOffset.UtcNow:yyyy}-{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
            EnvelopeJson = string.Empty,
            Signature = string.Empty,
            KeyId = signingService.KeyId,
            Algorithm = "ECDSA_P256_SHA256",
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            InstanceLimit = request.InstanceLimit,
            IssuedByUserId = actorUserId,
        };
        var envelope = new PxaOfflineLicenseEnvelope(
            1, license.Id, license.LicenseNumber, subscription.OrganizationId, organizationName,
            subscription.Edition.ToString(), subscription.AccountType.ToString(), subscription.DeploymentMode.ToString(),
            license.ValidFrom, license.ValidUntil, license.InstanceLimit, entitlements, license.IssuedAt);
        var artifact = signingService.Sign(envelope);
        license.EnvelopeJson = artifact.EnvelopeJson;
        license.Signature = artifact.Signature;
        license.KeyId = artifact.KeyId;
        license.Algorithm = artifact.Algorithm;
        dbContext.OfflineLicenses.Add(license);
        AddAuditEvent(license, actorUserId, "licenses.issue", null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(DownloadLicense), new { licenseId = license.Id },
            await BuildQuery(dbContext.OfflineLicenses.Where(value => value.Id == license.Id)).SingleAsync(cancellationToken));
    }

    [HttpGet("{licenseId:guid}/download")]
    [Authorize(Policy = PxaPermissions.LicensesRead)]
    public async Task<IActionResult> DownloadLicense(Guid licenseId, CancellationToken cancellationToken)
    {
        var license = await FindAccessibleLicense(licenseId, cancellationToken);
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
    [Authorize(Policy = PxaPermissions.LicensesRead)]
    public async Task<ActionResult<AdminLicenseValidationResponse>> ValidateLicense(
        Guid licenseId,
        CancellationToken cancellationToken)
    {
        var license = await FindAccessibleLicense(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        var now = DateTimeOffset.UtcNow;
        var signatureValid = signingService.Verify(license.EnvelopeJson, license.Signature);
        var valid = signatureValid && license.Status == OfflineLicenseStatus.Active &&
                    license.ValidFrom <= now && license.ValidUntil > now;
        return Ok(new AdminLicenseValidationResponse(
            valid,
            signatureValid,
            license.Status.ToString(),
            license.ValidFrom,
            license.ValidUntil,
            valid ? "PXA_LICENSE_VALID" : GetValidationCode(license, signatureValid, now)));
    }

    [HttpPost("{licenseId:guid}/revoke")]
    [Authorize(Policy = PxaPermissions.LicensesManage)]
    [PxaValidateAntiforgery]
    public async Task<IActionResult> RevokeLicense(
        Guid licenseId,
        RevokeOfflineLicenseRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        var license = await dbContext.OfflineLicenses.SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken);
        if (license is null || tenantContext.UserId is not { } actorUserId)
            return NotFound();
        if (license.Status != OfflineLicenseStatus.Active)
            return ConflictProblem("Only an active license can be revoked.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return ValidationProblem("A revocation reason is required.");
        license.Status = OfflineLicenseStatus.Revoked;
        license.RevokedAt = DateTimeOffset.UtcNow;
        license.RevocationReason = request.Reason.Trim();
        AddAuditEvent(license, actorUserId, "licenses.revoke", license.RevocationReason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<AdminLicenseResponse> BuildQuery(IQueryable<OfflineLicense> query) =>
        from license in query
        join organization in dbContext.Organizations.AsNoTracking() on license.OrganizationId equals organization.Id
        join subscription in dbContext.OrganizationSubscriptions.AsNoTracking() on license.SubscriptionId equals subscription.Id
        select new AdminLicenseResponse(
            license.Id, license.LicenseNumber, organization.Id, organization.Name, subscription.Edition.ToString(),
            subscription.DeploymentMode.ToString(), license.Status.ToString(), license.ValidFrom, license.ValidUntil,
            license.InstanceLimit, license.KeyId, license.Algorithm, license.IssuedAt, license.RevokedAt, license.RevocationReason);

    private async Task<OfflineLicense?> FindAccessibleLicense(Guid licenseId, CancellationToken cancellationToken)
    {
        var query = dbContext.OfflineLicenses.AsNoTracking().Where(value => value.Id == licenseId);
        if (!IsSystemAdministrator())
        {
            if (tenantContext.OrganizationId is not { } organizationId)
                return null;
            query = query.Where(value => value.OrganizationId == organizationId);
        }
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private bool IsSystemAdministrator() => User.IsInRole(PxaRoles.SystemAdministrator);
    private void AddAuditEvent(OfflineLicense license, Guid actorUserId, string action, string? reason) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = license.OrganizationId, ActorUserId = actorUserId, Action = action,
            TargetType = "offline-license", TargetId = license.Id.ToString(), Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { license.LicenseNumber, Reason = reason }),
        });
    private static string GetValidationCode(OfflineLicense license, bool signatureValid, DateTimeOffset now) =>
        !signatureValid ? "PXA_LICENSE_SIGNATURE_INVALID" :
        license.Status == OfflineLicenseStatus.Revoked ? "PXA_LICENSE_REVOKED" :
        license.ValidFrom > now ? "PXA_LICENSE_NOT_YET_VALID" :
        license.ValidUntil <= now ? "PXA_LICENSE_EXPIRED" : "PXA_LICENSE_INACTIVE";
    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: 409, title: "License operation rejected", detail: detail);
    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new ProblemDetails { Status = 400, Title = "Invalid license request", Detail = detail });
}

public sealed record AdminLicenseResponse(Guid Id, string LicenseNumber, Guid OrganizationId, string OrganizationName,
    string Edition, string DeploymentMode, string Status, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil,
    int InstanceLimit, string KeyId, string Algorithm, DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt,
    string? RevocationReason);
public sealed record IssueOfflineLicenseRequest(Guid SubscriptionId, DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil, int InstanceLimit);
public sealed record RevokeOfflineLicenseRequest(string Reason);
public sealed record AdminLicenseValidationResponse(bool Valid, bool SignatureValid, string Status,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string Code);
