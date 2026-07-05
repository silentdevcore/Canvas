namespace PXA.Migration.Abstractions;

public interface ISourceMigration
{
    MigrationResult Migrate(string sourceCode);
}
