namespace PVAD.WebApi.Infrastructure;

public static class PvadConfigurationAliases
{
    public static void Apply(ConfigurationManager configuration)
    {
        var keys = configuration.AsEnumerable().Select(entry => entry.Key)
            .Where(key => key.StartsWith("Pxa:", StringComparison.OrdinalIgnoreCase) ||
                          key.StartsWith("Pvad:", StringComparison.OrdinalIgnoreCase) ||
                          key.Equals("ConnectionStrings:PxaDatabase", StringComparison.OrdinalIgnoreCase) ||
                          key.Equals("ConnectionStrings:PvadDatabase", StringComparison.OrdinalIgnoreCase))
            .Select(CanonicalKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var providers = ((IConfigurationRoot)configuration).Providers.ToArray();
        var aliases = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var canonical in keys)
        {
            var legacy = LegacyKey(canonical);
            foreach (var provider in providers)
            {
                var hasCurrent = provider.TryGet(canonical, out var current);
                var hasLegacy = provider.TryGet(legacy, out var previous);
                if (hasCurrent && hasLegacy && current != previous)
                    throw new InvalidOperationException(
                        $"Conflicting configuration keys: {canonical} and {legacy}.");
                if (hasCurrent || hasLegacy)
                    aliases[canonical] = hasCurrent ? current : previous;
            }
        }
        // Resolve once at startup in standard provider order, before options binding.
        configuration.AddInMemoryCollection(aliases);
    }

    private static string CanonicalKey(string key) =>
        key.Equals("ConnectionStrings:PxaDatabase", StringComparison.OrdinalIgnoreCase)
            ? "ConnectionStrings:PvadDatabase"
            : key.StartsWith("Pxa:", StringComparison.OrdinalIgnoreCase)
                ? "Pvad:" + key[4..]
                : key;

    private static string LegacyKey(string key) =>
        key.Equals("ConnectionStrings:PvadDatabase", StringComparison.OrdinalIgnoreCase)
            ? "ConnectionStrings:PxaDatabase"
            : "Pxa:" + key[5..];
}
