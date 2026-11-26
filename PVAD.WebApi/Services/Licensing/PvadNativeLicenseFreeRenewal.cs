using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Licensing;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Services.Licensing;

public sealed partial class PvadNativeLicenseIssuanceService
{
    // Only this server-derived workflow may turn a customer request into signing.
    // The controller has already checked the live browser session and owner access.
    internal async Task<NativeLicenseFreeRenewal?> FreeRenewalStatusAsync(NativeLibraryLicense previous,
        Guid requester, PvadNativeLicenseAutomationOptions policy, CancellationToken cancellationToken)
    {
        try
        {
            await ValidateFreeRenewalAsync(previous, requester, policy, cancellationToken);
            if (await db.NativeLibraryLicenses.AnyAsync(row => row.PreviousLicenseId == previous.Id, cancellationToken)) return null;
            return new(previous.ValidUntil!.Value, clock.GetUtcNow() >= previous.ValidUntil.Value);
        }
        catch (PvadNativeIssuanceException) { return null; }
    }

    internal async Task<NativeLicenseResponse> RenewFreeAsync(NativeLibraryLicense previous, Guid requester,
        PvadNativeLicenseAutomationOptions policy, CancellationToken cancellationToken)
    {
        var original = await ValidateFreeRenewalAsync(previous, requester, policy, cancellationToken);
        var actor = await RequireFreeRenewalIssuerAsync(policy, cancellationToken);
        var successor = await db.NativeLibraryLicenses.AsNoTracking()
            .SingleOrDefaultAsync(row => row.PreviousLicenseId == previous.Id, cancellationToken);
        if (successor is not null)
        {
            // Also supports a retry after the response was lost, including key/actor rotation.
            await ValidateFreeRenewalAsync(successor, requester, policy, cancellationToken);
            VerifyFreeArtifact(successor.ArtifactJson);
            return Summary(successor);
        }
        var now = PvadLicenseTimestamps.Normalize(clock.GetUtcNow());
        if (now < original.ValidUntil) throw Error("PVAD_NATIVE_FREE_RENEWAL_TOO_EARLY", 409);
        // ReadStoredPayload checks ledger consistency; verify the actual signature as well.
        VerifyFreeArtifact(previous.ArtifactJson);
        var currentPolicy = previous.OwnerUserId is not null
            ? new PvadAccountPolicySnapshot(PvadLicenseAccountKind.IndividualDeveloper, [])
            : await PvadAccountPolicyService.ReadAsync(db, previous.OrganizationId!.Value, cancellationToken)
                ?? throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var allowed = currentPolicy.AllowedCapabilities();
        var payload = original with
        {
            LicenseId = Guid.NewGuid(), IssuedAt = now, ValidFrom = now, ValidUntil = now.AddMonths(3),
            AccountScope = original.AccountScope! with
            { Capabilities = original.AccountScope.Capabilities.Intersect(allowed, StringComparer.Ordinal).ToArray() },
        };
        ValidatePayload(payload);
        // Stable predecessor-based identity: neither retries nor simultaneous customers
        // can append a second successor or shift the first successful term.
        var checksum = RequestHash(new { operation = "free-renewal", previous.Id, policy.PolicyVersion });
        return await PersistAsync(actor, previous.Id, checksum, previous.OrganizationId, previous.OwnerUserId, previous.Id,
            "free-renewal", payload, cancellationToken, policy.PolicyVersion, requester);
    }

