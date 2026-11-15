namespace PVAD.Infrastructure.Persistence.Configuration;

public static class PvadEnvironment
{
    public static string? Read(string name, Func<string, string?>? read = null)
    {
        if (!name.StartsWith("PVAD_", StringComparison.Ordinal))
            throw new ArgumentException("Expected a PVAD environment key.", nameof(name));
        return (read ?? Environment.GetEnvironmentVariable)(name);
    }
}
