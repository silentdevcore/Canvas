namespace Canvas.Migration.Abstractions;

public sealed class MigrationDiagnostic
{
    public required string Id { get; init; }

    public required string Message { get; init; }

    public MigrationDiagnosticSeverity Severity { get; init; } = MigrationDiagnosticSeverity.Info;
}
