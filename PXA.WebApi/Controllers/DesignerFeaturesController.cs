using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[Route("api/pxa/v1/designer/features")]
public sealed class DesignerFeaturesController(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    PxaDesignerProductMetadata metadata,
    IPxaDesignerFeatureGate featureGate) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DesignerFeatureResponse>>> List(
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();

        var responses = new List<DesignerFeatureResponse>(metadata.Features.Features.Count);
        foreach (var feature in metadata.Features.Features)
        {
            var decision = await featureGate.EvaluateAsync(
                organizationId, userId, feature.Id, cancellationToken);
            responses.Add(ToResponse(feature, decision));
        }

        return Ok(responses);
    }

    [HttpPut("{featureId}/preference")]
    public async Task<ActionResult<DesignerFeatureResponse>> SetPreference(
        string featureId,
        SetDesignerFeaturePreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        var feature = metadata.FindFeature(featureId);
        if (feature is null)
            return NotFound();
        if (!string.Equals(feature.Maturity, "alpha", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Feature preference is not supported",
                Detail = "Only Alpha features require a user opt-in.",
            });

        var policy = await dbContext.DesignerFeaturePolicies
            .SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId && value.FeatureId == feature.Id,
                cancellationToken);
        if (request.Enabled && policy?.AlphaOptInAllowed != true)
            return Forbid();

        var preference = await dbContext.DesignerFeaturePreferences
            .SingleOrDefaultAsync(value =>
                value.OrganizationId == organizationId &&
                value.UserId == userId &&
                value.FeatureId == feature.Id,
                cancellationToken);
        if (preference is null)
        {
            preference = new DesignerFeaturePreference
            {
                OrganizationId = organizationId,
                UserId = userId,
                FeatureId = feature.Id,
            };
            dbContext.DesignerFeaturePreferences.Add(preference);
        }

        preference.Enabled = request.Enabled;
        preference.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(organizationId, userId, "designer.features.preference-updated", feature.Id,
            new { request.Enabled });
        await dbContext.SaveChangesAsync(cancellationToken);
        var decision = await featureGate.EvaluateAsync(
            organizationId, userId, feature.Id, cancellationToken);
        return Ok(ToResponse(feature, decision));
    }

    [HttpPut("{featureId}/organization-policy")]
    [Authorize(Roles = $"{PxaRoles.SystemAdministrator},{PxaRoles.OrganizationAdministrator}")]
    public async Task<ActionResult<DesignerFeatureResponse>> SetOrganizationPolicy(
        string featureId,
        SetDesignerFeaturePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var organizationId, out var userId))
            return Unauthorized();
        var feature = metadata.FindFeature(featureId);
        if (feature is null)
            return NotFound();

        var policy = await dbContext.DesignerFeaturePolicies.SingleOrDefaultAsync(value =>
            value.OrganizationId == organizationId && value.FeatureId == feature.Id,
            cancellationToken);
        if (policy is null)
        {
            policy = new DesignerFeaturePolicy
            {
                OrganizationId = organizationId,
                FeatureId = feature.Id,
                UpdatedByUserId = userId,
            };
            dbContext.DesignerFeaturePolicies.Add(policy);
        }

        policy.AlphaOptInAllowed = request.AlphaOptInAllowed;
        policy.EnabledOverride = request.EnabledOverride;
        policy.UpdatedByUserId = userId;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        AddAudit(organizationId, userId, "designer.features.organization-policy-updated", feature.Id,
            request);
        await dbContext.SaveChangesAsync(cancellationToken);
        var decision = await featureGate.EvaluateAsync(
            organizationId, userId, feature.Id, cancellationToken);
        return Ok(ToResponse(feature, decision));
    }

    private bool TryGetContext(out Guid organizationId, out Guid userId)
    {
        organizationId = tenantContext.OrganizationId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return organizationId != Guid.Empty && userId != Guid.Empty;
    }

    private void AddAudit(Guid organizationId, Guid userId, string action, string featureId, object details) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = userId,
            Action = action,
            TargetType = "designer_feature",
            TargetId = featureId,
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private static DesignerFeatureResponse ToResponse(
        DesignerFeatureDefinition feature,
        DesignerFeatureDecision decision) =>
        new(feature.Id, feature.TitleKey, feature.DescriptionKey, feature.FallbackTitle,
            feature.FallbackDescription, feature.Maturity, feature.IntroducedIn,
            feature.NewUntilVersion, feature.DocumentationPath, decision.Enabled,
            decision.Code, decision.Reason);
}

public sealed record SetDesignerFeaturePreferenceRequest(bool Enabled);
public sealed record SetDesignerFeaturePolicyRequest(bool AlphaOptInAllowed, bool? EnabledOverride);
public sealed record DesignerFeatureResponse(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string FallbackTitle,
    string FallbackDescription,
    string Maturity,
    string IntroducedIn,
    string? NewUntilVersion,
    string DocumentationPath,
    bool Enabled,
    string DecisionCode,
    string DecisionReason);
