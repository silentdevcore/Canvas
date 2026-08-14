using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[Route("api/pxa/v1/designer")]
public sealed class DesignerNotificationsController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    PxaDesignerProductMetadata metadata) : ControllerBase
{
    [HttpGet("releases")]
    public async Task<ActionResult<DesignerReleaseFeedResponse>> Releases(
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out _, out var userId))
            return Unauthorized();
        var readVersions = await dbContext.DesignerReleaseReads.AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => value.Version)
            .ToArrayAsync(cancellationToken);
        return Ok(new DesignerReleaseFeedResponse(metadata.Releases.Releases, readVersions));
    }

    [HttpPut("releases/{version}/read")]
    public async Task<IActionResult> MarkReleaseRead(string version, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out _, out var userId))
            return Unauthorized();
        var release = metadata.FindRelease(version);
        if (release is null)
            return NotFound();
        var existing = await dbContext.DesignerReleaseReads.SingleOrDefaultAsync(value =>
            value.UserId == userId && value.Version == release.Version, cancellationToken);
        if (existing is null)
        {
            dbContext.DesignerReleaseReads.Add(new DesignerReleaseRead
            {
                UserId = userId,
                Version = release.Version,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<DesignerNotificationPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = DateTimeOffset.UtcNow;
        var visible = dbContext.DesignerNotifications.AsNoTracking()
            .Where(value =>
                value.CreatedAt <= now &&
                (value.ExpiresAt == null || value.ExpiresAt > now) &&
                (value.UserId == userId ||
                 (value.UserId == null &&
                  (value.OrganizationId == null || value.OrganizationId == organizationId))) &&
                !dbContext.DesignerNotificationStates.Any(state =>
                    state.NotificationId == value.Id &&
                    state.UserId == userId &&
                    state.DismissedAt != null));
        var total = await visible.CountAsync(cancellationToken);
        var notifications = await visible
            .OrderByDescending(value => value.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var notificationIds = notifications.Select(value => value.Id).ToArray();
        var states = await dbContext.DesignerNotificationStates.AsNoTracking()
            .Where(value => value.UserId == userId && notificationIds.Contains(value.NotificationId))
            .ToDictionaryAsync(value => value.NotificationId, cancellationToken);
        var rows = notifications.Select(value =>
        {
            states.TryGetValue(value.Id, out var state);
            return new DesignerNotificationResponse(
                value.Id,
                value.Category.ToString(),
                value.Severity.ToString(),
                value.Title,
                value.Message,
                value.ActionLabel,
                SafeActionUrl(value.ActionUrl),
                value.Dismissible,
                value.CreatedAt,
                value.ExpiresAt,
                state?.ReadAt is not null);
        }).ToArray();
        return Ok(new DesignerNotificationPage(rows, page, pageSize, total));
    }

    [HttpGet("notifications/unread-count")]
    public async Task<ActionResult<DesignerUnreadCountResponse>> UnreadCount(
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var persistent = await dbContext.DesignerNotifications.AsNoTracking()
            .Where(value =>
                value.CreatedAt <= now &&
                (value.ExpiresAt == null || value.ExpiresAt > now) &&
                (value.UserId == userId ||
                 (value.UserId == null &&
                  (value.OrganizationId == null || value.OrganizationId == organizationId))) &&
                !dbContext.DesignerNotificationStates.Any(state =>
                    state.NotificationId == value.Id &&
                    state.UserId == userId &&
                    (state.ReadAt != null || state.DismissedAt != null)))
            .CountAsync(cancellationToken);
        var readReleases = await dbContext.DesignerReleaseReads.AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => value.Version)
            .ToArrayAsync(cancellationToken);
        var releases = metadata.Releases.Releases.Count(value =>
            !readReleases.Contains(value.Version, StringComparer.OrdinalIgnoreCase));
        return Ok(new DesignerUnreadCountResponse(persistent + releases, persistent, releases));
    }

    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        await MutateState(id, dismiss: false, cancellationToken);

    [HttpPut("notifications/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken) =>
        await MutateState(id, dismiss: true, cancellationToken);

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var ids = await dbContext.DesignerNotifications
            .Where(value =>
                value.CreatedAt <= now &&
                (value.ExpiresAt == null || value.ExpiresAt > now) &&
                (value.UserId == userId ||
                 (value.UserId == null &&
                  (value.OrganizationId == null || value.OrganizationId == organizationId))))
            .Select(value => value.Id)
            .ToArrayAsync(cancellationToken);
        var states = await dbContext.DesignerNotificationStates
            .Where(value => value.UserId == userId && ids.Contains(value.NotificationId))
            .ToDictionaryAsync(value => value.NotificationId, cancellationToken);
        foreach (var id in ids)
        {
            if (!states.TryGetValue(id, out var state))
            {
                state = new DesignerNotificationState { NotificationId = id, UserId = userId };
                dbContext.DesignerNotificationStates.Add(state);
            }
            state.ReadAt ??= now;
            state.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> MutateState(
        Guid notificationId,
        bool dismiss,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        var notification = await dbContext.DesignerNotifications.AsNoTracking().SingleOrDefaultAsync(value =>
            value.Id == notificationId &&
            (value.UserId == userId ||
             (value.UserId == null &&
              (value.OrganizationId == null || value.OrganizationId == organizationId))),
            cancellationToken);
        if (notification is null)
            return NotFound();
        if (dismiss && !notification.Dismissible)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Notification cannot be dismissed",
            });
        var state = await dbContext.DesignerNotificationStates.SingleOrDefaultAsync(value =>
            value.NotificationId == notificationId && value.UserId == userId, cancellationToken);
        if (state is null)
        {
            state = new DesignerNotificationState { NotificationId = notificationId, UserId = userId };
            dbContext.DesignerNotificationStates.Add(state);
        }
        var now = DateTimeOffset.UtcNow;
        state.ReadAt ??= now;
        if (dismiss)
            state.DismissedAt = now;
        state.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetContext(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private static string? SafeActionUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('/') || value.StartsWith("//"))
            return null;
        return Uri.TryCreate(value, UriKind.Relative, out _) ? value : null;
    }
}

public sealed record DesignerReleaseFeedResponse(
    IReadOnlyList<DesignerReleaseDefinition> Releases,
    IReadOnlyList<string> ReadVersions);
public sealed record DesignerNotificationPage(
    IReadOnlyList<DesignerNotificationResponse> Items,
    int Page,
    int PageSize,
    int Total);
public sealed record DesignerNotificationResponse(
    Guid Id,
    string Category,
    string Severity,
    string Title,
    string Message,
    string? ActionLabel,
    string? ActionUrl,
    bool Dismissible,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool Read);
public sealed record DesignerUnreadCountResponse(int Unread, int Persistent, int Releases);
