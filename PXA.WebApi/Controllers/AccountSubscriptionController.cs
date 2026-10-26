using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Application.Legal;
using PXA.WebApi.Application.Subscriptions;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(Policy = PxaAccountPermissions.SubscriptionRead)]
[Route("api/pxa/v1/account/subscription")]
public sealed class AccountSubscriptionController : ControllerBase
{
    private readonly IPxaTenantContext tenantContext;
    private readonly SubscriptionQueryService queryService;
    private readonly PxaConsumerCheckoutLegalGate checkoutLegalGate;

    public AccountSubscriptionController(
        IPxaTenantContext tenantContext,
        SubscriptionQueryService queryService,
        PxaConsumerCheckoutLegalGate checkoutLegalGate)
    {
        this.tenantContext = tenantContext;
        this.queryService = queryService;
        this.checkoutLegalGate = checkoutLegalGate;
    }

    [HttpGet]
    public async Task<ActionResult<AccountSubscriptionResponse>> GetSubscription(CancellationToken cancellationToken)
    {
        var subscription = await ResolveSubscriptionAsync(cancellationToken);
        return subscription is null ? NotFound() : Ok(ToResponse(subscription));
    }

    [HttpGet("seats")]
    public async Task<ActionResult<IReadOnlyList<AccountSubscriptionSeatResponse>>> GetSeats(
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        if (organizationId is null)
            return MissingOrganization();
        var subscriptionId = await queryService.GetSubscriptionIdForOrganizationAsync(organizationId.Value, cancellationToken);
        if (subscriptionId is null)
            return NotFound();

        var seats = await queryService.GetSeatsAsync(subscriptionId.Value, organizationId.Value, cancellationToken);
        return Ok(seats.Select(seat => new AccountSubscriptionSeatResponse(
            seat.MembershipId, seat.UserId, seat.DisplayName, seat.Email, seat.MembershipStatus, seat.Assigned)).ToArray());
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<AccountSubscriptionHistoryResponse>>> GetHistory(
        CancellationToken cancellationToken)
    {
        var subscriptionId = await ResolveSubscriptionIdAsync(cancellationToken);
        if (subscriptionId is null)
            return NotFound();

        var history = await queryService.GetHistoryAsync(subscriptionId.Value, cancellationToken);
        return Ok(history.Select(entry => new AccountSubscriptionHistoryResponse(
            entry.Action, entry.PreviousStatus, entry.CurrentStatus, entry.ActorName, entry.CreatedAt)).ToArray());
    }

    [HttpGet("usage")]
    public async Task<ActionResult<AccountSubscriptionUsageResponse>> GetUsage(CancellationToken cancellationToken)
    {
        var subscriptionId = await ResolveSubscriptionIdAsync(cancellationToken);
        if (subscriptionId is null)
            return NotFound();

        var usage = await queryService.GetUsageAsync(subscriptionId.Value, cancellationToken);
        return Ok(new AccountSubscriptionUsageResponse(
            usage.PeriodStartsAt,
            usage.PeriodEndsAt,
            usage.TotalQuantity,
            usage.Items.Select(item => new AccountSubscriptionUsageItem(
                item.Capability, item.Operation, item.Quantity, item.EventCount, item.LastOccurredAt)).ToArray()));
    }

    [HttpGet("checkout-readiness")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PxaConsumerCheckoutReadiness>> GetCheckoutReadiness(
        [FromQuery] string locale = "de",
        CancellationToken cancellationToken = default) =>
        Ok(await checkoutLegalGate.EvaluateAsync(
            locale,
            DateTimeOffset.UtcNow,
            cancellationToken));

    private async Task<Guid?> ResolveSubscriptionIdAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId;
        return organizationId is null
            ? null
            : await queryService.GetSubscriptionIdForOrganizationAsync(organizationId.Value, cancellationToken);
    }

    private async Task<SubscriptionRecord?> ResolveSubscriptionAsync(CancellationToken cancellationToken)
    {
        var subscriptionId = await ResolveSubscriptionIdAsync(cancellationToken);
        return subscriptionId is null ? null : await queryService.GetSubscriptionAsync(subscriptionId.Value, cancellationToken);
    }

    private static AccountSubscriptionResponse ToResponse(SubscriptionRecord subscription) => new(
        subscription.Id, subscription.OrganizationName, subscription.Edition, subscription.AccountType,
        subscription.Status, subscription.BillingPeriod, subscription.DeploymentMode,
        subscription.SeatLimit, subscription.AssignedSeats, subscription.StartsAt, subscription.CurrentPeriodStartsAt,
        subscription.TrialEndsAt, subscription.CurrentPeriodEndsAt, subscription.CancellationEffectiveAt,
        subscription.GracePeriodEndsAt,
        subscription.Entitlements.Select(entitlement => new AccountEntitlementResponse(
            entitlement.Capability, entitlement.Enabled, entitlement.Limit, entitlement.Unit, entitlement.ExpiresAt)).ToArray());

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required",
        detail: "The authenticated session does not contain an active organization.");
}

public sealed record AccountSubscriptionResponse(
    Guid Id, string OrganizationName, string Edition, string AccountType, string Status,
    string BillingPeriod, string DeploymentMode, int? SeatLimit, int AssignedSeats,
    DateTimeOffset StartsAt, DateTimeOffset CurrentPeriodStartsAt, DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt, DateTimeOffset? CancellationEffectiveAt, DateTimeOffset? GracePeriodEndsAt,
    IReadOnlyList<AccountEntitlementResponse> Entitlements);

public sealed record AccountEntitlementResponse(
    string Capability, bool Enabled, long? Limit, string? Unit, DateTimeOffset? ExpiresAt);

public sealed record AccountSubscriptionSeatResponse(
    Guid MembershipId, Guid UserId, string DisplayName, string Email, string MembershipStatus, bool Assigned);

public sealed record AccountSubscriptionHistoryResponse(
    string Action, string? PreviousStatus, string CurrentStatus, string ActorName, DateTimeOffset CreatedAt);

public sealed record AccountSubscriptionUsageItem(
    string Capability, string Operation, long Quantity, int EventCount, DateTimeOffset LastOccurredAt);

public sealed record AccountSubscriptionUsageResponse(
    DateTimeOffset PeriodStartsAt, DateTimeOffset? PeriodEndsAt, long TotalQuantity,
    IReadOnlyList<AccountSubscriptionUsageItem> Items);
