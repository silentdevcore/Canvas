using Microsoft.EntityFrameworkCore;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.WebApi.Application.Designer;

public interface IPvadDesignerFeatureGate
{
    Task<DesignerFeatureDecision> EvaluateAsync(
        Guid organizationId,
        Guid userId,
        string featureId,
        CancellationToken cancellationToken = default);
}

public sealed class PvadDesignerFeatureGate(
    PvadDbContext dbContext,
    PvadDesignerProductMetadata metadata,
    IPvadEntitlementService entitlementService) : IPvadDesignerFeatureGate
{
    public async Task<DesignerFeatureDecision> EvaluateAsync(
        Guid organizationId,
        Guid userId,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        var feature = metadata.FindFeature(featureId);
        if (feature is null)
            return new(false, "PXA_DESIGNER_FEATURE_UNKNOWN", "The Designer feature is not registered.", null, null);

        var policy = await dbContext.DesignerFeaturePolicies.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId && value.FeatureId == feature.Id,
                cancellationToken);
        var preference = await dbContext.DesignerFeaturePreferences.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId &&
                value.UserId == userId &&
                value.FeatureId == feature.Id,
                cancellationToken);

        if (policy?.EnabledOverride == false)
            return new(false, "PXA_DESIGNER_FEATURE_DISABLED", "The feature is disabled for this organization.", feature, policy);

        if (!string.IsNullOrWhiteSpace(feature.RequiredEntitlement))
        {
            var entitlement = await entitlementService.EvaluateAsync(
                organizationId, feature.RequiredEntitlement, cancellationToken: cancellationToken);
            if (!entitlement.Allowed)
                return new(false, entitlement.Code, entitlement.Reason, feature, policy);
        }

        var maturity = feature.Maturity.ToLowerInvariant();
        if (maturity == "alpha")
        {
            if (policy?.AlphaOptInAllowed != true)
                return new(false, "PXA_DESIGNER_ALPHA_NOT_ALLOWED", "Alpha access is not allowed by the organization.", feature, policy);
            if (preference?.Enabled != true)
                return new(false, "PXA_DESIGNER_ALPHA_OPT_IN_REQUIRED", "Enable this Alpha feature before use.", feature, policy);

            return new(true, "PXA_DESIGNER_FEATURE_ENABLED", "The Alpha feature is enabled for this user.", feature, policy);
        }

        var enabled = policy?.EnabledOverride ?? feature.DefaultEnabled;
        return enabled
            ? new(true, "PXA_DESIGNER_FEATURE_ENABLED", "The feature is available.", feature, policy)
            : new(false, "PXA_DESIGNER_FEATURE_DISABLED", "The feature is disabled.", feature, policy);
    }
}

public sealed record DesignerFeatureDecision(
    bool Enabled,
    string Code,
    string Reason,
    DesignerFeatureDefinition? Feature,
    PVAD.Domain.Entities.DesignerFeaturePolicy? Policy)
{
    public string? CanonicalCode => PVAD.Core.Contracts.PvadDiagnosticAliases.CanonicalCode(Code);
}
