using System.Text;

namespace PXA.Generator;

/// <summary>
/// The versioned page-layout and pagination contract the hybrid-invoice renderer implements.
/// </summary>
/// <remarks>
/// <para>
/// PXA renders an electronic invoice twice: the Designer projects it onto pages for the Canvas and
/// the Live Preview, and <see cref="EInvoice.CreatePdf"/> renders the dedicated hybrid PDF. The two
/// have to agree, or the document a user approves on screen is not the document that is issued.
/// </para>
/// <para>
/// They agree by implementing one contract rather than by sharing code: PXA.Generator must not take
/// a dependency on the Designer. The contract is pinned in
/// <c>test-assets/e-invoicing/pagination-contract.json</c>, and both sides assert themselves
/// against it - <c>EInvoicePaginationContractTests</c> here and
/// <c>eInvoicePaginationContract.test.ts</c> in the Designer. The Designer's half lives in
/// <c>pxa-designer/src/features/e-invoice/invoicePagination.ts</c> and
/// <c>invoiceTemplates.ts</c>.
/// </para>
/// <para>
/// Every measurement is in PDF points from the top of an A4 page.
/// </para>
/// </remarks>
public static class EInvoicePageLayout
{
    /// <summary>The contract version both implementations are pinned to.</summary>
    public const string ContractVersion = "pxa.e-invoice.pagination/1";

    public const double PageWidth = 595;
    public const double PageHeight = 842;

    public const double Left = 42;
    public const double Right = 553;

    /// <summary>First line row on page one, below the address block and the column headings.</summary>
    public const double FirstPageTableTop = 272;

    /// <summary>First line row on a continuation page, which carries no address block.</summary>
    public const double ContinuationTableTop = 82;

    /// <summary>Column headings sit this far above their table top.</summary>
    public const double ColumnHeaderOffset = 22;

    /// <summary>The heading rule sits this far above its table top.</summary>
    public const double HeaderRuleOffset = 6;

    /// <summary>Height of a line row whose description occupies a single line.</summary>
    public const double RowHeight = 18;

    /// <summary>Vertical advance of one wrapped description line.</summary>
    public const double LineHeight = 11;

    /// <summary>Breathing room added below a wrapped description before the next row.</summary>
    public const double RowPadding = 7;

    /// <summary>The lowest y a line row may occupy.</summary>
    public const double RowLimit = 740;

    /// <summary>The totals block needs this much vertical room.</summary>
    public const double TotalsHeight = 150;

    /// <summary>The lowest y the totals block may start at.</summary>
    public const double TotalsLimit = 790;

    /// <summary>Vertical advance of one tax-group row inside the totals block.</summary>
    public const double TaxRowHeight = 16;

    /// <summary>The footer note's top edge; nothing else may reach it.</summary>
    public const double FooterTop = 790;

    /// <summary>Gap between the final line row and the totals block.</summary>
    public const double TotalsGap = 18;

    /// <summary>Body text size for line rows, headings and totals.</summary>
    public const double BodyFontSize = 9;

    /// <summary>
    /// A description longer than this many fragments is refused rather than rendered.
    /// </summary>
    /// <remarks>
    /// Unreachable for a valid invoice: <c>PXAEINV109</c> caps a description at 4 096 characters,
    /// which is at most four fragments even when every character is full-em. It exists so that an
    /// unbounded description fails closed with a stable diagnostic instead of producing a document
    /// of unbounded length.
    /// </remarks>
    public const int MaximumDescriptionFragments = 8;

    /// <summary>A named, non-overlapping column: <c>X + Width</c> never reaches the next column's X.</summary>
    public readonly record struct Column(double X, double Width)
    {
        public double Right => X + Width;
    }

    public static Column Position { get; } = new(42, 26);
    public static Column Description { get; } = new(72, 208);
    public static Column Quantity { get; } = new(284, 52);
    public static Column UnitPrice { get; } = new(340, 68);
    public static Column TaxRate { get; } = new(412, 42);
    public static Column Net { get; } = new(458, 62);

    /// <summary>The currency code of the payable total, in its own column so it cannot overprint digits.</summary>
    public static Column Currency { get; } = new(524, 29);

    /// <summary>Totals labels are right-aligned and stop short of the amount column.</summary>
    public static Column TotalsLabel { get; } = new(284, 166);

    private static readonly HashSet<int> NarrowCharacters =
        [.. " ijlt.,:;'\"`!|()[]{}/\\-".EnumerateRunes().Select(static rune => rune.Value)];

    private static readonly HashSet<int> WideCharacters =
        [.. "WMQ@%&mw".EnumerateRunes().Select(static rune => rune.Value)];

