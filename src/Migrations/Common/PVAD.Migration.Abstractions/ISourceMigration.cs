namespace PVAD.Migration.Abstractions;

public interface ISourceMigration
{
    MigrationResult Migrate(string sourceCode);
}
