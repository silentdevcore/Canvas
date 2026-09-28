/**
 * @jest-environment node
 */
/**
 * Holds the Designer projection to the shared pagination contract.
 *
 * PXA renders an invoice twice - the Designer projects it onto Canvas and Live Preview pages, and
 * `EInvoice.CreatePdf` renders the hybrid PDF - and the two must agree, or the document approved on
 * screen is not the document issued. They agree by each implementing
 * `test-assets/e-invoicing/pagination-contract.json` rather than by sharing code, because
 * PXA.Generator must not depend on the Designer. The generator asserts the same file from C# in
 * `EInvoicePaginationContractTests`; when both pass, the two renderers put the same invoice content
 * on the same number of pages.
 */

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  MAXIMUM_DESCRIPTION_FRAGMENTS,
  planInvoicePages,
  wrapText,
  type InvoicePlan,
} from '@/features/e-invoice/invoicePagination';
import {
  INVOICE_TEMPLATE_COLUMNS,
  invoiceTemplate,
} from '@/features/e-invoice/invoiceTemplates';

interface ContractColumn { x: number; width: number }

interface ContractCase {
  id: string;
  description: string;
  descriptions: string[];
  taxGroups: number;
  expected: {
    pages: number;
    rowsPerPage: number[];
    distinctLinesPerPage: number[];
    totalsPages: number[];
    taxGroupsPerPage: number[];
    netTotalPages: number[];
    payablePages: number[];
    fragmentedLines: { lineIndex: number; fragments: number }[];
    overflowingLines: number[];
  };
}

const contract = JSON.parse(readFileSync(
  join(__dirname, '../../../test-assets/e-invoicing/pagination-contract.json'),
  'utf8',
)) as {
  schema: string;
  page: { width: number; height: number; left: number; right: number };
  table: { firstPageTop: number; continuationTop: number; columnHeaderOffset: number; headerRuleOffset: number };
  columns: Record<string, ContractColumn>;
  row: { height: number; lineHeight: number; padding: number; limit: number; bodyFontSize: number };
  totals: { height: number; limit: number; anchor: number; taxRowHeight: number; gap: number; footerTop: number };
  limits: { maximumDescriptionFragments: number };
  cases: ContractCase[];
};

const definition = invoiceTemplate('invoice-standard')!;
const { layout } = definition;

const planFor = (testCase: ContractCase): InvoicePlan => planInvoicePages({
  layout,
  wrappingWidth: contract.columns.description.width,
  wrappingFontSize: contract.row.bodyFontSize,
  rowTexts: testCase.descriptions,
  taxGroupCount: testCase.taxGroups,
});

