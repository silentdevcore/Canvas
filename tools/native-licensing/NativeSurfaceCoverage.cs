using System.Reflection;

internal static class NativeSurfaceCoverage
{
    private static readonly HashSet<string> Members = new(StringComparer.Ordinal);
    private sealed record Evidence(string? Missing, string? Incorrect, string? Valid, MemberInfo? Prerequisite);
    private static readonly Dictionary<string, Evidence> Phases = new(StringComparer.Ordinal);

    internal static void Record(MemberInfo member)
    {
        if (member.DeclaringType is null || !member.DeclaringType.IsVisible)
            throw new InvalidOperationException("Expected an exported native API member.");
        Members.Add(Symbol(member));
    }

    internal static void Bind(MemberInfo member, string? missing, string? incorrect, string? valid, MemberInfo? prerequisite = null)
    {
        Record(member);
        if (!Phases.TryAdd(Symbol(member), new(missing, incorrect, valid, prerequisite)))
            throw new InvalidOperationException("Duplicate native phase binding: " + Symbol(member));
    }

    internal static void BindPhases(MemberInfo member, string prefix, string key) =>
        Bind(member, prefix + ":missing:" + key, prefix + ":wrong:" + key, prefix + ":valid:" + key);

    internal static object[] PhaseSnapshot(IReadOnlyList<string> checks)
    {
        var observed = checks.ToHashSet(StringComparer.Ordinal);
        if (observed.Count != checks.Count) throw new InvalidOperationException("Duplicate executed check IDs.");
        return Members.Order(StringComparer.Ordinal).Select(member =>
        {
            Phases.TryGetValue(member, out var evidence);
            object? Phase(string? check, string phase)
            {
                if (check is null) return null;
                if (!observed.Contains(check)) throw new InvalidOperationException("Unexecuted native phase assertion: " + check);
                var prerequisite = phase == "missing" ? evidence?.Prerequisite : null;
                return new { check, mode = prerequisite is null ? "invoked" : "admissionPrerequisite",
                    diagnostic = phase == "valid" ? null : phase == "missing" ? "PVADLIC001" : "PVADLIC012",
                    prerequisite = prerequisite is null ? null : Symbol(prerequisite) };
            }
            return (object)new { member, missing = Phase(evidence?.Missing, "missing"),
                incorrect = Phase(evidence?.Incorrect, "incorrect"), valid = Phase(evidence?.Valid, "valid") };
        }).ToArray();
    }

    internal static void RecordMethods(Type type, int expectedCount)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName).ToArray();
        if (methods.Length != expectedCount) throw new InvalidOperationException("Unreviewed API method on " + type.FullName);
        foreach (var method in methods) Record(method);
    }

    internal static string[] Snapshot() => Members.Order(StringComparer.Ordinal).ToArray();
    private static string Symbol(MemberInfo member) =>
        $"{member.DeclaringType!.Assembly.GetName().Name}\n{member.DeclaringType.FullName}\n{member.MemberType}:{member}";
}
