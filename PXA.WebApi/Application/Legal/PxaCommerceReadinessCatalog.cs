using System.Text.Json;

namespace PXA.WebApi.Application.Legal;

public sealed class PxaCommerceReadinessCatalog
{
    private const string CommerceResource = "PXA.ProductMetadata.global-commerce-catalog.json";
    private const string CountryResource = "PXA.ProductMetadata.country-readiness.json";
    private readonly CommerceCatalog commerce;
    private readonly CountryCatalog countries;

    public PxaCommerceReadinessCatalog()
    {
        commerce = ReadEmbedded<CommerceCatalog>(CommerceResource);
        countries = ReadEmbedded<CountryCatalog>(CountryResource);

        if (commerce.PriceBooks.Count == 0 || countries.Regions.Count == 0)
            throw new InvalidOperationException("Commerce readiness metadata is incomplete.");
    }

    public PxaCommerceMarketReadiness Evaluate(string? countryCode, PxaCheckoutCustomerType customerType)
    {
        var normalizedCountry = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
        var region = countries.Regions.SingleOrDefault(value => value.CountryCodes.Contains(normalizedCountry));
        var marketStatus = customerType == PxaCheckoutCustomerType.Business
            ? region?.B2bStatus
            : region?.B2cStatus;
        var priceBookReady = region is not null && commerce.PriceBooks.Any(value =>
            string.Equals(value.Currency, region.PriceBookCurrency, StringComparison.Ordinal) &&
            string.Equals(value.Status, "approved", StringComparison.Ordinal));

        var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["catalog"] = commerce.ProductionApproved && commerce.Status == "approved",
            ["public-pricing"] = commerce.PublicPricingEnabled,
            ["billing-provider"] = commerce.MerchantOfRecord.Status == "approved",
            ["launch-recommendation"] = commerce.LaunchRecommendation.Status == "approved",
            ["restriction-policy"] = commerce.RestrictionPolicy.Status == "approved",
            ["country-catalog"] = countries.ProductionApproved,
            ["country"] = region is not null,
            ["market"] = marketStatus == "approved",
            ["price-book"] = priceBookReady,
            ["consumer-checkout"] = customerType == PxaCheckoutCustomerType.Business || commerce.ConsumerCheckoutEnabled,
        };
        var failedCheck = checks.FirstOrDefault(value => !value.Value);

        return new PxaCommerceMarketReadiness(
            checks.Values.All(value => value),
            normalizedCountry,
            customerType,
            region?.Id,
            region?.PriceBookCurrency,
            marketStatus,
            failedCheck.Key is null ? null : $"{failedCheck.Key}-not-ready",
            checks);
    }

    private static T ReadEmbedded<T>(string resourceName)
    {
        using var stream = typeof(PxaCommerceReadinessCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} is missing.");
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} is invalid.");
    }

    private sealed record CommerceCatalog(
        string Status,
        bool ProductionApproved,
        bool PublicPricingEnabled,
        bool ConsumerCheckoutEnabled,
        MerchantOfRecord MerchantOfRecord,
        LaunchRecommendation LaunchRecommendation,
        IReadOnlyList<PriceBook> PriceBooks,
        RestrictionPolicy RestrictionPolicy);

    private sealed record MerchantOfRecord(string Status);
    private sealed record LaunchRecommendation(string Status);
    private sealed record PriceBook(string Currency, string Status);
    private sealed record RestrictionPolicy(string Status);
    private sealed record CountryCatalog(bool ProductionApproved, IReadOnlyList<CountryRegion> Regions);
    private sealed record CountryRegion(
        string Id,
        IReadOnlyList<string> CountryCodes,
        string PriceBookCurrency,
        string B2bStatus,
        string B2cStatus);
}

public enum PxaCheckoutCustomerType
{
    Business,
    Consumer,
}

public sealed record PxaCommerceMarketReadiness(
    bool Available,
    string CountryCode,
    PxaCheckoutCustomerType CustomerType,
    string? Region,
    string? Currency,
    string? MarketStatus,
    string? Reason,
    IReadOnlyDictionary<string, bool> Checks);
