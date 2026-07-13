using PXA.Migration.Abstractions;
using PXA.Migration.Report.Designer.FastReport;

namespace PXA.Migration.Report.Designer.FastReport.Tests;

// Auto-discovery harness over local .frx fixtures (designer-simples/FastReport). Validates that every
// sample converts without throwing and produces a non-empty design. Skips gracefully when the local-only
// corpus is absent (e.g. CI without the samples). Genuine vendor Demos/Reports/*.frx can be dropped in
// the same folder and the harness picks them up.
public sealed class FastReportSamplesTests
{
    [Fact]
    public void Convert_FastReportSamples_AllConvertWithoutEmptyDesigns()
    {
        if (FindSamplesRoot() is not { } root)
            return;

        var files = Directory.GetFiles(root, "*.frx", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
            return;

        var converter = new FrxToDesignConverter();
        var failures = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var xml = File.ReadAllText(file);
                if (!FrxToDesignConverter.LooksLikeFrx(xml))
                {
                    failures.Add($"{Path.GetFileName(file)}: not detected as FRX");
                    continue;
                }

                var result = converter.Convert(xml);
                var count = result.Design.Pages.Sum(p => p.Elements.Count) + result.Design.SharedElements.Count;
                if (count == 0) failures.Add($"{Path.GetFileName(file)}: produced no elements");
                if (result.Design.PageSettings is null) failures.Add($"{Path.GetFileName(file)}: missing page settings");
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} sample(s) failed:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void Convert_EmployeesByCountryFixture_MultiPageWithGroupsAndTable()
    {
        if (FindSamplesRoot() is not { } root)
            return;
        var file = Directory.GetFiles(root, "EmployeesByCountry.frx", SearchOption.AllDirectories).FirstOrDefault();
        if (file is null)
            return;

        var result = new FrxToDesignConverter().Convert(File.ReadAllText(file));
        var design = result.Design;

        Assert.Equal(2, design.Pages.Count);                                         // two ReportPages
        var all = design.Pages.SelectMany(p => p.Elements).Concat(design.SharedElements).ToList();
        Assert.Contains(all, e => e.Name == "country" && e.Repeat is not null);       // group repeat
        Assert.Contains(all, e => e.Name == "summary" && e.Type == "table");          // table extracted
        Assert.True(Has(result.Diagnostics, "CANMIGFRX015"));                         // multi-page
    }

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

    private static string? FindSamplesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "designer-simples", "FastReport");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
