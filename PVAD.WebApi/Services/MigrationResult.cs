namespace PVAD.WebApi.Services;

public sealed record MigrationResult(
    string PvadCode,
    IReadOnlyList<MigrationDiagnostic> Diagnostics,
    MigrationSummary Summary)
{

    public PVAD.Migration.Abstractions.PdfTrustMigrationReport? PdfTrust { get; init; }
}

public sealed record MigrationSummary(
    int ConvertedCount,
    int WarningCount,
    int ErrorCount,
    int TotalDiagnostics);
