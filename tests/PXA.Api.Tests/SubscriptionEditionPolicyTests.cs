using PXA.Domain.Entities;
using PXA.WebApi.Application.Subscriptions;

namespace PXA.Api.Tests;

public sealed class SubscriptionEditionPolicyTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Pending, SubscriptionStatus.Trialing, true)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.GracePeriod, true)]
    [InlineData(SubscriptionStatus.GracePeriod, SubscriptionStatus.Suspended, true)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Expired, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Trialing, false)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active, false)]
    public void Lifecycle_transition_matrix_is_explicit(
        SubscriptionStatus from,
        SubscriptionStatus to,
        bool expected)
    {
        Assert.Equal(expected, SubscriptionEditionPolicy.CanTransition(from, to));
    }

    [Theory]
    [InlineData(SubscriptionEdition.Trial, SubscriptionEdition.Premium, true)]
    [InlineData(SubscriptionEdition.Trial, SubscriptionEdition.Enterprise, true)]
    [InlineData(SubscriptionEdition.Free, SubscriptionEdition.Premium, true)]
    [InlineData(SubscriptionEdition.Premium, SubscriptionEdition.Free, true)]
    [InlineData(SubscriptionEdition.Enterprise, SubscriptionEdition.Premium, true)]
    [InlineData(SubscriptionEdition.Premium, SubscriptionEdition.Trial, false)]
    [InlineData(SubscriptionEdition.Enterprise, SubscriptionEdition.Trial, false)]
    [InlineData(SubscriptionEdition.Enterprise, SubscriptionEdition.Free, false)]
    public void Conversion_matrix_is_explicit(
        SubscriptionEdition from,
        SubscriptionEdition to,
        bool expected)
    {
        Assert.Equal(expected, SubscriptionEditionPolicy.CanConvert(from, to));
    }

    [Theory]
    [InlineData(SubscriptionEdition.Trial, SubscriptionStatus.Trialing, SubscriptionBillingPeriod.None,
        SubscriptionDeploymentMode.Cloud, true)]
    [InlineData(SubscriptionEdition.Trial, SubscriptionStatus.Active, SubscriptionBillingPeriod.None,
        SubscriptionDeploymentMode.Cloud, false)]
    [InlineData(SubscriptionEdition.Free, SubscriptionStatus.Active, SubscriptionBillingPeriod.None,
        SubscriptionDeploymentMode.Cloud, true)]
    [InlineData(SubscriptionEdition.Free, SubscriptionStatus.Active, SubscriptionBillingPeriod.Monthly,
        SubscriptionDeploymentMode.Cloud, false)]
    [InlineData(SubscriptionEdition.Premium, SubscriptionStatus.Active, SubscriptionBillingPeriod.Monthly,
        SubscriptionDeploymentMode.Cloud, true)]
    [InlineData(SubscriptionEdition.Premium, SubscriptionStatus.Active, SubscriptionBillingPeriod.Monthly,
        SubscriptionDeploymentMode.OnPremise, false)]
    [InlineData(SubscriptionEdition.Enterprise, SubscriptionStatus.Active, SubscriptionBillingPeriod.Annual,
        SubscriptionDeploymentMode.Hybrid, true)]
    public void Edition_configuration_is_consistent(
        SubscriptionEdition edition,
        SubscriptionStatus status,
        SubscriptionBillingPeriod billingPeriod,
        SubscriptionDeploymentMode deploymentMode,
        bool expected)
    {
        Assert.Equal(expected, SubscriptionEditionPolicy.TryValidateConfiguration(
            edition, status, billingPeriod, deploymentMode, out _));
    }
}
