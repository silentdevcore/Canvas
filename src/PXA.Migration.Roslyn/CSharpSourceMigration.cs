using PXA.Migration.Abstractions;

namespace PXA.Migration.Roslyn;

public abstract class CSharpSourceMigration : ISourceMigration
{
    public abstract MigrationResult Migrate(string sourceCode);
}
