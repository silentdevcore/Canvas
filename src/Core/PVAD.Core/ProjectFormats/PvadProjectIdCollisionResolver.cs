using PVAD.Core.Contracts;

namespace PVAD.Core.ProjectFormats;

public sealed record PvadProjectIdRemapResult(IReadOnlyDictionary<string, string> Remaps)
{
    public bool HasRemaps => Remaps.Count > 0;
}

public static class PvadProjectIdCollisionResolver
{
    public static PvadProjectIdRemapResult Resolve(
        DesignExportDto document,
        IEnumerable<string>? reservedIds)
    {
        ArgumentNullException.ThrowIfNull(document);
        var allocator = new IdAllocator(reservedIds);

        document.Id = allocator.Allocate(document.Id, "document");
        foreach (var style in document.PageSettings?.NamedStyles ?? [])
            style.Id = allocator.Allocate(style.Id, "style");
        foreach (var page in document.Pages)
        {
            page.Id = allocator.Allocate(page.Id, "page");
            foreach (var element in page.Elements)
                element.Id = allocator.Allocate(element.Id, "element");
        }
        foreach (var element in document.SharedElements)
            element.Id = allocator.Allocate(element.Id, "element");

        foreach (var style in document.PageSettings?.NamedStyles ?? [])
        {
            style.BasedOn = allocator.Reference(style.BasedOn);
            style.NextStyle = allocator.Reference(style.NextStyle);
        }
        foreach (var element in document.Pages.SelectMany(page => page.Elements).Concat(document.SharedElements))
        {
            if (element.Accessibility is not null)
            {
                element.Accessibility.ParentElementId = allocator.Reference(element.Accessibility.ParentElementId);
                if (element.Accessibility.TableHeaderIds is not null)
                    element.Accessibility.TableHeaderIds = element.Accessibility.TableHeaderIds
                        .Select(id => allocator.Reference(id) ?? id).ToList();
            }
            if (element.Repeat is not null)
                element.Repeat.TemplateId = allocator.Reference(element.Repeat.TemplateId);
            element.BookmarkTarget = allocator.Reference(element.BookmarkTarget);
        }

        return new PvadProjectIdRemapResult(allocator.Remaps);
    }

    public static PvadProjectIdRemapResult Resolve(
        SpreadsheetDto workbook,
        IEnumerable<string>? reservedIds)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var allocator = new IdAllocator(reservedIds);

        workbook.Id = allocator.Allocate(workbook.Id, "workbook");
        foreach (var sheet in workbook.Sheets)
        {
            sheet.Id = allocator.Allocate(sheet.Id, "sheet");
            foreach (var image in sheet.Images)
                image.Id = allocator.Allocate(image.Id, "image");
        }

        return new PvadProjectIdRemapResult(allocator.Remaps);
    }

    private sealed class IdAllocator
    {
        private readonly HashSet<string> allocated;
        private readonly Dictionary<string, string> remaps = new(StringComparer.Ordinal);

        public IdAllocator(IEnumerable<string>? reservedIds)
        {
            allocated = new HashSet<string>(
                reservedIds?.Where(value => !string.IsNullOrWhiteSpace(value)) ?? [],
                StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, string> Remaps => remaps;

        public string Allocate(string? source, string fallback)
        {
            var original = string.IsNullOrWhiteSpace(source) ? fallback : source;
            if (allocated.Add(original))
                return original;

            var suffix = 2;
            string candidate;
            do
            {
                var suffixText = $"-import-{suffix++}";
                var stem = original[..Math.Min(original.Length, 200 - suffixText.Length)];
                candidate = stem + suffixText;
            } while (!allocated.Add(candidate));

            remaps.TryAdd(original, candidate);
            return candidate;
        }

        public string? Reference(string? source) =>
            source is not null && remaps.TryGetValue(source, out var replacement)
                ? replacement
                : source;
    }
}
