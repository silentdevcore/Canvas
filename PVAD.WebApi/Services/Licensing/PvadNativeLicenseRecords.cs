using System.Security.Cryptography;
using System.Text;
using PVAD.Domain.Entities;
using PVAD.Licensing;

namespace PVAD.WebApi.Services.Licensing;

public sealed record NativeLicensePage(IReadOnlyList<NativeLicenseResponse> Items, int Total, int Page, int PageSize);
public sealed record NativeLicenseDetails(NativeLicenseResponse License, string CustomerReference,
    bool RedistributionAllowed, PvadLicenseGrant[] Grants, Guid? RenewedByLicenseId)
{
    public PvadLicenseAccountScope? AccountScope { get; init; }
}
public sealed record NativeLicenseProduct(string Id, IReadOnlyList<string> Providers);
public sealed record NativeLicenseCapabilities(bool Enabled, IReadOnlyList<NativeLicenseProduct> Products);

internal static class PvadNativeLicenseRecords
{
    internal static void ValidatePagination(int page, int pageSize)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            throw new PvadNativeIssuanceException("PVAD_NATIVE_PAGINATION_INVALID", 400);
    }

    // Artifacts are signature-verified before their append-only issuance transaction.
    // Downloads retain those exact bytes; native consumers still verify their signature.
    internal static PvadLicensePayload ReadStoredPayload(NativeLibraryLicense license)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(license.ArtifactJson);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != license.ArtifactSha256)
                throw new InvalidOperationException();
            var envelope = PvadLicenseJson.Read<PvadSignedNativeLicense>(bytes, PvadLicenseJson.MaximumEnvelopeBytes);
            if (envelope.SchemaVersion is not (1 or 2) || envelope.Algorithm != PvadLicenseJson.Algorithm)
                throw new InvalidOperationException();
            var payload = PvadLicenseJson.Read<PvadLicensePayload>(Convert.FromBase64String(envelope.Payload), PvadLicenseJson.MaximumPayloadBytes);
            PvadLicenseVerifier.ValidatePayload(payload);
            if (payload.SchemaVersion != envelope.SchemaVersion) throw new InvalidOperationException();
            if ((license.OrganizationId is null) == (license.OwnerUserId is null) ||
                license.OrganizationId == Guid.Empty || license.OwnerUserId == Guid.Empty ||
                license.OwnerUserId is not null && payload.AccountScope?.AccountType != "individualDeveloper")
                throw new InvalidOperationException();
            if (payload.LicenseId != license.Id || payload.CustomerReference != license.CustomerReference ||
                payload.Model.ToString() != license.LicenseModel || payload.IssuedAt != license.IssuedAt ||
                payload.ValidFrom != license.ValidFrom || payload.ValidUntil != license.ValidUntil || payload.UpdatesUntil != license.UpdatesUntil)
                throw new InvalidOperationException();
            return payload;
        }
        catch (Exception error) when (error is PvadLicenseException or InvalidOperationException or FormatException or ArgumentException)
        { throw new PvadNativeIssuanceException("PVAD_NATIVE_LICENSE_UNVERIFIABLE", 409); }
    }
}
