using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Retention;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Roles = PxaRoles.SystemAdministrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pxa/v1/admin/system/retention")]
public sealed class AdminRetentionController(
    PxaDbContext dbContext,
    PxaRetentionPolicyCatalog catalog,
    PxaRetentionGovernanceService governance,
    IPxaTenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PxaRetentionStatusResponse>> GetStatus(
        CancellationToken cancellationToken) =>
        Ok(await governance.GetStatusAsync(cancellationToken));

    [HttpPost("dry-run")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("retention.dry-run")]
    public async Task<ActionResult<PxaRetentionDryRunResponse>> DryRun(
        CancellationToken cancellationToken) =>
        Ok(await governance.DryRunAsync(cancellationToken));

    [HttpGet("legal-holds")]
    public async Task<ActionResult<IReadOnlyList<RetentionLegalHoldResponse>>> GetLegalHolds(
        [FromQuery] bool includeReleased,
        CancellationToken cancellationToken)
    {
        var holds = await dbContext.RetentionLegalHolds.AsNoTracking()
            .Where(value => includeReleased || value.ReleasedAt == null)
            .OrderByDescending(value => value.CreatedAt)
            .Take(500)
            .Select(value => new RetentionLegalHoldResponse(
                value.Id,
                value.Category,
                value.OrganizationId,
                value.Reason,
                value.CreatedAt,
                value.ReleasedAt,
                value.ReleaseReason))
            .ToArrayAsync(cancellationToken);
        return Ok(holds);
    }

    [HttpPost("legal-holds")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("retention.legal-hold.created")]
    public async Task<ActionResult<RetentionLegalHoldResponse>> CreateLegalHold(
        CreateRetentionLegalHoldRequest request,
        CancellationToken cancellationToken)
    {
        if (!catalog.ContainsCategory(request.Category))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Category)] = ["The retention category is not registered."],
            }));
        if (tenantContext.UserId is not Guid actorUserId)
            return Unauthorized();
        if (request.OrganizationId is Guid organizationId &&
            !await dbContext.Organizations.AnyAsync(value => value.Id == organizationId, cancellationToken))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.OrganizationId)] = ["The organization does not exist."],
            }));
        }

        var duplicate = await dbContext.RetentionLegalHolds.AnyAsync(value =>
            value.Category == request.Category &&
            value.OrganizationId == request.OrganizationId &&
            value.ReleasedAt == null,
            cancellationToken);
        if (duplicate)
            return Conflict("An active legal hold already covers this category and organization scope.");

        var hold = new RetentionLegalHold
        {
            Category = request.Category,
            OrganizationId = request.OrganizationId,
            Reason = request.Reason.Trim(),
            CreatedByUserId = actorUserId,
        };
        dbContext.RetentionLegalHolds.Add(hold);
        dbContext.AuditEvents.Add(Audit(
            actorUserId,
            "retention.legal-hold.created",
            hold.Id,
            hold.OrganizationId,
            new { hold.Category, Scope = hold.OrganizationId is null ? "global" : "organization" }));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.RetentionLegalHolds.AnyAsync(value =>
                    value.Category == request.Category &&
                    value.OrganizationId == request.OrganizationId &&
                    value.ReleasedAt == null,
                    cancellationToken))
            {
                return Conflict("An active legal hold already covers this category and organization scope.");
            }

            throw;
        }
        return CreatedAtAction(nameof(GetLegalHolds), ToResponse(hold));
    }

    [HttpPost("legal-holds/{holdId:guid}/release")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("retention.legal-hold.released")]
    public async Task<ActionResult<RetentionLegalHoldResponse>> ReleaseLegalHold(
        Guid holdId,
        ReleaseRetentionLegalHoldRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is not Guid actorUserId)
            return Unauthorized();
        var hold = await dbContext.RetentionLegalHolds.SingleOrDefaultAsync(
            value => value.Id == holdId,
            cancellationToken);
        if (hold is null) return NotFound();
        if (!hold.IsActive) return Conflict("The legal hold has already been released.");

        hold.ReleasedAt = DateTimeOffset.UtcNow;
        hold.ReleasedByUserId = actorUserId;
        hold.ReleaseReason = request.Reason.Trim();
        dbContext.AuditEvents.Add(Audit(
            actorUserId,
            "retention.legal-hold.released",
            hold.Id,
            hold.OrganizationId,
            new { hold.Category, Scope = hold.OrganizationId is null ? "global" : "organization" }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(hold));
    }

    private static RetentionLegalHoldResponse ToResponse(RetentionLegalHold value) => new(
        value.Id,
        value.Category,
        value.OrganizationId,
        value.Reason,
        value.CreatedAt,
        value.ReleasedAt,
        value.ReleaseReason);

    private static AuditEvent Audit(
        Guid actorUserId,
        string action,
        Guid holdId,
        Guid? organizationId,
        object metadata) => new()
        {
            ActorUserId = actorUserId,
            OrganizationId = organizationId,
            Action = action,
            TargetType = "retention_legal_hold",
            TargetId = holdId.ToString(),
            Outcome = "succeeded",
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(metadata),
        };
}

public sealed record CreateRetentionLegalHoldRequest(
    [Required, MaxLength(100)] string Category,
    Guid? OrganizationId,
    [Required, MinLength(10), MaxLength(2000)] string Reason);

public sealed record ReleaseRetentionLegalHoldRequest(
    [Required, MinLength(10), MaxLength(2000)] string Reason);

public sealed record RetentionLegalHoldResponse(
    Guid Id,
    string Category,
    Guid? OrganizationId,
    string Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    string? ReleaseReason);