    /// <summary>The safety margin over the estimated width, so a real renderer never needs one more line.</summary>
    private const double WidthSafety = 1.05;

    /// <summary>
    /// Upper-bound advance width of one character, as a fraction of the font size.
    /// </summary>
    /// <remarks>
    /// These are bounds, not exact metrics: the aim is never to under-estimate a string's width. A
    /// character the table does not know about counts as a full em, which covers CJK and any script
    /// whose glyphs are wider than Latin. The Designer's <c>advanceRatio</c> carries the same table.
    /// </remarks>
    public static double AdvanceRatio(char character) => AdvanceRatio(new Rune(character));

    private static double AdvanceRatio(Rune character)
    {
        if (NarrowCharacters.Contains(character.Value)) return 0.32;
        if (WideCharacters.Contains(character.Value)) return 0.95;
        if (character.Value is >= '0' and <= '9') return 0.56;
        if (character.Value is >= 'A' and <= 'Z') return 0.72;
        if (character.Value is >= 'a' and <= 'z') return 0.56;
        // Latin-1 letters with diacritics keep Latin advances.
        if (character.Value is >= 'À' and <= 'ɏ') return 0.62;
        // Anything else - Arabic, Hebrew, CJK, symbols - is treated as full width.
        return 1;
    }

    /// <summary>Conservative width of <paramref name="text"/> in points, at <paramref name="fontSize"/>.</summary>
    public static double EstimateTextWidth(string text, double fontSize)
    {
        var ratio = 0d;
        foreach (var character in text.EnumerateRunes()) ratio += AdvanceRatio(character);
        return ratio * fontSize;
    }

    /// <summary>
    /// Greedily wraps <paramref name="text"/> to <paramref name="width"/>, the way both renderers do.
    /// </summary>
    /// <remarks>
    /// Tokens wider than the column are split at Unicode scalar boundaries. Keeping an over-wide
    /// token on one line would make the planned box too short and silently clip valid content.
    /// </remarks>
    public static IReadOnlyList<string> WrapText(string? text, double width, double fontSize)
    {
        var words = (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || width <= 0) return [string.Empty];

        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var remaining = word;
            while (remaining.Length > 0)
            {
                var candidate = current.Length == 0 ? remaining : $"{current} {remaining}";
                if (EstimateTextWidth(candidate, fontSize) * WidthSafety <= width)
                {
                    current = candidate;
                    remaining = string.Empty;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = string.Empty;
                    continue;
                }

                var chunk = new StringBuilder();
                var consumed = 0;
                foreach (var character in remaining.EnumerateRunes())
                {
                    var expanded = string.Concat(chunk, character.ToString());
                    if (chunk.Length > 0 && EstimateTextWidth(expanded, fontSize) * WidthSafety > width)
                        break;
                    chunk.Append(character.ToString());
                    consumed += character.Utf16SequenceLength;
                }
                lines.Add(chunk.ToString());
                remaining = remaining[consumed..];
            }
        }
        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    /// <summary>The height a fragment of <paramref name="lineCount"/> wrapped lines occupies.</summary>
    public static double RowHeightFor(int lineCount) =>
        Math.Max(RowHeight, lineCount * LineHeight + RowPadding);

    /// <summary>How many wrapped lines still fit above <see cref="RowLimit"/> from <paramref name="top"/>.</summary>
    public static int LinesThatFit(double top) =>
        (int)Math.Floor((RowLimit - top - RowPadding) / LineHeight);

    /// <summary>
    /// One invoice line - or one fragment of one - placed on a page. <c>LineIndex</c> is the index
    /// into the canonical invoice lines, so identity follows canonical order, and
    /// <c>FragmentIndex</c> is 0 for the row that carries the amounts and 1.. for description
    /// continuations.
    /// </summary>
    public sealed record PlannedRow(
        int LineIndex,
        int FragmentIndex,
        string Text,
        double Top,
        double Height,
        int TextLines);

    /// <summary>One planned page.</summary>
    public sealed record PlannedPage(
        int PageNumber,
        bool IsFirst,
        double? TableTop,
        IReadOnlyList<PlannedRow> Rows,
        double? TotalsTop,
        int TaxGroupStart,
        int TaxGroupCount,
        bool ShowNetTotal,
        bool ShowPayableTotal);

    /// <summary>
    /// The planned document. <c>OverflowingLines</c> holds the canonical line indexes whose
    /// description exceeded <see cref="MaximumDescriptionFragments"/>.
    /// </summary>
    public sealed record InvoicePlan(
        IReadOnlyList<PlannedPage> Pages,
        IReadOnlyList<int> OverflowingLines);

