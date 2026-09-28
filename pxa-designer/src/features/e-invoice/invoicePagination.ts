/**
 * Turns one canonical invoice into the pages the Designer shows.
 *
 * Two jobs. First, decide how tall each line row has to be, because a long description wraps and a
 * row that is shorter than its content would either clip it or let it collide with the row below.
 * Second, decide where the Designer page breaks fall. The dedicated hybrid-invoice renderer has a
 * separate fixed-row layout; parity with that renderer is tracked independently.
 *
 * Row heights come from a conservative estimate for the maintained projection fixtures. The
 * estimate is capped; maximum-length descriptions still require the shared overflow contract
 * tracked in the electronic-invoicing checklist.
 */

import type { InvoiceTemplateLayout } from './invoiceTemplates';

/** One invoice line, placed. */
export interface PlannedRow {
  /** Index into the canonical `invoice.lines`, so a row's identity follows the invoice order. */
  readonly lineIndex: number;
  readonly top: number;
  readonly height: number;
  /** How many lines the wrapping cell needs. */
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
}

/**
 * Upper-bound advance widths for Liberation Sans, as a fraction of the font size.
 *
 * These are bounds, not exact metrics: the aim is never to under-estimate a string's width. A
 * character the table does not know about counts as a full em, which covers CJK and any script
 * whose glyphs are wider than Latin.
 */
const NARROW = new Set([...' ijlt.,:;\'"`!|()[]{}/\\-']);
const WIDE = new Set([...'WMQ@%&mw']);

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
 * How many lines `text` needs in a `width`-point column, bounded by the maintained row limit.
 */
export function estimateTextLines(
  text: string | undefined,
  width: number,
  fontSize: number,
  maximumLines = 8,
): number {
  if (!text || width <= 0) return 1;
  const estimated = Math.ceil((estimateTextWidth(text, fontSize) * 1.05) / width);
  return Math.min(Math.max(estimated, 1), maximumLines);
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

/**
 * Plans the pages.
 *
 * A row is placed on the current page when it fits entirely above `rowLimit`; otherwise a
 * continuation page starts, which re-establishes the column headings at `continuationTableTop`. The
 * totals block follows the final row when there is room for it above `totalsLimit`, and moves to a
 * page of its own when there is not. Very large tax breakdowns continue across totals pages.
 */
export function planInvoicePages(input: PlanInput): InvoicePlan {
  const { layout } = input;
  // Two tax rows are already budgeted inside `totalsHeight`; each further group adds a row.
  const totalsHeight =
    layout.totalsHeight + Math.max(input.taxGroupCount - 2, 0) * layout.taxRowHeight;
  const pages: PlannedPage[] = [];

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

  input.rowTexts.forEach((text, lineIndex) => {
    const textLines = estimateTextLines(text, input.wrappingWidth, input.wrappingFontSize);
    // A wrapped cell needs its lines plus the row's own breathing room.
    const height = Math.max(layout.rowHeight, textLines * layout.lineHeight + 7);

    // A row is never split across pages: if it does not fit whole, it starts the next page.
    if (cursor + height > layout.rowLimit && rows.length > 0) {
      closePage(null, layout.continuationTableTop);
    }

    rows.push({ lineIndex, top: cursor, height, textLines });
    cursor += height;
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

  return { pages };
}
