using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Subscriptions;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/subscriptions")]
public sealed partial class AdminSubscriptionsController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly SubscriptionQueryService queryService;
    private readonly OrganizationNotificationService notifications;

    public AdminSubscriptionsController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        SubscriptionQueryService queryService,
        OrganizationNotificationService notifications)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.queryService = queryService;
        this.notifications = notifications;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.SubscriptionsRead)]
    public async Task<ActionResult<AdminSubscriptionPage>> GetSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? edition = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.OrganizationSubscriptions.AsNoTracking();
        if (!IsSystemAdministrator())
        {
            if (tenantContext.OrganizationId is not { } organizationId)
                return MissingOrganization();
            query = query.Where(value => value.OrganizationId == organizationId);
        }
        if (Enum.TryParse<SubscriptionStatus>(status, true, out var parsedStatus))
            query = query.Where(value => value.Status == parsedStatus);
        if (Enum.TryParse<SubscriptionEdition>(edition, true, out var parsedEdition))
            query = query.Where(value => value.Edition == parsedEdition);

        var total = await query.CountAsync(cancellationToken);
        var items = await (
                from subscription in query
                join organization in dbContext.Organizations.AsNoTracking()
                    on subscription.OrganizationId equals organization.Id
                orderby organization.Name
                select new AdminSubscriptionSummary(
                    subscription.Id,
                    organization.Id,
                    organization.Name,
                    subscription.Edition.ToString(),
                    subscription.AccountType.ToString(),
                    subscription.Status.ToString(),
                    subscription.BillingPeriod.ToString(),
                    subscription.DeploymentMode.ToString(),
                    subscription.SeatLimit,
                    dbContext.SubscriptionSeatAssignments.Count(seat =>
                        seat.SubscriptionId == subscription.Id && seat.RevokedAt == null),
                    subscription.TrialEndsAt,
                    subscription.CurrentPeriodEndsAt,
                    subscription.UpdatedAt))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(new AdminSubscriptionPage(items, page, pageSize, total));
    }

    [HttpGet("{subscriptionId:guid}")]
    [Authorize(Policy = PxaPermissions.SubscriptionsRead)]
    public async Task<ActionResult<AdminSubscriptionResponse>> GetSubscription(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var organizationId = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(value => value.Id == subscriptionId)
            .Select(value => (Guid?)value.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (organizationId is null || !CanAccess(organizationId.Value))
            return NotFound();
        var response = await BuildResponse(subscriptionId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscriptions.create")]
    public async Task<ActionResult<AdminSubscriptionResponse>> CreateSubscription(
        CreateAdminSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        if (tenantContext.UserId is not { } actorUserId)
            return Unauthorized();
        if (!await dbContext.Organizations.AnyAsync(value => value.Id == request.OrganizationId, cancellationToken))
            return NotFoundProblem("Organization does not exist.");
        var existingEdition = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(value => value.OrganizationId == request.OrganizationId)
            .Select(value => (SubscriptionEdition?)value.Edition)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingEdition == SubscriptionEdition.Trial)
            return TrialAlreadyClaimedProblem();
        if (existingEdition is not null)
            return ConflictProblem("The organization already has a subscription.");
        if (!TryParseRequest(request, out var values, out var validationError))
            return ValidationProblem(validationError);
        if (!SubscriptionEditionPolicy.TryValidateConfiguration(
                values.Edition, values.Status, values.BillingPeriod, values.DeploymentMode, out validationError))
            return ValidationProblem(validationError);

        var now = DateTimeOffset.UtcNow;
        var subscription = new OrganizationSubscription
        {
            OrganizationId = request.OrganizationId,
            Edition = values.Edition,
            AccountType = values.AccountType,
            Status = values.Status,
            BillingPeriod = values.BillingPeriod,
            DeploymentMode = values.DeploymentMode,
            SeatLimit = values.AccountType == SubscriptionAccountType.IndividualDeveloper ? 1 : request.SeatLimit,
            StartsAt = request.StartsAt ?? now,
            CurrentPeriodStartsAt = request.StartsAt ?? now,
            TrialEndsAt = values.Edition == SubscriptionEdition.Trial
                ? request.TrialEndsAt ?? (request.StartsAt ?? now).AddDays(30)
                : request.TrialEndsAt,
            CurrentPeriodEndsAt = request.CurrentPeriodEndsAt,
        };
        dbContext.OrganizationSubscriptions.Add(subscription);
        dbContext.SubscriptionEntitlements.AddRange(CreateEntitlements(subscription.Id, request.Entitlements, now));
        AddLifecycleEvent(subscription, actorUserId, "subscription.created", null, new { subscription.Edition, subscription.AccountType });
        AddAuditEvent(subscription, actorUserId, "subscriptions.create");
        await QueueSubscriptionNotification(
            subscription,
            $"subscription-created:{subscription.Id}",
            $"A {subscription.Edition} subscription was created with status {subscription.Status}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetSubscription), new { subscriptionId = subscription.Id },
            await BuildResponse(subscription.Id, cancellationToken));
    }

    [HttpPatch("{subscriptionId:guid}")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscriptions.update")]
    public async Task<ActionResult<AdminSubscriptionResponse>> UpdateSubscription(
        Guid subscriptionId,
        UpdateAdminSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        if (tenantContext.UserId is not { } actorUserId)
            return Unauthorized();
        var subscription = await dbContext.OrganizationSubscriptions.SingleOrDefaultAsync(
            value => value.Id == subscriptionId,
            cancellationToken);
        if (subscription is null)
            return NotFound();

        var previousStatus = subscription.Status;
        var previousEdition = subscription.Edition;
        if (!TryApplyUpdate(subscription, request, out var validationError))
            return ValidationProblem(validationError);
        if (!SubscriptionEditionPolicy.CanConvert(previousEdition, subscription.Edition))
            return ConflictProblem($"Conversion from {previousEdition} to {subscription.Edition} is not allowed.");
        if (subscription.Status != previousStatus &&
            !SubscriptionEditionPolicy.CanTransition(previousStatus, subscription.Status))
            return ConflictProblem($"Transition from {previousStatus} to {subscription.Status} is not allowed.");
        if (!SubscriptionEditionPolicy.TryValidateConfiguration(
                subscription.Edition,
                subscription.Status,
                subscription.BillingPeriod,
                subscription.DeploymentMode,
                out validationError))
            return ValidationProblem(validationError);
        var activeSeats = await dbContext.SubscriptionSeatAssignments.CountAsync(
            value => value.SubscriptionId == subscriptionId && value.RevokedAt == null,
            cancellationToken);
        if (subscription.SeatLimit is { } seatLimit && activeSeats > seatLimit)
            return ConflictProblem("The seat limit cannot be lower than the number of assigned seats.");

        if (request.Entitlements is not null)
        {
            var existing = await dbContext.SubscriptionEntitlements
                .Where(value => value.SubscriptionId == subscriptionId)
                .ToListAsync(cancellationToken);
            dbContext.SubscriptionEntitlements.RemoveRange(existing);
            dbContext.SubscriptionEntitlements.AddRange(
                CreateEntitlements(subscriptionId, request.Entitlements, DateTimeOffset.UtcNow));
        }
        else if (previousEdition == SubscriptionEdition.Trial &&
                 subscription.Edition != SubscriptionEdition.Trial)
        {
            var inheritedEntitlements = await dbContext.SubscriptionEntitlements
                .Where(value => value.SubscriptionId == subscriptionId &&
                                value.Source == EntitlementSource.EditionDefault)
                .ToListAsync(cancellationToken);
            foreach (var entitlement in inheritedEntitlements)
            {
                entitlement.ExpiresAt = null;
                entitlement.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        if (previousEdition == SubscriptionEdition.Trial &&
            subscription.Edition != SubscriptionEdition.Trial)
        {
            subscription.TrialEndsAt = null;
            subscription.CurrentPeriodStartsAt = DateTimeOffset.UtcNow;
        }
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        var action = subscription.Edition == previousEdition
            ? "subscription.updated"
            : "subscription.edition.converted";
        AddLifecycleEvent(subscription, actorUserId, action, previousStatus,
            new { PreviousEdition = previousEdition, subscription.Edition, subscription.Status, subscription.SeatLimit });
        AddAuditEvent(subscription, actorUserId, "subscriptions.update");
        await QueueSubscriptionNotification(
            subscription,
            $"subscription-updated:{subscription.Id}:{subscription.UpdatedAt.UtcDateTime.Ticks}",
            previousEdition == subscription.Edition
                ? $"Your {subscription.Edition} subscription settings changed. Current status: {subscription.Status}."
                : $"Your subscription changed from {previousEdition} to {subscription.Edition}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponse(subscriptionId, cancellationToken));
    }

    [HttpPost("{subscriptionId:guid}/seats/{membershipId:guid}")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscriptions.seat.assign")]
    public async Task<IActionResult> AssignSeat(Guid subscriptionId, Guid membershipId, CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        var subscription = await dbContext.OrganizationSubscriptions.FindAsync([subscriptionId], cancellationToken);
        if (subscription is null || tenantContext.UserId is not { } actorUserId)
            return NotFound();
        var membershipExists = await dbContext.OrganizationMemberships.AnyAsync(value =>
            value.Id == membershipId && value.OrganizationId == subscription.OrganizationId &&
            value.Status == OrganizationMembershipStatus.Active, cancellationToken);
        if (!membershipExists)
            return NotFoundProblem("An active membership in this organization is required.");
        var assignment = await dbContext.SubscriptionSeatAssignments.SingleOrDefaultAsync(value =>
            value.SubscriptionId == subscriptionId && value.OrganizationMembershipId == membershipId,
            cancellationToken);
        if (assignment?.RevokedAt is null && assignment is not null)
            return NoContent();
        var assignedCount = await dbContext.SubscriptionSeatAssignments.CountAsync(value =>
            value.SubscriptionId == subscriptionId && value.RevokedAt == null, cancellationToken);
        if (subscription.SeatLimit is { } seatLimit && assignedCount >= seatLimit)
            return ConflictProblem("The subscription seat limit has been reached.");
        if (assignment is null)
        {
            dbContext.SubscriptionSeatAssignments.Add(new SubscriptionSeatAssignment
            {
                SubscriptionId = subscriptionId,
                OrganizationMembershipId = membershipId,
                AssignedByUserId = actorUserId,
            });
        }
        else
        {
            assignment.RevokedAt = null;
            assignment.AssignedAt = DateTimeOffset.UtcNow;
            assignment.AssignedByUserId = actorUserId;
        }
        AddAuditEvent(subscription, actorUserId, "subscriptions.seat.assign");
        await notifications.QueueAdministratorsAsync(
            subscription.OrganizationId,
            "security.organization-changed",
            $"subscription-seat-assigned:{subscription.Id}:{membershipId}",
            new Dictionary<string, string>
            {
                ["summary"] = "A Designer subscription seat was assigned in your organization.",
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{subscriptionId:guid}/seats")]
    [Authorize(Policy = PxaPermissions.SubscriptionsRead)]
    public async Task<ActionResult<IReadOnlyList<AdminSubscriptionSeatResponse>>> GetSeats(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == subscriptionId, cancellationToken);
        if (subscription is null || !CanAccess(subscription.OrganizationId))
            return NotFound();
        var seats = await queryService.GetSeatsAsync(subscriptionId, subscription.OrganizationId, cancellationToken);
        return Ok(seats.Select(seat => new AdminSubscriptionSeatResponse(
            seat.MembershipId, seat.UserId, seat.DisplayName, seat.Email, seat.MembershipStatus, seat.Assigned)).ToArray());
    }

    [HttpGet("{subscriptionId:guid}/history")]
    [Authorize(Policy = PxaPermissions.SubscriptionsRead)]
    public async Task<ActionResult<IReadOnlyList<AdminSubscriptionHistoryResponse>>> GetHistory(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var organizationId = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(value => value.Id == subscriptionId)
            .Select(value => (Guid?)value.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
        if (organizationId is null || !CanAccess(organizationId.Value))
            return NotFound();
        var history = await queryService.GetHistoryAsync(subscriptionId, cancellationToken);
        return Ok(history.Select(entry => new AdminSubscriptionHistoryResponse(
            entry.Id, entry.Action, entry.PreviousStatus, entry.CurrentStatus,
            entry.ActorUserId, entry.ActorName, entry.CreatedAt)).ToArray());
    }

    [HttpGet("{subscriptionId:guid}/usage")]
    [Authorize(Policy = PxaPermissions.SubscriptionsRead)]
    public async Task<ActionResult<AdminSubscriptionUsageResponse>> GetUsage(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == subscriptionId, cancellationToken);
        if (subscription is null || !CanAccess(subscription.OrganizationId))
            return NotFound();
        var usage = await queryService.GetUsageAsync(subscriptionId, cancellationToken);
        return Ok(new AdminSubscriptionUsageResponse(
            usage.PeriodStartsAt,
            usage.PeriodEndsAt,
            usage.TotalQuantity,
            usage.Items.Select(item => new AdminSubscriptionUsageItem(
                item.Capability, item.Operation, item.Source, item.Quantity, item.EventCount, item.LastOccurredAt)).ToArray()));
    }

    [HttpPost("{subscriptionId:guid}/trial/extend")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscription.trial.extended")]
    public async Task<ActionResult<AdminSubscriptionResponse>> ExtendTrial(
        Guid subscriptionId,
        ExtendTrialRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > 365)
            return ValidationProblem("Trial extension must be between 1 and 365 days.");
        var subscription = await GetMutableSubscription(subscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound();
        if (subscription.Edition != SubscriptionEdition.Trial ||
            subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            return ConflictProblem("Only a current Trial subscription can be extended.");
        var previousEnd = subscription.TrialEndsAt;
        var baseline = previousEnd > DateTimeOffset.UtcNow ? previousEnd.Value : DateTimeOffset.UtcNow;
        subscription.TrialEndsAt = baseline.AddDays(request.Days);
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        RecordLifecycleMutation(subscription, "subscription.trial.extended", subscription.Status,
            new { request.Days, PreviousEnd = previousEnd, subscription.TrialEndsAt });
        await QueueSubscriptionNotification(
            subscription,
            $"trial-extended:{subscription.Id}:{subscription.TrialEndsAt.Value.UtcDateTime.Ticks}",
            $"Your Trial was extended through {subscription.TrialEndsAt.Value:yyyy-MM-dd}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponse(subscriptionId, cancellationToken));
    }

    [HttpPost("{subscriptionId:guid}/renew")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscription.renewed")]
    public async Task<ActionResult<AdminSubscriptionResponse>> Renew(
        Guid subscriptionId,
        RenewSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PeriodEndsAt <= DateTimeOffset.UtcNow)
            return ValidationProblem("Renewal period end must be in the future.");
        var subscription = await GetMutableSubscription(subscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound();
        if (subscription.Edition == SubscriptionEdition.Trial ||
            subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired or SubscriptionStatus.Trialing)
            return ConflictProblem("This subscription cannot be renewed in its current state.");
        var previousStatus = subscription.Status;
        subscription.Status = SubscriptionStatus.Active;
        subscription.CurrentPeriodStartsAt = DateTimeOffset.UtcNow;
        subscription.CurrentPeriodEndsAt = request.PeriodEndsAt;
        subscription.CancellationEffectiveAt = null;
        subscription.GracePeriodEndsAt = null;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        RecordLifecycleMutation(subscription, "subscription.renewed", previousStatus,
            new { request.PeriodEndsAt });
        await QueueSubscriptionNotification(
            subscription,
            $"subscription-renewed:{subscription.Id}:{request.PeriodEndsAt.UtcDateTime.Ticks}",
            $"Your {subscription.Edition} subscription was renewed through {request.PeriodEndsAt:yyyy-MM-dd}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponse(subscriptionId, cancellationToken));
    }

    [HttpPost("{subscriptionId:guid}/grace-period")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscription.grace-period.started")]
    public async Task<ActionResult<AdminSubscriptionResponse>> StartGracePeriod(
        Guid subscriptionId,
        GracePeriodRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndsAt <= DateTimeOffset.UtcNow)
            return ValidationProblem("Grace-period end must be in the future.");
        var subscription = await GetMutableSubscription(subscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound();
        if (subscription.Status is not (SubscriptionStatus.PastDue or SubscriptionStatus.GracePeriod))
            return ConflictProblem("A grace period requires a past-due or existing grace-period subscription.");
        var previousStatus = subscription.Status;
        subscription.Status = SubscriptionStatus.GracePeriod;
        subscription.GracePeriodEndsAt = request.EndsAt;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        RecordLifecycleMutation(subscription, "subscription.grace-period.started", previousStatus,
            new { request.EndsAt });
        await QueueSubscriptionNotification(
            subscription,
            $"subscription-grace:{subscription.Id}:{request.EndsAt.UtcDateTime.Ticks}",
            $"Your subscription entered a grace period through {request.EndsAt:yyyy-MM-dd}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponse(subscriptionId, cancellationToken));
    }

    [HttpPost("{subscriptionId:guid}/cancel")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscription.cancellation.scheduled")]
    public async Task<ActionResult<AdminSubscriptionResponse>> CancelSubscription(
        Guid subscriptionId,
        CancelSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await GetMutableSubscription(subscriptionId, cancellationToken);
        if (subscription is null)
            return NotFound();
        if (subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            return ConflictProblem("This subscription is already closed.");
        var now = DateTimeOffset.UtcNow;
        var effectiveAt = request.EffectiveAt ?? now;
        var previousStatus = subscription.Status;
        subscription.CancellationEffectiveAt = effectiveAt;
        if (effectiveAt <= now)
            subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = now;
        RecordLifecycleMutation(subscription, "subscription.cancellation.scheduled", previousStatus,
            new { EffectiveAt = effectiveAt, Immediate = effectiveAt <= now });
        await QueueSubscriptionNotification(
            subscription,
            $"subscription-cancel:{subscription.Id}:{effectiveAt.UtcDateTime.Ticks}",
            effectiveAt <= now
                ? "Your subscription was cancelled."
                : $"Your subscription is scheduled for cancellation on {effectiveAt:yyyy-MM-dd}.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await BuildResponse(subscriptionId, cancellationToken));
    }

    [HttpDelete("{subscriptionId:guid}/seats/{membershipId:guid}")]
    [Authorize(Policy = PxaPermissions.SubscriptionsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("subscriptions.seat.revoke")]
    public async Task<IActionResult> RevokeSeat(Guid subscriptionId, Guid membershipId, CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator())
            return Forbid();
        var subscription = await dbContext.OrganizationSubscriptions.FindAsync([subscriptionId], cancellationToken);
        var assignment = await dbContext.SubscriptionSeatAssignments.SingleOrDefaultAsync(value =>
            value.SubscriptionId == subscriptionId && value.OrganizationMembershipId == membershipId && value.RevokedAt == null,
            cancellationToken);
        if (subscription is null || assignment is null || tenantContext.UserId is not { } actorUserId)
            return NotFound();
        assignment.RevokedAt = DateTimeOffset.UtcNow;
        AddAuditEvent(subscription, actorUserId, "subscriptions.seat.revoke");
        await notifications.QueueAdministratorsAsync(
            subscription.OrganizationId,
            "security.organization-changed",
            $"subscription-seat-revoked:{subscription.Id}:{membershipId}:{assignment.RevokedAt.Value.UtcDateTime.Ticks}",
            new Dictionary<string, string>
            {
                ["summary"] = "A Designer subscription seat was revoked in your organization.",
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<AdminSubscriptionResponse?> BuildResponse(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await queryService.GetSubscriptionAsync(subscriptionId, cancellationToken);
        if (subscription is null)
            return null;
        return new AdminSubscriptionResponse(
            subscription.Id, subscription.OrganizationId, subscription.OrganizationName, subscription.Edition,
            subscription.AccountType, subscription.Status, subscription.BillingPeriod,
            subscription.DeploymentMode, subscription.SeatLimit, subscription.AssignedSeats, subscription.StartsAt,
            subscription.CurrentPeriodStartsAt, subscription.TrialEndsAt, subscription.CurrentPeriodEndsAt,
            subscription.CancellationEffectiveAt, subscription.GracePeriodEndsAt,
            subscription.Entitlements.Select(entitlement => new AdminEntitlementResponse(
                entitlement.Capability, entitlement.Enabled, entitlement.Limit, entitlement.Unit,
                entitlement.Source, entitlement.ExpiresAt)).ToArray(),
            subscription.CreatedAt, subscription.UpdatedAt);
    }

    private bool CanAccess(Guid organizationId) => IsSystemAdministrator() || tenantContext.OrganizationId == organizationId;
    private bool IsSystemAdministrator() => User.IsInRole(PxaRoles.SystemAdministrator);

    private async Task<OrganizationSubscription?> GetMutableSubscription(
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!IsSystemAdministrator() || tenantContext.UserId is null)
            return null;
        return await dbContext.OrganizationSubscriptions.SingleOrDefaultAsync(
            value => value.Id == subscriptionId,
            cancellationToken);
    }

    private void RecordLifecycleMutation(
        OrganizationSubscription subscription,
        string action,
        SubscriptionStatus previousStatus,
        object details)
    {
        var actorUserId = tenantContext.UserId!.Value;
        AddLifecycleEvent(subscription, actorUserId, action, previousStatus, details);
        AddAuditEvent(subscription, actorUserId, action);
    }

    private Task<int> QueueSubscriptionNotification(
        OrganizationSubscription subscription,
        string eventKey,
        string summary,
        CancellationToken cancellationToken) =>
        notifications.QueueAdministratorsAsync(
            subscription.OrganizationId,
            "subscription.changed",
            eventKey,
            new Dictionary<string, string>
            {
                ["summary"] = summary,
                ["edition"] = subscription.Edition.ToString(),
                ["status"] = subscription.Status.ToString(),
            },
            cancellationToken);

    private static bool TryParseRequest(CreateAdminSubscriptionRequest request, out ParsedValues values, out string error)
    {
        if (!Enum.TryParse<SubscriptionEdition>(request.Edition, true, out var edition) ||
            !Enum.TryParse<SubscriptionAccountType>(request.AccountType, true, out var accountType) ||
            !Enum.TryParse<SubscriptionStatus>(request.Status, true, out var status) ||
            !Enum.TryParse<SubscriptionBillingPeriod>(request.BillingPeriod, true, out var billingPeriod) ||
            !Enum.TryParse<SubscriptionDeploymentMode>(request.DeploymentMode, true, out var deploymentMode) ||
            request.SeatLimit is < 1)
        {
            values = default;
            error = "Edition, account type, lifecycle status, billing period, deployment mode, or seat limit is invalid.";
            return false;
        }
        if (!ValidateEntitlements(request.Entitlements, out error))
        {
            values = default;
            return false;
        }
        values = new ParsedValues(edition, accountType, status, billingPeriod, deploymentMode);
        return true;
    }

    private static bool TryApplyUpdate(
        OrganizationSubscription subscription,
        UpdateAdminSubscriptionRequest request,
        out string error)
    {
        if (request.Edition is not null)
        {
            if (!Enum.TryParse(request.Edition, true, out SubscriptionEdition edition))
                return Fail("Edition is invalid.", out error);
            subscription.Edition = edition;
        }
        if (request.Status is not null)
        {
            if (!Enum.TryParse(request.Status, true, out SubscriptionStatus status))
                return Fail("Lifecycle status is invalid.", out error);
            subscription.Status = status;
        }
        if (request.BillingPeriod is not null)
        {
            if (!Enum.TryParse(request.BillingPeriod, true, out SubscriptionBillingPeriod period))
                return Fail("Billing period is invalid.", out error);
            subscription.BillingPeriod = period;
        }
        if (request.DeploymentMode is not null)
        {
            if (!Enum.TryParse(request.DeploymentMode, true, out SubscriptionDeploymentMode mode))
                return Fail("Deployment mode is invalid.", out error);
            subscription.DeploymentMode = mode;
        }
        if (request.SeatLimit is < 1)
            return Fail("Seat limit must be positive.", out error);
        if (request.SeatLimit is not null)
            subscription.SeatLimit = subscription.AccountType == SubscriptionAccountType.IndividualDeveloper ? 1 : request.SeatLimit;
        if (!ValidateEntitlements(request.Entitlements, out error))
            return false;
        subscription.TrialEndsAt = request.TrialEndsAt ?? subscription.TrialEndsAt;
        subscription.CurrentPeriodEndsAt = request.CurrentPeriodEndsAt ?? subscription.CurrentPeriodEndsAt;
        subscription.CancellationEffectiveAt = request.CancellationEffectiveAt ?? subscription.CancellationEffectiveAt;
        subscription.GracePeriodEndsAt = request.GracePeriodEndsAt ?? subscription.GracePeriodEndsAt;
        error = string.Empty;
        return true;
    }

    private static bool ValidateEntitlements(IReadOnlyList<AdminEntitlementRequest>? entitlements, out string error)
    {
        if (entitlements is null)
        {
            error = string.Empty;
            return true;
        }
        if (entitlements.Select(value => value.Capability).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entitlements.Count ||
            entitlements.Any(value => !CapabilityPattern().IsMatch(value.Capability) || value.Limit is < 0 ||
                value.Unit?.Length > 40 || value.Source is not null &&
                !Enum.TryParse<EntitlementSource>(value.Source, true, out _)))
        {
            error = "Entitlement capabilities must be unique stable keys with valid non-negative limits.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static IEnumerable<SubscriptionEntitlement> CreateEntitlements(
        Guid subscriptionId, IReadOnlyList<AdminEntitlementRequest> entitlements, DateTimeOffset now) =>
        entitlements.Select(value => new SubscriptionEntitlement
        {
            SubscriptionId = subscriptionId,
            Capability = value.Capability.ToLowerInvariant(),
            Enabled = value.Enabled,
            Limit = value.Limit,
            Unit = value.Unit,
            Source = Enum.TryParse<EntitlementSource>(value.Source, true, out var source)
                ? source : EntitlementSource.EditionDefault,
            ExpiresAt = value.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now,
        });

    private void AddLifecycleEvent(
        OrganizationSubscription subscription, Guid actorUserId, string action,
        SubscriptionStatus? previousStatus, object details) =>
        dbContext.SubscriptionLifecycleEvents.Add(new SubscriptionLifecycleEvent
        {
            SubscriptionId = subscription.Id,
            OrganizationId = subscription.OrganizationId,
            ActorUserId = actorUserId,
            Action = action,
            PreviousStatus = previousStatus,
            CurrentStatus = subscription.Status,
            DetailsJson = JsonSerializer.Serialize(details),
        });

    private void AddAuditEvent(OrganizationSubscription subscription, Guid actorUserId, string action) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = subscription.OrganizationId,
            ActorUserId = actorUserId,
            Action = action,
            TargetType = "subscription",
            TargetId = subscription.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { subscription.Edition, subscription.Status }),
        });

    private static bool Fail(string message, out string error) { error = message; return false; }
    private ObjectResult MissingOrganization() => Problem(statusCode: 403, title: "Organization context required");
    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: 409, title: "Subscription change rejected", detail: detail);
    private ObjectResult TrialAlreadyClaimedProblem() => StatusCode(
        StatusCodes.Status409Conflict,
        PxaApiProblems.Create(
            HttpContext,
            StatusCodes.Status409Conflict,
            "Trial already claimed",
            "The organization already has a Trial subscription.",
            PxaApiProblems.TrialAlreadyClaimed));
    private ObjectResult NotFoundProblem(string detail) => Problem(statusCode: 404, title: "Subscription resource not found", detail: detail);
    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new ProblemDetails { Status = 400, Title = "Invalid subscription request", Detail = detail });

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CapabilityPattern();
    private readonly record struct ParsedValues(SubscriptionEdition Edition, SubscriptionAccountType AccountType,
        SubscriptionStatus Status, SubscriptionBillingPeriod BillingPeriod, SubscriptionDeploymentMode DeploymentMode);
}

public sealed record AdminSubscriptionPage(IReadOnlyList<AdminSubscriptionSummary> Items, int Page, int PageSize, int Total);
public sealed record AdminSubscriptionSummary(Guid Id, Guid OrganizationId, string OrganizationName, string Edition,
    string AccountType, string Status, string BillingPeriod, string DeploymentMode, int? SeatLimit, int AssignedSeats,
    DateTimeOffset? TrialEndsAt, DateTimeOffset? CurrentPeriodEndsAt, DateTimeOffset UpdatedAt);
public sealed record AdminSubscriptionResponse(Guid Id, Guid OrganizationId, string OrganizationName, string Edition,
    string AccountType, string Status, string BillingPeriod, string DeploymentMode, int? SeatLimit, int AssignedSeats,
    DateTimeOffset StartsAt, DateTimeOffset CurrentPeriodStartsAt, DateTimeOffset? TrialEndsAt, DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? CancellationEffectiveAt, DateTimeOffset? GracePeriodEndsAt,
    IReadOnlyList<AdminEntitlementResponse> Entitlements, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AdminEntitlementResponse(string Capability, bool Enabled, long? Limit, string? Unit, string Source, DateTimeOffset? ExpiresAt);
public sealed record AdminEntitlementRequest(string Capability, bool Enabled, long? Limit = null, string? Unit = null,
    string? Source = null, DateTimeOffset? ExpiresAt = null);
public sealed record CreateAdminSubscriptionRequest(Guid OrganizationId, string Edition, string AccountType, string Status,
    string BillingPeriod, string DeploymentMode, int? SeatLimit, DateTimeOffset? StartsAt, DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt, IReadOnlyList<AdminEntitlementRequest> Entitlements);
public sealed record UpdateAdminSubscriptionRequest(string? Edition, string? Status, string? BillingPeriod,
    string? DeploymentMode, int? SeatLimit, DateTimeOffset? TrialEndsAt, DateTimeOffset? CurrentPeriodEndsAt,
    DateTimeOffset? CancellationEffectiveAt, DateTimeOffset? GracePeriodEndsAt,
    IReadOnlyList<AdminEntitlementRequest>? Entitlements);
public sealed record AdminSubscriptionSeatResponse(Guid MembershipId, Guid UserId, string DisplayName, string Email,
    string MembershipStatus, bool Assigned);
public sealed record AdminSubscriptionHistoryResponse(Guid Id, string Action, string? PreviousStatus,
    string CurrentStatus, Guid ActorUserId, string ActorName, DateTimeOffset CreatedAt);
public sealed record ExtendTrialRequest(int Days);
public sealed record RenewSubscriptionRequest(DateTimeOffset PeriodEndsAt);
public sealed record GracePeriodRequest(DateTimeOffset EndsAt);
public sealed record CancelSubscriptionRequest(DateTimeOffset? EffectiveAt);
public sealed record AdminSubscriptionUsageResponse(DateTimeOffset PeriodStartsAt,
    DateTimeOffset? PeriodEndsAt, long TotalQuantity, IReadOnlyList<AdminSubscriptionUsageItem> Items);
public sealed record AdminSubscriptionUsageItem(string Capability, string Operation, string Source,
    long Quantity, int EventCount, DateTimeOffset LastOccurredAt);
