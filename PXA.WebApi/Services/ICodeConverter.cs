namespace PXA.WebApi.Services;

public interface ICodeConverter
{
    string FrameworkId { get; }
    string FrameworkName { get; }
    string Status { get; }
    string Description { get; }

    /// <summary>Migration target: "pdf" (→ PXA.Pdf code) or "spreadsheet" (→ PXA spreadsheet code).</summary>
    string Kind { get; }

    string ConvertCode(string sourceCode);
    byte[] GeneratePreview(string sourceCode);
    IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode);
}
