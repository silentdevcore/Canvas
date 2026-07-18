using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Infrastructure.Persistence.Identity;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/auth")]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<PxaIdentityUser> userManager;
    private readonly IUserClaimsPrincipalFactory<PxaIdentityUser> principalFactory;
    private readonly PxaDbContext dbContext;
    private readonly IAntiforgery antiforgery;

    public AuthController(
        UserManager<PxaIdentityUser> userManager,
        IUserClaimsPrincipalFactory<PxaIdentityUser> principalFactory,
        PxaDbContext dbContext,
        IAntiforgery antiforgery)
    {
        this.userManager = userManager;
        this.principalFactory = principalFactory;
        this.dbContext = dbContext;
        this.antiforgery = antiforgery;
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<CsrfResponse> GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfResponse(tokens.RequestToken!));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (!await HasValidCsrfTokenAsync())
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid CSRF token",
                detail: "Request a fresh CSRF token before submitting credentials.");
        }

        var identifier = request.Identifier.Trim();
        var user = identifier.Contains('@', StringComparison.Ordinal)
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);

        if (user is null || !user.IsActive || !user.EmailConfirmed)
            return InvalidCredentials();

        if (await userManager.IsLockedOutAsync(user))
        {
            return Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Account locked",
                detail: "Too many unsuccessful login attempts. Try again later.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = user.LastLoginAt.Value;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return IdentityFailure(updateResult);

        var principal = await CreatePrincipalAsync(user, null, cancellationToken);
        var properties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = request.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(request.RememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(8)),
        };

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);
        return Ok(new LoginResponse(await CreateUserInfoAsync(user, null, cancellationToken)));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfo>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
            return Unauthorized();

        var activeOrganizationId = Guid.TryParse(
            User.FindFirstValue(PxaClaimTypes.ActiveOrganization),
            out var parsedOrganizationId)
            ? parsedOrganizationId
            : (Guid?)null;
        return Ok(await CreateUserInfoAsync(user, activeOrganizationId, cancellationToken));
    }

    [Authorize]
    [HttpPost("switch-organization")]
    [PxaValidateAntiforgery]
    public async Task<ActionResult<LoginResponse>> SwitchOrganization(
        SwitchOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
            return Unauthorized();

        var principal = await CreatePrincipalAsync(user, request.OrganizationId, cancellationToken);
        var selectedOrganization = principal.FindFirstValue(PxaClaimTypes.ActiveOrganization);
        if (!Guid.TryParse(selectedOrganization, out var selectedOrganizationId) ||
            selectedOrganizationId != request.OrganizationId)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Organization access denied",
                detail: "The organization is unavailable to this administrator.");
        }

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });
        return Ok(new LoginResponse(
            await CreateUserInfoAsync(user, request.OrganizationId, cancellationToken)));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        if (!await HasValidCsrfTokenAsync())
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid CSRF token",
                detail: "Request a fresh CSRF token before ending the session.");
        }

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    private async Task<bool> HasValidCsrfTokenAsync()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private async Task<ClaimsPrincipal> CreatePrincipalAsync(
        PxaIdentityUser user,
        Guid? requestedOrganizationId,
        CancellationToken cancellationToken)
    {
        var principal = await principalFactory.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        var memberships = await GetActiveMembershipsAsync(user.Id, cancellationToken);

        foreach (var membership in memberships)
            identity.AddClaim(new Claim(PxaClaimTypes.Organization, membership.OrganizationId.ToString()));

        var activeMembership = requestedOrganizationId is null
            ? memberships.FirstOrDefault()
            : memberships.FirstOrDefault(value => value.OrganizationId == requestedOrganizationId);
        if (activeMembership is null && requestedOrganizationId is not null &&
            await userManager.IsInRoleAsync(user, PxaRoles.SystemAdministrator))
        {
            activeMembership = await GetSystemOrganizationAsync(requestedOrganizationId.Value, cancellationToken);
        }
        if (activeMembership is not null)
            identity.AddClaim(new Claim(PxaClaimTypes.ActiveOrganization, activeMembership.OrganizationId.ToString()));

        var roles = await GetSessionRolesAsync(user, activeMembership?.MembershipId, cancellationToken);
        foreach (var permission in roles
                     .SelectMany(role => PxaRoles.Permissions.GetValueOrDefault(role, []))
                     .Distinct(StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(PxaClaimTypes.Permission, permission));
        }

        return principal;
    }

    private async Task<UserInfo> CreateUserInfoAsync(
        PxaIdentityUser user,
        Guid? activeOrganizationId,
        CancellationToken cancellationToken)
    {
        var memberships = await GetActiveMembershipsAsync(user.Id, cancellationToken);
        var activeMembership = activeOrganizationId is null
            ? memberships.FirstOrDefault()
            : memberships.FirstOrDefault(value => value.OrganizationId == activeOrganizationId);
        if (activeMembership is null && activeOrganizationId is not null &&
            await userManager.IsInRoleAsync(user, PxaRoles.SystemAdministrator))
        {
            activeMembership = await GetSystemOrganizationAsync(activeOrganizationId.Value, cancellationToken);
            if (activeMembership is not null)
                memberships.Add(activeMembership);
        }
        var roles = await GetSessionRolesAsync(user, activeMembership?.MembershipId, cancellationToken);
        var organizations = memberships
            .Select(value => new OrganizationInfo(value.OrganizationId, value.OrganizationName, value.OrganizationSlug))
            .ToArray();

        return new UserInfo(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles,
            organizations,
            activeMembership?.OrganizationId,
            user.LastLoginAt);
    }

    private Task<List<ActiveMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        (from membership in dbContext.OrganizationMemberships.AsNoTracking()
         join organization in dbContext.Organizations.AsNoTracking()
             on membership.OrganizationId equals organization.Id
         where membership.UserId == userId &&
               membership.Status == OrganizationMembershipStatus.Active &&
               organization.Status == OrganizationStatus.Active
         orderby membership.CreatedAt
         select new ActiveMembership(
             membership.Id,
             organization.Id,
             organization.Name,
             organization.Slug))
        .ToListAsync(cancellationToken);

    private Task<ActiveMembership?> GetSystemOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.Organizations.AsNoTracking()
            .Where(organization =>
                organization.Id == organizationId && organization.Status == OrganizationStatus.Active)
            .Select(organization => new ActiveMembership(
                Guid.Empty,
                organization.Id,
                organization.Name,
                organization.Slug))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<string>> GetSessionRolesAsync(
        PxaIdentityUser user,
        Guid? membershipId,
        CancellationToken cancellationToken)
    {
        var globalRoles = await userManager.GetRolesAsync(user);
        var organizationRoles = membershipId is null
            ? []
            : await (from membershipRole in dbContext.OrganizationMembershipRoles.AsNoTracking()
                     join role in dbContext.Roles.AsNoTracking() on membershipRole.RoleId equals role.Id
                     where membershipRole.OrganizationMembershipId == membershipId
                     select role.Name!)
                .ToListAsync(cancellationToken);

        return globalRoles.Concat(organizationRoles)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private ObjectResult InvalidCredentials() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Invalid credentials",
        detail: "The supplied username or password is invalid.");

    private ObjectResult IdentityFailure(IdentityResult result) => Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "Identity update failed",
        detail: string.Join(" ", result.Errors.Select(error => error.Description)));

    private sealed record ActiveMembership(
        Guid MembershipId,
        Guid OrganizationId,
        string OrganizationName,
        string OrganizationSlug);
}

public sealed record CsrfResponse(string Token);

public sealed record LoginRequest(
    [Required] string Identifier,
    [Required] string Password,
    bool RememberMe = false);

public sealed record SwitchOrganizationRequest(Guid OrganizationId);

public sealed record LoginResponse(UserInfo User);

public sealed record UserInfo(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<OrganizationInfo> Organizations,
    Guid? ActiveOrganizationId,
    DateTimeOffset? LastLoginAt);

public sealed record OrganizationInfo(Guid Id, string Name, string Slug);
