using CanvasDiagnostic = Canvas.Migration.Abstractions.MigrationDiagnostic;
using CanvasDiagnosticSeverity = Canvas.Migration.Abstractions.MigrationDiagnosticSeverity;
using PxaDiagnostic = PXA.Migration.Abstractions.MigrationDiagnostic;
using PxaDiagnosticSeverity = PXA.Migration.Abstractions.MigrationDiagnosticSeverity;

namespace PXA.Migration.Report;

internal static class ReportDiagnosticMapper
{
    public static IReadOnlyList<PxaDiagnostic> ToPxaDiagnostics(IReadOnlyList<CanvasDiagnostic> diagnostics)
    {
        return diagnostics.Select(ToPxaDiagnostic).ToArray();
    }

    private static PxaDiagnostic ToPxaDiagnostic(CanvasDiagnostic diagnostic)
    {
        return new PxaDiagnostic
        {
            Id = diagnostic.Id,
            Message = diagnostic.Message,
            Severity = ToPxaSeverity(diagnostic.Severity),
        };
    }

    private static PxaDiagnosticSeverity ToPxaSeverity(CanvasDiagnosticSeverity severity)
    {
        return severity switch
        {
            CanvasDiagnosticSeverity.Warning => PxaDiagnosticSeverity.Warning,
            CanvasDiagnosticSeverity.Error => PxaDiagnosticSeverity.Error,
            _ => PxaDiagnosticSeverity.Info,
        };
    }
}
