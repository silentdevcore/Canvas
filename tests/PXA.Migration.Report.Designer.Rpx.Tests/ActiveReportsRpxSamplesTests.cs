using PXA.Migration.Abstractions;
using PXA.Migration.Report.Designer.Rpx;

namespace PXA.Migration.Report.Designer.Rpx.Tests;

public sealed class ActiveReportsRpxSamplesTests
{
    [Fact]
    public void Convert_ActiveReportsRpxSamples_AllConvertWithoutEmptyDesigns()
    {
        if (FindActiveReportsSamplesRoot() is not { } root)
            return; // local-only sample corpus not present (for example CI) — skip gracefully

        var files = Directory.GetFiles(root, "*.rpx", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            return; // ActiveReports sample corpus currently only contains .rdlx page reports.

        var converter = new RpxToDesignConverter();
        var failures = new List<string>();

        foreach (var file in files)
        {
            try
            {
                var xml = File.ReadAllText(file);
                if (!RpxToDesignConverter.LooksLikeRpx(xml))
                {
                    failures.Add($"{Relative(root, file)}: not detected as RPX");
                    continue;
                }

                var result = converter.Convert(xml);
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

    [Fact]
    public void Convert_GroupedSalesRpxFixture_PreservesGroupsPageBreakAndSubreport()
    {
        if (FindActiveReportsSamplesRoot() is not { } root)
            return; // local-only corpus not present

        var file = Directory.GetFiles(root, "GroupedSales.rpx", SearchOption.AllDirectories).FirstOrDefault();
        if (file is null)
            return;

        var result = new RpxToDesignConverter().Convert(File.ReadAllText(file));
        var design = result.Design;
        var elements = design.Pages.SelectMany(p => p.Elements).Concat(design.SharedElements).ToList();

        // Landscape Letter page.
        Assert.True(design.PageSettings!.Width > design.PageSettings.Height);
        // Group bands carry repeat metadata; CanGrow, page-break, and sub-report are all surfaced.
        Assert.Contains(elements, e => e.Style is not null && e.Style.ContainsKey("rpxGroupRepeat"));
        Assert.True(Has(result.Diagnostics, "CANMIGRPX013"));   // group repeat
        Assert.True(Has(result.Diagnostics, "CANMIGRPX014"));   // CanGrow/CanShrink
        Assert.Contains(elements, e => e.Content is not null && e.Content.Contains("Sub-report"));
    }

    [Fact]
    public void Convert_InvoiceRpxFixture_PortraitWithSharedHeaderFooter()
    {
        if (FindActiveReportsSamplesRoot() is not { } root)
            return;

        var file = Directory.GetFiles(root, "Invoice.rpx", SearchOption.AllDirectories).FirstOrDefault();
        if (file is null)
            return;

        var design = new RpxToDesignConverter().Convert(File.ReadAllText(file)).Design;
        Assert.True(design.PageSettings!.Height > design.PageSettings.Width);   // A4 portrait
        Assert.NotEmpty(design.Pages[0].Elements);
        Assert.NotEmpty(design.SharedElements);                                 // page header/footer
    }

    private static bool Has(IEnumerable<MigrationDiagnostic> diags, string id) => diags.Any(x => x.Id == id);

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
