using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Organizations;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/organizations")]
public sealed partial class AdminOrganizationsController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly OrganizationMembershipService membershipService;

    public AdminOrganizationsController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        OrganizationMembershipService membershipService)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.membershipService = membershipService;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.OrganizationsRead)]
    public async Task<ActionResult<AdminOrganizationPage>> GetOrganizations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Organizations.AsNoTracking();

        if (!IsSystemAdministrator())
        {
            if (tenantContext.OrganizationId is not { } organizationId)
                return MissingOrganization();
            query = query.Where(organization => organization.Id == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(organization =>
                EF.Functions.ILike(organization.Name, pattern) ||
                EF.Functions.ILike(organization.Slug, pattern));
        }

        if (Enum.TryParse<OrganizationStatus>(status, true, out var parsedStatus))
            query = query.Where(organization => organization.Status == parsedStatus);

        var total = await query.CountAsync(cancellationToken);
        var organizations = await query
            .OrderBy(organization => organization.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(organization => new OrganizationRecord(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Status,
                organization.CreatedAt,
                organization.UpdatedAt,
                dbContext.OrganizationMemberships.Count(membership =>
                    membership.OrganizationId == organization.Id &&
                    membership.Status != OrganizationMembershipStatus.Removed)))
            .ToListAsync(cancellationToken);

        return Ok(new AdminOrganizationPage(
            organizations.Select(ToResponse).ToArray(),
            page,
            pageSize,
            total));
    }

    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = PxaPermissions.OrganizationsRead)]
    public async Task<ActionResult<AdminOrganizationResponse>> GetOrganization(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!CanAccess(organizationId))
            return NotFound();

        var organization = await GetRecordAsync(organizationId, cancellationToken);
        return organization is null ? NotFound() : Ok(ToResponse(organization));
    }

    [HttpPost]
    [Authorize(Roles = PxaRoles.SystemAdministrator)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("organizations.create")]
    public async Task<ActionResult<AdminOrganizationResponse>> CreateOrganization(
        CreateAdminOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = tenantContext.UserId;
        if (actorUserId is null)
            return Unauthorized();

        var name = request.Name.Trim();
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (name.Length is < 2 or > 200 || !SlugPattern().IsMatch(slug))
            return ValidationProblem("Name or slug is invalid.");
        if (await dbContext.Organizations.AnyAsync(value => value.Slug == slug, cancellationToken))
            return ConflictProblem("An organization with this slug already exists.");

        var organization = new Organization { Name = name, Slug = slug };
        dbContext.Organizations.Add(organization);
        AddAuditEvent(organization.Id, actorUserId.Value, "organizations.create", organization.Id, new { name, slug });
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(
            nameof(GetOrganization),
            new { organizationId = organization.Id },
            ToResponse(new OrganizationRecord(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Status,
                organization.CreatedAt,
                organization.UpdatedAt,
                0)));
    }

    [HttpPatch("{organizationId:guid}")]
    [Authorize(Policy = PxaPermissions.OrganizationsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("organizations.update")]
    public async Task<ActionResult<AdminOrganizationResponse>> UpdateOrganization(
        Guid organizationId,
        UpdateAdminOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = tenantContext.UserId;
        if (actorUserId is null || !CanAccess(organizationId))
            return NotFound();

        var organization = await dbContext.Organizations.SingleOrDefaultAsync(
            value => value.Id == organizationId,
            cancellationToken);
        if (organization is null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (name.Length is < 2 or > 200)
                return ValidationProblem("Organization name must contain between 2 and 200 characters.");
            organization.Name = name;
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<OrganizationStatus>(request.Status, true, out var requestedStatus))
                return ValidationProblem("Organization status is invalid.");
            if (!IsSystemAdministrator())
                return Forbid();
            organization.Status = requestedStatus;
        }

        organization.UpdatedAt = DateTimeOffset.UtcNow;
        AddAuditEvent(
            organization.Id,
            actorUserId.Value,
            "organizations.update",
            organization.Id,
            new { organization.Name, organization.Status });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse((await GetRecordAsync(organizationId, cancellationToken))!));
    }

    [HttpGet("{organizationId:guid}/members")]
    [Authorize(Policy = PxaPermissions.UsersRead)]
    public async Task<ActionResult<IReadOnlyList<AdminOrganizationMemberResponse>>> GetMembers(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!CanAccess(organizationId))
            return NotFound();

        var members = await membershipService.GetMembersAsync(organizationId, cancellationToken);
        return Ok(members.Select(ToMemberResponse).ToArray());
    }

    [HttpPost("{organizationId:guid}/members")]
    [Authorize(Policy = PxaPermissions.UsersCreate)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("memberships.add")]
    public async Task<ActionResult<AdminOrganizationMemberResponse>> AddMember(
        Guid organizationId,
        AddAdminOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var actorUserId = tenantContext.UserId;
        if (actorUserId is null || !CanAccess(organizationId))
            return NotFound();
        var requestedRoles = OrganizationMembershipService.NormalizeRoles(request.Roles);
        if (requestedRoles is null)
            return ValidationProblem("Only organization-scoped roles can be assigned.");

        var result = await membershipService.AddMemberAsync(
            organizationId, request.Email, requestedRoles, actorUserId.Value, cancellationToken);
        if (result.Outcome == MembershipMutationOutcome.UserNotFound)
            return NotFoundProblem("No existing PXA user has this email address.");
        if (result.Outcome == MembershipMutationOutcome.AlreadyMember)
            return ConflictProblem("This user already belongs to the organization.");

        AddAuditEvent(organizationId, actorUserId.Value, "memberships.add", result.Member!.UserId, new { Roles = requestedRoles });
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToMemberResponse(result.Member));
    }

    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    [Authorize(Policy = PxaPermissions.UsersDisable)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("memberships.remove")]
    public async Task<IActionResult> RemoveMember(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actorUserId = tenantContext.UserId;
        if (actorUserId is null || !CanAccess(organizationId))
            return NotFound();

        var result = await membershipService.RemoveMemberAsync(
            organizationId,
            userId,
            actorUserId.Value,
            actorIsRemovingOwnActiveMembership: tenantContext.OrganizationId == organizationId,
            cancellationToken);
        switch (result.Outcome)
        {
            case MembershipMutationOutcome.CannotRemoveSelf:
                return ConflictProblem("Administrators cannot remove their own active organization membership.");
            case MembershipMutationOutcome.MembershipNotFound:
                return NotFound();
            case MembershipMutationOutcome.LastOwnerProtected:
                return ConflictProblem("The last active Organization Administrator cannot be removed.");
        }

        AddAuditEvent(organizationId, actorUserId.Value, "memberships.remove", userId, new { });
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool IsSystemAdministrator() => User.IsInRole(PxaRoles.SystemAdministrator);

    private bool CanAccess(Guid organizationId) =>
        IsSystemAdministrator() || tenantContext.OrganizationId == organizationId;

    private Task<OrganizationRecord?> GetRecordAsync(Guid organizationId, CancellationToken cancellationToken) =>
        dbContext.Organizations.AsNoTracking()
            .Where(organization => organization.Id == organizationId)
            .Select(organization => new OrganizationRecord(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.Status,
                organization.CreatedAt,
                organization.UpdatedAt,
                dbContext.OrganizationMemberships.Count(membership =>
                    membership.OrganizationId == organization.Id &&
                    membership.Status != OrganizationMembershipStatus.Removed)))
            .SingleOrDefaultAsync(cancellationToken);

    private void AddAuditEvent(Guid organizationId, Guid actorUserId, string action, Guid targetId, object details) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = action,
            TargetType = action.StartsWith("organizations.", StringComparison.Ordinal) ? "organization" : "membership",
            TargetId = targetId.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Administration change rejected",
        detail: detail);

    private ObjectResult NotFoundProblem(string detail) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "User not found",
        detail: detail);

    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid organization request",
        Detail = detail,
    });

    private static AdminOrganizationResponse ToResponse(OrganizationRecord organization) => new(
        organization.Id,
        organization.Name,
        organization.Slug,
        organization.Status.ToString(),
        organization.MemberCount,
        organization.CreatedAt,
        organization.UpdatedAt);

    private static AdminOrganizationMemberResponse ToMemberResponse(OrganizationMemberRecord member) => new(
        member.UserId,
        member.MembershipId,
        member.DisplayName,
        member.Email,
        member.IsActive,
        member.Status.ToString(),
        member.Roles,
        member.CreatedAt);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    private sealed record OrganizationRecord(
        Guid Id,
        string Name,
        string Slug,
        OrganizationStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        int MemberCount);
}

public sealed record AdminOrganizationPage(
    IReadOnlyList<AdminOrganizationResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AdminOrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminOrganizationMemberResponse(
    Guid UserId,
    Guid MembershipId,
    string DisplayName,
    string Email,
    bool IsActive,
    string MembershipStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record CreateAdminOrganizationRequest(string Name, string Slug);

public sealed record UpdateAdminOrganizationRequest(string? Name, string? Status);

public sealed record AddAdminOrganizationMemberRequest(string Email, IReadOnlyList<string> Roles);
