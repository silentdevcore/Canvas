using System.Reflection;
using PVAD.Licensing;

internal sealed class NativeAdmissionMatrix
{
    private sealed record Entry(MemberInfo Member, string Dispatch, Action Invoke,
        Func<string> Invariant, ConstructorInfo? Prerequisite, Action? MissingInvocation, Type? Implementation)
    {
        internal Dictionary<string, object> Phases { get; } = new();
    }

    private readonly List<Entry> entries = [];

    internal void Add(MemberInfo member, string dispatch, Action invoke, Func<string> invariant,
        ConstructorInfo? prerequisite = null, Action? missingInvocation = null, Type? implementation = null)
    {
        if ((prerequisite is null) != (missingInvocation is null))
            throw new InvalidOperationException("A constructor prerequisite needs an actual denial invocation.");
        if (entries.Any(entry => Symbol(entry.Member) == Symbol(member) && entry.Dispatch == dispatch && entry.Implementation == implementation))
            throw new InvalidOperationException("Duplicate admission case.");
        entries.Add(new(member, dispatch, invoke, invariant, prerequisite, missingInvocation, implementation));
    }

    internal void RequireSurface(Type type, params string[] methodNames)
    {
        var actual = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && (methodNames.Length == 0 || methodNames.Contains(method.Name)))
            .Select(Symbol).Order().ToArray();
        var registered = entries.Where(entry => entry.Member.DeclaringType == type && entry.Member is MethodInfo)
            .Select(entry => Symbol(entry.Member)).Distinct().Order().ToArray();
        if (!actual.SequenceEqual(registered))
            throw new InvalidOperationException($"Unreviewed admission surface: {type.FullName}");
    }

    internal void RequireConstructors(Type type)
    {
        var actual = type.GetConstructors().Select(Symbol).Order().ToArray();
        var registered = entries.Where(entry => entry.Member.DeclaringType == type && entry.Member is ConstructorInfo)
            .Select(entry => Symbol(entry.Member)).Distinct().Order().ToArray();
        if (!actual.SequenceEqual(registered))
            throw new InvalidOperationException($"Unreviewed admission constructor: {type.FullName}");
    }

    internal void Run(string phase)
    {
        if (phase is not ("missing" or "incorrect" or "valid"))
            throw new ArgumentException("Unknown admission phase.", nameof(phase));
        foreach (var entry in entries)
        {
            if (entry.Phases.ContainsKey(phase)) throw new InvalidOperationException("Duplicate admission phase.");
            var prerequisite = phase == "missing" && entry.Prerequisite is not null;
            var invoke = prerequisite ? entry.MissingInvocation! : entry.Invoke;
            if (phase == "valid")
            {
                invoke();
                entry.Phases.Add(phase, new { mode = "invoked", outcome = "accepted" });
                continue;
            }
            var before = entry.Invariant();
            var code = phase == "missing" ? "PVADLIC001" : "PVADLIC012";
            try { invoke(); }
            catch (PvadLicenseException exception) when (exception.Code == code)
            {
                if (before != entry.Invariant())
                    throw new InvalidOperationException($"Denied operation changed input/output: {Symbol(entry.Member)}");
                entry.Phases.Add(phase, new { mode = prerequisite ? "constructorPrerequisite" : "invoked",
                    outcome = "denied", diagnostic = code, unchanged = true,
                    prerequisite = prerequisite ? Symbol(entry.Prerequisite!) : null });
                continue;
            }
            throw new InvalidOperationException($"Expected {code}: {Symbol(entry.Member)}");
        }
    }

    internal object[] Snapshot()
    {
        if (entries.Count == 0 || entries.Any(entry => entry.Phases.Count != 3))
            throw new InvalidOperationException("Incomplete admission matrix.");
        return entries.Select(entry => (object)new { member = Symbol(entry.Member),
            dispatch = entry.Dispatch, implementation = entry.Implementation?.FullName, phases = entry.Phases }).ToArray();
    }

    private static string Symbol(MemberInfo member) =>
        $"{member.DeclaringType!.FullName}\n{member.MemberType}:{member}";
}
