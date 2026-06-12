namespace Canvas.WebApi.Services;

public interface ICodeConverter
{
    string FrameworkId { get; }
    string FrameworkName { get; }
    string Status { get; }
    string Description { get; }

    string ConvertCode(string sourceCode);
    byte[] GeneratePreview(string sourceCode);
    IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode);
}
