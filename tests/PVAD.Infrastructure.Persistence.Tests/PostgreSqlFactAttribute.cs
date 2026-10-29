namespace PVAD.Infrastructure.Persistence.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!string.Equals(
                PVAD.Testing.PvadTestEnvironment.Read("PVAD_RUN_POSTGRES_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set PVAD_RUN_POSTGRES_TESTS=1 to run PostgreSQL container tests.";
        }
    }
}
