using System.Reflection;

internal static class NativeSurfaceCoverage
{
    private static readonly HashSet<string> Members = new(StringComparer.Ordinal);

    internal static void Record(MemberInfo member)
    {
        if (member.DeclaringType is null || !member.DeclaringType.IsVisible)
            throw new InvalidOperationException("Expected an exported native API member.");
        Members.Add($"{member.DeclaringType.Assembly.GetName().Name}\n{member.DeclaringType.FullName}\n{member.MemberType}:{member}");
    }

    internal static void RecordMethods(Type type, int expectedCount)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName).ToArray();
        if (methods.Length != expectedCount) throw new InvalidOperationException("Unreviewed API method on " + type.FullName);
        foreach (var method in methods) Record(method);
    }

    internal static string[] Snapshot() => Members.Order(StringComparer.Ordinal).ToArray();
}
