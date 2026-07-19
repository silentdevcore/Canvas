using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

/// <summary>
/// Retention-safe account/organization closure: requests are recorded and
/// cancellable up until <see cref="AccountClosureRequest.ScheduledPurgeAt"/>.
/// Deliberately out of scope here: an automated purge executor that actually
/// erases data once the window elapses - this phase only covers the
/// request/cancel workflow, per the checklist's "retention-safe workflow"
/// wording, not a scheduled-deletion job.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pxa/v1/account/closure")]
public sealed class AccountClosureController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly PxaAccountClosureOptions options;

    public AccountClosureController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        IOptions<PxaAccountClosureOptions> options)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.options = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountClosureResponse>>> GetRequests(
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var userId = tenantContext.UserId;
        if (organizationId is null || userId is null)
            return MissingOrganization();

        var requests = await dbContext.AccountClosureRequests.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId &&
                            (value.TargetType == AccountClosureTargetType.Organization ||
                             value.TargetId == userId))
            .OrderByDescending(value => value.RequestedAt)
            .ToListAsync(cancellationToken);
        return Ok(requests.Select(ToResponse).ToArray());
    }

    [HttpPost("account")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.closure.account-requested")]
    public async Task<ActionResult<AccountClosureResponse>> RequestAccountClosure(
        RequestAccountClosureRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var userId = tenantContext.UserId;
        if (organizationId is null || userId is null)
            return MissingOrganization();

        var hasPending = await dbContext.AccountClosureRequests.AnyAsync(value =>
            value.TargetType == AccountClosureTargetType.User &&
            value.TargetId == userId &&
            value.Status == AccountClosureStatus.Pending,
            cancellationToken);
        if (hasPending)
            return ClosureConflict("A closure request for your account is already pending.");

        var now = DateTimeOffset.UtcNow;
        var closure = new AccountClosureRequest
        {
            TargetType = AccountClosureTargetType.User,
            TargetId = userId.Value,
            OrganizationId = organizationId,
            RequestedByUserId = userId.Value,
            Reason = request.Reason?.Trim(),
            ScheduledPurgeAt = now.Add(options.RetentionPeriod),
        };
        dbContext.AccountClosureRequests.Add(closure);

        var sessions = await dbContext.UserSessions
            .Where(value => value.UserId == userId && value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedByUserId = userId;
            session.RevocationReason = "account-closure-requested";
        }

        dbContext.AuditEvents.Add(NewAuditEvent(
            organizationId.Value, userId.Value, "account.closure.account-requested", closure.Id, "user",
            new { RevokedSessions = sessions.Count }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(closure));
    }

    [HttpPost("organization")]
    [Authorize(Policy = PxaAccountPermissions.ClosureRequest)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.closure.organization-requested")]
    public async Task<ActionResult<AccountClosureResponse>> RequestOrganizationClosure(
        RequestAccountClosureRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var userId = tenantContext.UserId;
        if (organizationId is null || userId is null)
            return MissingOrganization();

        var hasPending = await dbContext.AccountClosureRequests.AnyAsync(value =>
            value.TargetType == AccountClosureTargetType.Organization &&
            value.TargetId == organizationId &&
            value.Status == AccountClosureStatus.Pending,
            cancellationToken);
        if (hasPending)
            return ClosureConflict("A closure request for your organization is already pending.");

        var organization = await dbContext.Organizations.SingleOrDefaultAsync(
            value => value.Id == organizationId, cancellationToken);
        if (organization is null)
            return NotFound();

        var now = DateTimeOffset.UtcNow;
        var closure = new AccountClosureRequest
        {
            TargetType = AccountClosureTargetType.Organization,
            TargetId = organizationId.Value,
            OrganizationId = organizationId,
            RequestedByUserId = userId.Value,
            Reason = request.Reason?.Trim(),
            ScheduledPurgeAt = now.Add(options.RetentionPeriod),
        };
        dbContext.AccountClosureRequests.Add(closure);
        organization.Status = OrganizationStatus.Closed;
        organization.UpdatedAt = now;

        dbContext.AuditEvents.Add(NewAuditEvent(
            organizationId.Value, userId.Value, "account.closure.organization-requested", closure.Id, "organization", new { }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(closure));
    }

    [HttpPost("{requestId:guid}/cancel")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.closure.cancelled")]
    public async Task<ActionResult<AccountClosureResponse>> CancelClosure(
        Guid requestId, CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var userId = tenantContext.UserId;
        if (organizationId is null || userId is null)
            return MissingOrganization();

        var closure = await dbContext.AccountClosureRequests.SingleOrDefaultAsync(
            value => value.Id == requestId && value.OrganizationId == organizationId, cancellationToken);
        if (closure is null)
            return NotFound();
        if (closure.TargetType == AccountClosureTargetType.User && closure.TargetId != userId)
            return NotFound();
        if (closure.TargetType == AccountClosureTargetType.Organization &&
            !User.HasClaim(PxaClaimTypes.Permission, PxaAccountPermissions.ClosureRequest))
            return Forbid();
        var now = DateTimeOffset.UtcNow;
        if (closure.Status != AccountClosureStatus.Pending || closure.ScheduledPurgeAt <= now)
            return ClosureConflict("Only a pending closure request within its retention window can be cancelled.");

        closure.Status = AccountClosureStatus.Cancelled;
        closure.ResolvedAt = now;
        if (closure.TargetType == AccountClosureTargetType.Organization)
        {
            var organization = await dbContext.Organizations.SingleAsync(
                value => value.Id == closure.TargetId, cancellationToken);
            organization.Status = OrganizationStatus.Active;
            organization.UpdatedAt = now;
        }

        dbContext.AuditEvents.Add(NewAuditEvent(
            organizationId.Value, userId.Value, "account.closure.cancelled", closure.Id,
            closure.TargetType == AccountClosureTargetType.Organization ? "organization" : "user", new { }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(closure));
    }

    private static AuditEvent NewAuditEvent(
        Guid organizationId, Guid actorUserId, string action, Guid targetId, string targetType, object details) => new()
    {
        OrganizationId = organizationId,
        ActorUserId = actorUserId,
        Action = action,
        TargetType = targetType,
        TargetId = targetId.ToString(),
        Outcome = "succeeded",
        DetailsJson = JsonSerializer.Serialize(details),
    };

    private static AccountClosureResponse ToResponse(AccountClosureRequest closure) => new(
        closure.Id, closure.TargetType.ToString(), closure.Status.ToString(), closure.Reason,
        closure.RequestedAt, closure.ScheduledPurgeAt, closure.ResolvedAt);

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");

    private ObjectResult ClosureConflict(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Closure request rejected",
        detail: detail);
}

public sealed record RequestAccountClosureRequest(string? Reason);

public sealed record AccountClosureResponse(
    Guid Id, string TargetType, string Status, string? Reason,
    DateTimeOffset RequestedAt, DateTimeOffset ScheduledPurgeAt, DateTimeOffset? ResolvedAt);
