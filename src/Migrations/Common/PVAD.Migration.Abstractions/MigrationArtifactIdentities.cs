namespace PVAD.Migration.Abstractions;

public sealed record MigrationArtifactIdentity(string Id, string MediaType, string FileExtension);

public static class MigrationArtifactIdentities
{
    public const string LegacyViewerPackageKind = "pxa.pdf-viewer-package";
    public const string ViewerPackageKind = "pvad.pdf-viewer-package";

    public static MigrationArtifactIdentity PdfCode { get; } = new("pvad-csharp", "text/x-csharp", ".cs");
    public static MigrationArtifactIdentity SpreadsheetCode { get; } = new("pvad-spreadsheet-csharp", "text/x-csharp", ".cs");
    public static MigrationArtifactIdentity ViewerPackage { get; } =
        new("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json");

    public static MigrationArtifactIdentity? Resolve(string id, string mediaType, string? extension) =>
        (id, mediaType, extension) switch
        {
            ("pxa-csharp", "text/x-csharp", ".cs") or
            ("pvad-csharp", "text/x-csharp", ".cs") => PdfCode,
            ("pxa-spreadsheet-csharp", "text/x-csharp", ".cs") or
            ("pvad-spreadsheet-csharp", "text/x-csharp", ".cs") => SpreadsheetCode,
            ("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json") or
            ("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json") =>
                ViewerPackage,
            _ => null,
        };

    public static MigrationArtifactIdentity ForOutput(MigrationArtifactIdentity identity, bool legacy) =>
        !legacy ? identity : identity switch
        {
            var value when value == PdfCode => new("pxa-csharp", "text/x-csharp", ".cs"),
            var value when value == SpreadsheetCode => new("pxa-spreadsheet-csharp", "text/x-csharp", ".cs"),
            var value when value == ViewerPackage =>
                new("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json"),
            _ => identity,
        };
}
