/**
 * Turns one canonical invoice into the pages the Designer shows.
 *
 * This is the Designer half of the pagination contract recorded in
 * `test-assets/e-invoicing/pagination-contract.json`. `EInvoicePageLayout` in `PVAD.Generator`
 * implements the same contract for the dedicated hybrid-invoice renderer, independently - neither
 * side depends on the other, and `eInvoicePaginationContract.test.ts` and
 * `EInvoicePaginationContractTests` each assert their implementation against the pinned file. That
 * is what makes the Designer and `EInvoice.CreatePdf` put the same invoice content on the same
 * number of pages.
 *
 * Three jobs. Decide how tall each line row has to be, because a long description wraps and a row
 * shorter than its content would clip it or collide with the row below. Decide where the page
 * breaks fall. And, when a single description is longer than a whole page can hold, split it into
 * deterministic continuation fragments rather than clipping it.
 */

import type { InvoiceTemplateLayout } from './invoiceTemplates';

/**
 * A description longer than this many fragments is refused rather than rendered.
 *
 * The bound cannot be reached by a valid invoice: `PXAEINV109` caps a description at 4 096
 * characters, which is at most four fragments even when every character is full-em. It exists so
 * that an unbounded description fails closed with a stable diagnostic instead of producing a
 * document of unbounded length.
 */
export const MAXIMUM_DESCRIPTION_FRAGMENTS = 8;

/** One invoice line - or one fragment of one - placed on a page. */
export interface PlannedRow {
  /** Index into the canonical `invoice.lines`, so a row's identity follows the invoice order. */
  readonly lineIndex: number;
  /** 0 for the row that carries the amounts, 1.. for description continuation fragments. */
  readonly fragmentIndex: number;
  /** The description text this fragment shows. */
  readonly text: string;
  readonly top: number;
  readonly height: number;
  /** How many wrapped lines this fragment's text occupies. */
  readonly textLines: number;
}

export interface PlannedPage {
  /** 1-based. */
  readonly pageNumber: number;
  /** True for page one, which carries the address block. */
  readonly isFirst: boolean;
  /** Where this page's line table starts, or null when the page carries only totals. */
  readonly tableTop: number | null;
  readonly rows: readonly PlannedRow[];
  /** Set on the page that carries the totals block. */
  readonly totalsTop: number | null;
  readonly taxGroupStart: number;
  readonly taxGroupCount: number;
  readonly showNetTotal: boolean;
  readonly showPayableTotal: boolean;
}

export interface InvoicePlan {
  readonly pages: readonly PlannedPage[];
  /** Canonical line indexes whose description exceeded `MAXIMUM_DESCRIPTION_FRAGMENTS`. */
  readonly overflowingLines: readonly number[];
}

/**
 * Upper-bound advance widths for Liberation Sans, as a fraction of the font size.
 *
 * These are bounds, not exact metrics: the aim is never to under-estimate a string's width. A
 * character the table does not know about counts as a full em, which covers CJK and any script
 * whose glyphs are wider than Latin. `EInvoicePageLayout.AdvanceRatio` carries the same table.
 */
const NARROW = new Set([...' ijlt.,:;\'"`!|()[]{}/\\-']);
const WIDE = new Set([...'WMQ@%&mw']);

/** The safety margin over the estimated width, so a real renderer never needs one more line. */
const WIDTH_SAFETY = 1.05;

function advanceRatio(character: string): number {
  if (NARROW.has(character)) return 0.32;
  if (WIDE.has(character)) return 0.95;
  if (character >= '0' && character <= '9') return 0.56;
  if (character >= 'A' && character <= 'Z') return 0.72;
  if (character >= 'a' && character <= 'z') return 0.56;
  // Latin-1 letters with diacritics keep Latin advances.
  if (character >= 'À' && character <= 'ɏ') return 0.62;
  // Anything else - Arabic, Hebrew, CJK, symbols - is treated as full width.
  return 1;
}

