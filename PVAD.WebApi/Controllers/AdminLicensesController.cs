using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Services.Mail;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pvad/v1/admin/licenses")]
public sealed class AdminLicensesController : ControllerBase
{
    private readonly PvadDbContext dbContext;
    private readonly IPvadTenantContext tenantContext;
    private readonly IPvadLicenseSigningService signingService;
    private readonly PvadOfflineLicenseValidator licenseValidator;
    private readonly OrganizationNotificationService notifications;
    private readonly TimeProvider timeProvider;

    public AdminLicensesController(
        PvadDbContext dbContext,
        IPvadTenantContext tenantContext,
        IPvadLicenseSigningService signingService,
        PvadOfflineLicenseValidator licenseValidator,
        OrganizationNotificationService notifications,
        TimeProvider? timeProvider = null)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.signingService = signingService;
        this.licenseValidator = licenseValidator;
        this.notifications = notifications;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    [HttpGet]
    [Authorize(Policy = PvadPermissions.LicensesRead)]
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

    [HttpGet("{licenseId:guid}")]
    [Authorize(Policy = PvadPermissions.LicensesRead)]
    public async Task<ActionResult<AdminLicenseResponse>> GetLicense(
        Guid licenseId,
        CancellationToken cancellationToken)
    {
        var license = await FindAccessibleLicense(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        return Ok(await BuildQuery(dbContext.OfflineLicenses.AsNoTracking().Where(value => value.Id == licenseId))
            .SingleAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PvadPermissions.LicensesManage)]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("licenses.issue")]
    public async Task<ActionResult<AdminLicenseResponse>> IssueLicense(
        IssueOfflineLicenseRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        if (tenantContext.UserId is not { } actorUserId)
            return Unauthorized();
        var validFrom = PvadLicenseTimestamps.Normalize(request.ValidFrom);
        var validUntil = PvadLicenseTimestamps.Normalize(request.ValidUntil);
        if (validFrom >= validUntil || validUntil <= DateTimeOffset.UtcNow ||
            request.InstanceLimit is < 1 or > 1000)
            return ValidationProblem("Validity and instance limit are invalid.");
        if (!Version.TryParse(request.ProductVersion, out _) ||
            string.IsNullOrWhiteSpace(request.DeploymentId) ||
            request.DeploymentId.Length > 160 ||
            !Regex.IsMatch(request.DeploymentId, "^[A-Za-z0-9][A-Za-z0-9._:-]*$"))
            return ValidationProblem("Product version or deployment ID is invalid.");
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
            .Select(value => new PvadOfflineLicenseEntitlement(
                value.Capability, value.Enabled, value.Limit, value.Unit, value.ExpiresAt))
            .ToListAsync(cancellationToken);
        var license = new OfflineLicense
        {
            OrganizationId = subscription.OrganizationId,
            SubscriptionId = subscription.Id,
            LicenseNumber = $"PVAD-{DateTimeOffset.UtcNow:yyyy}-{Guid.NewGuid():N}"[..26].ToUpperInvariant(),
            EnvelopeJson = string.Empty,
            Signature = string.Empty,
            KeyId = signingService.KeyId,
            Algorithm = "ECDSA_P256_SHA256",
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            IssuedAt = PvadLicenseTimestamps.Normalize(DateTimeOffset.UtcNow),
            InstanceLimit = request.InstanceLimit,
            IssuedByUserId = actorUserId,
        };
        var envelope = new PvadOfflineLicenseEnvelope(
            2, license.Id, license.LicenseNumber, subscription.OrganizationId, organizationName,
            subscription.Edition.ToString(), subscription.AccountType.ToString(), subscription.DeploymentMode.ToString(),
            license.ValidFrom, license.ValidUntil, license.InstanceLimit,
            request.ProductVersion, request.DeploymentId, entitlements, license.IssuedAt);
        var artifact = signingService.Sign(envelope);
        license.EnvelopeJson = artifact.EnvelopeJson;
        license.Signature = artifact.Signature;
        license.KeyId = artifact.KeyId;
        license.Algorithm = artifact.Algorithm;
        dbContext.OfflineLicenses.Add(license);
        AddAuditEvent(license, actorUserId, "licenses.issue", null);
        await QueueLicenseNotification(
            license,
            $"license-issued:{license.Id}",
            $"Offline license {license.LicenseNumber} was issued through {license.ValidUntil:yyyy-MM-dd}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(DownloadLicense), new { licenseId = license.Id },
            await BuildQuery(dbContext.OfflineLicenses.Where(value => value.Id == license.Id)).SingleAsync(cancellationToken));
    }

