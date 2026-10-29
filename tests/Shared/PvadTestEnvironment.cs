using System;

namespace PVAD.Testing;

internal static class PvadTestEnvironment
{
    internal static string? Read(string name, Func<string, string?>? read = null)
    {
        if (!name.StartsWith("PVAD_", StringComparison.Ordinal))
            throw new ArgumentException("Expected a PVAD environment key.", nameof(name));
        read ??= Environment.GetEnvironmentVariable;
        var legacy = "PXA_" + name[5..];
        var current = read(name);
        var previous = read(legacy);
        if (current is not null && previous is not null && current != previous)
            throw new InvalidOperationException($"Conflicting configuration keys: {name} and {legacy}.");
        return current ?? previous;
    }
}
