using Canvas.Core.Contracts;
using PXA.Migration.Abstractions;

namespace PXA.Migration.Report;

public sealed class ReportMigrationResult
{
    public required DesignExportDto Design { get; init; }

    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();
}
