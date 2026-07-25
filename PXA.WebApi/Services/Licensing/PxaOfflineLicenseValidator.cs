using System.Text.Json;
using PXA.Domain.Entities;

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
        if (!string.Equals(license.Algorithm, "ECDSA_P256_SHA256", StringComparison.Ordinal) ||
            !string.Equals(license.KeyId, signatureVerifier.KeyId, StringComparison.Ordinal))
            return Invalid("PXA_LICENSE_SIGNING_METADATA_INVALID");

        if (!signatureVerifier.Verify(license.EnvelopeJson, license.Signature))
            return Invalid("PXA_LICENSE_SIGNATURE_INVALID");

        PxaOfflineLicenseEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PxaOfflineLicenseEnvelope>(license.EnvelopeJson, JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid("PXA_LICENSE_MALFORMED", signatureValid: true);
        }

        if (envelope is null || envelope.SchemaVersion is not (1 or 2) ||
            envelope.LicenseId != license.Id ||
            envelope.OrganizationId != license.OrganizationId ||
            !string.Equals(envelope.LicenseNumber, license.LicenseNumber, StringComparison.Ordinal) ||
            envelope.ValidFrom != license.ValidFrom ||
            envelope.ValidUntil != license.ValidUntil ||
            envelope.InstanceLimit != license.InstanceLimit ||
            envelope.IssuedAt != license.IssuedAt)
            return Invalid("PXA_LICENSE_ENVELOPE_MISMATCH", signatureValid: true, envelope);

        if (!Enum.TryParse<SubscriptionEdition>(envelope.Edition, true, out var edition) ||
            !Enum.TryParse<SubscriptionAccountType>(envelope.AccountType, true, out var accountType) ||
            !Enum.TryParse<SubscriptionDeploymentMode>(envelope.DeploymentMode, true, out var deploymentMode) ||
            edition != subscription.Edition ||
            accountType != subscription.AccountType ||
            deploymentMode != subscription.DeploymentMode ||
            subscription.Id != license.SubscriptionId)
            return Invalid("PXA_LICENSE_SUBSCRIPTION_MISMATCH", signatureValid: true, envelope);

        if (edition != SubscriptionEdition.Enterprise ||
            deploymentMode is not (SubscriptionDeploymentMode.OnPremise or SubscriptionDeploymentMode.Hybrid))
            return Invalid("PXA_LICENSE_DEPLOYMENT_NOT_ALLOWED", signatureValid: true, envelope);
        if (license.Status == OfflineLicenseStatus.Revoked)
            return Invalid("PXA_LICENSE_REVOKED", signatureValid: true, envelope);
        if (license.Status == OfflineLicenseStatus.Replaced)
            return Invalid("PXA_LICENSE_REPLACED", signatureValid: true, envelope);
        if (license.Status == OfflineLicenseStatus.Expired || envelope.ValidUntil <= now)
            return Invalid("PXA_LICENSE_EXPIRED", signatureValid: true, envelope);
        if (envelope.ValidFrom > now)
            return Invalid("PXA_LICENSE_NOT_YET_VALID", signatureValid: true, envelope);
        if (license.Status != OfflineLicenseStatus.Active)
            return Invalid("PXA_LICENSE_INACTIVE", signatureValid: true, envelope);
        if (envelope.InstanceLimit < 1 ||
            envelope.SchemaVersion == 2 &&
            (string.IsNullOrWhiteSpace(envelope.ProductVersion) ||
             string.IsNullOrWhiteSpace(envelope.DeploymentId)))
            return Invalid("PXA_LICENSE_MALFORMED", signatureValid: true, envelope);
        if (envelope.SchemaVersion == 1 &&
            (expectedProductVersion is not null || expectedDeploymentId is not null))
            return Invalid("PXA_LICENSE_SCHEMA_UPGRADE_REQUIRED", signatureValid: true, envelope);
        if (expectedProductVersion is not null &&
            !string.Equals(envelope.ProductVersion, expectedProductVersion, StringComparison.OrdinalIgnoreCase))
            return Invalid("PXA_LICENSE_VERSION_INCOMPATIBLE", signatureValid: true, envelope);
        if (expectedDeploymentId is not null &&
            !string.Equals(envelope.DeploymentId, expectedDeploymentId, StringComparison.Ordinal))
            return Invalid("PXA_LICENSE_DEPLOYMENT_MISMATCH", signatureValid: true, envelope);
        if (activeInstances < 1 || activeInstances > envelope.InstanceLimit)
            return Invalid("PXA_LICENSE_INSTANCE_LIMIT_EXCEEDED", signatureValid: true, envelope);

        return new PxaLicenseValidationResult(true, true, "PXA_LICENSE_VALID", envelope);
    }

    private static PxaLicenseValidationResult Invalid(
        string code,
        bool signatureValid = false,
        PxaOfflineLicenseEnvelope? envelope = null) =>
        new(false, signatureValid, code, envelope);
}

public sealed record PxaLicenseValidationResult(
    bool Valid,
    bool SignatureValid,
    string Code,
    PxaOfflineLicenseEnvelope? Envelope);
