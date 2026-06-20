using Canvas.Migration.Rdl;

namespace Canvas.Migration.Rdl.Tests;

// Smoke test over the local Syncfusion / Bold Reports `.rdl` corpus (145 real-world reports under
// designer-simples/syncfustion). Locks in that the RDL converter — shared by Syncfusion, SSRS/RDLC,
// and ActiveReports `.rdlx` — converts every real sample without throwing and without producing an
// empty design. Skips gracefully when the local-only sample corpus is absent (e.g. CI checkouts).
public sealed class SyncfusionRdlSamplesTests
{
    [Fact]
    public void Convert_AllSyncfusionRdlSamples_NeverThrowAndProduceElements()
    {
        if (FindSamplesDir() is not { } dir)
            return; // local-only resources not present

        var files = Directory.GetFiles(dir, "*.rdl", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var file in files)
        {
            var xml = File.ReadAllText(file);
            if (!RdlToDesignConverter.LooksLikeRdl(xml))
            {
                failures.Add($"{Path.GetFileName(file)}: not detected as RDL");
                continue;
            }
            try
            {
                var result = new RdlToDesignConverter().Convert(xml);
                var count = result.Design.Pages.Sum(p => p.Elements.Count) + result.Design.SharedElements.Count;
                if (count == 0)
                    failures.Add($"{Path.GetFileName(file)}: produced no elements");
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} sample(s) failed:\n{string.Join("\n", failures)}");
    }

    private static string? FindSamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "designer-simples", "syncfustion");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
