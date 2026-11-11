using System;

namespace PVAD.Testing;

internal static class PvadTestEnvironment
{
    internal static string? Read(string name, Func<string, string?>? read = null)
    {
        if (!name.StartsWith("PVAD_", StringComparison.Ordinal))
            throw new ArgumentException("Expected a PVAD environment key.", nameof(name));
        return (read ?? Environment.GetEnvironmentVariable)(name);
    }
}
