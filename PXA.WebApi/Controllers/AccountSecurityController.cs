using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PxaAccountPermissions.SessionsManage)]
[Route("api/pxa/v1/account/security")]
public sealed class AccountSecurityController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AccountSecurityController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AccountSessionResponse>>> GetSessions(
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        PxaSessionService.TryGetSessionId(User, out var currentSessionId);
        var now = DateTimeOffset.UtcNow;
        var sessions = await dbContext.UserSessions.AsNoTracking()
            .Where(session => session.UserId == userId)
            .OrderByDescending(session => session.LastSeenAt)
            .Select(session => new AccountSessionResponse(
                session.Id,
                session.UserAgent,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.RevokedAt,
                session.Id == currentSessionId,
                session.RevokedAt == null && session.ExpiresAt > now))
            .ToListAsync(cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("sessions/{sessionId:guid}/revoke")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.sessions.revoked")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            value => value.Id == sessionId && value.UserId == userId, cancellationToken);
        if (session is null)
            return NotFound();
        if (session.RevokedAt is null)
        {
            Revoke(session, userId.Value, "self-service");
            dbContext.AuditEvents.Add(NewAuditEvent(userId.Value, "account.sessions.revoked", new { SessionId = session.Id }));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("sessions/revoke-all")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.sessions.revoked-all")]
    public async Task<ActionResult<AccountRevokeSessionsResponse>> RevokeAllSessions(
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId;
        if (userId is null)
            return Unauthorized();

        PxaSessionService.TryGetSessionId(User, out var currentSessionId);
        var sessions = await dbContext.UserSessions
            .Where(value => value.UserId == userId && value.RevokedAt == null && value.Id != currentSessionId)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            Revoke(session, userId.Value, "self-service-bulk");

        dbContext.AuditEvents.Add(NewAuditEvent(userId.Value, "account.sessions.revoked-all", new { Count = sessions.Count }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new AccountRevokeSessionsResponse(sessions.Count));
    }

    private static void Revoke(UserSession session, Guid actorUserId, string reason)
    {
        session.RevokedAt = DateTimeOffset.UtcNow;
        session.RevokedByUserId = actorUserId;
        session.RevocationReason = reason;
    }

    private static AuditEvent NewAuditEvent(Guid userId, string action, object details) => new()
    {
        OrganizationId = null,
        ActorUserId = userId,
        Action = action,
        TargetType = "session",
        TargetId = userId.ToString(),
        Outcome = "succeeded",
        DetailsJson = System.Text.Json.JsonSerializer.Serialize(details),
    };
}

public sealed record AccountSessionResponse(
    Guid Id, string UserAgent, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt, bool IsCurrent, bool IsActive);

public sealed record AccountRevokeSessionsResponse(int RevokedCount);
