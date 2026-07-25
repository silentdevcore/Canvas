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
using PXA.WebApi.Application.Organizations;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/account/organization")]
public sealed class AccountOrganizationController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly OrganizationMembershipService membershipService;
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPxaMailQueue mailQueue;
    private readonly PxaMailOptions mailOptions;

    public AccountOrganizationController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        OrganizationMembershipService membershipService,
        UserManager<PxaIdentityUser> userManager,
        IdentityActionTokenService actionTokens,
        IPxaMailQueue mailQueue,
        IOptions<PxaMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.membershipService = membershipService;
        this.userManager = userManager;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
    }

    [HttpGet]
    [Authorize(Policy = PxaAccountPermissions.OrganizationRead)]
    public async Task<ActionResult<AccountOrganizationResponse>> GetOrganization(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();

        var organization = await dbContext.Organizations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == organizationId, cancellationToken);
        return organization is null ? NotFound() : Ok(ToResponse(organization));
    }

    [HttpPatch]
    [Authorize(Policy = PxaAccountPermissions.OrganizationManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.organization.update")]
    public async Task<ActionResult<AccountOrganizationResponse>> UpdateOrganization(
        UpdateAccountOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var name = request.Name.Trim();
        if (name.Length is < 2 or > 200)
            return ValidationProblem("Organization name must contain between 2 and 200 characters.");

        var organization = await dbContext.Organizations.SingleOrDefaultAsync(
            value => value.Id == organizationId, cancellationToken);
        if (organization is null)
            return NotFound();

        organization.Name = name;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEvents.Add(NewAuditEvent(organizationId.Value, actorUserId.Value,
            "account.organization.updated", organization.Id, "organization", new { organization.Name }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(organization));
    }

    [HttpGet("members")]
    [Authorize(Policy = PxaAccountPermissions.MembersRead)]
    public async Task<ActionResult<IReadOnlyList<AccountOrganizationMemberResponse>>> GetMembers(
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();

        var members = await membershipService.GetMembersAsync(organizationId.Value, cancellationToken);
        return Ok(members.Select(ToMemberResponse).ToArray());
    }

    [HttpPost("members")]
    [Authorize(Policy = PxaAccountPermissions.MembersInvite)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.members.invite")]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<AccountOrganizationMemberResponse>> InviteMember(
        InviteAccountOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        var roles = OrganizationMembershipService.NormalizeRoles(request.Roles);
        if (displayName.Length is < 2 or > 200 || roles is null || roles.Length == 0)
            return ValidationProblem("Provide a display name and at least one valid organization role.");
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && await dbContext.OrganizationMemberships.AnyAsync(
                value => value.OrganizationId == organizationId &&
                         value.UserId == user.Id,
                cancellationToken))
        {
            return ConflictProblem("This PXA account is already a member or has a pending invitation.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (user is null)
        {
            user = new PxaIdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                DisplayName = displayName,
                IsActive = false,
            };
            var creation = await userManager.CreateAsync(user);
            if (!creation.Succeeded)
            {
                return ValidationProblem(string.Join(" ", creation.Errors.Select(error => error.Description)));
            }
        }

        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId.Value,
            UserId = user.Id,
            Status = OrganizationMembershipStatus.Invited,
        };
        dbContext.OrganizationMemberships.Add(membership);
        var roleEntities = await dbContext.Roles
            .Where(role => roles.Contains(role.Name!))
            .ToListAsync(cancellationToken);
        dbContext.OrganizationMembershipRoles.AddRange(roleEntities.Select(role => new OrganizationMembershipRole
        {
            OrganizationMembershipId = membership.Id,
            RoleId = role.Id,
            AssignedByUserId = actorUserId.Value,
        }));

        var issued = await actionTokens.IssueAsync(
            user.Id,
            organizationId,
            email,
            IdentityActionTokenService.InvitationPurpose,
            new { organizationId, roles },
            TimeSpan.FromDays(7),
            cancellationToken);
        var actionUrl =
            $"{mailOptions.AccountBaseUrl.TrimEnd('/')}/accept-invitation?token={Uri.EscapeDataString(issued.RawToken)}";
        mailQueue.Enqueue(
            organizationId,
            user.Id,
            email,
            "identity.invitation",
            new { displayName, actionUrl },
            $"account-invitation:{issued.Entity.Id}");
        dbContext.AuditEvents.Add(NewAuditEvent(organizationId.Value, actorUserId.Value,
            "account.members.invited", user.Id, "user", new { Roles = roles }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Accepted(new AccountOrganizationMemberResponse(
            user.Id, membership.Id, displayName, email, false, membership.Status.ToString(), roles, membership.CreatedAt));
    }

    [HttpPut("members/{userId:guid}/roles")]
    [Authorize(Policy = PxaAccountPermissions.MembersInvite)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.members.roles-updated")]
    public async Task<ActionResult<AccountOrganizationMemberResponse>> UpdateMemberRoles(
        Guid userId,
        UpdateAccountOrganizationMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var roles = OrganizationMembershipService.NormalizeRoles(request.Roles);
        if (roles is null || roles.Length == 0)
            return ValidationProblem("Provide at least one valid organization role.");

        var result = await membershipService.ReplaceMemberRolesAsync(
            organizationId.Value, userId, roles, actorUserId.Value, cancellationToken);
        switch (result.Outcome)
        {
            case MembershipMutationOutcome.MembershipNotFound:
                return NotFound();
            case MembershipMutationOutcome.LastOwnerProtected:
                return LastOwnerProblem("The last active Organization Administrator role cannot be removed.");
        }

        dbContext.AuditEvents.Add(NewAuditEvent(organizationId.Value, actorUserId.Value,
            "account.members.roles-updated", userId, "membership", new { Roles = roles }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToMemberResponse(result.Member!));
    }

    [HttpDelete("members/{userId:guid}")]
    [Authorize(Policy = PxaAccountPermissions.MembersRemove)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.members.removed")]
    public async Task<IActionResult> RemoveMember(Guid userId, CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return MissingOrganization();

        var result = await membershipService.RemoveMemberAsync(
            organizationId.Value, userId, actorUserId.Value, actorIsRemovingOwnActiveMembership: true, cancellationToken);
        switch (result.Outcome)
        {
            case MembershipMutationOutcome.CannotRemoveSelf:
                return ConflictProblem("You cannot remove your own active organization membership.");
            case MembershipMutationOutcome.MembershipNotFound:
                return NotFound();
            case MembershipMutationOutcome.LastOwnerProtected:
                return LastOwnerProblem("The last active Organization Administrator cannot be removed.");
        }

        dbContext.AuditEvents.Add(NewAuditEvent(organizationId.Value, actorUserId.Value,
            "account.members.removed", userId, "membership", new { }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
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

    private static AccountOrganizationResponse ToResponse(Organization organization) => new(
        organization.Id, organization.Name, organization.Slug, organization.Status.ToString());

    private static AccountOrganizationMemberResponse ToMemberResponse(OrganizationMemberRecord member) => new(
        member.UserId, member.MembershipId, member.DisplayName, member.Email,
        member.IsActive, member.Status.ToString(), member.Roles, member.CreatedAt);

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Organization change rejected",
        detail: detail);

    private ObjectResult LastOwnerProblem(string detail) => StatusCode(
        StatusCodes.Status409Conflict,
        PxaApiProblems.Create(
            HttpContext,
            StatusCodes.Status409Conflict,
            "Organization change rejected",
            detail,
            PxaApiProblems.LastOwnerProtected));

    private ObjectResult ValidationProblem(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid organization request",
        detail: detail);
}

public sealed record AccountOrganizationResponse(Guid Id, string Name, string Slug, string Status);

public sealed record UpdateAccountOrganizationRequest([Required] string Name);

public sealed record AccountOrganizationMemberResponse(
    Guid UserId,
    Guid MembershipId,
    string DisplayName,
    string Email,
    bool IsActive,
    string MembershipStatus,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record InviteAccountOrganizationMemberRequest(
    [Required, EmailAddress] string Email,
    [Required] string DisplayName,
    [Required] IReadOnlyList<string> Roles);

public sealed record UpdateAccountOrganizationMemberRolesRequest([Required] IReadOnlyList<string> Roles);
