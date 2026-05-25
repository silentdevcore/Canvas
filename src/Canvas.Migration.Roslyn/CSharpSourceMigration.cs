using Canvas.Migration.Abstractions;

namespace Canvas.Migration.Roslyn;

public abstract class CSharpSourceMigration : ISourceMigration
{
    public abstract MigrationResult Migrate(string sourceCode);
}
