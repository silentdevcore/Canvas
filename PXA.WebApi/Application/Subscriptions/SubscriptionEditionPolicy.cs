using PXA.Domain.Entities;

namespace PXA.WebApi.Application.Subscriptions;

public static class SubscriptionEditionPolicy
{
    public static bool CanTransition(SubscriptionStatus from, SubscriptionStatus to) => from switch
    {
        SubscriptionStatus.Pending => to is SubscriptionStatus.Trialing or SubscriptionStatus.Active or
            SubscriptionStatus.Cancelled,
        SubscriptionStatus.Trialing => to is SubscriptionStatus.Active or SubscriptionStatus.Suspended or
            SubscriptionStatus.Cancelled or SubscriptionStatus.Expired,
        SubscriptionStatus.Active => to is SubscriptionStatus.PastDue or SubscriptionStatus.Suspended or
            SubscriptionStatus.Cancelled,
        SubscriptionStatus.PastDue => to is SubscriptionStatus.Active or SubscriptionStatus.GracePeriod or
            SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled,
        SubscriptionStatus.GracePeriod => to is SubscriptionStatus.Active or SubscriptionStatus.Suspended or
            SubscriptionStatus.Cancelled or SubscriptionStatus.Expired,
        SubscriptionStatus.Suspended => to is SubscriptionStatus.Active or SubscriptionStatus.Cancelled or
            SubscriptionStatus.Expired,
        SubscriptionStatus.Cancelled => to is SubscriptionStatus.Expired,
        SubscriptionStatus.Expired => false,
        _ => false,
    };

    public static bool CanConvert(SubscriptionEdition from, SubscriptionEdition to) =>
        from == to || from switch
        {
            SubscriptionEdition.Free => to is SubscriptionEdition.Premium or SubscriptionEdition.Enterprise,
            SubscriptionEdition.Trial => to is SubscriptionEdition.Free or SubscriptionEdition.Premium or
                SubscriptionEdition.Enterprise,
            SubscriptionEdition.Premium => to is SubscriptionEdition.Free or SubscriptionEdition.Enterprise,
            SubscriptionEdition.Enterprise => to == SubscriptionEdition.Premium,
            _ => false,
        };

    public static bool TryValidateConfiguration(
        SubscriptionEdition edition,
        SubscriptionStatus status,
        SubscriptionBillingPeriod billingPeriod,
        SubscriptionDeploymentMode deploymentMode,
        out string error)
    {
        if (edition == SubscriptionEdition.Trial &&
            (status is not (SubscriptionStatus.Pending or SubscriptionStatus.Trialing or
                 SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired) ||
             billingPeriod != SubscriptionBillingPeriod.None ||
             deploymentMode != SubscriptionDeploymentMode.Cloud))
        {
            error = "Trial requires a Trial lifecycle state, no billing period, and Cloud deployment.";
            return false;
        }

        if (edition == SubscriptionEdition.Free &&
            (status == SubscriptionStatus.Trialing ||
             billingPeriod != SubscriptionBillingPeriod.None ||
             deploymentMode != SubscriptionDeploymentMode.Cloud))
        {
            error = "Free requires no billing period, Cloud deployment, and a non-Trial lifecycle state.";
            return false;
        }

        if (edition == SubscriptionEdition.Premium &&
            (status is SubscriptionStatus.Pending or SubscriptionStatus.Trialing ||
             billingPeriod == SubscriptionBillingPeriod.None ||
             deploymentMode != SubscriptionDeploymentMode.Cloud))
        {
            error = "Premium requires an active commercial lifecycle, a billing period, and Cloud deployment.";
            return false;
        }

        if (edition == SubscriptionEdition.Enterprise &&
            (status is SubscriptionStatus.Pending or SubscriptionStatus.Trialing ||
             billingPeriod == SubscriptionBillingPeriod.None))
        {
            error = "Enterprise requires an active commercial lifecycle and a billing period.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
