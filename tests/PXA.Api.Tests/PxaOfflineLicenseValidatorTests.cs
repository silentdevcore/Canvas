using System.Diagnostics.Metrics;
using System.Text.Json;
using PXA.Domain.Entities;
using PXA.WebApi.Observability;
using PXA.WebApi.Services.Licensing;

namespace PXA.Api.Tests;

public sealed class PxaOfflineLicenseValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_enterprise_license_is_accepted()
    {
        var scenario = CreateScenario();

        var result = scenario.Validator.Validate(
            scenario.License, scenario.Subscription, Now, "1.2.3", "customer-prod", 2);

        Assert.True(result.Valid);
        Assert.True(result.SignatureValid);
        Assert.Equal("PXA_LICENSE_VALID", result.Code);
    }

    [Theory]
    [InlineData("version", "PXA_LICENSE_VERSION_INCOMPATIBLE")]
    [InlineData("deployment", "PXA_LICENSE_DEPLOYMENT_MISMATCH")]
    [InlineData("instances", "PXA_LICENSE_INSTANCE_LIMIT_EXCEEDED")]
    [InlineData("future", "PXA_LICENSE_NOT_YET_VALID")]
    [InlineData("expired", "PXA_LICENSE_EXPIRED")]
    [InlineData("revoked", "PXA_LICENSE_REVOKED")]
    [InlineData("record", "PXA_LICENSE_ENVELOPE_MISMATCH")]
    [InlineData("subscription", "PXA_LICENSE_SUBSCRIPTION_MISMATCH")]
    public void Invalid_license_conditions_return_stable_codes(string condition, string expectedCode)
    {
        var scenario = CreateScenario();
        var productVersion = "1.2.3";
        var deploymentId = "customer-prod";
        var activeInstances = 2;

        switch (condition)
        {
            case "version":
                productVersion = "2.0.0";
                break;
            case "deployment":
                deploymentId = "other-prod";
                break;
            case "instances":
                activeInstances = 4;
                break;
            case "future":
                scenario = CreateScenario(validFrom: Now.AddMinutes(1));
                break;
            case "expired":
                scenario = CreateScenario(validUntil: Now);
                break;
            case "revoked":
                scenario.License.Status = OfflineLicenseStatus.Revoked;
                break;
            case "record":
                scenario.License.InstanceLimit++;
                break;
            case "subscription":
                scenario.Subscription.DeploymentMode = SubscriptionDeploymentMode.Cloud;
                break;
        }

        var result = scenario.Validator.Validate(
            scenario.License, scenario.Subscription, Now, productVersion, deploymentId, activeInstances);

        Assert.False(result.Valid);
        Assert.True(result.SignatureValid);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public void Invalid_signature_is_rejected_before_envelope_data_is_trusted()
    {
        var outcomes = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "pxa.licensing.operations")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var outcome = tags.ToArray().Single(tag => tag.Key == "licensing.outcome").Value;
            outcomes.Add(Assert.IsType<string>(outcome));
        });
        listener.Start();
        var scenario = CreateScenario(signatureValid: false);
        scenario.License.EnvelopeJson = "{not-json";

        var result = scenario.Validator.Validate(scenario.License, scenario.Subscription, Now);

        Assert.False(result.Valid);
        Assert.False(result.SignatureValid);
        Assert.Equal("PXA_LICENSE_SIGNATURE_INVALID", result.Code);
        Assert.Null(result.Envelope);
        Assert.Contains("signature_invalid", outcomes);
    }

    [Fact]
    public void Signed_malformed_envelope_returns_stable_diagnostic()
    {
        var scenario = CreateScenario();
        scenario.License.EnvelopeJson = "{not-json";

        var result = scenario.Validator.Validate(scenario.License, scenario.Subscription, Now);

        Assert.False(result.Valid);
        Assert.True(result.SignatureValid);
        Assert.Equal("PXA_LICENSE_MALFORMED", result.Code);
    }

    [Fact]
    public void Schema_one_remains_valid_until_version_or_deployment_binding_is_required()
    {
        var scenario = CreateScenario();
        scenario.License.EnvelopeJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            licenseId = scenario.License.Id,
            licenseNumber = scenario.License.LicenseNumber,
            organizationId = scenario.License.OrganizationId,
            organizationName = "Test Customer",
            edition = scenario.Subscription.Edition.ToString(),
            accountType = scenario.Subscription.AccountType.ToString(),
            deploymentMode = scenario.Subscription.DeploymentMode.ToString(),
            validFrom = scenario.License.ValidFrom,
            validUntil = scenario.License.ValidUntil,
            instanceLimit = scenario.License.InstanceLimit,
            entitlements = Array.Empty<object>(),
            issuedAt = scenario.License.IssuedAt,
        });

        Assert.True(scenario.Validator.Validate(
            scenario.License, scenario.Subscription, Now).Valid);
        Assert.Equal(
            "PXA_LICENSE_SCHEMA_UPGRADE_REQUIRED",
            scenario.Validator.Validate(
                scenario.License, scenario.Subscription, Now, expectedProductVersion: "1.2.3").Code);
    }

    private static Scenario CreateScenario(
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null,
        bool signatureValid = true)
    {
        var subscription = new OrganizationSubscription
        {
            Edition = SubscriptionEdition.Enterprise,
            AccountType = SubscriptionAccountType.Company,
            DeploymentMode = SubscriptionDeploymentMode.Hybrid,
            BillingPeriod = SubscriptionBillingPeriod.Annual,
            Status = SubscriptionStatus.Active,
        };
        var license = new OfflineLicense
        {
            OrganizationId = subscription.OrganizationId,
            SubscriptionId = subscription.Id,
            LicenseNumber = "PXA-UNIT-0001",
            EnvelopeJson = string.Empty,
            Signature = "test-signature",
            KeyId = "test-key",
            Algorithm = "ECDSA_P256_SHA256",
            ValidFrom = validFrom ?? Now.AddDays(-1),
            ValidUntil = validUntil ?? Now.AddDays(30),
            InstanceLimit = 3,
            IssuedByUserId = Guid.NewGuid(),
            IssuedAt = Now.AddDays(-2),
        };
        var envelope = new PxaOfflineLicenseEnvelope(
            2,
            license.Id,
            license.LicenseNumber,
            license.OrganizationId,
            "Test Customer",
            subscription.Edition.ToString(),
            subscription.AccountType.ToString(),
            subscription.DeploymentMode.ToString(),
            license.ValidFrom,
            license.ValidUntil,
            license.InstanceLimit,
            "1.2.3",
            "customer-prod",
            [],
            license.IssuedAt);
        license.EnvelopeJson = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signingService = new TestSigningService(signatureValid);
        return new Scenario(
            license,
            subscription,
            new PxaOfflineLicenseValidator(signingService));
    }

    private sealed record Scenario(
        OfflineLicense License,
        OrganizationSubscription Subscription,
        PxaOfflineLicenseValidator Validator);

    private sealed class TestSigningService(bool signatureValid) : IPxaLicenseSignatureVerifier
    {
        public bool Verify(string envelopeJson, string signature) => signatureValid;
        public string PublicKeyPem => string.Empty;
        public string KeyId => "test-key";
    }
}
