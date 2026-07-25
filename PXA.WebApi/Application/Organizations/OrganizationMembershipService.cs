using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Application.Organizations;

/// <summary>
/// Shared organization-membership logic used by both the System-Administrator-facing
/// Admin controllers and the tenant-scoped Account controllers, so last-owner
/// protection and role-replacement semantics can never drift between the two
/// surfaces. Does not call <c>SaveChangesAsync</c> or write audit events -
/// callers own the transaction boundary and their own audit action names.
/// </summary>
public sealed class OrganizationMembershipService(PxaDbContext dbContext)
{
    public static readonly string[] OrganizationRoles =
    [
        PxaRoles.OrganizationAdministrator,
        PxaRoles.Manager,
        PxaRoles.Editor,
        PxaRoles.Viewer,
    ];

    public static string[]? NormalizeRoles(IReadOnlyList<string> roles)
    {
        var result = roles.Distinct(StringComparer.Ordinal).ToArray();
        return result.All(role => OrganizationRoles.Contains(role, StringComparer.Ordinal)) ? result : null;
    }

    public async Task<IReadOnlyList<OrganizationMemberRecord>> GetMembersAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var records = await (
                from membership in dbContext.OrganizationMemberships.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId &&
                      membership.Status != OrganizationMembershipStatus.Removed
                orderby user.DisplayName
                select new OrganizationMemberRecord(
                    membership.Id,
                    user.Id,
                    user.DisplayName,
                    user.Email ?? string.Empty,
                    user.IsActive,
                    membership.Status,
                    membership.CreatedAt,
                    Array.Empty<string>()))
            .ToListAsync(cancellationToken);