describe('electronic invoice pagination contract', () => {
  it('is the version this projection implements', () => {
    expect(contract.schema).toBe('pxa.e-invoice.pagination/1');
    expect(contract.cases.length).toBeGreaterThanOrEqual(12);
  });

  it('matches the page and table geometry', () => {
    expect([definition.pageWidth, definition.pageHeight])
      .toEqual([contract.page.width, contract.page.height]);
    expect(layout.tableTop).toBe(contract.table.firstPageTop);
    expect(layout.continuationTableTop).toBe(contract.table.continuationTop);
    expect(layout.columnHeaderOffset).toBe(contract.table.columnHeaderOffset);
    expect(layout.headerRuleOffset).toBe(contract.table.headerRuleOffset);
    expect(layout.rowHeight).toBe(contract.row.height);
    expect(layout.lineHeight).toBe(contract.row.lineHeight);
    expect(layout.rowLimit).toBe(contract.row.limit);
    expect(layout.totalsHeight).toBe(contract.totals.height);
    expect(layout.totalsLimit).toBe(contract.totals.limit);
    expect(layout.totalsAnchor).toBe(contract.totals.anchor);
    expect(layout.taxRowHeight).toBe(contract.totals.taxRowHeight);
    expect(MAXIMUM_DESCRIPTION_FRAGMENTS).toBe(contract.limits.maximumDescriptionFragments);
  });

  it('matches the columns, which never overlap', () => {
    for (const [name, column] of Object.entries(contract.columns)) {
      const actual = (INVOICE_TEMPLATE_COLUMNS as Record<string, ContractColumn>)[name];
      expect({ name, ...actual }).toEqual({ name, ...column });
    }

    // Left to right, each table column ends before the next one starts. Overlapping boxes were a
    // real defect once: a totals label drew over the amount beside it.
    const order = ['position', 'description', 'quantity', 'unitPrice', 'taxRate', 'net', 'currency'];
    for (let index = 1; index < order.length; index += 1) {
      const previous = contract.columns[order[index - 1]];
      expect(previous.x + previous.width).toBeLessThanOrEqual(contract.columns[order[index]].x);
    }
    const last = contract.columns[order[order.length - 1]];
    expect(last.x + last.width).toBeLessThanOrEqual(contract.page.right);
  });

  it.each(contract.cases.map(testCase => [testCase.id, testCase] as const))(
    'reproduces the pinned plan for %s',
    (_id, testCase) => {
      const plan = planFor(testCase);
      const { expected } = testCase;

      expect(plan.pages).toHaveLength(expected.pages);
      expect(plan.pages.map(page => page.rows.length)).toEqual(expected.rowsPerPage);
      expect(plan.pages.map(page => new Set(page.rows.map(row => row.lineIndex)).size))
        .toEqual(expected.distinctLinesPerPage);
      expect(plan.pages.filter(page => page.totalsTop !== null).map(page => page.pageNumber))
        .toEqual(expected.totalsPages);
      expect(plan.pages.map(page => page.taxGroupCount)).toEqual(expected.taxGroupsPerPage);
      expect(plan.pages.filter(page => page.showNetTotal).map(page => page.pageNumber))
        .toEqual(expected.netTotalPages);
      expect(plan.pages.filter(page => page.showPayableTotal).map(page => page.pageNumber))
        .toEqual(expected.payablePages);
      expect([...plan.overflowingLines]).toEqual(expected.overflowingLines);

      const fragments = new Map<number, number>();
      for (const page of plan.pages) {
        for (const row of page.rows) {
          fragments.set(row.lineIndex, (fragments.get(row.lineIndex) ?? 0) + 1);
        }
      }
      expect([...fragments].filter(([, count]) => count > 1).map(([lineIndex, count]) => ({
        lineIndex, fragments: count,
      }))).toEqual(expected.fragmentedLines);
    },
  );

  it('keeps every row inside the page and in canonical order, in every case', () => {
    for (const testCase of contract.cases) {
      const plan = planFor(testCase);
      const order: { line: number; fragment: number }[] = [];

      for (const page of plan.pages) {
        let previousBottom = Number.NEGATIVE_INFINITY;
        for (const row of page.rows) {
          expect(row.top).toBeGreaterThanOrEqual(previousBottom);
          expect(row.top + row.height).toBeLessThanOrEqual(contract.row.limit);
          previousBottom = row.top + row.height;
          order.push({ line: row.lineIndex, fragment: row.fragmentIndex });
        }
      }

      // Every canonical line appears, in order, with its fragments in sequence.
      expect([...new Set(order.map(entry => entry.line))])
        .toEqual(testCase.descriptions.map((_text, index) => index));
      for (const line of new Set(order.map(entry => entry.line))) {
        const sequence = order.filter(entry => entry.line === line).map(entry => entry.fragment);
        expect(sequence).toEqual(sequence.map((_value, index) => index));
      }
    }
  });

  it('never loses a word when a description is split into fragments', () => {
    const testCase = contract.cases.find(item => item.id === 'description-longer-than-a-page')!;
    const plan = planFor(testCase);
    const rows = plan.pages.flatMap(page => page.rows);

    expect(rows.length).toBeGreaterThan(1);
    // The fragments rejoin to exactly the wrapped description - nothing dropped, nothing repeated.
    expect(rows.map(row => row.text).join(' '))
      .toBe(wrapText(testCase.descriptions[0], contract.columns.description.width, contract.row.bodyFontSize).join(' '));
  });

  it('splits a maximum-length token without clipping or losing Unicode characters', () => {
    const description = `${'x'.repeat(4094)}\u{1F642}`;
    const lines = wrapText(description, contract.columns.description.width, contract.row.bodyFontSize);

    expect(lines.length).toBeGreaterThan(1);
    expect(lines.join('')).toBe(description);
    expect(lines.every(line => line.length > 0)).toBe(true);

    const plan = planInvoicePages({
      layout,
      wrappingWidth: contract.columns.description.width,
      wrappingFontSize: contract.row.bodyFontSize,
      rowTexts: [description],
      taxGroupCount: 1,
    });
    expect(plan.pages.flatMap(page => page.rows).map(row => row.text).join('').replace(/\s/g, ''))
      .toBe(description);
  });

  it('reports a description beyond the bound rather than rendering it', () => {
    // The public API cannot produce this: PXAEINV109 caps a description at 4 096 characters. The
    // planner is driven directly, which is the only way to reach the bound.
    const plan = planInvoicePages({
      layout,
      wrappingWidth: contract.columns.description.width,
      wrappingFontSize: contract.row.bodyFontSize,
      rowTexts: [Array.from({ length: 4000 }, () => 'maintenance').join(' ')],
      taxGroupCount: 1,
    });

    expect([...plan.overflowingLines]).toEqual([0]);
    // Still planned rather than truncated, so nothing is silently dropped before the caller refuses.
    const lines = new Set(plan.pages.flatMap(page => page.rows).map(row => row.lineIndex));
    expect([...lines]).toEqual([0]);
  });
});