/** Conservative width of `text` in points, at `fontSize`. */
export function estimateTextWidth(text: string, fontSize: number): number {
  let ratio = 0;
  for (const character of text) ratio += advanceRatio(character);
  return ratio * fontSize;
}

/**
 * Greedily wraps `text` to `width`, the way both renderers do.
 *
 * Tokens wider than the column are split at Unicode code-point boundaries. This matters for valid
 * identifiers and descriptions without whitespace: keeping an over-wide token on one line would
 * make the planned box too short and silently clip it in both renderers.
 */
export function wrapText(text: string | undefined, width: number, fontSize: number): string[] {
  const words = (text ?? '').split(/\s+/).filter(Boolean);
  if (words.length === 0 || width <= 0) return [''];

  const lines: string[] = [];
  let current = '';
  for (const word of words) {
    let remaining = word;
    while (remaining) {
      const candidate = current ? `${current} ${remaining}` : remaining;
      if (estimateTextWidth(candidate, fontSize) * WIDTH_SAFETY <= width) {
        current = candidate;
        remaining = '';
        continue;
      }

      if (current) {
        lines.push(current);
        current = '';
        continue;
      }

      let chunk = '';
      let consumed = 0;
      for (const character of remaining) {
        const expanded = `${chunk}${character}`;
        if (chunk && estimateTextWidth(expanded, fontSize) * WIDTH_SAFETY > width) break;
        chunk = expanded;
        consumed += character.length;
      }
      lines.push(chunk);
      remaining = remaining.slice(consumed);
    }
  }
  if (current) lines.push(current);
  return lines;
}

/** How many lines `text` needs in a `width`-point column. Unbounded: a description is never cut. */
export function estimateTextLines(
  text: string | undefined,
  width: number,
  fontSize: number,
): number {
  return wrapText(text, width, fontSize).length;
}

export interface PlanInput {
  readonly layout: InvoiceTemplateLayout;
  /** The wrapping cell's column width and font size, taken from the template element. */
  readonly wrappingWidth: number;
  readonly wrappingFontSize: number;
  /** The text of each invoice line's wrapping cell, in canonical order. */
  readonly rowTexts: readonly (string | undefined)[];
  /** Number of tax groups, which decides how tall the totals block is. */
  readonly taxGroupCount: number;
}

/** The height a fragment of `lineCount` wrapped lines occupies. */
function rowHeightFor(lineCount: number, layout: InvoiceTemplateLayout): number {
  return Math.max(layout.rowHeight, lineCount * layout.lineHeight + 7);
}

/** How many wrapped lines still fit above `rowLimit` when the cursor is at `top`. */
function linesThatFit(top: number, layout: InvoiceTemplateLayout): number {
  return Math.floor((layout.rowLimit - top - 7) / layout.lineHeight);
}

/**
 * Plans the pages.
 *
 * A row is placed whole on the current page when it fits above `rowLimit`; otherwise it starts a
 * continuation page, which re-establishes the column headings at `continuationTableTop`. A row too
 * tall for any page is split into fragments instead of being clipped. The totals block follows the
 * final row when there is room above `totalsLimit` and takes its own page when there is not; very
 * large tax breakdowns continue across further totals pages.
 */
