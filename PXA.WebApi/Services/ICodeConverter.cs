namespace PXA.WebApi.Services;

public interface ICodeConverter
{
    string FrameworkId { get; }
    string FrameworkName { get; }
    string Status { get; }
    string Description { get; }

    /// <summary>Migration target: "pdf" (→ Canvas.Pdf code) or "spreadsheet" (→ Canvas spreadsheet code).</summary>
    string Kind { get; }

    string ConvertCode(string sourceCode);
    byte[] GeneratePreview(string sourceCode);
    IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode);
}
