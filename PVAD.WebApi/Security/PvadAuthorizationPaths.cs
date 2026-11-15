namespace PVAD.WebApi.Security;

internal static class PvadAuthorizationPaths
{
    public static PathString Normalize(PathString path)
    {
        if (path.StartsWithSegments("/api/pvad/v1", StringComparison.OrdinalIgnoreCase))
            return path;
        if (path.StartsWithSegments("/api/pvad", StringComparison.OrdinalIgnoreCase, out var remainder) &&
            new[] { "/document", "/export", "/migration", "/pdf-viewer", "/spreadsheet", "/templates" }
                .Any(area => remainder.StartsWithSegments(area, StringComparison.OrdinalIgnoreCase)))
            return new PathString("/api").Add(remainder);
        return path;
    }
}