export function planInvoicePages(input: PlanInput): InvoicePlan {
  const { layout } = input;
  // Two tax rows are already budgeted inside `totalsHeight`; each further group adds a row, and a
  // breakdown of more than one group adds the aggregate tax-total row beneath them.
  const totalsHeight = layout.totalsHeight
    + Math.max(input.taxGroupCount - 2, 0) * layout.taxRowHeight
    + (input.taxGroupCount > 1 ? layout.taxRowHeight : 0);
  const pages: PlannedPage[] = [];
  const overflowingLines: number[] = [];

  let pageNumber = 1;
  let tableTop = layout.tableTop;
  let cursor = tableTop;
  let rows: PlannedRow[] = [];

  /** Closes the current page. `nextTableTop` is null when no further line page follows. */
  const closePage = (
    totalsTop: number | null,
    nextTableTop: number | null,
    totals: Partial<Pick<PlannedPage,
      'taxGroupStart' | 'taxGroupCount' | 'showNetTotal' | 'showPayableTotal'>> = {},
  ): void => {
    pages.push({
      pageNumber,
      isFirst: pageNumber === 1,
      tableTop,
      rows,
      totalsTop,
      taxGroupStart: totals.taxGroupStart ?? 0,
      taxGroupCount: totals.taxGroupCount ?? 0,
      showNetTotal: totals.showNetTotal ?? false,
      showPayableTotal: totals.showPayableTotal ?? false,
    });
    pageNumber += 1;
    rows = [];
    if (nextTableTop !== null) {
      tableTop = nextTableTop;
      cursor = nextTableTop;
    }
  };

  // The tallest row a page can hold at all, which decides whether a line has to be fragmented.
  const wholePageLines = linesThatFit(layout.continuationTableTop, layout);

  input.rowTexts.forEach((text, lineIndex) => {
    const wrapped = wrapText(text, input.wrappingWidth, input.wrappingFontSize);

    if (wrapped.length <= wholePageLines) {
      // The common case: the row is never split, it only moves to the next page whole.
      const height = rowHeightFor(wrapped.length, layout);
      if (cursor + height > layout.rowLimit && rows.length > 0) {
        closePage(null, layout.continuationTableTop);
      }
      rows.push({
        lineIndex,
        fragmentIndex: 0,
        text: wrapped.join(' '),
        top: cursor,
        height,
        textLines: wrapped.length,
      });
      cursor += height;
      return;
    }

    // Longer than a page: emit deterministic continuation fragments.
    const fragments = Math.ceil(wrapped.length / wholePageLines);
    if (fragments > MAXIMUM_DESCRIPTION_FRAGMENTS) overflowingLines.push(lineIndex);

    let taken = 0;
    let fragmentIndex = 0;
    while (taken < wrapped.length) {
      let available = linesThatFit(cursor, layout);
      if (available < 1) {
        closePage(null, layout.continuationTableTop);
        available = linesThatFit(cursor, layout);
      }
      const count = Math.min(available, wrapped.length - taken);
      const height = rowHeightFor(count, layout);
      rows.push({
        lineIndex,
        fragmentIndex,
        text: wrapped.slice(taken, taken + count).join(' '),
        top: cursor,
        height,
        textLines: count,
      });
      cursor += height;
      taken += count;
      fragmentIndex += 1;
    }
  });

  const totalsTop = cursor + 18;
  if (totalsTop + totalsHeight <= layout.totalsLimit) {
    closePage(totalsTop, null, {
      taxGroupCount: input.taxGroupCount,
      showNetTotal: true,
      showPayableTotal: true,
    });
  } else {
    // No room under the last row: the totals start on a page of their own.
    closePage(null, layout.continuationTableTop);
    const fixedHeight = Math.max(layout.totalsHeight - 2 * layout.taxRowHeight, 0);
    const groupsPerPage = Math.max(
      1,
      Math.floor((layout.totalsLimit - layout.continuationTableTop - fixedHeight)
        / layout.taxRowHeight),
    );
    const groupCount = Math.max(input.taxGroupCount, 1);
    for (let start = 0; start < groupCount; start += groupsPerPage) {
      const count = Math.min(groupsPerPage, groupCount - start);
      const isLast = start + count >= groupCount;
      pages.push({
        pageNumber: pageNumber++,
        isFirst: false,
        tableTop: null,
        rows: [],
        totalsTop: layout.continuationTableTop,
        taxGroupStart: start,
        taxGroupCount: input.taxGroupCount === 0 ? 0 : count,
        showNetTotal: start === 0,
        showPayableTotal: isLast,
      });
    }
  }

  return { pages, overflowingLines };
}
