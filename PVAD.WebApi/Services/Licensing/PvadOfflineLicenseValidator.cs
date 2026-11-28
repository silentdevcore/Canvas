using System.Diagnostics;
using System.Text.Json;
using PVAD.Domain.Entities;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Services.Licensing;

public sealed class PvadOfflineLicenseValidator(IPvadLicenseSignatureVerifier signatureVerifier)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PvadLicenseValidationResult Validate(
        OfflineLicense license,
        OrganizationSubscription subscription,
        DateTimeOffset now,
        string? expectedProductVersion = null,
        string? expectedDeploymentId = null,
        int activeInstances = 1)
    {
        var stopwatch = Stopwatch.StartNew();
        PvadLicenseValidationResult Complete(PvadLicenseValidationResult result)
        {
            PvadTelemetry.RecordLicensingOperation(
                "offline_validation",
                NormalizeOutcome(result.Code),
                stopwatch.Elapsed);
            return result;
        }

        try
        {
            if (!string.Equals(license.Algorithm, "ECDSA_P256_SHA256", StringComparison.Ordinal) ||
                !string.Equals(license.KeyId, signatureVerifier.KeyId, StringComparison.Ordinal))
                return Complete(Invalid("PVAD_LICENSE_SIGNING_METADATA_INVALID"));

            if (!signatureVerifier.Verify(license.EnvelopeJson, license.Signature))
                return Complete(Invalid("PVAD_LICENSE_SIGNATURE_INVALID"));

            PvadOfflineLicenseEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<PvadOfflineLicenseEnvelope>(license.EnvelopeJson, JsonOptions);
            }
            catch (JsonException)
            {
                return Complete(Invalid("PVAD_LICENSE_MALFORMED", signatureValid: true));
            }

            if (envelope is null || envelope.SchemaVersion is not (1 or 2) ||
                envelope.LicenseId != license.Id ||
                envelope.OrganizationId != license.OrganizationId ||
                !string.Equals(envelope.LicenseNumber, license.LicenseNumber, StringComparison.Ordinal) ||
                envelope.ValidFrom != license.ValidFrom ||
                envelope.ValidUntil != license.ValidUntil ||
                envelope.InstanceLimit != license.InstanceLimit ||
                envelope.IssuedAt != license.IssuedAt)
                return Complete(Invalid("PVAD_LICENSE_ENVELOPE_MISMATCH", signatureValid: true, envelope));

            if (!Enum.TryParse<SubscriptionEdition>(envelope.Edition, true, out var edition) ||
                !Enum.TryParse<SubscriptionAccountType>(envelope.AccountType, true, out var accountType) ||
                !Enum.TryParse<SubscriptionDeploymentMode>(envelope.DeploymentMode, true, out var deploymentMode) ||
                edition != subscription.Edition ||
                accountType != subscription.AccountType ||
                deploymentMode != subscription.DeploymentMode ||
                subscription.Id != license.SubscriptionId)
                return Complete(Invalid("PVAD_LICENSE_SUBSCRIPTION_MISMATCH", signatureValid: true, envelope));

            if (edition != SubscriptionEdition.Enterprise ||
                deploymentMode is not (SubscriptionDeploymentMode.OnPremise or SubscriptionDeploymentMode.Hybrid))
                return Complete(Invalid("PVAD_LICENSE_DEPLOYMENT_NOT_ALLOWED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Revoked)
                return Complete(Invalid("PVAD_LICENSE_REVOKED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Replaced)
                return Complete(Invalid("PVAD_LICENSE_REPLACED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Expired || envelope.ValidUntil <= now)
                return Complete(Invalid("PVAD_LICENSE_EXPIRED", signatureValid: true, envelope));
            if (envelope.ValidFrom > now)
                return Complete(Invalid("PVAD_LICENSE_NOT_YET_VALID", signatureValid: true, envelope));
            if (license.Status != OfflineLicenseStatus.Active)
                return Complete(Invalid("PVAD_LICENSE_INACTIVE", signatureValid: true, envelope));
            if (envelope.InstanceLimit < 1 ||
                envelope.SchemaVersion == 2 &&
                (string.IsNullOrWhiteSpace(envelope.ProductVersion) ||
                 string.IsNullOrWhiteSpace(envelope.DeploymentId)))
                return Complete(Invalid("PVAD_LICENSE_MALFORMED", signatureValid: true, envelope));
            if (envelope.SchemaVersion == 1 &&
                (expectedProductVersion is not null || expectedDeploymentId is not null))
                return Complete(Invalid("PVAD_LICENSE_SCHEMA_UPGRADE_REQUIRED", signatureValid: true, envelope));
            if (expectedProductVersion is not null &&
                !string.Equals(envelope.ProductVersion, expectedProductVersion, StringComparison.OrdinalIgnoreCase))
                return Complete(Invalid("PVAD_LICENSE_VERSION_INCOMPATIBLE", signatureValid: true, envelope));
            if (expectedDeploymentId is not null &&
                !string.Equals(envelope.DeploymentId, expectedDeploymentId, StringComparison.Ordinal))
                return Complete(Invalid("PVAD_LICENSE_DEPLOYMENT_MISMATCH", signatureValid: true, envelope));
            if (activeInstances < 1 || activeInstances > envelope.InstanceLimit)
                return Complete(Invalid("PVAD_LICENSE_INSTANCE_LIMIT_EXCEEDED", signatureValid: true, envelope));

            return Complete(new PvadLicenseValidationResult(true, true, "PVAD_LICENSE_VALID", envelope));
        }
        catch (PvadLicenseVerificationUnavailableException)
        {
            return Complete(Invalid(PvadLicenseVerificationUnavailableException.Code));
        }
        catch
        {
            PvadTelemetry.RecordLicensingOperation(
                "offline_validation",
                "failed",
                stopwatch.Elapsed);
            throw;
        }
    }

    private static PvadLicenseValidationResult Invalid(
        string code,
        bool signatureValid = false,
        PvadOfflineLicenseEnvelope? envelope = null) =>
        new(false, signatureValid, code, envelope);

    private static string NormalizeOutcome(string code) => code switch
    {
        "PVAD_LICENSE_VALID" => "valid",
        "PVAD_LICENSE_SIGNING_METADATA_INVALID" or
        "PVAD_LICENSE_SIGNATURE_INVALID" => "signature_invalid",
        "PVAD_LICENSE_MALFORMED" or
        "PVAD_LICENSE_ENVELOPE_MISMATCH" => "malformed",
        "PVAD_LICENSE_EXPIRED" => "expired",
        "PVAD_LICENSE_REVOKED" => "revoked",
        "PVAD_LICENSE_REPLACED" => "replaced",
        "PVAD_LICENSE_NOT_YET_VALID" or
        "PVAD_LICENSE_INACTIVE" => "inactive",
        "PVAD_LICENSE_INSTANCE_LIMIT_EXCEEDED" => "limit_exceeded",
        PvadLicenseVerificationUnavailableException.Code => "failed",
        _ => "incompatible",
    };
}

public sealed record PvadLicenseValidationResult(
    bool Valid,
    bool SignatureValid,
    string Code,
    PvadOfflineLicenseEnvelope? Envelope)
{
}