        var roles = await GetMemberRolesAsync(records.Select(record => record.MembershipId).ToArray(), cancellationToken);
        return records
            .Select(record => record with { Roles = roles.GetValueOrDefault(record.MembershipId, []) })
            .ToArray();
    }

    public async Task<MembershipMutationResult> AddMemberAsync(
        Guid organizationId,
        string email,
        IReadOnlyList<string> roles,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(
            value => value.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null)
            return MembershipMutationResult.UserNotFound();

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.UserId == user.Id,
            cancellationToken);
        if (membership is null)
        {
            membership = new OrganizationMembership { OrganizationId = organizationId, UserId = user.Id };
            dbContext.OrganizationMemberships.Add(membership);
        }
        else
        {
            if (membership.Status != OrganizationMembershipStatus.Removed)
                return MembershipMutationResult.AlreadyMember();
            membership.Status = OrganizationMembershipStatus.Active;
            membership.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await ReplaceRolesAsync(membership, roles, actorUserId, cancellationToken);
        return MembershipMutationResult.Succeeded(new OrganizationMemberRecord(
            membership.Id,
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.IsActive,
            membership.Status,
            membership.CreatedAt,
            roles));
    }

    public async Task<MembershipMutationResult> ReplaceMemberRolesAsync(
        Guid organizationId,
        Guid userId,
        IReadOnlyList<string> roles,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId &&
            value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);
        if (membership is null)
            return MembershipMutationResult.MembershipNotFound();

        // Checked against the membership's *current* roles, before ReplaceRolesAsync
        // mutates them, so demoting the last active administrator is blocked.
        if (!roles.Contains(PxaRoles.OrganizationAdministrator, StringComparer.Ordinal) &&
            await IsLastOrganizationAdministratorAsync(membership, cancellationToken))
        {
            return MembershipMutationResult.LastOwnerProtected();
        }

        await ReplaceRolesAsync(membership, roles, actorUserId, cancellationToken);
        await RevokeOrganizationSessionsAsync(
            organizationId, userId, actorUserId, "organization-roles-changed", cancellationToken);
        var user = await dbContext.Users.SingleAsync(value => value.Id == userId, cancellationToken);
        return MembershipMutationResult.Succeeded(new OrganizationMemberRecord(
            membership.Id,
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.IsActive,
            membership.Status,
            membership.CreatedAt,
            roles));
    }

    public async Task<MembershipMutationResult> RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        bool actorIsRemovingOwnActiveMembership,
        CancellationToken cancellationToken)
    {
        if (actorUserId == userId && actorIsRemovingOwnActiveMembership)
            return MembershipMutationResult.CannotRemoveSelf();

        var membership = await dbContext.OrganizationMemberships.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId &&
            value.UserId == userId &&
            value.Status != OrganizationMembershipStatus.Removed,
            cancellationToken);
        if (membership is null)
            return MembershipMutationResult.MembershipNotFound();
        if (await IsLastOrganizationAdministratorAsync(membership, cancellationToken))
            return MembershipMutationResult.LastOwnerProtected();

        membership.Status = OrganizationMembershipStatus.Removed;
        membership.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeOrganizationSessionsAsync(
            organizationId, userId, actorUserId, "organization-membership-removed", cancellationToken);
        return MembershipMutationResult.Succeeded();
    }

    public async Task<bool> IsLastOrganizationAdministratorAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken)
    {
        var roleId = await dbContext.Roles
            .Where(role => role.Name == PxaRoles.OrganizationAdministrator)
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (roleId is null || !await dbContext.OrganizationMembershipRoles.AnyAsync(value =>
                value.OrganizationMembershipId == membership.Id && value.RoleId == roleId,
                cancellationToken))
            return false;

        return await (
                from membershipRole in dbContext.OrganizationMembershipRoles
                join candidate in dbContext.OrganizationMemberships
                    on membershipRole.OrganizationMembershipId equals candidate.Id
                join user in dbContext.Users on candidate.UserId equals user.Id
                where candidate.OrganizationId == membership.OrganizationId &&
                      membershipRole.RoleId == roleId &&
                      candidate.Status == OrganizationMembershipStatus.Active &&
                      user.IsActive
                select candidate.UserId)
            .Distinct()
            .CountAsync(cancellationToken) <= 1;
    }

    private async Task ReplaceRolesAsync(
        OrganizationMembership membership,
        IReadOnlyList<string> roles,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.OrganizationMembershipRoles
            .Where(value => value.OrganizationMembershipId == membership.Id)
            .ToListAsync(cancellationToken);
        dbContext.OrganizationMembershipRoles.RemoveRange(existing);
        var roleEntities = await dbContext.Roles.Where(role => roles.Contains(role.Name!)).ToListAsync(cancellationToken);
        dbContext.OrganizationMembershipRoles.AddRange(roleEntities.Select(role => new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = role.Id,
            AssignedByUserId = actorUserId,
        }));
    }

    private async Task RevokeOrganizationSessionsAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = await dbContext.UserSessions
            .Where(value =>
                value.OrganizationId == organizationId &&
                value.UserId == userId &&
                value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedByUserId = actorUserId;
            session.RevocationReason = reason;
        }
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetMemberRolesAsync(
        Guid[] membershipIds,
        CancellationToken cancellationToken)
    {
        var roles = await (
                from membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                where membershipIds.Contains(membershipRole.OrganizationMembershipId)
                select new { membershipRole.OrganizationMembershipId, Role = role.Name! })
            .ToListAsync(cancellationToken);
        return roles.GroupBy(value => value.OrganizationMembershipId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(value => value.Role).Order().ToArray());
    }
}

public sealed record OrganizationMemberRecord(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string Email,
    bool IsActive,
    OrganizationMembershipStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles);

public enum MembershipMutationOutcome
{
    Succeeded,
    UserNotFound,
    AlreadyMember,
    CannotRemoveSelf,
    LastOwnerProtected,
    MembershipNotFound,
}

public sealed record MembershipMutationResult(MembershipMutationOutcome Outcome, OrganizationMemberRecord? Member = null)
{
    public static MembershipMutationResult Succeeded(OrganizationMemberRecord? member = null) =>
        new(MembershipMutationOutcome.Succeeded, member);

    public static MembershipMutationResult UserNotFound() => new(MembershipMutationOutcome.UserNotFound);
    public static MembershipMutationResult AlreadyMember() => new(MembershipMutationOutcome.AlreadyMember);
    public static MembershipMutationResult CannotRemoveSelf() => new(MembershipMutationOutcome.CannotRemoveSelf);
    public static MembershipMutationResult LastOwnerProtected() => new(MembershipMutationOutcome.LastOwnerProtected);
    public static MembershipMutationResult MembershipNotFound() => new(MembershipMutationOutcome.MembershipNotFound);
}
