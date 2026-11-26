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
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/pvad/v1/account/native-licenses")]
public sealed class AccountNativeLicensesController(PvadDbContext db, IPvadTenantContext tenant, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NativeLicensePage>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var access = await ReadAccessAsync(cancellationToken);
        if (access is null) return Forbid();
        try { PvadNativeLicenseRecords.ValidatePagination(page, pageSize); }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
        var query = OwnLicenses(access);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(value => value.IssuedAt).ThenByDescending(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var issuance = db.NativeLicenseRegistrationJobs.AsNoTracking().Where(value => value.OwnerUserId == access.UserId ||
            (access.OrganizationId != null && value.OrganizationId == access.OrganizationId));
        return Ok(new NativeLicensePage(rows.Select(PvadNativeLicenseIssuanceService.Summary).ToArray(), total, page, pageSize)
        {
            AutomaticIssuancePending = await issuance.AnyAsync(value => value.Status == NativeLicenseRegistrationStatus.Pending || value.Status == NativeLicenseRegistrationStatus.Processing, cancellationToken),
            AutomaticIssuanceNeedsReview = await issuance.AnyAsync(value => value.Status == NativeLicenseRegistrationStatus.DeadLetter, cancellationToken),
        });
    }

    [HttpGet("{licenseId:guid}")]
    public async Task<ActionResult<NativeLicenseDetails>> Get(Guid licenseId, CancellationToken cancellationToken)
    {
        var access = await ReadAccessAsync(cancellationToken);
        if (access is null) return Forbid();
        var row = await OwnLicenses(access).SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken);
        if (row is null) return NotFound();
        try
        {
            var payload = PvadNativeLicenseRecords.ReadStoredPayload(row);
            var successor = await OwnLicenses(access).Where(value => value.PreviousLicenseId == licenseId)
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
        var access = await ReadAccessAsync(cancellationToken);
        if (access is null) return Forbid();
        var row = await OwnLicenses(access).SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken);
        if (row is null) return NotFound();
        try
        {
            PvadNativeLicenseRecords.ReadStoredPayload(row);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(Encoding.UTF8.GetBytes(row.ArtifactJson), "application/json", $"PVAD-native-{row.Id:N}.json");
        }
        catch (PvadNativeIssuanceException error) { return Failure(error); }
    }

    private sealed record LicenseAccess(Guid UserId, Guid? OrganizationId);

    private IQueryable<NativeLibraryLicense> OwnLicenses(LicenseAccess access) => db.NativeLibraryLicenses.AsNoTracking()
        .Where(value => value.OwnerUserId == access.UserId ||
            (access.OrganizationId != null && value.OrganizationId == access.OrganizationId));

    private async Task<LicenseAccess?> ReadAccessAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || tenant.UserId is not { } userId ||
            User.HasClaim(claim => claim.Type == PvadClaimTypes.ServiceAccount) ||
            !PvadSessionService.TryGetSessionId(User, out var sessionId)) return null;
        var now = clock.GetUtcNow();
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive && user.EmailConfirmed &&
                (user.LockoutEnd == null || user.LockoutEnd <= now), cancellationToken) ||
            !await db.UserSessions.AsNoTracking().AnyAsync(session => session.Id == sessionId && session.UserId == userId &&
                session.OrganizationId == tenant.OrganizationId && session.RevokedAt == null && session.CreatedAt <= now &&
                session.ExpiresAt > now, cancellationToken)) return null;
        var hasWorkspaceMembership = tenant.OrganizationId is { } organizationId &&
            await (from membership in db.OrganizationMemberships.AsNoTracking()
                join organization in db.Organizations.AsNoTracking() on membership.OrganizationId equals organization.Id
                where membership.UserId == userId && membership.OrganizationId == organizationId &&
                    membership.Status == OrganizationMembershipStatus.Active && organization.Status == OrganizationStatus.Active
                select membership.Id).AnyAsync(cancellationToken);
        Guid? readableOrganization = hasWorkspaceMembership && User.HasClaim(PvadClaimTypes.Permission, PvadAccountPermissions.LicensesRead)
            ? tenant.OrganizationId : null;
        // Personal ownership survives workspace changes and membership removal.
        // Never compare a nullable organization directly without a non-null guard.
        if (tenant.OrganizationId is not null && !hasWorkspaceMembership &&
            !await db.NativeLibraryLicenses.AsNoTracking().AnyAsync(value => value.OwnerUserId == userId, cancellationToken) &&
            !await db.NativeLicenseRegistrationJobs.AsNoTracking().AnyAsync(value => value.OwnerUserId == userId, cancellationToken)) return null;
        return new(userId, readableOrganization);
    }

    private ObjectResult Failure(PvadNativeIssuanceException error) => Problem(statusCode: error.StatusCode,
        title: "Native license unavailable", detail: "The native license could not be loaded.",
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
