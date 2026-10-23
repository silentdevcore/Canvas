namespace PVAD.Migration.Abstractions;

public sealed class MigrationResult
{
    public required string MigratedCode { get; init; }

    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();

    public PdfTrustMigrationReport? PdfTrust { get; init; }
}
