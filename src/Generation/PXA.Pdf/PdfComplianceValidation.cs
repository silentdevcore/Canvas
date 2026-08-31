namespace PXA.Pdf;

public enum PdfComplianceDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed record PdfComplianceDiagnostic(
    string Code,
    PdfComplianceDiagnosticSeverity Severity,
    string Message,
    int? PageNumber = null);

public sealed record PdfComplianceValidationReport(
    PdfComplianceProfile Profile,
    bool IsValid,
    string Validator,
    IReadOnlyList<PdfComplianceDiagnostic> Diagnostics);

public sealed class PdfComplianceException : ArgumentException
{
    public PdfComplianceException(PdfComplianceValidationReport report)
        : base(BuildMessage(report))
    {
        Report = report;
    }

    public PdfComplianceValidationReport Report { get; }

    private static string BuildMessage(PdfComplianceValidationReport report)
    {
        var errors = report.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == PdfComplianceDiagnosticSeverity.Error)
            .Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}");
        return $"The document cannot be written as {report.Profile}: {string.Join("; ", errors)}";
    }
}
