using System.Text.Json;
using System.ComponentModel.DataAnnotations;
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
[Route("api/pxa/v1/admin/invitations")]
public sealed class AdminInvitationsController : ControllerBase
{
    private static readonly string[] OrganizationRoles =
    [
        PxaRoles.OrganizationAdministrator,
        PxaRoles.Manager,
        PxaRoles.Editor,
        PxaRoles.Viewer,
    ];

    private readonly PxaDbContext dbContext;
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IPxaTenantContext tenantContext;
    private readonly IdentityActionTokenService actionTokens;
    private readonly IPxaMailQueue mailQueue;
    private readonly PxaMailOptions mailOptions;

    public AdminInvitationsController(
        PxaDbContext dbContext,
        UserManager<PxaIdentityUser> userManager,
        IPxaTenantContext tenantContext,
        IdentityActionTokenService actionTokens,
        IPxaMailQueue mailQueue,
        IOptions<PxaMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.tenantContext = tenantContext;
        this.actionTokens = actionTokens;
        this.mailQueue = mailQueue;
        this.mailOptions = mailOptions.Value;
    }

    [HttpPost]
    [Authorize(Policy = PxaPermissions.UsersCreate)]
    [PxaValidateAntiforgery]
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
        if (await userManager.FindByEmailAsync(email) is not null)
            return ConflictProblem("A PXA account with this email address already exists.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new PxaIdentityUser
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
        var actionUrl = $"{mailOptions.AdminBaseUrl.TrimEnd('/')}/accept-invitation?token={Uri.EscapeDataString(issued.RawToken)}";
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
