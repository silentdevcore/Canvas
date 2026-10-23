namespace PVAD.Infrastructure.Persistence.Configuration;

public static class PvadEnvironment
{
    public static string? Read(string name, Func<string, string?>? read = null)
    {
        if (!name.StartsWith("PVAD_", StringComparison.Ordinal))
            throw new ArgumentException("Expected a PVAD environment key.", nameof(name));
        read ??= Environment.GetEnvironmentVariable;
        var legacyName = "PXA_" + name[5..];
        var current = read(name);
        var previous = read(legacyName);
        if (current is not null && previous is not null && current != previous)
            throw new InvalidOperationException(
                $"Conflicting configuration keys: {name} and {legacyName}.");
        return current ?? previous;
    }
}
