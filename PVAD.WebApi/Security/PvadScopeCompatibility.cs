namespace PVAD.WebApi.Security;

public static class PvadScopeCompatibility
{
    public static string[] SupportedScopes => PvadOAuthScopes.All
        .SelectMany(scope => new[] { Canonical(scope), scope })
        .Distinct(StringComparer.Ordinal).ToArray();

    public static string[] ReadOnlyScopes => PvadOAuthScopes.ReadOnly
        .SelectMany(scope => new[] { Canonical(scope), scope })
        .Distinct(StringComparer.Ordinal).ToArray();

    public static bool Contains(IEnumerable<string> scopes, string required) =>
        scopes.Any(scope => Equivalent(scope, required));

    public static string Canonical(string scope) => PvadOAuthScopes.All.Contains(scope, StringComparer.Ordinal)
        ? "pvad." + scope["pxa.".Length..] : scope;

    public static bool Equivalent(string left, string right) =>
        string.Equals(Canonical(left), Canonical(right), StringComparison.Ordinal);

    public static bool IsReadOnly(string scope) => PvadOAuthScopes.ReadOnly.Any(value => Equivalent(value, scope));
}
