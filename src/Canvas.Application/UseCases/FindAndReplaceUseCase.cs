using Canvas.Core.Contracts;

namespace Canvas.Application.UseCases;

public sealed class FindAndReplaceRequest
{
    public required DesignExportDto Design { get; set; }
    public required string Find { get; set; }
    public required string Replace { get; set; }
    public bool CaseSensitive { get; set; } = false;
    public bool WholeWord { get; set; } = false;
    public bool UseRegex { get; set; } = false;
}

public sealed class FindAndReplaceResult
{
    public required DesignExportDto Design { get; set; }
    public int ReplacementCount { get; set; }
    public List<string> AffectedElementIds { get; set; } = [];
}

/// <summary>
/// Searches all text-bearing element fields across every page and shared element
/// and replaces occurrences of a search string (or regex) with a replacement value.
/// The original <see cref="DesignExportDto"/> is not mutated — a shallow copy with
/// replaced string values is returned.
/// </summary>
public sealed class FindAndReplaceUseCase
{
    public FindAndReplaceResult Execute(FindAndReplaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Find))
            return new FindAndReplaceResult { Design = request.Design, ReplacementCount = 0 };

        var count     = 0;
        var affected  = new List<string>();
        var design    = request.Design;

        var allElements = design.Pages
            .SelectMany(p => p.Elements)
            .Concat(design.SharedElements)
            .ToList();

        foreach (var el in allElements)
            Replace(el, request, ref count, affected);

        return new FindAndReplaceResult
        {
            Design           = design,
            ReplacementCount = count,
            AffectedElementIds = affected,
        };
    }

    private static void Replace(ElementDto el, FindAndReplaceRequest req, ref int count, List<string> affected)
    {
        var changed = false;

        // C# doesn't allow ref on auto-property getters — copy to local, write back.
        changed |= ReplaceProperty(el, req, ref count, () => el.Content,                    v => el.Content                    = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.HtmlContent,               v => el.HtmlContent                = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.NoteTitle,                 v => el.NoteTitle                  = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.NoteBody,                  v => el.NoteBody                   = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.FootnoteText,              v => el.FootnoteText               = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.CommentText,               v => el.CommentText                = v);
        changed |= ReplaceProperty(el, req, ref count, () => el.ContentControlPlaceholder, v => el.ContentControlPlaceholder  = v);

        if (el.CellData is not null)
        {
            for (int r = 0; r < el.CellData.Length; r++)
            {
                var row = el.CellData[r];
                if (row is null) continue;
                for (int c = 0; c < row.Length; c++)
                {
                    var cell = row[c];
                    if (ReplaceField(ref cell, req, ref count))
                    {
                        row[c] = cell ?? "";
                        changed = true;
                    }
                }
            }
        }

        if (changed && !affected.Contains(el.Id))
            affected.Add(el.Id);
    }

    private static bool ReplaceField(ref string? value, FindAndReplaceRequest req, ref int count)
    {
        if (string.IsNullOrEmpty(value)) return false;

        string original = value;
        string result;

        if (req.UseRegex)
        {
            var options = req.CaseSensitive
                ? System.Text.RegularExpressions.RegexOptions.None
                : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            var regex = new System.Text.RegularExpressions.Regex(req.Find, options);
            int before = regex.Matches(value).Count;
            result = regex.Replace(value, req.Replace);
            count += before;
        }
        else
        {
            var comparison = req.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var pattern = req.WholeWord ? $@"\b{System.Text.RegularExpressions.Regex.Escape(req.Find)}\b" : null;

            if (pattern is not null)
            {
                var options = req.CaseSensitive
                    ? System.Text.RegularExpressions.RegexOptions.None
                    : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
                var regex = new System.Text.RegularExpressions.Regex(pattern, options);
                int before = regex.Matches(value).Count;
                result = regex.Replace(value, req.Replace);
                count += before;
            }
            else
            {
                int before = CountOccurrences(value, req.Find, comparison);
                result = value.Replace(req.Find, req.Replace, comparison);
                count += before;
            }
        }

        if (result == original) return false;
        value = result;
        return true;
    }

    private static bool ReplaceProperty(ElementDto el, FindAndReplaceRequest req, ref int count,
        Func<string?> get, Action<string?> set)
    {
        var value = get();
        if (!ReplaceField(ref value, req, ref count)) return false;
        set(value);
        return true;
    }

    private static int CountOccurrences(string source, string find, StringComparison comparison)
    {
        int n = 0, idx = 0;
        while ((idx = source.IndexOf(find, idx, comparison)) >= 0)
        {
            n++;
            idx += find.Length;
        }
        return n;
    }
}
