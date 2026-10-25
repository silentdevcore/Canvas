namespace PVAD.WebApi.Security;

public static class PvadScopeCompatibility
{
    private static readonly IReadOnlyDictionary<string, string> KnownAliases = PvadOAuthScopes.All
        .ToDictionary(scope => "pxa." + scope["pvad.".Length..], scope => scope, StringComparer.Ordinal);

    public static string[] SupportedScopes => PvadOAuthScopes.All
        .SelectMany(scope => new[] { scope, Legacy(scope) })
        .Distinct(StringComparer.Ordinal).ToArray();

    public static string[] ReadOnlyScopes => PvadOAuthScopes.ReadOnly
        .SelectMany(scope => new[] { scope, Legacy(scope) })
        .Distinct(StringComparer.Ordinal).ToArray();

    public static bool Contains(IEnumerable<string> scopes, string required) =>
        scopes.Any(scope => Equivalent(scope, required));

    public static string Canonical(string scope) => KnownAliases.TryGetValue(scope, out var canonical) ? canonical : scope;

    public static string Legacy(string scope) => PvadOAuthScopes.All.Contains(scope, StringComparer.Ordinal)
        ? "pxa." + scope["pvad.".Length..] : scope;

    public static bool Equivalent(string left, string right) =>
        string.Equals(Canonical(left), Canonical(right), StringComparison.Ordinal);

    public static bool IsReadOnly(string scope) => PvadOAuthScopes.ReadOnly.Any(value => Equivalent(value, scope));
}
