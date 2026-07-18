namespace PXA.Api.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("PXA_RUN_POSTGRES_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set PXA_RUN_POSTGRES_TESTS=1 to run PostgreSQL container tests.";
        }
    }
}
