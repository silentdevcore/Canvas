using PVAD.Core.Contracts;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Report;

public sealed class ReportMigrationResult
{
    public required DesignExportDto Design { get; init; }

    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();
}
