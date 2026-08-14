namespace PXA.WebApi.Application.Legal;

public sealed class PxaPaidCheckoutReadinessGate(
    PxaCommerceReadinessCatalog commerce,
    PxaConsumerCheckoutLegalGate consumerLegalGate)
{
    public async Task<PxaPaidCheckoutReadiness> EvaluateAsync(
        string? countryCode,
        PxaCheckoutCustomerType customerType,
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var market = commerce.Evaluate(countryCode, customerType);
        PxaConsumerCheckoutReadiness? consumerLegal = null;
        if (customerType == PxaCheckoutCustomerType.Consumer)
            consumerLegal = await consumerLegalGate.EvaluateAsync(locale, now, cancellationToken);

        var available = market.Available && (consumerLegal?.Available ?? true);
        return new PxaPaidCheckoutReadiness(
            available,
            available ? null : market.Reason ?? consumerLegal?.Reason ?? "checkout-not-ready",
            market,
            consumerLegal);
    }

    public async Task RequireAsync(
        string? countryCode,
        PxaCheckoutCustomerType customerType,
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var readiness = await EvaluateAsync(countryCode, customerType, locale, now, cancellationToken);
        if (!readiness.Available)
            throw new PxaPaidCheckoutNotReadyException(readiness);
    }
}

public sealed record PxaPaidCheckoutReadiness(
    bool Available,
    string? Reason,
    PxaCommerceMarketReadiness Market,
    PxaConsumerCheckoutReadiness? ConsumerLegal);

public sealed class PxaPaidCheckoutNotReadyException(PxaPaidCheckoutReadiness readiness)
    : InvalidOperationException($"Paid checkout is not ready: {readiness.Reason}.")
{
    public PxaPaidCheckoutReadiness Readiness { get; } = readiness;
}
