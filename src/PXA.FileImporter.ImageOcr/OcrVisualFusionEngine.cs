using SkiaSharp;

namespace PXA.FileImporter.ImageOcr;

// Pipeline stage 3: fuses OCR text with visual detection results. Detects text
// tables (aligned-word / split-line / word-grid), enriches them with visual rule
// and background bounds, maps OCR labels onto field/signature candidates, and
// groups the remaining OCR lines into standalone text groups. Promoted verbatim
// from ImageToPdfConverter.
internal static class OcrVisualFusionEngine
{
    // ----- Table detection -----

    public static IReadOnlyList<OcrTableCandidate> DetectTables(
        IReadOnlyList<OcrLine> lines,
        ImageToPdfConversionOptions options)
    {
        if (!string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.LayoutMode, "tables", StringComparison.OrdinalIgnoreCase))
            return [];

        var result = new List<OcrTableCandidate>();
        var usedLines = new HashSet<OcrLine>();
        var index = 0;
        while (index < lines.Count)
        {
            var line = lines[index];
            var columnCount = CountUsableWords(line);
            if (columnCount < 2)
            {
                index++;
                continue;
            }

            var group = new List<OcrLine> { line };
            var anchors = GetWordAnchors(line);
            var tolerance = Math.Max(12, line.Bounds.Height * 1.5);
            var next = index + 1;

            while (next < lines.Count &&
                   TryMergeTableAnchors(anchors, GetWordAnchors(lines[next]), tolerance, out var mergedAnchors))
            {
                group.Add(lines[next]);
                anchors = mergedAnchors;
                columnCount = anchors.Length;
                next++;
            }

            if (group.Count >= 2)
            {
                result.Add(new OcrTableCandidate(
                    group,
                    anchors,
                    null,
                    null,
                    group.Select(l => (IReadOnlyList<OcrLine>)[l]).ToArray(),
                    "aligned-word-table",
                    null));
                foreach (var groupedLine in group)
                    usedLines.Add(groupedLine);
                index = next;
            }
            else
            {
                index++;
            }
        }

        var remainingLines = lines.Where(l => !usedLines.Contains(l)).ToArray();
        var splitLineTables = DetectSplitLineTables(remainingLines);
        result.AddRange(splitLineTables);
        foreach (var groupedLine in splitLineTables.SelectMany(t => t.Lines))
            usedLines.Add(groupedLine);

