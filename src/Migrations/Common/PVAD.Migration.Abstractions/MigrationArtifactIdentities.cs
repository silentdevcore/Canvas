namespace PVAD.Migration.Abstractions;

public sealed record MigrationArtifactIdentity(string Id, string MediaType, string FileExtension);

public static class MigrationArtifactIdentities
{
    public const string ViewerPackageKind = "pvad.pdf-viewer-package";

    public static MigrationArtifactIdentity PdfCode { get; } = new("pvad-csharp", "text/x-csharp", ".cs");
    public static MigrationArtifactIdentity SpreadsheetCode { get; } = new("pvad-spreadsheet-csharp", "text/x-csharp", ".cs");
    public static MigrationArtifactIdentity ViewerPackage { get; } =
        new("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json");

    public static MigrationArtifactIdentity? Resolve(string id, string mediaType, string? extension) =>
        new[] { PdfCode, SpreadsheetCode, ViewerPackage }
            .FirstOrDefault(identity => identity == new MigrationArtifactIdentity(id, mediaType, extension ?? ""));
}