    private async Task<PvadLicensePayload> ValidateFreeRenewalAsync(NativeLibraryLicense previous, Guid requester,
        PvadNativeLicenseAutomationOptions policy, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !policy.Enabled || !policy.FreeRenewalEnabled || !policy.IsValid() ||
            policy.Model != PvadLicenseModel.Subscription || policy.ValidityMonths != 3 || policy.ValidityDays is not null)
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var now = clock.GetUtcNow();
        if (!await db.Users.AnyAsync(user => user.Id == requester && user.IsActive && user.EmailConfirmed &&
                (user.LockoutEnd == null || user.LockoutEnd <= now), cancellationToken))
            throw Error("PVAD_NATIVE_FREE_RENEWAL_FORBIDDEN", 403);
        if (previous.OwnerUserId is { } owner ? owner != requester :
            !await db.OrganizationMemberships.AnyAsync(member => member.UserId == requester && member.OrganizationId == previous.OrganizationId &&
                member.Status == OrganizationMembershipStatus.Active, cancellationToken))
            throw Error("PVAD_NATIVE_FREE_RENEWAL_FORBIDDEN", 403);
        var ownerKey = previous.OwnerUserId is { } userId ? $"user:{userId:D}" : $"organization:{previous.OrganizationId:D}";
        var job = await db.NativeLicenseRegistrationJobs.AsNoTracking().SingleOrDefaultAsync(row => row.OwnerKey == ownerKey &&
            row.Status == NativeLicenseRegistrationStatus.Completed, cancellationToken);
        if (job?.ContractJson is null || job.LicenseId is null || job.OrganizationId != previous.OrganizationId || job.OwnerUserId != previous.OwnerUserId)
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var contract = JsonSerializer.Deserialize<PvadNativeAutomaticContract>(job.ContractJson, PvadLicenseJson.Options);
        if (contract is null || contract.PolicyVersion != policy.PolicyVersion || contract.Model != PvadLicenseModel.Subscription ||
            contract.ValidUntil != contract.ValidFrom.AddMonths(3) || contract.GracePeriodDays != 0)
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var source = await db.OrganizationSubscriptions.AsNoTracking().SingleOrDefaultAsync(row => row.Id == job.SourceSubscriptionId &&
            row.OrganizationId == job.SourceOrganizationId, cancellationToken);
        // The separate 30-day Cloud trial must not make the free native renewal
        // impossible after three months. Explicit suspension/cancellation still blocks it.
        if (source is null || source.AccountType != job.AccountType || source.CancellationEffectiveAt <= now ||
            !(source.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing ||
              source.Status == SubscriptionStatus.Expired && source.Edition == SubscriptionEdition.Trial && source.TrialEndsAt <= now) ||
            (job.AccountType == SubscriptionAccountType.IndividualDeveloper
                ? job.SourceUserId != requester || previous.OwnerUserId != requester
                : previous.OrganizationId != job.SourceOrganizationId ||
                  !await db.Organizations.AnyAsync(row => row.Id == previous.OrganizationId && row.Status == OrganizationStatus.Active, cancellationToken) ||
                  await PvadAccountPolicyService.ReadAsync(db, job.SourceOrganizationId, cancellationToken) is null))
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var payload = PvadNativeLicenseRecords.ReadStoredPayload(previous);
        var products = new[] { PvadLicenseProducts.Pdf, PvadLicenseProducts.Word, PvadLicenseProducts.Spreadsheet,
            PvadLicenseProducts.Importer, PvadLicenseProducts.Converter };
        if (payload.Model != PvadLicenseModel.Subscription || payload.GracePeriodDays != 0 || payload.RedistributionAllowed ||
            payload.ValidUntil != payload.ValidFrom.AddMonths(3) || payload.UpdatesUntil is not null ||
            payload.CustomerReference != "free-registration:" + ownerKey || payload.AccountScope?.AccountType !=
                (job.AccountType == SubscriptionAccountType.Company ? "company" : "individualDeveloper") ||
            payload.Grants.Length != products.Length || payload.Grants.Any(grant => !products.Contains(grant.Product) || grant.Providers.Length != 0))
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        var audit = await db.AuditEvents.AsNoTracking().SingleAsync(row => row.Id == previous.AuditEventId, cancellationToken);
        using var details = JsonDocument.Parse(audit.DetailsJson!);
        if (!details.RootElement.TryGetProperty("AutomationPolicyVersion", out var version) || version.GetString() != policy.PolicyVersion ||
            (previous.Id != job.LicenseId && (!details.RootElement.TryGetProperty("ReasonCode", out var reason) || reason.GetString() != "free-renewal")))
            throw Error("PVAD_NATIVE_FREE_RENEWAL_UNAVAILABLE", 409);
        return payload;
    }

    private void VerifyFreeArtifact(string artifact)
    {
        try { signer.Verify(artifact); }
        catch (PvadLicenseException) { throw Error("PVAD_NATIVE_LICENSE_UNVERIFIABLE", 409); }
    }

    private async Task<Guid> RequireFreeRenewalIssuerAsync(PvadNativeLicenseAutomationOptions policy, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == policy.IssuerUserId, cancellationToken);
        if (actor is null || !options.Value.AllowedUserIds.Contains(actor.Id) || !actor.IsActive || !actor.EmailConfirmed ||
            actor.LockoutEnd > now || !operatorAccess.IsAuthorized(actor) ||
            !await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id
                where assignment.UserId == actor.Id && role.Name == PvadRoles.SystemAdministrator && role.OrganizationId == null
                select assignment).AnyAsync(cancellationToken)) throw Error("PVAD_NATIVE_ISSUER_FORBIDDEN", 403);
        return actor.Id;
    }
}
