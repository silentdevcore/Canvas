using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Services.Licensing;

public sealed record IssueNativeLicenseRequest(Guid RequestId, Guid? OrganizationId, string CustomerReference,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PvadLicenseModel>))] PvadLicenseModel Model,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, DateTimeOffset? UpdatesUntil,
    bool RedistributionAllowed, PvadLicenseGrant[] Grants, string ReasonCode)
{
    public PvadLicenseAccountScope? AccountScope { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OwnerUserId { get; init; }
}
public sealed record RenewNativeLicenseRequest(Guid RequestId, DateTimeOffset? ValidUntil,
    DateTimeOffset? UpdatesUntil, string ReasonCode);
public sealed record NativeLicenseResponse(Guid Id, Guid? OrganizationId, Guid? PreviousLicenseId, string Model,
    DateTimeOffset IssuedAt, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil, DateTimeOffset? UpdatesUntil,
    string ArtifactSha256)
{
    public Guid? OwnerUserId { get; init; }
}

public sealed class PvadNativeLicenseIssuanceService
{
    private readonly PvadDbContext db;
    private readonly IPvadNativeLicenseIssuerSigner signer;
    private readonly IOptions<PvadNativeIssuerOptions> options;
    private readonly IOptions<PvadStepUpOptions> stepUp;
    private readonly PvadSystemOperatorAccess operatorAccess;
    private readonly TimeProvider clock;

    internal PvadNativeLicenseIssuanceService(PvadDbContext db, IPvadNativeLicenseIssuerSigner signer,
        IOptions<PvadNativeIssuerOptions> options, IOptions<PvadStepUpOptions> stepUp,
        PvadSystemOperatorAccess operatorAccess, TimeProvider clock)
    {
        this.db = db; this.signer = signer; this.options = options; this.stepUp = stepUp;
        this.operatorAccess = operatorAccess; this.clock = clock;
    }
    internal async Task<Guid> RequireIssuerAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) throw Error("PVAD_NATIVE_ISSUANCE_DISABLED", 403);
        if (principal.Identity?.IsAuthenticated != true || !principal.IsInRole(PvadRoles.SystemAdministrator) ||
            !principal.HasClaim(PvadClaimTypes.Permission, PvadPermissions.LicensesManage) ||
            principal.HasClaim(claim => claim.Type == PvadClaimTypes.ServiceAccount) ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !options.Value.AllowedUserIds.Contains(userId) ||
            !PvadSessionService.TryGetSessionId(principal, out var sessionId))
            throw Error("PVAD_NATIVE_ISSUER_FORBIDDEN", 403);
        var now = clock.GetUtcNow();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.EmailConfirmed || user.LockoutEnd > now || !operatorAccess.IsAuthorized(user) ||
            !await (from assignment in db.UserRoles join role in db.Roles on assignment.RoleId equals role.Id
                where assignment.UserId == userId && role.Name == PvadRoles.SystemAdministrator && role.OrganizationId == null
                select assignment).AnyAsync(cancellationToken) ||
            !await db.UserSessions.AsNoTracking().AnyAsync(session => session.Id == sessionId && session.UserId == userId &&
                session.RevokedAt == null && session.ExpiresAt > now && session.CreatedAt <= now &&
                session.CreatedAt >= now.AddMinutes(-stepUp.Value.MaximumAuthenticationAgeMinutes), cancellationToken))
            throw Error("PVAD_NATIVE_ISSUER_FORBIDDEN", 403);
        return userId;
    }

    public async Task<NativeLicenseResponse> IssueAsync(ClaimsPrincipal principal, IssueNativeLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireIssuerAsync(principal, cancellationToken);
        ValidateRequest(request.RequestId, request.ReasonCode);
        var now = PvadLicenseTimestamps.Normalize(clock.GetUtcNow());
        if ((request.OrganizationId is null) == (request.OwnerUserId is null) ||
            request.OrganizationId == Guid.Empty || request.OwnerUserId == Guid.Empty)
            throw Error("PVAD_NATIVE_OWNER_INVALID", 400);
        var accountPolicy = request.OwnerUserId is not null
            ? new PvadAccountPolicySnapshot(PvadLicenseAccountKind.IndividualDeveloper, [])
            : await PvadAccountPolicyService.ReadAsync(db, request.OrganizationId!.Value, cancellationToken);
        var requestedScope = request.AccountScope;
        if (accountPolicy is not null)
        {
            var accountType = accountPolicy.Kind == PvadLicenseAccountKind.Company ? "company" : "individualDeveloper";
            if (requestedScope is not null && requestedScope.AccountType != accountType)
                throw Error("PVAD_NATIVE_ISSUANCE_INVALID", 400);
            // This is the separately authorized manufacturer-admin path: selecting
            // explicit provider-bound Migration grants constitutes signed approval.
            // Future customer self-service must derive approvals from server records.
            requestedScope ??= new(accountType, PvadAccountLicensePolicy.Capabilities.Where(capability =>
                PvadAccountLicensePolicy.Evaluate(accountPolicy.Kind, capability.Id) == PvadAccountLicenseDecision.Allowed ||
                PvadAccountLicensePolicy.Evaluate(accountPolicy.Kind, capability.Id) == PvadAccountLicenseDecision.RequiresAdminApproval &&
                request.Grants?.Any(grant => grant?.Product == capability.Id) == true)
                .Select(capability => capability.Id).ToArray());
        }
        var payload = new PvadLicensePayload(requestedScope is null ? 1 : 2, Guid.NewGuid(), request.CustomerReference, request.Model, now,
            Timestamp(request.ValidFrom), Timestamp(request.ValidUntil), Timestamp(request.UpdatesUntil),
            request.RedistributionAllowed, request.Grants?.Select(grant => grant is null ? null! :
                new PvadLicenseGrant(grant.Product, grant.Providers?.ToArray()!)).ToArray()!)
        {
            AccountScope = requestedScope is { } scope
                ? new(scope.AccountType, scope.Capabilities?.ToArray()!) : null,
        };
        ValidatePayload(payload);
        if (payload.ValidUntil <= now) throw Error("PVAD_NATIVE_ISSUANCE_INVALID", 400);
        var canonicalRequest = request with { Grants = payload.Grants, ValidFrom = payload.ValidFrom,
            ValidUntil = payload.ValidUntil, UpdatesUntil = payload.UpdatesUntil, AccountScope = payload.AccountScope };
        var checksum = RequestHash(new { operation = "issue", request = canonicalRequest });
        if (request.OrganizationId is not null && payload.AccountScope?.AccountType == "individualDeveloper")
        {
            // Retained requests remain idempotent; only new private issuance
            // requires personal ownership. Existing artifacts are never reassigned.
            var retained = await FindRequestAsync(actor, request.RequestId, checksum, cancellationToken);
            if (retained is not null) return Summary(retained);
            throw Error("PVAD_NATIVE_PERSONAL_OWNER_REQUIRED", 400);
        }
        return await PersistAsync(actor, request.RequestId, checksum, request.OrganizationId, request.OwnerUserId, null, request.ReasonCode,
            payload, cancellationToken);
    }

    public async Task<NativeLicenseResponse> RenewAsync(ClaimsPrincipal principal, Guid licenseId,
        RenewNativeLicenseRequest request, CancellationToken cancellationToken)
    {
        var actor = await RequireIssuerAsync(principal, cancellationToken);
        ValidateRequest(request.RequestId, request.ReasonCode);
        var checksum = RequestHash(new { operation = "renew", licenseId, request });
        var existing = await FindRequestAsync(actor, request.RequestId, checksum, cancellationToken);
        if (existing is not null) return Summary(existing);
        var previous = await db.NativeLibraryLicenses.AsNoTracking().SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken)
            ?? throw Error("PVAD_NATIVE_LICENSE_NOT_FOUND", 404);
        PvadLicensePayload original;
        try
        {
            PvadNativeLicenseRecords.ReadStoredPayload(previous);
            if (Hash(previous.ArtifactJson) != previous.ArtifactSha256) throw new InvalidOperationException();
            original = signer.Verify(previous.ArtifactJson);
            if (original.LicenseId != previous.Id || original.CustomerReference != previous.CustomerReference ||
                original.Model.ToString() != previous.LicenseModel || original.ValidFrom != previous.ValidFrom ||
                original.ValidUntil != previous.ValidUntil || original.UpdatesUntil != previous.UpdatesUntil)
                throw new InvalidOperationException();
        }
        catch (Exception error) when (error is PvadLicenseException or InvalidOperationException)
        { throw Error("PVAD_NATIVE_LICENSE_UNVERIFIABLE", 409); }
        var now = PvadLicenseTimestamps.Normalize(clock.GetUtcNow());
        var until = Timestamp(request.ValidUntil);
        var updates = Timestamp(request.UpdatesUntil);
        if (original.Model == PvadLicenseModel.Trial ||
            (original.Model == PvadLicenseModel.Subscription && (until is null || until <= original.ValidUntil || until <= now || updates is not null)) ||
            (original.Model == PvadLicenseModel.Perpetual && (updates is null || updates <= original.UpdatesUntil || updates <= now || until is not null)))
            throw Error("PVAD_NATIVE_RENEWAL_INVALID", 400);
        var payload = original with { LicenseId = Guid.NewGuid(), IssuedAt = now, ValidUntil = until, UpdatesUntil = updates };
        ValidatePayload(payload);
        return await PersistAsync(actor, request.RequestId, checksum, previous.OrganizationId, previous.OwnerUserId, previous.Id,
            request.ReasonCode, payload, cancellationToken);
    }

    public async Task<NativeLibraryLicense> ReadAsync(ClaimsPrincipal principal, Guid licenseId, CancellationToken cancellationToken)
    {
        await RequireIssuerAsync(principal, cancellationToken);
        return await db.NativeLibraryLicenses.AsNoTracking().SingleOrDefaultAsync(value => value.Id == licenseId, cancellationToken)
            ?? throw Error("PVAD_NATIVE_LICENSE_NOT_FOUND", 404);
    }

    public async Task<NativeLicenseCapabilities> CapabilitiesAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return new(false, []);
        await RequireIssuerAsync(principal, cancellationToken);
        return new(true, PvadLicenseProducts.All.Select(product => new NativeLicenseProduct(product,
            MigrationProviderCatalog.All.Where(provider => $"migration.{provider.Domain}.{provider.MigrationKind}" == product && provider.ProviderKey != "auto")
                .Select(provider => provider.ProviderKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())).ToArray());
    }

    public async Task<NativeLicensePage> ListAsync(ClaimsPrincipal principal, Guid? organizationId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        await RequireIssuerAsync(principal, cancellationToken);
        PvadNativeLicenseRecords.ValidatePagination(page, pageSize);
        var query = db.NativeLibraryLicenses.AsNoTracking().Where(value => organizationId == null || value.OrganizationId == organizationId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(value => value.IssuedAt).ThenByDescending(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(Summary).ToArray(), total, page, pageSize);
    }

    public async Task<NativeLicenseDetails> DetailsAsync(ClaimsPrincipal principal, Guid licenseId, CancellationToken cancellationToken)
    {
        var row = await ReadAsync(principal, licenseId, cancellationToken);
        var payload = PvadNativeLicenseRecords.ReadStoredPayload(row);
        var successor = await db.NativeLibraryLicenses.AsNoTracking().Where(value => value.PreviousLicenseId == licenseId)
            .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(cancellationToken);
        return new(Summary(row), payload.CustomerReference, payload.RedistributionAllowed, payload.Grants, successor)
        { AccountScope = payload.AccountScope };
    }

    private async Task<NativeLicenseResponse> PersistAsync(Guid actor, Guid requestId, string requestHash,
        Guid? organizationId, Guid? ownerUserId, Guid? previousId, string reasonCode, PvadLicensePayload payload, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (organizationId is { } organization &&
                !await db.Organizations.AnyAsync(value => value.Id == organization && value.Status == OrganizationStatus.Active, cancellationToken))
                throw Error("PVAD_NATIVE_ORGANIZATION_UNAVAILABLE", 409);
            if (ownerUserId is { } owner &&
                !await db.Users.AnyAsync(value => value.Id == owner && value.IsActive && value.EmailConfirmed &&
                    (value.LockoutEnd == null || value.LockoutEnd <= clock.GetUtcNow()), cancellationToken))
                throw Error("PVAD_NATIVE_OWNER_UNAVAILABLE", 409);
            var existing = await FindRequestAsync(actor, requestId, requestHash, cancellationToken);
            if (existing is not null) return Summary(existing);
            if (previousId is not null && await db.NativeLibraryLicenses.AnyAsync(value => value.PreviousLicenseId == previousId, cancellationToken))
                throw Error("PVAD_NATIVE_LICENSE_ALREADY_RENEWED", 409);
            var artifact = signer.Sign(payload);
            var audit = new AuditEvent
            {
                OrganizationId = organizationId, ActorUserId = actor,
                Action = previousId is null ? "native-licenses.issue" : "native-licenses.renew",
                TargetType = "native-library-license", TargetId = payload.LicenseId.ToString(), Outcome = "succeeded",
                CreatedAt = payload.IssuedAt,
                DetailsJson = JsonSerializer.Serialize(new { PreviousLicenseId = previousId, ArtifactSha256 = Hash(artifact),
                    Model = payload.Model.ToString(), ReasonCode = reasonCode, OwnerUserId = ownerUserId }),
            };
            var license = new NativeLibraryLicense
            {
                Id = payload.LicenseId, OrganizationId = organizationId, OwnerUserId = ownerUserId, IssuedByUserId = actor, AuditEventId = audit.Id,
                RequestId = requestId, RequestSha256 = requestHash, PreviousLicenseId = previousId,
                ArtifactJson = artifact, ArtifactSha256 = Hash(artifact), LicenseModel = payload.Model.ToString(),
                CustomerReference = payload.CustomerReference, IssuedAt = payload.IssuedAt, ValidFrom = payload.ValidFrom,
                ValidUntil = payload.ValidUntil, UpdatesUntil = payload.UpdatesUntil,
            };
            db.AuditEvents.Add(audit); db.NativeLibraryLicenses.Add(license);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Summary(license);
        }
        catch (Exception error) when (error is DbUpdateConcurrencyException ||
            error.GetBaseException() is Npgsql.PostgresException { SqlState: "40001" or "40P01" } ||
            error.GetBaseException() is Npgsql.PostgresException { SqlState: "23505", SchemaName: "administration", TableName: "native_library_licenses" })
        { throw Error("PVAD_NATIVE_ISSUANCE_CONFLICT", 409); }
    }

    private async Task<NativeLibraryLicense?> FindRequestAsync(Guid actor, Guid requestId, string checksum, CancellationToken cancellationToken)
    {
        var existing = await db.NativeLibraryLicenses.AsNoTracking().SingleOrDefaultAsync(value =>
            value.IssuedByUserId == actor && value.RequestId == requestId, cancellationToken);
        if (existing is not null && existing.RequestSha256 != checksum) throw Error("PVAD_NATIVE_REQUEST_CONFLICT", 409);
        return existing;
    }

    private static void ValidateRequest(Guid id, string reason)
    {
        if (id == Guid.Empty || reason is not ("purchase" or "evaluation" or "renewal" or "correction"))
            throw Error("PVAD_NATIVE_ISSUANCE_INVALID", 400);
    }

    private static void ValidatePayload(PvadLicensePayload payload)
    {
        try { PvadLicenseVerifier.ValidatePayload(payload); }
        catch (PvadLicenseException) { throw Error("PVAD_NATIVE_ISSUANCE_INVALID", 400); }
        foreach (var grant in payload.Grants.Where(grant => PvadLicenseProducts.IsMigration(grant.Product)))
        foreach (var provider in grant.Providers)
            if (!MigrationProviderCatalog.All.Any(value => $"migration.{value.Domain}.{value.MigrationKind}" == grant.Product && value.ProviderKey == provider))
                throw Error("PVAD_NATIVE_PROVIDER_UNKNOWN", 400);
    }

    private static DateTimeOffset Timestamp(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw Error("PVAD_NATIVE_ISSUANCE_INVALID", 400);
        return PvadLicenseTimestamps.Normalize(value);
    }
    private static DateTimeOffset? Timestamp(DateTimeOffset? value) => value is null ? null : Timestamp(value.Value);
    private static string RequestHash<T>(T request) => Hash(JsonSerializer.Serialize(request, PvadLicenseJson.Options));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static PvadNativeIssuanceException Error(string code, int status) => new(code, status);
    internal static NativeLicenseResponse Summary(NativeLibraryLicense license) => new(license.Id, license.OrganizationId,
        license.PreviousLicenseId, license.LicenseModel, license.IssuedAt, license.ValidFrom, license.ValidUntil,
        license.UpdatesUntil, license.ArtifactSha256) { OwnerUserId = license.OwnerUserId };
}
