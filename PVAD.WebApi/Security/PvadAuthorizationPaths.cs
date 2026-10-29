namespace PVAD.WebApi.Security;

internal static class PvadAuthorizationPaths
{
    public static PathString Normalize(PathString path)
    {
        if (path.StartsWithSegments("/api/pxa/v1", StringComparison.OrdinalIgnoreCase, out var versioned))
            return new PathString("/api/pvad/v1").Add(versioned);
        foreach (var prefix in new[] { "/api/pvad", "/api/pxa" })
        {
            if (!path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase, out var remainder)) continue;
            if (new[] { "/document", "/export", "/migration", "/pdf-viewer", "/spreadsheet", "/templates" }
                .Any(area => remainder.StartsWithSegments(area, StringComparison.OrdinalIgnoreCase)))
                return new PathString("/api").Add(remainder);
        }
        return path;
    }
}
