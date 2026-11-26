using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PvadAccountPermissions.LicensesRead)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pvad/v1/account/native-licenses")]
public sealed class AccountNativeLicensesController(PvadDbContext db, IPvadTenantContext tenant, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NativeLicensePage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!await HasWorkspaceAccessAsync(cancellationToken)) return Forbid();
        try { PvadNativeLicenseRecords.ValidatePagination(page, pageSize); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
        var query = OwnLicenses();
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(value => value.IssuedAt).ThenByDescending(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new NativeLicensePage(rows.Select(PvadNativeLicenseIssuanceService.Summary).ToArray(), total, page, pageSize));
    }

    [HttpGet("{licenseId:guid}")]
    public async Task<ActionResult<NativeLicenseDetails>> Get(Guid licenseId, CancellationToken cancellationToken)
    {
        if (!await HasWorkspaceAccessAsync(cancellationToken)) return Forbid();
        var row = await OwnLicenses().SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken);
        if (row is null) return NotFound();
        try
        {
            var payload = PvadNativeLicenseRecords.ReadStoredPayload(row);
            var successor = await OwnLicenses().Where(value => value.PreviousLicenseId == licenseId)
                .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(cancellationToken);
            // Customer references are manufacturer bookkeeping, not customer-facing metadata.
            return Ok(new NativeLicenseDetails(PvadNativeLicenseIssuanceService.Summary(row), string.Empty,
                payload.RedistributionAllowed, payload.Grants, successor) { AccountScope = payload.AccountScope });
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    [HttpGet("{licenseId:guid}/download")]
    public async Task<IActionResult> Download(Guid licenseId, CancellationToken cancellationToken)
    {
        if (!await HasWorkspaceAccessAsync(cancellationToken)) return Forbid();
        var row = await OwnLicenses().SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken);
        if (row is null) return NotFound();
        try
        {
            PvadNativeLicenseRecords.ReadStoredPayload(row);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(Encoding.UTF8.GetBytes(row.ArtifactJson), "application/json", $"PVAD-native-{row.Id:N}.json");
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    private IQueryable<NativeLibraryLicense> OwnLicenses() => db.NativeLibraryLicenses.AsNoTracking()
        .Where(value => value.OrganizationId == tenant.OrganizationId);

    private async Task<bool> HasWorkspaceAccessAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || tenant.UserId is not { } userId || tenant.OrganizationId is not { } organizationId ||
            User.HasClaim(claim => claim.Type == PvadClaimTypes.ServiceAccount) ||
            !PvadSessionService.TryGetSessionId(User, out var sessionId)) return false;
        var now = clock.GetUtcNow();
        return await (from membership in db.OrganizationMemberships.AsNoTracking()
            join organization in db.Organizations.AsNoTracking() on membership.OrganizationId equals organization.Id
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.UserId == userId && membership.OrganizationId == organizationId &&
                membership.Status == OrganizationMembershipStatus.Active && organization.Status == OrganizationStatus.Active &&
                user.IsActive && user.EmailConfirmed && (user.LockoutEnd == null || user.LockoutEnd <= now)
            select membership.Id).AnyAsync(cancellationToken) &&
            await db.UserSessions.AsNoTracking().AnyAsync(session => session.Id == sessionId && session.UserId == userId &&
                session.OrganizationId == organizationId && session.RevokedAt == null && session.CreatedAt <= now &&
                session.ExpiresAt > now, cancellationToken);
    }

    private ObjectResult Failure(PvadNativeIssuanceException error) => Problem(statusCode: error.StatusCode,
        title: "Native license unavailable", detail: "The native license could not be loaded.",
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
