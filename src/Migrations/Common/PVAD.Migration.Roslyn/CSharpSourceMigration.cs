using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Roslyn;

public abstract class CSharpSourceMigration : ISourceMigration
{
    public abstract MigrationResult Migrate(string sourceCode);
}
