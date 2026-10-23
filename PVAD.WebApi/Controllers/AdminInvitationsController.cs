using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Mail;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pvad/v1/admin/invitations")]
public sealed class AdminInvitationsController : ControllerBase
{
    private static readonly string[] OrganizationRoles =
    [
        PvadRoles.OrganizationAdministrator,
        PvadRoles.Manager,
        PvadRoles.Editor,
        PvadRoles.Viewer,
    ];

    private readonly PvadDbContext dbContext;
    private readonly UserManager<PvadIdentityUser> userManager;
    private readonly IPvadTenantContext tenantContext;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPvadMailQueue mailQueue;
    private readonly PvadMailOptions mailOptions;

    public AdminInvitationsController(
        PvadDbContext dbContext,
        UserManager<PvadIdentityUser> userManager,
        IPvadTenantContext tenantContext,
        IdentityActionTokenService actionTokens,
        IPvadMailQueue mailQueue,
        IOptions<PvadMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.tenantContext = tenantContext;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
    }

    [HttpPost]
    [Authorize(Policy = PvadPermissions.UsersCreate)]
    [PvadValidateAntiforgery]
    [PvadAuditedMutation("invitations.create")]
    [EnableRateLimiting("invitations")]
    public async Task<ActionResult<AdminInvitationResponse>> CreateInvitation(
        CreateAdminInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        var actorUserId = tenantContext.UserId;
        if (organizationId is null || actorUserId is null)
            return Problem(statusCode: 403, title: "Organization context required");

        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        var roles = request.Roles.Distinct(StringComparer.Ordinal).ToArray();
        if (displayName.Length is < 2 or > 200 ||
            roles.Length == 0 ||
            roles.Any(role => !OrganizationRoles.Contains(role, StringComparer.Ordinal)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid invitation",
                Detail = "Provide a display name and at least one valid organization role.",
            });
        }
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && await dbContext.OrganizationMemberships.AnyAsync(
                value => value.OrganizationId == organizationId &&
                         value.UserId == user.Id,
                cancellationToken))
        {
            return ConflictProblem("This PVADE account is already a member or has a pending invitation.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (user is null)
        {
            user = new PvadIdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                DisplayName = displayName,
                IsActive = false,
            };
            var creation = await userManager.CreateAsync(user);
            if (!creation.Succeeded)
                return IdentityFailure(creation);
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
            AssignedByUserId = actorUserId,
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
        var message = mailQueue.Enqueue(
            organizationId,
            user.Id,
            email,
            "identity.invitation",
            new { displayName, actionUrl },
            $"invitation:{issued.Entity.Id}");
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId.Value,
            ActorUserId = actorUserId.Value,
            Action = "invitations.create",
            TargetType = "user",
            TargetId = user.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { Roles = roles }),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Accepted(new AdminInvitationResponse(
            user.Id,
            membership.Id,
            email,
            displayName,
            roles,
            issued.Entity.ExpiresAt,
            message.Id));
    }

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: 409,
        title: "Invitation rejected",
        detail: detail);

    private ObjectResult IdentityFailure(IdentityResult result) => Problem(
        statusCode: 400,
        title: "Invalid account details",
        detail: string.Join(" ", result.Errors.Select(error => error.Description)));
}

public sealed record CreateAdminInvitationRequest(
    [Required, EmailAddress] string Email,
    [Required] string DisplayName,
    [Required] IReadOnlyList<string> Roles);

public sealed record AdminInvitationResponse(
    Guid UserId,
    Guid MembershipId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAt,
    Guid MailMessageId);
