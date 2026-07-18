using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

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
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPxaMailQueue mailQueue;
    private readonly PxaMailOptions mailOptions;

    public AdminUsersController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        UserManager<PxaIdentityUser> userManager,
        IdentityActionTokenService actionTokens,
        IPxaMailQueue mailQueue,
        IOptions<PxaMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.userManager = userManager;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
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
            where membership.OrganizationId == organizationId
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
            "deleted" => query.Where(value =>
                value.membership.Status == OrganizationMembershipStatus.Removed),
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
                value.user.PendingEmail,
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
                      membership.UserId == userId
                select new UserRecord(
                    user.Id,
                    membership.Id,
                    user.DisplayName,
                    user.Email ?? string.Empty,
                    user.UserName ?? string.Empty,
                    user.PendingEmail,
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

    [HttpPatch("{userId:guid}/profile")]
    [Authorize(Policy = PxaPermissions.UsersUpdate)]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<AdminUserResponse>> UpdateProfile(
        Guid userId,
        UpdateAdminUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId &&
            value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();

        var displayName = request.DisplayName.Trim();
        var email = request.Email.Trim();
        if (displayName.Length is < 2 or > 200 || !new EmailAddressAttribute().IsValid(email))
            return ValidationProblem("Provide a valid display name and email address.");

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null && existing.Id != user.Id)
                return ConflictProblem("The email address is already assigned to another PXA account.");

            user.PendingEmail = email;
            var issued = await actionTokens.IssueAsync(
                user.Id,
                organizationId,
                email,
                IdentityActionTokenService.EmailChangePurpose,
                new { },
                TimeSpan.FromHours(24),
                cancellationToken);
            var actionUrl = $"{mailOptions.AdminBaseUrl.TrimEnd('/')}/confirm-email?token={Uri.EscapeDataString(issued.RawToken)}";
            mailQueue.Enqueue(
                organizationId,
                user.Id,
                email,
                "identity.email-verification",
                new { displayName, actionUrl },
                $"email-change:{issued.Entity.Id}");
        }

        user.DisplayName = displayName;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            "users.update",
            userId,
            new { EmailVerificationQueued = emailChanged });
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await GetRolesAsync([userId], organizationId.Value, cancellationToken);
        return Ok(ToResponse(ToRecord(user, membership), roles.GetValueOrDefault(userId, [])));
    }

    [HttpPost("{userId:guid}/password-reset")]
    [Authorize(Policy = PxaPermissions.UsersUpdate)]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<AdminPasswordResetResponse>> RequestPasswordReset(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        if (!await IsOrganizationUserAsync(userId, organizationId.Value, cancellationToken))
            return NotFound();

        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email) || !user.EmailConfirmed)
            return ConflictProblem("Password reset requires a verified email address.");

        var issued = await actionTokens.IssueAsync(
            user.Id,
            organizationId,
            user.Email,
            IdentityActionTokenService.PasswordResetPurpose,
            new { RequestedByAdministrator = true },
            TimeSpan.FromHours(1),
            cancellationToken);
        var actionUrl = $"{mailOptions.AdminBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(issued.RawToken)}";
        var message = mailQueue.Enqueue(
            organizationId,
            user.Id,
            user.Email,
            "identity.password-reset",
            new { displayName = user.DisplayName, actionUrl },
            $"password-reset:{issued.Entity.Id}");
        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            "users.password-reset.request",
            userId,
            new { MailMessageId = message.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Accepted(new AdminPasswordResetResponse(message.Id, issued.Entity.ExpiresAt));
    }

    [HttpPatch("{userId:guid}/deletion")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminUserResponse>> UpdateDeletion(
        Guid userId,
        UpdateAdminUserDeletionRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        if (request.IsDeleted && userId == actorUserId)
            return ConflictProblem("Administrators cannot delete their own active membership.");

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.UserId == userId,
            cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (membership is null || user is null)
            return NotFound();
        if (request.IsDeleted && await IsLastOrganizationAdministratorAsync(membership, cancellationToken))
            return ConflictProblem("The last active Organization Administrator cannot be deleted.");

        membership.Status = request.IsDeleted
            ? OrganizationMembershipStatus.Removed
            : user.EmailConfirmed ? OrganizationMembershipStatus.Active : OrganizationMembershipStatus.Invited;
        membership.UpdatedAt = DateTimeOffset.UtcNow;
        if (!request.IsDeleted && user.EmailConfirmed)
            user.IsActive = true;
        if (request.IsDeleted)
            await RevokeOrganizationSessionsAsync(userId, organizationId.Value, actorUserId.Value, "membership-deleted", cancellationToken);

        AddAuditEvent(
            organizationId.Value,
            actorUserId.Value,
            request.IsDeleted ? "users.delete" : "users.restore",
            userId,
            new { });
        await dbContext.SaveChangesAsync(cancellationToken);
        var roles = await GetRolesAsync([userId], organizationId.Value, cancellationToken);
        return Ok(ToResponse(ToRecord(user, membership), roles.GetValueOrDefault(userId, [])));
    }

    [HttpGet("{userId:guid}/audit")]
    [Authorize(Policy = PxaPermissions.AuditRead)]
    public async Task<ActionResult<IReadOnlyList<AdminUserAuditResponse>>> GetUserAudit(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();
        if (!await dbContext.OrganizationMemberships.AnyAsync(value =>
                value.OrganizationId == organizationId && value.UserId == userId,
                cancellationToken))
            return NotFound();

        var events = await (
                from audit in dbContext.AuditEvents.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking() on audit.ActorUserId equals actor.Id into actors
                from actor in actors.DefaultIfEmpty()
                where audit.OrganizationId == organizationId &&
                      ((audit.TargetType == "user" && audit.TargetId == userId.ToString()) ||
                       (audit.ActorUserId == userId && audit.Action.StartsWith("security.")))
                orderby audit.CreatedAt descending
                select new AdminUserAuditResponse(
                    audit.Id,
                    audit.Action,
                    audit.Outcome,
                    actor == null ? "System" : actor.DisplayName,
                    audit.CreatedAt))
            .Take(50)
            .ToListAsync(cancellationToken);
        return Ok(events);
    }

    [HttpPost("bulk")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<AdminBulkUserResponse>> BulkUpdate(
        BulkAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();
        var userIds = request.UserIds.Distinct().Take(100).ToArray();
        if (userIds.Length == 0 || !new[] { "enable", "disable", "delete", "restore", "revoke-sessions" }.Contains(request.Action))
            return ValidationProblem("Select users and a supported bulk action.");

        var memberships = await dbContext.OrganizationMemberships
            .Where(value => value.OrganizationId == organizationId && userIds.Contains(value.UserId))
            .ToListAsync(cancellationToken);
        var users = await dbContext.Users
            .Where(value => userIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var succeeded = new List<Guid>();
        var rejected = new List<Guid>();
        foreach (var membership in memberships)
        {
            if (!users.TryGetValue(membership.UserId, out var user) ||
                ((request.Action is "disable" or "delete") && membership.UserId == actorUserId) ||
                ((request.Action is "disable" or "delete") && await IsLastOrganizationAdministratorAsync(membership, cancellationToken)))
            {
                rejected.Add(membership.UserId);
                continue;
            }

            switch (request.Action)
            {
                case "enable":
                    user.IsActive = true;
                    membership.Status = OrganizationMembershipStatus.Active;
                    break;
                case "disable":
                    user.IsActive = false;
                    membership.Status = OrganizationMembershipStatus.Suspended;
                    await RevokeActiveSessionsAsync(user.Id, actorUserId.Value, "bulk-disable", cancellationToken);
                    break;
                case "delete":
                    membership.Status = OrganizationMembershipStatus.Removed;
                    await RevokeOrganizationSessionsAsync(user.Id, organizationId.Value, actorUserId.Value, "bulk-delete", cancellationToken);
                    break;
                case "restore":
                    membership.Status = user.EmailConfirmed
                        ? OrganizationMembershipStatus.Active
                        : OrganizationMembershipStatus.Invited;
                    if (user.EmailConfirmed) user.IsActive = true;
                    break;
                case "revoke-sessions":
                    await RevokeOrganizationSessionsAsync(user.Id, organizationId.Value, actorUserId.Value, "bulk-revoke", cancellationToken);
                    break;
            }
            membership.UpdatedAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = membership.UpdatedAt;
            AddAuditEvent(organizationId.Value, actorUserId.Value, $"users.bulk.{request.Action}", user.Id, new { });
            succeeded.Add(user.Id);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new AdminBulkUserResponse(succeeded, rejected));
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
        if (!await IsOrganizationUserAsync(userId, organizationId.Value, cancellationToken, includeRemoved: true))
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
            user.PendingEmail,
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
            user.PendingEmail,
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
        CancellationToken cancellationToken,
        bool includeRemoved = false) =>
        dbContext.OrganizationMemberships.AnyAsync(value =>
            value.UserId == userId &&
            value.OrganizationId == organizationId &&
            (includeRemoved || value.Status != OrganizationMembershipStatus.Removed),
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

    private async Task RevokeOrganizationSessionsAsync(
        Guid userId,
        Guid organizationId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions
            .Where(value =>
                value.UserId == userId &&
                value.OrganizationId == organizationId &&
                value.RevokedAt == null)
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
            user.PendingEmail,
            user.IsActive,
            user.MembershipStatus.ToString(),
            roles,
            user.LastLoginAt,
            user.CreatedAt);

    private static UserRecord ToRecord(
        PxaIdentityUser user,
        OrganizationMembership membership) =>
        new(
            user.Id,
            membership.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.PendingEmail,
            user.IsActive,
            membership.Status,
            user.LastLoginAt,
            user.CreatedAt);

    private sealed record UserRecord(
        Guid Id,
        Guid MembershipId,
        string DisplayName,
        string Email,
        string Username,
        string? PendingEmail,
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
    string? PendingEmail,
    bool IsActive,
    string MembershipStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record UpdateAdminUserStatusRequest(bool IsActive);

public sealed record UpdateAdminUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record UpdateAdminUserProfileRequest(
    [Required] string DisplayName,
    [Required, EmailAddress] string Email);

public sealed record UpdateAdminUserDeletionRequest(bool IsDeleted);

public sealed record BulkAdminUserRequest(
    [Required] IReadOnlyList<Guid> UserIds,
    [Required] string Action);

public sealed record AdminBulkUserResponse(
    IReadOnlyList<Guid> SucceededUserIds,
    IReadOnlyList<Guid> RejectedUserIds);

public sealed record AdminPasswordResetResponse(Guid MailMessageId, DateTimeOffset ExpiresAt);

public sealed record AdminUserAuditResponse(
    Guid Id,
    string Action,
    string Outcome,
    string Actor,
    DateTimeOffset CreatedAt);

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