        result.AddRange(DetectWordGridTables(lines.Where(l => !usedLines.Contains(l)).ToArray()));
        return result;
    }

    // ----- Column-aligned (borderless) table detection -----
    //
    // Reconstructs tables from text alignment alone — no rule lines or shaded cells required —
    // for the text-background / text-only layout modes where invoices and reports commonly use
    // borderless, light-header tables. OCR frequently emits each cell as a separate line at the
    // same baseline, so lines are first merged into visual rows; a wide row with >=3 well-separated
    // word groups seeds the column anchors (typically the header), and following rows that fall into
    // those columns are absorbed. Requiring >=3 columns excludes 2-column layouts such as the
    // FROM/BILL-TO block and the totals summary, which previously caused false-positive tables.
    private sealed record VisualRow(IReadOnlyList<OcrLine> Lines, int Top, int Bottom, int Height, IReadOnlyList<double> WordCenters);

    public static IReadOnlyList<OcrTableCandidate> DetectColumnAlignedTables(IReadOnlyList<OcrLine> lines)
    {
        var usable = lines.Where(l => l.Words.Any(w => !string.IsNullOrWhiteSpace(w.Text))).ToList();
        var words = usable.SelectMany(l => l.Words).Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
        if (usable.Count < 3 || words.Count == 0)
            return [];

        var contentLeft = words.Min(w => w.Bounds.X);
        var contentRight = words.Max(w => w.Bounds.X + w.Bounds.Width);
        var contentWidth = Math.Max(1, contentRight - contentLeft);
        var medianHeight = Math.Max(1, usable.OrderBy(l => l.Bounds.Height).ElementAt(usable.Count / 2).Bounds.Height);
        var columnTolerance = Math.Max(30.0, medianHeight * 1.2);

        var rows = BuildVisualRows(usable);
        if (rows.Count < 3)
            return [];

        var result = new List<OcrTableCandidate>();
        var consumed = new bool[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            if (consumed[i])
                continue;

            var anchors = ClusterCenters(rows[i].WordCenters, columnTolerance);
            if (anchors.Count < 3 || anchors[^1] - anchors[0] < contentWidth * 0.35)
                continue;

            var minGap = MinAdjacentGap(anchors);
            if (minGap < 30)
                continue;
            var tolerance = minGap * 0.5;
            var requiredHits = Math.Max(2, anchors.Count - 1);

            var run = new List<VisualRow> { rows[i] };
            var j = i + 1;
            while (j < rows.Count && !consumed[j])
            {
                var gap = rows[j].Top - run[^1].Bottom;
                if (gap > Math.Max(run[^1].Height, rows[j].Height) * 2.2)
                    break;
                if (DistinctColumnsHit(rows[j], anchors, tolerance) < requiredHits)
                    break;
                run.Add(rows[j]);
                j++;
            }

            if (run.Count < 3) // header + at least two body rows
                continue;

            var candidateLines = run.SelectMany(r => r.Lines).ToList();
            var rowGroups = run.Select(r => (IReadOnlyList<OcrLine>)r.Lines).ToList();
            result.Add(new OcrTableCandidate(candidateLines, anchors, null, null, rowGroups, "column-aligned-text-table", null));

            for (var k = i; k < j; k++)
                consumed[k] = true;
            i = j - 1;
        }

        return result;
    }

    private static IReadOnlyList<VisualRow> BuildVisualRows(IReadOnlyList<OcrLine> lines)
    {
        var rows = new List<VisualRow>();
        foreach (var line in lines.OrderBy(l => l.Bounds.Y).ThenBy(l => l.Bounds.X))
        {
            var centerY = line.Bounds.Y + line.Bounds.Height / 2.0;
            var attached = false;
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var rowCenter = (row.Top + row.Bottom) / 2.0;
                // Same visual row when vertical centers are close relative to text height.
                if (Math.Abs(centerY - rowCenter) <= Math.Max(row.Height, line.Bounds.Height) * 0.6)
                {
                    var merged = row.Lines.Append(line).ToList();
                    var top = Math.Min(row.Top, line.Bounds.Y);
                    var bottom = Math.Max(row.Bottom, line.Bounds.Y + line.Bounds.Height);
                    var centers = merged
                        .SelectMany(l => l.Words)
                        .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                        .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
                        .OrderBy(c => c)
                        .ToList();
                    rows[r] = new VisualRow(merged, top, bottom, bottom - top, centers);
                    attached = true;
                    break;
                }
            }

            if (!attached)
            {
                var centers = line.Words
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
                    .OrderBy(c => c)
                    .ToList();
                rows.Add(new VisualRow([line], line.Bounds.Y, line.Bounds.Y + line.Bounds.Height, line.Bounds.Height, centers));
            }
        }

        return rows.OrderBy(r => r.Top).ToList();
    }

    // Collapses word centers into column anchors: centers within <paramref name="tolerance"/> of the
    // running cluster mean are merged, so multi-word cells (e.g. "Visual identity & logo system")
    // contribute a single column rather than several spurious ones.
    private static List<double> ClusterCenters(IReadOnlyList<double> centers, double tolerance)
    {
        var anchors = new List<double>();
        if (centers.Count == 0)
            return anchors;

        var clusterSum = centers[0];
        var clusterCount = 1;
        for (var i = 1; i < centers.Count; i++)
        {
            var mean = clusterSum / clusterCount;
            if (centers[i] - mean <= tolerance)
            {
                clusterSum += centers[i];
                clusterCount++;
            }
            else
            {
                anchors.Add(clusterSum / clusterCount);
                clusterSum = centers[i];
                clusterCount = 1;
            }
        }
        anchors.Add(clusterSum / clusterCount);
        return anchors;
    }

    private static double MinAdjacentGap(IReadOnlyList<double> anchors)
    {
        var min = double.MaxValue;
        for (var i = 1; i < anchors.Count; i++)
            min = Math.Min(min, anchors[i] - anchors[i - 1]);
        return min == double.MaxValue ? 0 : min;
    }

    private static int DistinctColumnsHit(VisualRow row, IReadOnlyList<double> anchors, double tolerance)
    {
        var hit = new HashSet<int>();
        foreach (var center in row.WordCenters)
        {
            var col = OcrLayoutHelpers.FindNearestColumn(center, anchors, tolerance);
            if (col >= 0)
                hit.Add(col);
        }
        return hit.Count;
    }

    private static IReadOnlyList<OcrTableCandidate> DetectWordGridTables(IReadOnlyList<OcrLine> lines)
    {
        var rows = BuildWordGridRows(lines)
            .Where(r => r.Anchors.Length >= 2)
            .ToArray();
        if (rows.Length < 2)
            return [];

        var result = new List<OcrTableCandidate>();
        var index = 0;
        while (index < rows.Length)
        {
            var group = new List<OcrTableRowCandidate> { rows[index] };
            var next = index + 1;
            while (next < rows.Length && AreRowsCloseForTable(group[^1], rows[next]))
            {
                group.Add(rows[next]);
                next++;
            }

            if (TryBuildWordGridTable(group, out var table))
                result.Add(table);

            index = Math.Max(index + 1, next);
        }

        return result;
    }

    private static bool TryBuildWordGridTable(
        IReadOnlyList<OcrTableRowCandidate> rows,
        out OcrTableCandidate table)
    {
        table = new OcrTableCandidate([], [], null, null, [], "word-grid-table", null);
        if (rows.Count < 2)
            return false;

        var anchors = BuildStableColumnAnchors(rows);
        if (anchors.Length < 2)
            return false;

        var tolerance = EstimateWordGridColumnTolerance(rows, anchors);
        var matchedRows = rows
            .Select(row => CountMatchedColumns(row.Anchors, anchors, tolerance))
            .ToArray();
        if (matchedRows.Count(c => c >= 2) < 2)
            return false;

        var populatedCells = matchedRows.Sum();
        var possibleCells = rows.Count * anchors.Length;
        if (populatedCells < possibleCells * 0.45)
            return false;

        if (!HasWordGridTableEvidence(rows, anchors.Length))
            return false;

        var rowGroups = rows.Select(r => r.Lines).ToArray();
        var tableLines = rowGroups.SelectMany(r => r).Distinct().ToArray();
        table = new OcrTableCandidate(tableLines, anchors, null, null, rowGroups, "word-grid-table", null);
        return true;
    }

    private static IReadOnlyList<OcrTableRowCandidate> BuildWordGridRows(IReadOnlyList<OcrLine> lines)
    {
        var sorted = lines
            .Where(l => CountUsableWords(l) > 0)
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToArray();
        if (sorted.Length == 0)
            return [];

        var rows = new List<List<OcrLine>>();
        foreach (var line in sorted)
        {
            var match = rows
                .Select((row, index) => new
                {
                    Row = row,
                    Index = index,
                    Distance = Math.Abs(row.Average(l => WordCenterY(l)) - WordCenterY(line)),
                    Tolerance = Math.Max(6, Math.Max(row.Average(l => AverageWordHeight(l)), AverageWordHeight(line)) * 0.85),
                })
                .Where(x => x.Distance <= x.Tolerance)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (match is null)
                rows.Add([line]);
            else
                rows[match.Index].Add(line);
        }

        return rows
            .Select(row => row.OrderBy(l => l.Bounds.X).ToArray())
            .Select(row => new OcrTableRowCandidate(row, GetRowWordAnchors(row)))
            .ToArray();
    }

    private static double[] BuildStableColumnAnchors(IReadOnlyList<OcrTableRowCandidate> rows)
    {
        var averageHeight = rows.SelectMany(r => r.Lines).Average(AverageWordHeight);
        var tolerance = Math.Max(12, averageHeight * 1.7);
        var clusters = new List<List<double>>();

        foreach (var anchor in rows.SelectMany(r => r.Anchors).Order())
        {
            var match = clusters
                .Select((cluster, index) => new
                {
                    Cluster = cluster,
                    Index = index,
                    Distance = Math.Abs(cluster.Average() - anchor),
                })
                .Where(x => x.Distance <= tolerance)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (match is null)
                clusters.Add([anchor]);
            else
                clusters[match.Index].Add(anchor);
        }

        return clusters
            .Where(c => c.Count >= 2)
            .Select(c => c.Average())
            .Order()
            .ToArray();
    }

    private static double EstimateWordGridColumnTolerance(IReadOnlyList<OcrTableRowCandidate> rows, IReadOnlyList<double> anchors)
    {
        var averageHeight = rows.SelectMany(r => r.Lines).Average(AverageWordHeight);
        if (anchors.Count < 2)
            return Math.Max(12, averageHeight * 1.7);

        var minGap = anchors.Zip(anchors.Skip(1), (a, b) => b - a).Min();
        return Math.Max(12, Math.Min(minGap * 0.38, averageHeight * 2.4));
    }

    private static int CountMatchedColumns(IReadOnlyList<double> rowAnchors, IReadOnlyList<double> anchors, double tolerance)
    {
        var matched = new HashSet<int>();
        foreach (var rowAnchor in rowAnchors)
        {
            var column = OcrLayoutHelpers.FindNearestColumn(rowAnchor, anchors, tolerance);
            if (column >= 0)
                matched.Add(column);
        }

        return matched.Count;
    }

    private static bool HasWordGridTableEvidence(IReadOnlyList<OcrTableRowCandidate> rows, int columnCount)
    {
        if (columnCount >= 3)
            return true;

        return rows
            .Skip(1)
            .SelectMany(r => r.Lines)
            .SelectMany(l => l.Words)
            .Any(w => !string.IsNullOrWhiteSpace(w.Text) && w.Text.Any(char.IsDigit));
    }

    private static IReadOnlyList<OcrTableCandidate> DetectSplitLineTables(IReadOnlyList<OcrLine> lines)
    {
        var rows = BuildSameBaselineRows(lines)
            .Where(r => r.Anchors.Length >= 2)
            .ToArray();
        if (rows.Length < 2)
            return [];

        var result = new List<OcrTableCandidate>();
        var index = 0;
        while (index < rows.Length)
        {
            var anchors = rows[index].Anchors;
            var tolerance = EstimateTableRowTolerance(rows[index]);
            var group = new List<OcrTableRowCandidate> { rows[index] };
            var next = index + 1;

            while (next < rows.Length &&
                   AreRowsCloseForTable(group[^1], rows[next]) &&
                   TryMergeTableAnchors(anchors, rows[next].Anchors, tolerance, out var mergedAnchors))
            {
                group.Add(rows[next]);
                anchors = mergedAnchors;
                tolerance = Math.Max(tolerance, EstimateTableRowTolerance(rows[next]));
                next++;
            }

            if (group.Count >= 2 && HasSplitLineTableEvidence(group, anchors.Length))
            {
                var rowGroups = group.Select(r => r.Lines).ToArray();
                var tableLines = rowGroups.SelectMany(r => r).ToArray();
                result.Add(new OcrTableCandidate(tableLines, anchors, null, null, rowGroups, "split-line-table", null));
                index = next;
            }
            else
            {
                index++;
            }
        }

        return result;
    }

    private static IReadOnlyList<OcrTableRowCandidate> BuildSameBaselineRows(IReadOnlyList<OcrLine> lines)
    {
        var sorted = lines
            .Where(l => CountUsableWords(l) > 0)
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToArray();
        if (sorted.Length == 0)
            return [];

        var rows = new List<List<OcrLine>>();
        foreach (var line in sorted)
        {
            if (rows.Count == 0 || !IsSameBaselineRow(rows[^1], line))
            {
                rows.Add([line]);
                continue;
            }

            rows[^1].Add(line);
        }

        return rows
            .Select(row => row.OrderBy(l => l.Bounds.X).ToArray())
            .Select(row => new OcrTableRowCandidate(row, GetRowCellAnchors(row)))
            .ToArray();
    }

    private static bool IsSameBaselineRow(IReadOnlyList<OcrLine> row, OcrLine line)
    {
        var rowCenter = row.Average(l => l.Bounds.Y + l.Bounds.Height / 2.0);
        var rowHeight = row.Average(l => Math.Max(1, l.Bounds.Height));
        var lineCenter = line.Bounds.Y + line.Bounds.Height / 2.0;
        var tolerance = Math.Max(6, Math.Max(rowHeight, line.Bounds.Height) * 0.7);
        return Math.Abs(lineCenter - rowCenter) <= tolerance;
    }

    private static double[] GetRowCellAnchors(IReadOnlyList<OcrLine> row)
    {
        if (row.Count >= 2)
        {
            return row
                .OrderBy(l => l.Bounds.X)
                .Select(l => l.Bounds.X + l.Bounds.Width / 2.0)
                .ToArray();
        }

        return GetWordAnchors(row[0]);
    }

    private static double[] GetRowWordAnchors(IReadOnlyList<OcrLine> row) =>
        row
            .SelectMany(l => l.Words)
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderBy(w => w.Bounds.X)
            .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
            .ToArray();

    private static double WordCenterY(OcrLine line)
    {
        var words = line.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToArray();
        if (words.Length == 0)
            return line.Bounds.Y + line.Bounds.Height / 2.0;

        return words.Average(w => w.Bounds.Y + w.Bounds.Height / 2.0);
    }

    private static double AverageWordHeight(OcrLine line)
    {
        var heights = line.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => Math.Max(1, w.Bounds.Height))
            .ToArray();
        return heights.Length == 0 ? Math.Max(1, line.Bounds.Height) : heights.Average();
    }

    private static bool AreRowsCloseForTable(OcrTableRowCandidate previous, OcrTableRowCandidate current)
    {
        var previousBounds = OcrLayoutHelpers.UnionBounds(previous.Lines.Select(l => l.Bounds));
        var currentBounds = OcrLayoutHelpers.UnionBounds(current.Lines.Select(l => l.Bounds));
        var averageHeight = Math.Max(1, (previousBounds.Height + currentBounds.Height) / 2.0);
        var verticalGap = currentBounds.Y - (previousBounds.Y + previousBounds.Height);
        return verticalGap >= -averageHeight * 0.5 && verticalGap <= Math.Max(18, averageHeight * 1.6);
    }

    private static double EstimateTableRowTolerance(OcrTableRowCandidate row)
    {
        var rowHeight = row.Lines.Average(l => Math.Max(1, l.Bounds.Height));
        if (row.Anchors.Length < 2)
            return Math.Max(12, rowHeight * 1.5);

        var minGap = row.Anchors.Zip(row.Anchors.Skip(1), (a, b) => b - a).Min();
        return Math.Max(12, Math.Min(minGap * 0.4, rowHeight * 2.0));
    }

    private static bool HasSplitLineTableEvidence(IReadOnlyList<OcrTableRowCandidate> rows, int columnCount)
    {
        if (rows.Any(r => r.Lines.Count < 2))
            return false;

        if (columnCount >= 3)
            return true;

        return rows
            .Skip(1)
            .SelectMany(r => r.Lines)
            .SelectMany(l => l.Words)
            .Any(w => !string.IsNullOrWhiteSpace(w.Text) && w.Text.Any(char.IsDigit));
    }

    private static int CountUsableWords(OcrLine line) =>
        line.Words.Count(w => !string.IsNullOrWhiteSpace(w.Text));

    private static double[] GetWordAnchors(OcrLine line) =>
        line.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderBy(w => w.Bounds.X)
            .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
            .ToArray();

    private static bool TryMergeTableAnchors(
        double[] expected,
        double[] actual,
        double tolerance,
        out double[] merged)
    {
        merged = expected;
        if (expected.Length < 2 || actual.Length == 0 || actual.Length > expected.Length + 1)
            return false;

        if (actual.Length >= 2 && expected.Length == actual.Length)
        {
            var directMatches = expected
                .Zip(actual, (e, a) => Math.Abs(e - a) <= tolerance)
                .Count(matches => matches);
            if (directMatches == expected.Length)
            {
                merged = expected.Zip(actual, (e, a) => (e + a) / 2.0).ToArray();
                return true;
            }
        }

        var assignments = new HashSet<int>();
        foreach (var anchor in actual)
        {
            var column = OcrLayoutHelpers.FindNearestColumn(anchor, expected, tolerance);
            if (column < 0 || !assignments.Add(column))
                return false;
        }

        if (assignments.Count < Math.Min(2, expected.Length))
            return false;

        return true;
    }

    // ----- Table rule/background fusion -----

    public static RuleBoundsMatch FindRuleBounds(OcrTableCandidate table, IReadOnlyList<RuleSegment> segments)
    {
        if (segments.Count == 0)
            return new RuleBoundsMatch(null, "no-rule-segments");

        var wordBounds = OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var margin = Math.Max(20, table.Lines.Average(l => l.Bounds.Height) * 2.5);

        var horizontal = segments
            .Where(s => s.Orientation == RuleOrientation.Horizontal &&
                        s.X <= wordBounds.X + wordBounds.Width + margin &&
                        s.X + s.Length >= wordBounds.X - margin &&
                        s.Y >= wordBounds.Y - margin &&
                        s.Y <= wordBounds.Y + wordBounds.Height + margin)
            .ToList();
        var vertical = segments
            .Where(s => s.Orientation == RuleOrientation.Vertical &&
                        s.Y <= wordBounds.Y + wordBounds.Height + margin &&
                        s.Y + s.Length >= wordBounds.Y - margin &&
                        s.X >= wordBounds.X - margin &&
                        s.X <= wordBounds.X + wordBounds.Width + margin)
            .ToList();

        if (horizontal.Count < 2)
            return new RuleBoundsMatch(null, "fewer-than-two-horizontal-rules-near-table");

        var top = horizontal.Min(s => s.Y);
        var bottom = horizontal.Max(s => s.Y);
        var horizontalLeft = horizontal.Min(s => s.X);
        var horizontalRight = horizontal.Max(s => s.X + s.Length);
        var left = vertical.Count >= 2 ? vertical.Min(s => s.X) : horizontalLeft;
        var right = vertical.Count >= 2 ? vertical.Max(s => s.X) : horizontalRight;
        if (right <= left || bottom <= top)
            return new RuleBoundsMatch(null, "invalid-rule-bounds");

        if (wordBounds.X < left - 1 || wordBounds.Y < top - 1 ||
            wordBounds.X + wordBounds.Width > right + 1 ||
            wordBounds.Y + wordBounds.Height > bottom + 1)
            return new RuleBoundsMatch(null, "ocr-words-outside-rule-bounds");

        if (!HasEnoughHorizontalTableCoverage(horizontal, left, right, top, bottom))
            return new RuleBoundsMatch(null, "insufficient-horizontal-rule-coverage");

        return new RuleBoundsMatch(
            new OcrBoundingBox(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)),
            null);
    }

    public static OcrBoundingBox? FindTableBackgroundBounds(OcrTableCandidate table, OcrPixels bitmap, ImageToPdfConversionOptions options)
    {
        if (table.RowGroups.Count < 2 || table.ColumnAnchors.Count < 2)
            return null;

        var background = OcrLayoutHelpers.EstimateBackgroundColor(bitmap);
        var wordBounds = OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var averageHeight = table.Lines.Average(l => Math.Max(1, l.Bounds.Height));
        var horizontalPadding = (int)Math.Round(Math.Max(12, OcrLayoutHelpers.EstimateTableColumnTolerance(table)));
        var verticalPadding = (int)Math.Round(Math.Max(4, averageHeight * 0.65));
        var leftLimit = Math.Clamp(wordBounds.X - horizontalPadding, 0, bitmap.Width - 1);
        var rightLimit = Math.Clamp(wordBounds.X + wordBounds.Width + horizontalPadding, 1, bitmap.Width);
        if (rightLimit <= leftLimit)
            return null;

        var bands = new List<OcrBoundingBox>();
        foreach (var row in table.RowGroups)
        {
            var rowBounds = OcrLayoutHelpers.UnionBounds(row.Select(l => l.Bounds));
            var top = Math.Clamp(rowBounds.Y - verticalPadding, 0, bitmap.Height - 1);
            var bottom = Math.Clamp(rowBounds.Y + rowBounds.Height + verticalPadding, top + 1, bitmap.Height);
            var band = FindLightFillBand(bitmap, background, leftLimit, rightLimit, top, bottom, options);
            if (band is not null)
                bands.Add(band);
        }

        if (bands.Count == 0)
            return null;

        var minWidth = Math.Max(wordBounds.Width * 0.85, Math.Max(24, (table.ColumnAnchors.Last() - table.ColumnAnchors.First()) * 0.9));
        if (bands.Max(b => b.Width) < minWidth)
            return null;

        var bounds = OcrLayoutHelpers.UnionBounds(bands);
        if (bounds.Height < wordBounds.Height * 0.75)
            return null;

        return OcrLayoutHelpers.ExpandBounds(bounds, 0);
    }

    private static OcrBoundingBox? FindLightFillBand(
        OcrPixels bitmap,
        SKColor background,
        int leftLimit,
        int rightLimit,
        int top,
        int bottom,
        ImageToPdfConversionOptions options)
    {
        var rowHits = new List<(int Y, int Left, int Right)>();
        for (var y = top; y < bottom; y++)
        {
            var first = -1;
            var last = -1;
            var hits = 0;
            for (var x = leftLimit; x < rightLimit; x++)
            {
                if (!IsLightTableFillPixel(bitmap.GetPixel(x, y), background, options))
                    continue;

                first = first < 0 ? x : first;
                last = x;
                hits++;
            }

            if (hits >= Math.Max(8, (rightLimit - leftLimit) * 0.22))
                rowHits.Add((y, first, last + 1));
        }

        if (rowHits.Count < Math.Max(2, (bottom - top) * 0.25))
            return null;

        var left = rowHits.Min(r => r.Left);
        var right = rowHits.Max(r => r.Right);
        var width = right - left;
        if (width < Math.Max(24, (rightLimit - leftLimit) * 0.45))
            return null;

        var bandTop = rowHits.Min(r => r.Y);
        var bandBottom = rowHits.Max(r => r.Y) + 1;
        return new OcrBoundingBox(left, bandTop, Math.Max(1, width), Math.Max(1, bandBottom - bandTop));
    }

    private static bool IsLightTableFillPixel(SKColor color, SKColor background, ImageToPdfConversionOptions options)
    {
        if (color.Alpha < 180 || OcrLayoutHelpers.IsLikelyTextPixel(color))
            return false;

        if (OcrLayoutHelpers.Saturation(color) > options.LightFillMaxSaturation)
            return false;

        var luma = OcrLayoutHelpers.Luma(color);
        if (luma < options.LightFillMinLuma)
            return false;

        var distance = OcrLayoutHelpers.ColorDistance(color, background);
        return distance >= options.LightFillMinDistance && distance <= options.LightFillMaxDistance;
    }

    private static bool HasEnoughHorizontalTableCoverage(
        IReadOnlyList<RuleSegment> horizontal,
        int left,
        int right,
        int top,
        int bottom)
    {
        var requiredWidth = Math.Max(1, right - left);
        var topCoverage = MeasureHorizontalCoverage(horizontal.Where(s => Math.Abs(s.Y - top) <= 1), left, right);
        var bottomCoverage = MeasureHorizontalCoverage(horizontal.Where(s => Math.Abs(s.Y - bottom) <= 1), left, right);
        return topCoverage >= requiredWidth * 0.55 && bottomCoverage >= requiredWidth * 0.55;
    }

    private static int MeasureHorizontalCoverage(IEnumerable<RuleSegment> segments, int left, int right)
    {
        var intervals = segments
            .Select(s => (Start: Math.Max(left, s.X), End: Math.Min(right, s.X + s.Length)))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ToList();
        if (intervals.Count == 0)
            return 0;

        var coverage = 0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= currentEnd + 2)
            {
                currentEnd = Math.Max(currentEnd, interval.End);
                continue;
            }

            coverage += currentEnd - currentStart;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        coverage += currentEnd - currentStart;
        return coverage;
    }

    // ----- Fields -----

    public static IReadOnlyList<OcrFieldCandidate> DetectFields(
        IReadOnlyList<OcrShapeCandidate> shapes,
        OcrPixels bitmap,
        IReadOnlyList<OcrLine> labelLines)
    {
        var fields = new List<OcrFieldCandidate>();
        foreach (var shape in shapes.Where(s => s.Kind == OcrShapeKind.Rectangle))
        {
            if (!IsLikelyFieldBounds(shape.Bounds) || !VisualElementDetector.HasMostlyEmptyInterior(bitmap, shape.Bounds))
                continue;

            var label = FindBestFieldLabel(shape.Bounds, labelLines);
            if (label is null)
                continue;

            fields.Add(new OcrFieldCandidate(shape.Bounds, label, 0.84));
        }

        return fields;
    }

    private static bool IsLikelyFieldBounds(OcrBoundingBox bounds)
    {
        var width = Math.Max(1, bounds.Width);
        var height = Math.Max(1, bounds.Height);
        var aspect = width / (double)height;
        return width >= 45 &&
               height is >= 10 and <= 60 &&
               aspect >= 1.8 &&
               !VisualElementDetector.IsLikelyCheckboxBounds(bounds);
    }

    private static OcrLine? FindBestFieldLabel(OcrBoundingBox fieldBounds, IReadOnlyList<OcrLine> lines)
    {
        return lines
            .Select(line => new { Line = line, Score = ScoreFieldLabel(fieldBounds, line) })
            .Where(x => x.Score < double.MaxValue)
            .OrderBy(x => x.Score)
            .Select(x => x.Line)
            .FirstOrDefault();
    }

    private static double ScoreFieldLabel(OcrBoundingBox fieldBounds, OcrLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Text) || line.Text.Trim().Length > 64)
            return double.MaxValue;

        var lineRight = line.Bounds.X + line.Bounds.Width;
        var lineBottom = line.Bounds.Y + line.Bounds.Height;
        var fieldRight = fieldBounds.X + fieldBounds.Width;
        var fieldBottom = fieldBounds.Y + fieldBounds.Height;
        var lineCenterY = line.Bounds.Y + line.Bounds.Height / 2.0;
        var fieldCenterY = fieldBounds.Y + fieldBounds.Height / 2.0;
        var lineCenterX = line.Bounds.X + line.Bounds.Width / 2.0;
        var fieldCenterX = fieldBounds.X + fieldBounds.Width / 2.0;
        var averageHeight = (Math.Max(1, line.Bounds.Height) + Math.Max(1, fieldBounds.Height)) / 2.0;

        var leftGap = fieldBounds.X - lineRight;
        if (leftGap is >= 2 and <= 120 && Math.Abs(lineCenterY - fieldCenterY) <= Math.Max(8, averageHeight * 0.8))
            return leftGap;

        var aboveGap = fieldBounds.Y - lineBottom;
        var horizontalOverlap = Math.Max(0, Math.Min(lineRight, fieldRight) - Math.Max(line.Bounds.X, fieldBounds.X));
        var overlapRatio = horizontalOverlap / (double)Math.Max(1, Math.Min(line.Bounds.Width, fieldBounds.Width));
        var leftAlignment = Math.Abs(line.Bounds.X - fieldBounds.X);
        if (aboveGap is >= 2 and <= 32 &&
            (overlapRatio >= 0.35 || leftAlignment <= Math.Max(12, averageHeight)))
            return 200 + aboveGap + Math.Abs(lineCenterX - fieldCenterX) * 0.05;

        return double.MaxValue;
    }

    // ----- Signatures -----

    public static IReadOnlyList<OcrSignatureCandidate> DetectSignatures(
        IReadOnlyList<OcrShapeCandidate> shapes,
        IReadOnlyList<OcrLine> labelLines)
    {
        var signatures = new List<OcrSignatureCandidate>();
        foreach (var shape in shapes.Where(s => s.Kind == OcrShapeKind.HorizontalLine))
        {
            if (!IsLikelySignatureLine(shape.Bounds))
                continue;

            var label = FindBestSignatureLabel(shape.Bounds, labelLines);
            if (label is null)
                continue;

            signatures.Add(new OcrSignatureCandidate(shape.Bounds, label, 0.82));
        }

        return signatures;
    }

    private static bool IsLikelySignatureLine(OcrBoundingBox bounds) =>
        bounds.Width >= 60 && bounds.Height <= 2;

    private static OcrLine? FindBestSignatureLabel(OcrBoundingBox lineBounds, IReadOnlyList<OcrLine> lines)
    {
        return lines
            .Where(line => IsLikelySignatureLabel(line.Text))
            .Select(line => new { Line = line, Score = ScoreSignatureLabel(lineBounds, line) })
            .Where(x => x.Score < double.MaxValue)
            .OrderBy(x => x.Score)
            .Select(x => x.Line)
            .FirstOrDefault();
    }

    private static bool IsLikelySignatureLabel(string text)
    {
        var normalized = text.Trim().TrimEnd(':').ToLowerInvariant();
        if (normalized.Length is < 2 or > 40)
            return false;

        return normalized.Contains("signature", StringComparison.Ordinal) ||
               normalized.Contains("signatur", StringComparison.Ordinal) ||
               normalized.Contains("unterschrift", StringComparison.Ordinal) ||
               normalized.Contains("datum", StringComparison.Ordinal) ||
               normalized.Contains("date", StringComparison.Ordinal) ||
               normalized.Contains("name", StringComparison.Ordinal);
    }

    private static double ScoreSignatureLabel(OcrBoundingBox lineBounds, OcrLine label)
    {
        var labelRight = label.Bounds.X + label.Bounds.Width;
        var labelBottom = label.Bounds.Y + label.Bounds.Height;
        var lineRight = lineBounds.X + lineBounds.Width;
        var labelCenterY = label.Bounds.Y + label.Bounds.Height / 2.0;
        var lineCenterY = lineBounds.Y + lineBounds.Height / 2.0;
        var labelCenterX = label.Bounds.X + label.Bounds.Width / 2.0;
        var lineCenterX = lineBounds.X + lineBounds.Width / 2.0;
        var averageHeight = (Math.Max(1, label.Bounds.Height) + Math.Max(1, lineBounds.Height)) / 2.0;

        var leftGap = lineBounds.X - labelRight;
        if (leftGap is >= 2 and <= 120 && Math.Abs(labelCenterY - lineCenterY) <= Math.Max(10, averageHeight * 1.2))
            return leftGap;

        var aboveGap = lineBounds.Y - labelBottom;
        var horizontalOverlap = Math.Max(0, Math.Min(labelRight, lineRight) - Math.Max(label.Bounds.X, lineBounds.X));
        var overlapRatio = horizontalOverlap / (double)Math.Max(1, Math.Min(label.Bounds.Width, lineBounds.Width));
        var leftAlignment = Math.Abs(label.Bounds.X - lineBounds.X);
        if (aboveGap is >= 2 and <= 36 &&
            (overlapRatio >= 0.25 || leftAlignment <= Math.Max(16, averageHeight * 2.0)))
            return 200 + aboveGap + Math.Abs(labelCenterX - lineCenterX) * 0.05;

        return double.MaxValue;
    }

    // ----- Standalone text groups -----

    public static IReadOnlyList<OcrTextGroup> BuildTextGroups(
        IReadOnlyList<OcrLine> lines,
        ImageToPdfConversionOptions options)
    {
        if (lines.Count == 0)
            return [];

        var ordered = lines
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToList();
        var typicalLineHeight = EstimateTypicalLineHeight(ordered);
        var roles = ordered.ToDictionary(l => l, l => ClassifyTextRole(l, typicalLineHeight));

        if (!ShouldGroupParagraphs(options))
            return ordered.Select(l => new OcrTextGroup([l], roles[l], 0, 1)).ToList();

        var groups = new List<OcrTextGroup>();
        foreach (var column in DetectTextColumns(ordered))
            groups.AddRange(GroupParagraphLines(column.Lines, roles, column.Index, column.Count));

        return groups;
    }

    private static IReadOnlyList<OcrTextGroup> GroupParagraphLines(
        IReadOnlyList<OcrLine> lines,
        IReadOnlyDictionary<OcrLine, OcrTextRole> roles,
        int columnIndex,
        int columnCount)
    {
        var groups = new List<OcrTextGroup>();
        var current = new List<OcrLine> { lines[0] };
        for (var i = 1; i < lines.Count; i++)
        {
            var previous = current[^1];
            var line = lines[i];
            if (roles[previous] == roles[line] && CanJoinParagraphLine(previous, line))
            {
                current.Add(line);
                continue;
            }

            groups.Add(new OcrTextGroup(current.ToList(), roles[current[0]], columnIndex, columnCount));
            current = [line];
        }

        groups.Add(new OcrTextGroup(current.ToList(), roles[current[0]], columnIndex, columnCount));
        return groups;
    }

    private static double EstimateTypicalLineHeight(IReadOnlyList<OcrLine> lines)
    {
        var heights = lines
            .Where(l => CountUsableWords(l) > 0)
            .Select(l => Math.Max(1, l.Words.Count > 0 ? l.Words.Average(w => w.Bounds.Height) : l.Bounds.Height))
            .Order()
            .ToArray();
        if (heights.Length == 0)
            return 1;

        return heights[(heights.Length - 1) / 2];
    }

    private static OcrTextRole ClassifyTextRole(OcrLine line, double typicalLineHeight)
    {
        var lineHeight = Math.Max(1, line.Words.Count > 0 ? line.Words.Average(w => w.Bounds.Height) : line.Bounds.Height);
        var wordCount = CountUsableWords(line);
        if (typicalLineHeight > 0 && lineHeight >= typicalLineHeight * 1.35 && wordCount <= 10)
            return OcrTextRole.Heading;
        if (typicalLineHeight > 0 && lineHeight <= typicalLineHeight * 0.78)
            return OcrTextRole.Caption;

        return OcrTextRole.Body;
    }

    private static IReadOnlyList<OcrTextColumn> DetectTextColumns(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count < 4)
            return [new OcrTextColumn(lines, 0, 1)];

        var averageHeight = lines.Average(l => Math.Max(1, l.Bounds.Height));
        var tolerance = Math.Max(12, averageHeight * 1.5);
        var clusters = new List<List<OcrLine>>();

        foreach (var line in lines.OrderBy(l => l.Bounds.X).ThenBy(l => l.Bounds.Y))
        {
            var match = clusters
                .Select((cluster, index) => new
                {
                    Cluster = cluster,
                    Index = index,
                    Distance = Math.Abs(cluster.Average(l => l.Bounds.X) - line.Bounds.X),
                })
                .Where(x => x.Distance <= tolerance)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (match is null)
                clusters.Add([line]);
            else
                clusters[match.Index].Add(line);
        }

        if (clusters.Count < 2 || clusters.Any(c => c.Count < 2))
            return [new OcrTextColumn(lines, 0, 1)];

        var sorted = clusters
            .Select(c => c.OrderBy(l => l.Bounds.Y).ThenBy(l => l.Bounds.X).ToList())
            .OrderBy(c => c.Min(l => l.Bounds.X))
            .ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            var previousRight = sorted[i - 1].Max(l => l.Bounds.X + l.Bounds.Width);
            var currentLeft = sorted[i].Min(l => l.Bounds.X);
            if (currentLeft - previousRight < Math.Max(24, averageHeight * 3))
                return [new OcrTextColumn(lines, 0, 1)];
        }

        return sorted
            .Select((columnLines, index) => new OcrTextColumn(columnLines, index, sorted.Count))
            .ToList();
    }

    private static bool ShouldGroupParagraphs(ImageToPdfConversionOptions options) =>
        string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.LayoutMode, "paragraphs", StringComparison.OrdinalIgnoreCase);

    private static bool CanJoinParagraphLine(OcrLine previous, OcrLine current)
    {
        if (CountUsableWords(previous) == 0 || CountUsableWords(current) == 0)
            return false;

        var previousHeight = Math.Max(1, previous.Bounds.Height);
        var currentHeight = Math.Max(1, current.Bounds.Height);
        var averageHeight = (previousHeight + currentHeight) / 2.0;
        var heightSimilarity = Math.Min(previousHeight, currentHeight) / (double)Math.Max(previousHeight, currentHeight);
        if (heightSimilarity < 0.65)
            return false;

        var verticalGap = current.Bounds.Y - (previous.Bounds.Y + previous.Bounds.Height);
        if (verticalGap < 0 || verticalGap > Math.Max(8, averageHeight * 0.9))
            return false;

        var leftDelta = Math.Abs(current.Bounds.X - previous.Bounds.X);
        if (leftDelta > Math.Max(8, averageHeight * 0.8))
            return false;

        var widthRatio = Math.Min(previous.Bounds.Width, current.Bounds.Width) / (double)Math.Max(previous.Bounds.Width, current.Bounds.Width);
        return widthRatio >= 0.35;
    }

    // ----- Image-region exclusion zones -----

    public static IReadOnlyList<OcrBoundingBox> BuildImageRegionExcludedBounds(
        IReadOnlyList<OcrLine> lines,
        IReadOnlyList<OcrTableCandidate> tables,
        IReadOnlyList<OcrBoundingBox> checkboxBounds,
        IReadOnlyList<OcrBoundingBox> fieldBounds,
        IReadOnlyList<OcrBoundingBox> signatureBounds,
        IReadOnlyList<OcrBoundingBox> filledRectangleBounds,
        IReadOnlyList<OcrBoundingBox> circleBounds,
        IReadOnlyList<OcrBoundingBox> shapeBounds)
    {
        var bounds = new List<OcrBoundingBox>();
        bounds.AddRange(lines.Select(l => OcrLayoutHelpers.ExpandBounds(l.Bounds, 20)));
        bounds.AddRange(tables.Select(t => OcrLayoutHelpers.ExpandBounds(t.RuleBounds ?? OcrLayoutHelpers.UnionBounds(t.Lines.SelectMany(l => l.Words.Select(w => w.Bounds))), 2)));
        bounds.AddRange(checkboxBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        bounds.AddRange(fieldBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        bounds.AddRange(signatureBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        bounds.AddRange(filledRectangleBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        bounds.AddRange(circleBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        bounds.AddRange(shapeBounds.Select(b => OcrLayoutHelpers.ExpandBounds(b, 2)));
        return bounds;
    }
}
