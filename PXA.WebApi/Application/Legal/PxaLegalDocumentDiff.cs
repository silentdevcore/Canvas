namespace PXA.WebApi.Application.Legal;

public static class PxaLegalDocumentDiff
{
    private const long MaximumMatrixCells = 2_000_000;

    public static LegalDocumentDiffResult Compare(string baseMarkdown, string targetMarkdown)
    {
        var baseLines = Lines(baseMarkdown);
        var targetLines = Lines(targetMarkdown);
        var operations = (long)(baseLines.Length + 1) * (targetLines.Length + 1) <= MaximumMatrixCells
            ? BuildLcsOperations(baseLines, targetLines)
            : BuildBoundedOperations(baseLines, targetLines);
        var rows = AlignChangeRuns(operations);
        return new LegalDocumentDiffResult(
            rows,
            rows.Count(value => value.Kind == LegalDiffKind.Unchanged),
            rows.Count(value => value.Kind == LegalDiffKind.Modified),
            rows.Count(value => value.Kind == LegalDiffKind.Added),
            rows.Count(value => value.Kind == LegalDiffKind.Removed));
    }

    private static string[] Lines(string value) =>
        PxaLegalDocumentService.NormalizeMarkdown(value).Split('\n');

    private static IReadOnlyList<DiffOperation> BuildLcsOperations(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        var lengths = new int[left.Count + 1, right.Count + 1];
        for (var leftIndex = left.Count - 1; leftIndex >= 0; leftIndex--)
        for (var rightIndex = right.Count - 1; rightIndex >= 0; rightIndex--)
        {
            lengths[leftIndex, rightIndex] = string.Equals(
                left[leftIndex], right[rightIndex], StringComparison.Ordinal)
                ? lengths[leftIndex + 1, rightIndex + 1] + 1
                : Math.Max(
                    lengths[leftIndex + 1, rightIndex],
                    lengths[leftIndex, rightIndex + 1]);
        }

        var operations = new List<DiffOperation>();
        var i = 0;
        var j = 0;
        while (i < left.Count && j < right.Count)
        {
            if (string.Equals(left[i], right[j], StringComparison.Ordinal))
            {
                operations.Add(new DiffOperation(LegalDiffKind.Unchanged, left[i], right[j]));
                i++;
                j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                operations.Add(new DiffOperation(LegalDiffKind.Removed, left[i++], null));
            }
            else
            {
                operations.Add(new DiffOperation(LegalDiffKind.Added, null, right[j++]));
            }
        }
        while (i < left.Count)
            operations.Add(new DiffOperation(LegalDiffKind.Removed, left[i++], null));
        while (j < right.Count)
            operations.Add(new DiffOperation(LegalDiffKind.Added, null, right[j++]));
        return operations;
    }

    private static IReadOnlyList<DiffOperation> BuildBoundedOperations(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        var prefix = 0;
        while (prefix < left.Count &&
               prefix < right.Count &&
               string.Equals(left[prefix], right[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < left.Count - prefix &&
               suffix < right.Count - prefix &&
               string.Equals(
                   left[left.Count - suffix - 1],
                   right[right.Count - suffix - 1],
                   StringComparison.Ordinal))
        {
            suffix++;
        }

        var operations = new List<DiffOperation>();
        for (var index = 0; index < prefix; index++)
            operations.Add(new DiffOperation(LegalDiffKind.Unchanged, left[index], right[index]));
        for (var index = prefix; index < left.Count - suffix; index++)
            operations.Add(new DiffOperation(LegalDiffKind.Removed, left[index], null));
        for (var index = prefix; index < right.Count - suffix; index++)
            operations.Add(new DiffOperation(LegalDiffKind.Added, null, right[index]));
        for (var index = suffix; index > 0; index--)
        {
            operations.Add(new DiffOperation(
                LegalDiffKind.Unchanged,
                left[left.Count - index],
                right[right.Count - index]));
        }
        return operations;
    }

    private static IReadOnlyList<LegalDocumentDiffLine> AlignChangeRuns(
        IReadOnlyList<DiffOperation> operations)
    {
        var rows = new List<LegalDocumentDiffLine>();
        var leftLine = 1;
        var rightLine = 1;
        var index = 0;
        while (index < operations.Count)
        {
            if (operations[index].Kind == LegalDiffKind.Unchanged)
            {
                rows.Add(new LegalDocumentDiffLine(
                    LegalDiffKind.Unchanged,
                    leftLine++,
                    rightLine++,
                    operations[index].Left,
                    operations[index].Right));
                index++;
                continue;
            }

            var removed = new List<string>();
            var added = new List<string>();
            while (index < operations.Count && operations[index].Kind != LegalDiffKind.Unchanged)
            {
                if (operations[index].Kind == LegalDiffKind.Removed)
                    removed.Add(operations[index].Left!);
                else
                    added.Add(operations[index].Right!);
                index++;
            }
            var count = Math.Max(removed.Count, added.Count);
            for (var changeIndex = 0; changeIndex < count; changeIndex++)
            {
                var hasLeft = changeIndex < removed.Count;
                var hasRight = changeIndex < added.Count;
                rows.Add(new LegalDocumentDiffLine(
                    hasLeft && hasRight
                        ? LegalDiffKind.Modified
                        : hasLeft ? LegalDiffKind.Removed : LegalDiffKind.Added,
                    hasLeft ? leftLine++ : null,
                    hasRight ? rightLine++ : null,
                    hasLeft ? removed[changeIndex] : null,
                    hasRight ? added[changeIndex] : null));
            }
        }
        return rows;
    }

    private sealed record DiffOperation(
        LegalDiffKind Kind,
        string? Left,
        string? Right);
}

public enum LegalDiffKind
{
    Unchanged,
    Modified,
    Added,
    Removed,
}

public sealed record LegalDocumentDiffLine(
    LegalDiffKind Kind,
    int? BaseLineNumber,
    int? TargetLineNumber,
    string? BaseText,
    string? TargetText);

public sealed record LegalDocumentDiffResult(
    IReadOnlyList<LegalDocumentDiffLine> Lines,
    int Unchanged,
    int Modified,
    int Added,
    int Removed);
