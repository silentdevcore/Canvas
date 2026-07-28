using System.Diagnostics;
using System.Text.Json;
using PXA.Domain.Entities;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Services.Licensing;

public sealed class PxaOfflineLicenseValidator(IPxaLicenseSignatureVerifier signatureVerifier)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PxaLicenseValidationResult Validate(
        OfflineLicense license,
        OrganizationSubscription subscription,
        DateTimeOffset now,
        string? expectedProductVersion = null,
        string? expectedDeploymentId = null,
        int activeInstances = 1)
    {
        var stopwatch = Stopwatch.StartNew();
        PxaLicenseValidationResult Complete(PxaLicenseValidationResult result)
        {
            PxaTelemetry.RecordLicensingOperation(
                "offline_validation",
                NormalizeOutcome(result.Code),
                stopwatch.Elapsed);
            return result;
        }

        try
        {
            if (!string.Equals(license.Algorithm, "ECDSA_P256_SHA256", StringComparison.Ordinal) ||
                !string.Equals(license.KeyId, signatureVerifier.KeyId, StringComparison.Ordinal))
                return Complete(Invalid("PXA_LICENSE_SIGNING_METADATA_INVALID"));

            if (!signatureVerifier.Verify(license.EnvelopeJson, license.Signature))
                return Complete(Invalid("PXA_LICENSE_SIGNATURE_INVALID"));

            PxaOfflineLicenseEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<PxaOfflineLicenseEnvelope>(license.EnvelopeJson, JsonOptions);
            }
            catch (JsonException)
            {
                return Complete(Invalid("PXA_LICENSE_MALFORMED", signatureValid: true));
            }

            if (envelope is null || envelope.SchemaVersion is not (1 or 2) ||
                envelope.LicenseId != license.Id ||
                envelope.OrganizationId != license.OrganizationId ||
                !string.Equals(envelope.LicenseNumber, license.LicenseNumber, StringComparison.Ordinal) ||
                envelope.ValidFrom != license.ValidFrom ||
                envelope.ValidUntil != license.ValidUntil ||
                envelope.InstanceLimit != license.InstanceLimit ||
                envelope.IssuedAt != license.IssuedAt)
                return Complete(Invalid("PXA_LICENSE_ENVELOPE_MISMATCH", signatureValid: true, envelope));

            if (!Enum.TryParse<SubscriptionEdition>(envelope.Edition, true, out var edition) ||
                !Enum.TryParse<SubscriptionAccountType>(envelope.AccountType, true, out var accountType) ||
                !Enum.TryParse<SubscriptionDeploymentMode>(envelope.DeploymentMode, true, out var deploymentMode) ||
                edition != subscription.Edition ||
                accountType != subscription.AccountType ||
                deploymentMode != subscription.DeploymentMode ||
                subscription.Id != license.SubscriptionId)
                return Complete(Invalid("PXA_LICENSE_SUBSCRIPTION_MISMATCH", signatureValid: true, envelope));

            if (edition != SubscriptionEdition.Enterprise ||
                deploymentMode is not (SubscriptionDeploymentMode.OnPremise or SubscriptionDeploymentMode.Hybrid))
                return Complete(Invalid("PXA_LICENSE_DEPLOYMENT_NOT_ALLOWED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Revoked)
                return Complete(Invalid("PXA_LICENSE_REVOKED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Replaced)
                return Complete(Invalid("PXA_LICENSE_REPLACED", signatureValid: true, envelope));
            if (license.Status == OfflineLicenseStatus.Expired || envelope.ValidUntil <= now)
                return Complete(Invalid("PXA_LICENSE_EXPIRED", signatureValid: true, envelope));
            if (envelope.ValidFrom > now)
                return Complete(Invalid("PXA_LICENSE_NOT_YET_VALID", signatureValid: true, envelope));
            if (license.Status != OfflineLicenseStatus.Active)
                return Complete(Invalid("PXA_LICENSE_INACTIVE", signatureValid: true, envelope));
            if (envelope.InstanceLimit < 1 ||
                envelope.SchemaVersion == 2 &&
                (string.IsNullOrWhiteSpace(envelope.ProductVersion) ||
                 string.IsNullOrWhiteSpace(envelope.DeploymentId)))
                return Complete(Invalid("PXA_LICENSE_MALFORMED", signatureValid: true, envelope));
            if (envelope.SchemaVersion == 1 &&
                (expectedProductVersion is not null || expectedDeploymentId is not null))
                return Complete(Invalid("PXA_LICENSE_SCHEMA_UPGRADE_REQUIRED", signatureValid: true, envelope));
            if (expectedProductVersion is not null &&
                !string.Equals(envelope.ProductVersion, expectedProductVersion, StringComparison.OrdinalIgnoreCase))
                return Complete(Invalid("PXA_LICENSE_VERSION_INCOMPATIBLE", signatureValid: true, envelope));
            if (expectedDeploymentId is not null &&
                !string.Equals(envelope.DeploymentId, expectedDeploymentId, StringComparison.Ordinal))
                return Complete(Invalid("PXA_LICENSE_DEPLOYMENT_MISMATCH", signatureValid: true, envelope));
            if (activeInstances < 1 || activeInstances > envelope.InstanceLimit)
                return Complete(Invalid("PXA_LICENSE_INSTANCE_LIMIT_EXCEEDED", signatureValid: true, envelope));

            return Complete(new PxaLicenseValidationResult(true, true, "PXA_LICENSE_VALID", envelope));
        }
        catch
        {
            PxaTelemetry.RecordLicensingOperation(
                "offline_validation",
                "failed",
                stopwatch.Elapsed);
            throw;
        }
    }

    private static PxaLicenseValidationResult Invalid(
        string code,
        bool signatureValid = false,
        PxaOfflineLicenseEnvelope? envelope = null) =>
        new(false, signatureValid, code, envelope);

    private static string NormalizeOutcome(string code) => code switch
    {
        "PXA_LICENSE_VALID" => "valid",
        "PXA_LICENSE_SIGNING_METADATA_INVALID" or
        "PXA_LICENSE_SIGNATURE_INVALID" => "signature_invalid",
        "PXA_LICENSE_MALFORMED" or
        "PXA_LICENSE_ENVELOPE_MISMATCH" => "malformed",
        "PXA_LICENSE_EXPIRED" => "expired",
        "PXA_LICENSE_REVOKED" => "revoked",
        "PXA_LICENSE_REPLACED" => "replaced",
        "PXA_LICENSE_NOT_YET_VALID" or
        "PXA_LICENSE_INACTIVE" => "inactive",
        "PXA_LICENSE_INSTANCE_LIMIT_EXCEEDED" => "limit_exceeded",
        _ => "incompatible",
    };
}

public sealed record PxaLicenseValidationResult(
    bool Valid,
    bool SignatureValid,
    string Code,
    PxaOfflineLicenseEnvelope? Envelope);
