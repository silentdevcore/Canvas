using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private static readonly string[] OrganizationRoles =
    [
        PxaRoles.OrganizationAdministrator,
        PxaRoles.Manager,
        PxaRoles.Editor,
        PxaRoles.Viewer,
    ];

    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AdminUsersController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<AdminUserPage>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string sort = "name",
        [FromQuery] string direction = "asc",
        CancellationToken cancellationToken = default)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.Status != OrganizationMembershipStatus.Removed
            select new { membership, user };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(value =>
                EF.Functions.ILike(value.user.DisplayName, pattern) ||
                EF.Functions.ILike(value.user.Email ?? string.Empty, pattern) ||
                EF.Functions.ILike(value.user.UserName ?? string.Empty, pattern));
        }

        query = status?.ToLowerInvariant() switch
        {
            "active" => query.Where(value =>
                value.user.IsActive && value.membership.Status == OrganizationMembershipStatus.Active),
            "disabled" => query.Where(value => !value.user.IsActive),
            "suspended" => query.Where(value =>
                value.membership.Status == OrganizationMembershipStatus.Suspended),
            "invited" => query.Where(value =>
                value.membership.Status == OrganizationMembershipStatus.Invited),
            _ => query,
        };

        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort.ToLowerInvariant(), descending) switch
        {
            ("email", false) => query.OrderBy(value => value.user.Email),
            ("email", true) => query.OrderByDescending(value => value.user.Email),
            ("lastlogin", false) => query.OrderBy(value => value.user.LastLoginAt),
            ("lastlogin", true) => query.OrderByDescending(value => value.user.LastLoginAt),
            ("status", false) => query.OrderBy(value => value.membership.Status),
            ("status", true) => query.OrderByDescending(value => value.membership.Status),
            (_, false) => query.OrderBy(value => value.user.DisplayName),
            _ => query.OrderByDescending(value => value.user.DisplayName),
        };

        var total = await query.CountAsync(cancellationToken);
        var records = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(value => new UserRecord(
                value.user.Id,
                value.membership.Id,
                value.user.DisplayName,
                value.user.Email ?? string.Empty,
                value.user.UserName ?? string.Empty,
                value.user.IsActive,
                value.membership.Status,
                value.user.LastLoginAt,
                value.user.CreatedAt))
            .ToListAsync(cancellationToken);

        var roles = await GetRolesAsync(records.Select(value => value.Id).ToArray(), organizationId.Value, cancellationToken);
        var items = records.Select(value => ToResponse(value, roles.GetValueOrDefault(value.Id, []))).ToArray();
        return Ok(new AdminUserPage(items, page, pageSize, total));
    }

    [HttpGet("{userId:guid}")]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<AdminUserResponse>> GetUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();

        var record = await (
                from membership in dbContext.OrganizationMemberships.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId &&
                      membership.UserId == userId &&
                      membership.Status != OrganizationMembershipStatus.Removed
                select new UserRecord(
                    user.Id,
                    membership.Id,
                    user.DisplayName,
                    user.Email ?? string.Empty,
                    user.UserName ?? string.Empty,
                    user.IsActive,
                    membership.Status,
                    user.LastLoginAt,
                    user.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
        if (record is null)
            return NotFound();

        var roles = await GetRolesAsync([userId], organizationId.Value, cancellationToken);
        return Ok(ToResponse(record, roles.GetValueOrDefault(userId, [])));
    }

    [HttpGet("{userId:guid}/sessions")]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<IReadOnlyList<AdminUserSessionResponse>>> GetSessions(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();
        if (!await IsOrganizationUserAsync(userId, organizationId.Value, cancellationToken))
            return NotFound();

        PxaSessionService.TryGetSessionId(User, out var currentSessionId);
        var now = DateTimeOffset.UtcNow;
        var sessions = await dbContext.UserSessions.AsNoTracking()
            .Where(session => session.UserId == userId && session.OrganizationId == organizationId)
            .OrderByDescending(session => session.LastSeenAt)
            .Select(session => new AdminUserSessionResponse(
                session.Id,
                session.UserAgent,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.RevokedAt,
                session.RevocationReason,
                session.Id == currentSessionId,
                session.RevokedAt == null && session.ExpiresAt > now))
            .ToListAsync(cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("{userId:guid}/sessions/{sessionId:guid}/revoke")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    public async Task<IActionResult> RevokeSession(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        if (!await IsOrganizationUserAsync(userId, organizationId.Value, cancellationToken))
            return NotFound();

        var session = await dbContext.UserSessions.SingleOrDefaultAsync(value =>
            value.Id == sessionId &&
            value.UserId == userId &&
            value.OrganizationId == organizationId,
            cancellationToken);
        if (session is null)
            return NotFound();
        if (session.RevokedAt is null)
        {
            Revoke(session, actorUserId.Value, "administrator");
            AddAuditEvent(
                organizationId.Value,
                actorUserId.Value,
                "sessions.revoke",
                userId,
                new { SessionId = session.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("{userId:guid}/sessions/revoke-all")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<RevokeSessionsResponse>> RevokeAllSessions(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        if (!await IsOrganizationUserAsync(userId, organizationId.Value, cancellationToken))
            return NotFound();

        PxaSessionService.TryGetSessionId(User, out var currentSessionId);
        var sessions = await dbContext.UserSessions
            .Where(value =>
                value.UserId == userId &&
                value.OrganizationId == organizationId &&
                value.RevokedAt == null &&
                (userId != actorUserId || value.Id != currentSessionId))
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            Revoke(session, actorUserId.Value, "administrator-bulk");

        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            "sessions.revoke-all",
            userId,
            new { Count = sessions.Count, CurrentSessionPreserved = userId == actorUserId });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new RevokeSessionsResponse(sessions.Count));
    }

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminUserResponse>> UpdateStatus(
        Guid userId,
        UpdateAdminUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        if (!request.IsActive && userId == actorUserId)
            return ConflictProblem("Administrators cannot disable their own active session.");

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId &&
            value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();

        if (!request.IsActive && await IsLastOrganizationAdministratorAsync(membership, cancellationToken))
            return ConflictProblem("The last active Organization Administrator cannot be disabled.");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.SecurityStamp = Guid.NewGuid().ToString();
        await RevokeActiveSessionsAsync(userId, actorUserId.Value, "account-status-change", cancellationToken);
        membership.Status = request.IsActive
            ? OrganizationMembershipStatus.Active
            : OrganizationMembershipStatus.Suspended;
        membership.UpdatedAt = user.UpdatedAt;

        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            request.IsActive ? "users.enable" : "users.disable",
            userId,
            new { request.IsActive });
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetRolesAsync([userId], organizationId.Value, cancellationToken);
        return Ok(ToResponse(new UserRecord(
            user.Id,
            membership.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.IsActive,
            membership.Status,
            user.LastLoginAt,
            user.CreatedAt), roles.GetValueOrDefault(userId, [])));
    }

    [HttpPut("{userId:guid}/roles")]
    [Authorize(Policy = PxaPermissions.RolesAssign)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminUserResponse>> UpdateRoles(
        Guid userId,
        UpdateAdminUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var requestedRoles = request.Roles
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedRoles.Any(role => !OrganizationRoles.Contains(role, StringComparer.Ordinal)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid organization role",
                Detail = "Only organization-scoped roles can be assigned through this endpoint.",
            });
        }

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId &&
            value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();

        var removesOrganizationAdministrator =
            !requestedRoles.Contains(PxaRoles.OrganizationAdministrator, StringComparer.Ordinal) &&
            await IsLastOrganizationAdministratorAsync(membership, cancellationToken);
        if (removesOrganizationAdministrator)
            return ConflictProblem("The last active Organization Administrator role cannot be removed.");

        var roleEntities = await dbContext.Roles
            .Where(role => requestedRoles.Contains(role.Name!))
            .ToListAsync(cancellationToken);
        if (roleEntities.Count != requestedRoles.Length)
            return ConflictProblem("Built-in organization roles have not been initialized.");

        var existing = await dbContext.OrganizationMembershipRoles
            .Where(value => value.OrganizationMembershipId == membership.Id)
            .ToListAsync(cancellationToken);
        dbContext.OrganizationMembershipRoles.RemoveRange(existing);
        dbContext.OrganizationMembershipRoles.AddRange(roleEntities.Select(role =>
            new OrganizationMembershipRole
            {
                OrganizationMembershipId = membership.Id,
                RoleId = role.Id,
                AssignedByUserId = actorUserId,
            }));

        user.SecurityStamp = Guid.NewGuid().ToString();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeActiveSessionsAsync(userId, actorUserId.Value, "role-change", cancellationToken);

        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            "roles.assign",
            userId,
            new { Roles = requestedRoles });
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetRolesAsync([userId], organizationId.Value, cancellationToken);
        return Ok(ToResponse(new UserRecord(
            user.Id,
            membership.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.IsActive,
            membership.Status,
            user.LastLoginAt,
            user.CreatedAt), roles.GetValueOrDefault(userId, [])));
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetRolesAsync(
        Guid[] userIds,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var globalRoles = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, Role = role.Name! })
            .ToListAsync(cancellationToken);

        var organizationRoles = await (
                from membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                join membership in dbContext.OrganizationMemberships.AsNoTracking()
                    on membershipRole.OrganizationMembershipId equals membership.Id
                join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                where membership.OrganizationId == organizationId && userIds.Contains(membership.UserId)
                select new { UserId = membership.UserId, Role = role.Name! })
            .ToListAsync(cancellationToken);

        return globalRoles.Concat(organizationRoles)
            .GroupBy(value => value.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(value => value.Role)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
    }

    private async Task<bool> IsLastOrganizationAdministratorAsync(
        OrganizationMembership targetMembership,
        CancellationToken cancellationToken)
    {
        var roleId = await dbContext.Roles
            .Where(role => role.Name == PxaRoles.OrganizationAdministrator)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (roleId is null)
            return false;

        var targetIsAdministrator = await dbContext.OrganizationMembershipRoles.AnyAsync(value =>
            value.OrganizationMembershipId == targetMembership.Id && value.RoleId == roleId,
            cancellationToken);
        if (!targetIsAdministrator)
            return false;

        var activeAdministratorCount = await (
                from membershipRole in dbContext.OrganizationMembershipRoles
                join membership in dbContext.OrganizationMemberships
                    on membershipRole.OrganizationMembershipId equals membership.Id
                join user in dbContext.Users on membership.UserId equals user.Id
                where membership.OrganizationId == targetMembership.OrganizationId &&
                      membershipRole.RoleId == roleId &&
                      membership.Status == OrganizationMembershipStatus.Active &&
                      user.IsActive
                select membership.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        return activeAdministratorCount <= 1;
    }

    private Task<bool> IsOrganizationUserAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.OrganizationMemberships.AnyAsync(value =>
            value.UserId == userId &&
            value.OrganizationId == organizationId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);

    private async Task RevokeActiveSessionsAsync(
        Guid userId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions
            .Where(value => value.UserId == userId && value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            Revoke(session, actorUserId, reason);
    }

    private static void Revoke(UserSession session, Guid actorUserId, string reason)
    {
        session.RevokedAt = DateTimeOffset.UtcNow;
        session.RevokedByUserId = actorUserId;
        session.RevocationReason = reason;
    }

    private void AddAuditEvent(
        Guid organizationId,
        Guid actorUserId,
        string action,
        Guid targetUserId,
        object details)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = action,
            TargetType = "user",
            TargetId = targetUserId.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details),
        });
    }

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Administration change rejected",
        detail: detail);

    private static AdminUserResponse ToResponse(UserRecord user, IReadOnlyList<string> roles) =>
        new(
            user.Id,
            user.MembershipId,
            user.DisplayName,
            user.Email,
            user.Username,
            user.IsActive,
            user.MembershipStatus.ToString(),
            roles,
            user.LastLoginAt,
            user.CreatedAt);

    private sealed record UserRecord(
        Guid Id,
        Guid MembershipId,
        string DisplayName,
        string Email,
        string Username,
        bool IsActive,
        OrganizationMembershipStatus MembershipStatus,
        DateTimeOffset? LastLoginAt,
        DateTimeOffset CreatedAt);
}

public sealed record AdminUserPage(
    IReadOnlyList<AdminUserResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AdminUserResponse(
    Guid Id,
    Guid MembershipId,
    string DisplayName,
    string Email,
    string Username,
    bool IsActive,
    string MembershipStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record UpdateAdminUserStatusRequest(bool IsActive);

public sealed record UpdateAdminUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record AdminUserSessionResponse(
    Guid Id,
    string UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    string? RevocationReason,
    bool IsCurrent,
    bool IsActive);

public sealed record RevokeSessionsResponse(int RevokedCount);
