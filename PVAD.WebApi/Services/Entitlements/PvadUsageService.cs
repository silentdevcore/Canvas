using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Services.Entitlements;

public interface IPvadUsageService
{
    Task<PvadUsageResult> RecordAsync(
        Guid organizationId,
        string capability,
        string operation,
        long quantity,
        string requestId,
        string source,
        CancellationToken cancellationToken = default);
}

public sealed class PvadUsageService : IPvadUsageService
{
    private readonly PvadDbContext dbContext;
    private readonly IPvadEntitlementService entitlementService;

    public PvadUsageService(PvadDbContext dbContext, IPvadEntitlementService entitlementService)
    {
        this.dbContext = dbContext;
        this.entitlementService = entitlementService;
    }

    public async Task<PvadUsageResult> RecordAsync(
        Guid organizationId,
        string capability,
        string operation,
        long quantity,
        string requestId,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(operation))
            return new(false, false, "PXA_USAGE_INVALID", "Quantity, request ID, and operation are required.", null);
        var existing = await dbContext.SubscriptionUsageEvents.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.RequestId == requestId,
                cancellationToken);
        if (existing is not null)
            return new(true, true, "PXA_USAGE_ALREADY_RECORDED", "The idempotent usage event already exists.", existing.Id);

        var decision = await entitlementService.EvaluateAsync(
            organizationId, capability, quantity, cancellationToken);
        if (!decision.Allowed)
            return new(false, false, decision.Code, decision.Reason, null);
        var usageEvent = new SubscriptionUsageEvent
        {
            OrganizationId = organizationId,
            SubscriptionId = decision.SubscriptionId!.Value,
            Capability = capability.Trim().ToLowerInvariant(),
            Operation = operation.Trim().ToLowerInvariant(),
            Quantity = quantity,
            RequestId = requestId.Trim(),
            Source = string.IsNullOrWhiteSpace(source) ? "api" : source.Trim().ToLowerInvariant(),
        };
        dbContext.SubscriptionUsageEvents.Add(usageEvent);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(usageEvent).State = EntityState.Detached;
            var concurrent = await dbContext.SubscriptionUsageEvents.AsNoTracking()
                .SingleOrDefaultAsync(value => value.OrganizationId == organizationId && value.RequestId == requestId,
                    cancellationToken);
            if (concurrent is not null)
                return new(true, true, "PXA_USAGE_ALREADY_RECORDED", "The idempotent usage event already exists.", concurrent.Id);
            throw;
        }
        return new(true, false, "PXA_USAGE_RECORDED", "Usage was recorded.", usageEvent.Id);
    }
}

public sealed record PvadUsageResult(bool Recorded, bool Duplicate, string Code, string Reason, Guid? UsageEventId);