    /// <summary>
    /// Plans the pages.
    /// </summary>
    /// <remarks>
    /// A row is placed whole on the current page when it fits above <see cref="RowLimit"/>;
    /// otherwise it starts a continuation page, which re-establishes the column headings at
    /// <see cref="ContinuationTableTop"/>. A row too tall for any page is split into fragments
    /// instead of being clipped. The totals block follows the final row when there is room above
    /// <see cref="TotalsLimit"/> and takes its own page when there is not; very large tax breakdowns
    /// continue across further totals pages.
    /// </remarks>
    public static InvoicePlan Plan(IReadOnlyList<string?> descriptions, int taxGroupCount)
    {
        ArgumentNullException.ThrowIfNull(descriptions);

        // Two tax rows are already budgeted inside TotalsHeight; each further group adds a row, and
        // a breakdown of more than one group adds the aggregate tax-total row beneath them.
        var totalsHeight = TotalsHeight
            + Math.Max(taxGroupCount - 2, 0) * TaxRowHeight
            + (taxGroupCount > 1 ? TaxRowHeight : 0);
        var pages = new List<PlannedPage>();
        var overflowing = new List<int>();

        var pageNumber = 1;
        var tableTop = FirstPageTableTop;
        var cursor = tableTop;
        var rows = new List<PlannedRow>();

        void ClosePage(
            double? totalsTop,
            double? nextTableTop,
            int taxGroupStart = 0,
            int groupCount = 0,
            bool showNetTotal = false,
            bool showPayableTotal = false)
        {
            pages.Add(new PlannedPage(
                pageNumber,
                pageNumber == 1,
                tableTop,
                rows,
                totalsTop,
                taxGroupStart,
                groupCount,
                showNetTotal,
                showPayableTotal));
            pageNumber++;
            rows = [];
            if (nextTableTop is { } next)
            {
                tableTop = next;
                cursor = next;
            }
        }

        // The tallest row a page can hold at all, which decides whether a line has to be fragmented.
        var wholePageLines = LinesThatFit(ContinuationTableTop);

        for (var lineIndex = 0; lineIndex < descriptions.Count; lineIndex++)
        {
            var wrapped = WrapText(descriptions[lineIndex], Description.Width, BodyFontSize);

            if (wrapped.Count <= wholePageLines)
            {
                // The common case: the row is never split, it only moves to the next page whole.
                var height = RowHeightFor(wrapped.Count);
                if (cursor + height > RowLimit && rows.Count > 0)
                    ClosePage(null, ContinuationTableTop);

                rows.Add(new PlannedRow(
                    lineIndex, 0, string.Join(' ', wrapped), cursor, height, wrapped.Count));
                cursor += height;
                continue;
            }

            // Longer than a page: emit deterministic continuation fragments.
            if ((int)Math.Ceiling((double)wrapped.Count / wholePageLines) > MaximumDescriptionFragments)
                overflowing.Add(lineIndex);

            var taken = 0;
            var fragmentIndex = 0;
            while (taken < wrapped.Count)
            {
                var available = LinesThatFit(cursor);
                if (available < 1)
                {
                    ClosePage(null, ContinuationTableTop);
                    available = LinesThatFit(cursor);
                }
                var count = Math.Min(available, wrapped.Count - taken);
                var height = RowHeightFor(count);
                rows.Add(new PlannedRow(
                    lineIndex,
                    fragmentIndex,
                    string.Join(' ', wrapped.Skip(taken).Take(count)),
                    cursor,
                    height,
                    count));
                cursor += height;
                taken += count;
                fragmentIndex++;
            }
        }

        var totalsTop = cursor + TotalsGap;
        if (totalsTop + totalsHeight <= TotalsLimit)
        {
            ClosePage(totalsTop, null, 0, taxGroupCount, showNetTotal: true, showPayableTotal: true);
        }
        else
        {
            // No room under the last row: the totals start on a page of their own.
            ClosePage(null, ContinuationTableTop);
            var fixedHeight = Math.Max(TotalsHeight - 2 * TaxRowHeight, 0);
            var groupsPerPage = Math.Max(
                1,
                (int)Math.Floor((TotalsLimit - ContinuationTableTop - fixedHeight) / TaxRowHeight));
            var groups = Math.Max(taxGroupCount, 1);
            for (var start = 0; start < groups; start += groupsPerPage)
            {
                var count = Math.Min(groupsPerPage, groups - start);
                var isLast = start + count >= groups;
                pages.Add(new PlannedPage(
                    pageNumber++,
                    false,
                    null,
                    [],
                    ContinuationTableTop,
                    start,
                    taxGroupCount == 0 ? 0 : count,
                    start == 0,
                    isLast));
            }
        }

        return new InvoicePlan(pages, overflowing);
    }
}
