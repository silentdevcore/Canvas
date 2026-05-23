using Canvas.Core.Contracts;

namespace Canvas.Core.Primitives;

public sealed record PlannedPage(string PageId, IReadOnlyList<ElementDto> Elements);

public static class DesignLayoutPlanner
{
    public static IReadOnlyList<PlannedPage> BuildPages(
        DesignExportDto design,
        Func<ElementDto, int>? zIndexSelector = null)
    {
        ArgumentNullException.ThrowIfNull(design);

        var sharedElements = design.SharedElements ?? [];
        var pages = design.Pages ?? [];
        var allPages = pages.Count > 0
            ? pages
            : [new PageDto { Id = "p1", Elements = sharedElements }];

        var planned = new List<PlannedPage>(allPages.Count);
        foreach (var page in allPages)
        {
            var pageElements = page.Elements ?? [];
            var visible = pageElements
                .Concat(sharedElements.Where(s => !pageElements.Any(e => e.Id == s.Id)))
                .Where(e => e.Hidden != true)
                .OrderBy(e => e.Y)
                .ThenBy(e => e.X)
                .ThenBy(zIndexSelector ?? (_ => 0))
                .ThenBy(e => e.Id, StringComparer.Ordinal)
                .ToList();

            planned.Add(new PlannedPage(page.Id, visible));
        }

        return planned;
    }
}