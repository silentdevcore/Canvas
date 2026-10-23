namespace PVAD.Migration.Abstractions;

public sealed record MigrationArtifactIdentity(string Id, string MediaType, string FileExtension);

public static class MigrationArtifactIdentities
{
    public const string LegacyViewerPackageKind = "pxa.pdf-viewer-package";
    public const string ViewerPackageKind = "pvad.pdf-viewer-package";

    public static MigrationArtifactIdentity? Resolve(string id, string mediaType, string? extension) =>
        (id, mediaType, extension) switch
        {
            ("pxa-csharp", "text/x-csharp", ".cs") or
            ("pvad-csharp", "text/x-csharp", ".cs") => new("pvad-csharp", "text/x-csharp", ".cs"),
            ("pxa-spreadsheet-csharp", "text/x-csharp", ".cs") or
            ("pvad-spreadsheet-csharp", "text/x-csharp", ".cs") => new("pvad-spreadsheet-csharp", "text/x-csharp", ".cs"),
            ("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json") or
            ("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json") =>
                new("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json"),
            _ => null,
        };
}
