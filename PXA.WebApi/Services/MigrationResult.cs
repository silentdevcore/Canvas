namespace PXA.WebApi.Services;

public sealed record MigrationResult(
    string PxaCode,
    IReadOnlyList<MigrationDiagnostic> Diagnostics,
    MigrationSummary Summary);

public sealed record MigrationSummary(
    int ConvertedCount,
    int WarningCount,
    int ErrorCount,
    int TotalDiagnostics);