    [HttpGet("{licenseId:guid}/download")]
    [Authorize(Policy = PvadPermissions.LicensesRead)]
    public async Task<IActionResult> DownloadLicense(Guid licenseId, CancellationToken cancellationToken)
    {
        var license = await FindAccessibleLicense(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        return PvadLicenseDownload.Create(license);
    }

    [HttpPost("{licenseId:guid}/replace")]
    [Authorize(Policy = PvadPermissions.LicensesManage)]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("licenses.replace")]
    public async Task<ActionResult<AdminLicenseResponse>> ReplaceLicense(
        Guid licenseId, ReplaceOfflineLicenseRequest request, CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator()) return Forbid();
        if (tenantContext.UserId is not { } actorUserId) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return ValidationProblem("A replacement reason is required (maximum 500 characters).");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var previous = await dbContext.OfflineLicenses.SingleOrDefaultAsync(
                value => value.Id == licenseId, cancellationToken);
            if (previous is null) return NotFound();
            var subscription = await dbContext.OrganizationSubscriptions.SingleAsync(
                value => value.Id == previous.SubscriptionId, cancellationToken);
            if (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.GracePeriod))
                return ConflictProblem("An active subscription is required to replace a license.");
            if (subscription.OrganizationId != previous.OrganizationId ||
                !await dbContext.Organizations.AnyAsync(value => value.Id == previous.OrganizationId &&
                    value.Status == OrganizationStatus.Active, cancellationToken))
                return ConflictProblem("The license organization is unavailable or inconsistent.");
            var validation = licenseValidator.Validate(previous, subscription, DateTimeOffset.UtcNow);
            if (!validation.Valid || validation.Envelope is not { } original)
                return ConflictProblem("Only an active, valid, verifiable license can be replaced.");
            try
            {
                JsonSerializer.Deserialize<PvadOfflineLicenseEnvelope>(previous.EnvelopeJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                    {
                        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
                    });
            }
            catch (JsonException)
            {
                return ConflictProblem("Unknown signed fields require a reviewed license migration.");
            }
            var version = original.ProductVersion;
            var deployment = original.DeploymentId;
            if (original.SchemaVersion == 1)
            {
                version = request.ProductVersion;
                deployment = request.DeploymentId;
            }
            else if ((request.ProductVersion is not null && request.ProductVersion != version) ||
                     (request.DeploymentId is not null && request.DeploymentId != deployment))
                return ValidationProblem("Replacement cannot change an existing version or deployment binding.");
            if (!Version.TryParse(version, out _) || string.IsNullOrWhiteSpace(deployment) ||
                deployment.Length > 160 || !Regex.IsMatch(deployment, "^[A-Za-z0-9][A-Za-z0-9._:-]*$"))
                return ValidationProblem("A valid product version and deployment ID are required.");

            var now = PvadLicenseTimestamps.Normalize(timeProvider.GetUtcNow());
            var replacement = new OfflineLicense
            {
                OrganizationId = previous.OrganizationId, SubscriptionId = previous.SubscriptionId,
                LicenseNumber = $"PVAD-{now:yyyy}-{Guid.NewGuid():N}"[..26].ToUpperInvariant(),
                EnvelopeJson = "", Signature = "", KeyId = signingService.KeyId,
                Algorithm = "ECDSA_P256_SHA256", ValidFrom = previous.ValidFrom,
                ValidUntil = previous.ValidUntil, InstanceLimit = previous.InstanceLimit,
                IssuedByUserId = actorUserId, IssuedAt = now,
            };
            var artifact = signingService.Sign(original with
            {
                SchemaVersion = 2, LicenseId = replacement.Id, LicenseNumber = replacement.LicenseNumber,
                IssuedAt = now, ProductVersion = version, DeploymentId = deployment,
            });
            replacement.EnvelopeJson = artifact.EnvelopeJson;
            replacement.Signature = artifact.Signature;
            replacement.KeyId = artifact.KeyId;
            replacement.Algorithm = artifact.Algorithm;
            previous.Status = OfflineLicenseStatus.Replaced;
            dbContext.OfflineLicenses.Add(replacement);
            dbContext.AuditEvents.Add(new AuditEvent
            {
                OrganizationId = previous.OrganizationId, ActorUserId = actorUserId,
                Action = "licenses.replace", TargetType = "offline-license",
                TargetId = previous.Id.ToString(), Outcome = "succeeded",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    PreviousLicenseId = previous.Id, ReplacementLicenseId = replacement.Id,
                    Reason = request.Reason.Trim(),
                }),
            });
            await QueueLicenseNotification(replacement, $"license-replaced:{previous.Id}",
                $"Offline license {previous.LicenseNumber} was replaced by {replacement.LicenseNumber}.", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CreatedAtAction(nameof(DownloadLicense), new { licenseId = replacement.Id },
                await BuildQuery(dbContext.OfflineLicenses.AsNoTracking().Where(value => value.Id == replacement.Id))
                    .SingleAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException ||
            exception.GetBaseException() is Npgsql.PostgresException { SqlState: "40001" or "40P01" } ||
            exception.GetBaseException() is Npgsql.PostgresException
            {
                SqlState: "23505", SchemaName: "administration", TableName: "mail_outbox",
                ConstraintName: "IX_mail_outbox_IdempotencyKey",
            })
        {
            return ConflictProblem("The license changed concurrently. Reload before retrying.");
        }
    }

    [HttpGet("{licenseId:guid}/validate")]
    [Authorize(Policy = PvadPermissions.LicensesRead)]
    public async Task<ActionResult<AdminLicenseValidationResponse>> ValidateLicense(
        Guid licenseId,
        [FromQuery] string? productVersion,
        [FromQuery] string? deploymentId,
        [FromQuery] int activeInstances = 1,
        CancellationToken cancellationToken = default)
    {
        var license = await FindAccessibleLicense(licenseId, cancellationToken);
        if (license is null)
            return NotFound();
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleAsync(value => value.Id == license.SubscriptionId, cancellationToken);
        var validation = licenseValidator.Validate(
            license, subscription, DateTimeOffset.UtcNow, productVersion, deploymentId, activeInstances);
        return Ok(new AdminLicenseValidationResponse(
            validation.Valid,
            validation.SignatureValid,
            license.Status.ToString(),
            license.ValidFrom,
            license.ValidUntil,
            validation.Code));
    }

    [HttpPost("{licenseId:guid}/revoke")]
    [Authorize(Policy = PvadPermissions.LicensesManage)]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("licenses.revoke")]
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
        await QueueLicenseNotification(
            license,
            $"license-revoked:{license.Id}:{license.RevokedAt.Value.UtcDateTime.Ticks}",
            $"Offline license {license.LicenseNumber} was revoked.",
            cancellationToken);
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

    private bool IsSystemAdministrator() => User.IsInRole(PvadRoles.SystemAdministrator);
    private Task<int> QueueLicenseNotification(
        OfflineLicense license,
        string eventKey,
        string summary,
        CancellationToken cancellationToken) =>
        notifications.QueueAdministratorsAsync(
            license.OrganizationId,
            "license.changed",
            eventKey,
            new Dictionary<string, string> { ["summary"] = summary },
            cancellationToken);

    private void AddAuditEvent(OfflineLicense license, Guid actorUserId, string action, string? reason) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = license.OrganizationId, ActorUserId = actorUserId, Action = action,
            TargetType = "offline-license", TargetId = license.Id.ToString(), Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { license.LicenseNumber, Reason = reason }),
        });
    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: 409, title: "License operation rejected", detail: detail);
    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new ProblemDetails { Status = 400, Title = "Invalid license request", Detail = detail });
}

public sealed record AdminLicenseResponse(Guid Id, string LicenseNumber, Guid OrganizationId, string OrganizationName,
    string Edition, string DeploymentMode, string Status, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil,
    int InstanceLimit, string KeyId, string Algorithm, DateTimeOffset IssuedAt, DateTimeOffset? RevokedAt,
    string? RevocationReason);
public sealed record IssueOfflineLicenseRequest(Guid SubscriptionId, DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil, int InstanceLimit, string ProductVersion, string DeploymentId);
public sealed record RevokeOfflineLicenseRequest(string Reason);
public sealed record ReplaceOfflineLicenseRequest(string Reason, string? ProductVersion = null, string? DeploymentId = null);
public sealed record AdminLicenseValidationResponse(bool Valid, bool SignatureValid, string Status,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, string Code)
{
    public string? CanonicalCode => PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(Code);
}
