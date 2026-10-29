namespace PVAD.WebApi.Application.Legal;

public sealed class PvadPaidCheckoutReadinessGate(
    PvadCommerceReadinessCatalog commerce,
    PvadConsumerCheckoutLegalGate consumerLegalGate)
{
    public async Task<PvadPaidCheckoutReadiness> EvaluateAsync(
        string? countryCode,
        PvadCheckoutCustomerType customerType,
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var market = commerce.Evaluate(countryCode, customerType);
        PvadConsumerCheckoutReadiness? consumerLegal = null;
        if (customerType == PvadCheckoutCustomerType.Consumer)
            consumerLegal = await consumerLegalGate.EvaluateAsync(locale, now, cancellationToken);

        var available = market.Available && (consumerLegal?.Available ?? true);
        return new PvadPaidCheckoutReadiness(
            available,
            available ? null : market.Reason ?? consumerLegal?.Reason ?? "checkout-not-ready",
            market,
            consumerLegal);
    }

    public async Task RequireAsync(
        string? countryCode,
        PvadCheckoutCustomerType customerType,
        string? locale,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var readiness = await EvaluateAsync(countryCode, customerType, locale, now, cancellationToken);
        if (!readiness.Available)
            throw new PvadPaidCheckoutNotReadyException(readiness);
    }
}

public sealed record PvadPaidCheckoutReadiness(
    bool Available,
    string? Reason,
    PvadCommerceMarketReadiness Market,
    PvadConsumerCheckoutReadiness? ConsumerLegal);

public sealed class PvadPaidCheckoutNotReadyException(PvadPaidCheckoutReadiness readiness)
    : InvalidOperationException($"Paid checkout is not ready: {readiness.Reason}.")
{
    public PvadPaidCheckoutReadiness Readiness { get; } = readiness;
}
