using Canvas.Migration.ActiveReportsJs;

namespace Canvas.Migration.ActiveReportsJs.Tests;

public sealed class ActiveReportsJsSamplesTests
{
    [Fact]
    public void Convert_ActiveReportsJsSamples_AllMarkedReportsConvertWithoutEmptyDesigns()
    {
        if (FindActiveReportsSamplesRoot() is not { } root)
            return; // local-only sample corpus not present (for example CI) - skip gracefully

        var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            return; // ActiveReports sample corpus currently contains .rdlx page reports, not JS JSON.

        var converter = new ActiveReportsJsToDesignConverter();
        var failures = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                if (!ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(json))
                    continue; // Ignore data/config JSON until we have real ActiveReports JS markers.

                var result = converter.Convert(json);
                var elements = result.Design.Pages.SelectMany(p => p.Elements)
                    .Concat(result.Design.SharedElements)
                    .ToList();

                if (elements.Count == 0)
                    failures.Add($"{Relative(root, file)}: converted to an empty design");
                if (result.Design.PageSettings is null)
                    failures.Add($"{Relative(root, file)}: missing page settings");
            }
            catch (Exception ex)
            {
                failures.Add($"{Relative(root, file)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} sample(s) failed:\n{string.Join("\n", failures)}");
    }

    private static string? FindActiveReportsSamplesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "designer-simples", "ActiveReports");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file);
}
