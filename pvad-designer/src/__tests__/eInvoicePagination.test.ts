/**
 * @jest-environment jsdom
 */
/**
 * EINV-05: multi-page projection of the canonical invoice.
 *
 * The Designer shows an invoice as pages, but the invoice itself is one canonical document. These
 * tests hold the join between the two: every line is projected exactly once, in canonical order, on
 * a page where it neither overlaps its neighbour nor runs off the sheet; the totals follow the last
 * line; and persisted element identity survives line insertion, removal and reordering.
 *
 * They work against the real projection, not a model of it, so the page capacities below are
 * derived from the maintained layout rather than written down. A layout change moves the numbers
 * and these tests keep asserting the same properties.
 */

import {
  projectInvoicePages,
  readEInvoiceDocument,
  writeEInvoiceDocument,
  createEInvoiceDesignerDocument,
} from '@/features/e-invoice/designerDocument';
import { invoiceTemplate } from '@/features/e-invoice/invoiceTemplates';
import { planInvoicePages } from '@/features/e-invoice/invoicePagination';
import { createEmptyInvoice, type EInvoiceDocument } from '@/features/e-invoice/invoiceModel';
import { DEFAULT_PAGE_SETTINGS, useEditorStore, type Template } from '@/store';
import type { SimpleElement } from '@/types';

const definition = invoiceTemplate('invoice-standard')!;
const translate = (key: string) => key.replace(/^template\./, '');

/** An invoice with `count` short lines, so a row takes the layout's minimum height. */
function invoiceWithLines(count: number, taxRates: number[] = [19]): EInvoiceDocument {
  return {
    ...createEmptyInvoice('2026-09-27'),
    invoiceNumber: 'PAGINATION-1',
    seller: {
      name: 'Seller GmbH',
      address: { street: 'Hauptstraße 1', city: 'Berlin', postalCode: '10115', countryCode: 'DE' },
    },
    buyer: {
      name: 'Buyer SARL',
      address: { street: 'Rue 2', city: 'Paris', postalCode: '75008', countryCode: 'FR' },
    },
    lines: Array.from({ length: count }, (_, index) => ({
      identifier: String(index + 1),
      description: `Line ${index + 1}`,
      quantity: 1,
      unitCode: 'C62',
      unitPrice: 10 + index,
      taxRate: taxRates[index % taxRates.length],
      taxCategoryCode: 'S',
    })),
  };
}

const project = (invoice: EInvoiceDocument) =>
  projectInvoicePages(definition, invoice, translate);

/** The description text of each projected line row, in the order the pages present them. */
function projectedDescriptions(pages: ReturnType<typeof project>): string[] {
  return pages.flatMap(page => page.elements
    .filter(element => element.id.startsWith('einv-lineDescription-'))
    .map(element => String(element.content ?? '')));
}

/** The largest line count the standard layout still fits on a single page. */
function singlePageCapacity(): number {
  for (let count = 1; count < 400; count += 1) {
    if (project(invoiceWithLines(count)).length > 1) return count - 1;
  }
  throw new Error('The layout never overflowed; the capacity probe needs a wider range.');
}

const boxOf = (element: SimpleElement) => ({
  id: element.id,
  left: element.x,
  right: element.x + element.width,
  top: element.y,
  bottom: element.y + element.height,
});

describe('electronic invoice pagination', () => {
  it('keeps a short invoice on one page, with the totals under the last line', () => {
    const pages = project(invoiceWithLines(3));

    expect(pages).toHaveLength(1);
    expect(projectedDescriptions(pages)).toEqual(['Line 1', 'Line 2', 'Line 3']);

    const ids = pages[0].elements.map(element => element.id);
    expect(ids).toContain('einv-payableAmount-t');

    const lastRow = boxOf(pages[0].elements.find(e => e.id === 'einv-lineDescription-l3')!);
    const payable = boxOf(pages[0].elements.find(e => e.id === 'einv-payableAmount-t')!);
    expect(payable.top).toBeGreaterThanOrEqual(lastRow.bottom);
  });

  it('fills the first page exactly to capacity before breaking', () => {
    const capacity = singlePageCapacity();
    // A sanity floor: a layout that fit only a handful of rows would make the rest meaningless.
    expect(capacity).toBeGreaterThan(10);

    const exact = project(invoiceWithLines(capacity));
    expect(exact).toHaveLength(1);
    expect(projectedDescriptions(exact)).toHaveLength(capacity);
  });

  it('moves one line beyond capacity onto a continuation page, losing nothing', () => {
    const capacity = singlePageCapacity();
    const pages = project(invoiceWithLines(capacity + 1));

    expect(pages.length).toBeGreaterThan(1);
    expect(projectedDescriptions(pages)).toEqual(
      Array.from({ length: capacity + 1 }, (_, index) => `Line ${index + 1}`),
    );
  });

  it('repeats the column headings on every page that carries rows', () => {
    const pages = project(invoiceWithLines(singlePageCapacity() * 2));

    for (const page of pages) {
      const rows = page.elements.filter(e => e.id.startsWith('einv-lineDescription-l'));
      const headings = page.elements.filter(e => e.id.startsWith('einv-lineDescriptionHeading-'));
      // A page has a heading exactly when it has rows to head.
      expect(headings.length).toBe(rows.length > 0 ? 1 : 0);
      if (rows.length > 0) {
        const heading = boxOf(headings[0]);
        const firstRow = boxOf(rows[0]);
        expect(heading.bottom).toBeLessThanOrEqual(firstRow.top);
      }
    }
  });

  it('repeats only the headings, never the address block', () => {
    const pages = project(invoiceWithLines(singlePageCapacity() * 2));
    const sellerPages = pages.filter(page =>
      page.elements.some(element => element.id === 'einv-sellerName-p1'));

    expect(sellerPages).toHaveLength(1);
    expect(sellerPages[0]).toBe(pages[0]);
  });

  it('produces as many continuation pages as the line count needs', () => {
    const capacity = singlePageCapacity();
    const pages = project(invoiceWithLines(capacity * 3));

    expect(pages.length).toBeGreaterThanOrEqual(3);
    expect(projectedDescriptions(pages)).toHaveLength(capacity * 3);
  });

  it('grows the totals block for every tax group and keeps each one', () => {
    const rates = [5, 7, 10, 13, 19, 20, 21, 23];
    const pages = project(invoiceWithLines(rates.length * 2, rates));
    const taxRows = pages.flatMap(page =>
      page.elements.filter(element => element.id.startsWith('einv-taxAmount-g')));

    expect(taxRows).toHaveLength(rates.length);
    // Distinct ids, so no group was projected twice.
    expect(new Set(taxRows.map(row => row.id)).size).toBe(rates.length);
    // Stacked, never on top of one another.
    const tops = taxRows.map(row => row.y).sort((a, b) => a - b);
    expect(new Set(tops).size).toBe(rates.length);
  });

  it('moves the totals to a page of their own when the last line leaves no room', () => {
    const capacity = singlePageCapacity();
    // Just past capacity the totals cannot follow the final row on the same sheet.
    const pages = project(invoiceWithLines(capacity + 1));
    const totalsPage = pages.find(page =>
      page.elements.some(element => element.id === 'einv-payableAmount-t'))!;

    expect(totalsPage).toBeDefined();
    // Exactly one page carries the totals.
    expect(pages.filter(page =>
      page.elements.some(element => element.id === 'einv-payableAmount-t'))).toHaveLength(1);
    // And it is the last one.
    expect(totalsPage).toBe(pages[pages.length - 1]);
  });

  it('never clips a page, overlaps two rows, or duplicates a line', () => {
    const invoice = invoiceWithLines(120, [7, 19, 21]);
    const pages = project(invoice);

    const descriptions = projectedDescriptions(pages);
    // Every line, exactly once, in canonical order.
    expect(descriptions).toEqual(invoice.lines.map(line => line.description));
    expect(new Set(descriptions).size).toBe(invoice.lines.length);

    for (const page of pages) {
      // Nothing runs off the sheet.
      for (const element of page.elements) {
        const box = boxOf(element);
        expect(box.top).toBeGreaterThanOrEqual(0);
        expect(box.bottom).toBeLessThanOrEqual(definition.pageHeight);
        expect(box.left).toBeGreaterThanOrEqual(0);
        expect(box.right).toBeLessThanOrEqual(definition.pageWidth);
      }

      // Consecutive rows in the wrapping column never overlap.
      const rows = page.elements
        .filter(element => element.id.startsWith('einv-lineDescription-l'))
        .map(boxOf)
        .sort((a, b) => a.top - b.top);
      for (let index = 1; index < rows.length; index += 1) {
        expect(rows[index].top).toBeGreaterThanOrEqual(rows[index - 1].bottom);
      }

      // The totals never land on top of a line row.
      const payable = page.elements.find(element => element.id === 'einv-payableAmount-t');
      if (payable && rows.length > 0) {
        expect(boxOf(payable).top).toBeGreaterThanOrEqual(rows[rows.length - 1].bottom);
      }
    }
  });

  it('gives a wrapped description room for every line it needs', () => {
    const long = 'Consulting engagement covering the migration assessment, the implementation '
      + 'plan, two workshops and the follow-up review scheduled for the next quarter';
    const invoice = invoiceWithLines(4);
    invoice.lines[1] = { ...invoice.lines[1], description: long };
    const pages = project(invoice);

    const wrapped = pages[0].elements.find(element => element.id === 'einv-lineDescription-l2')!;
    const plain = pages[0].elements.find(element => element.id === 'einv-lineDescription-l1')!;
    expect(wrapped.height).toBeGreaterThan(plain.height);

    // The row below starts under the taller cell, so nothing is covered.
    const next = pages[0].elements.find(element => element.id === 'einv-lineDescription-l3')!;
    expect(next.y).toBeGreaterThanOrEqual(wrapped.y + wrapped.height);
  });

  it('keeps every page and element id stable across an edit that adds no row', () => {
    const before = project(invoiceWithLines(40));
    const edited = { ...invoiceWithLines(40), invoiceNumber: 'PAGINATION-2' };
    const after = project(edited);

    expect(after.map(page => page.id)).toEqual(before.map(page => page.id));
    expect(after.map(page => page.elements.map(element => element.id)))
      .toEqual(before.map(page => page.elements.map(element => element.id)));
    // The value did change; only identity held still.
    const numberOf = (pages: typeof before) => pages[0].elements
      .find(element => element.id === 'einv-invoiceNumber-p1')!.content;
    expect(numberOf(after)).not.toBe(numberOf(before));
  });

  it('adds ids only for the rows that were added', () => {
    const before = project(invoiceWithLines(5));
    const after = project(invoiceWithLines(6));

    const idsOf = (pages: typeof before) =>
      new Set(pages.flatMap(page => page.elements.map(element => element.id)));
    const beforeIds = idsOf(before);
    const afterIds = idsOf(after);

    for (const id of beforeIds) expect(afterIds.has(id)).toBe(true);
    const added = [...afterIds].filter(id => !beforeIds.has(id));
    expect(added.length).toBeGreaterThan(0);
    // Every new id belongs to the sixth row.
    expect(added.every(id => id.endsWith('-l6'))).toBe(true);
  });

  it('drops the ids of a removed row and renumbers nothing else', () => {
    const five = project(invoiceWithLines(5));
    const invoice = invoiceWithLines(5);
    invoice.lines.splice(2, 1);
    const four = project(invoice);

    const ids = new Set(four.flatMap(page => page.elements.map(element => element.id)));
    expect(ids.has('einv-lineDescription-l5')).toBe(false);
    expect(ids.has('einv-lineDescription-l4')).toBe(true);
    // The rows that remain keep their canonical order, now closed up.
    expect(projectedDescriptions(four)).toEqual(['Line 1', 'Line 2', 'Line 4', 'Line 5']);
    expect(projectedDescriptions(five)).toHaveLength(5);
  });

  it('keeps business-line ids stable when a stored line is removed or reordered', () => {
    const invoice = invoiceWithLines(5);
    const ids = ['line-11', 'line-12', 'line-13', 'line-14', 'line-15'];
    const before = projectInvoicePages(definition, invoice, translate, ids);

    const removedInvoice = { ...invoice, lines: invoice.lines.filter((_line, index) => index !== 1) };
    const removedIds = ids.filter((_id, index) => index !== 1);
    const afterRemoval = projectInvoicePages(definition, removedInvoice, translate, removedIds);
    expect(afterRemoval.flatMap(page => page.elements.map(element => element.id)))
      .toContain('einv-lineDescription-l15');
    expect(afterRemoval.flatMap(page => page.elements.map(element => element.id)))
      .not.toContain('einv-lineDescription-l12');

    const reorderedInvoice = { ...invoice, lines: [...invoice.lines] };
    const reorderedIds = [...ids];
    [reorderedInvoice.lines[1], reorderedInvoice.lines[3]] = [reorderedInvoice.lines[3], reorderedInvoice.lines[1]];
    [reorderedIds[1], reorderedIds[3]] = [reorderedIds[3], reorderedIds[1]];
    const afterReorder = projectInvoicePages(definition, reorderedInvoice, translate, reorderedIds);
    const descriptionIds = afterReorder.flatMap(page => page.elements)
      .filter(element => element.id.startsWith('einv-lineDescription-'))
      .map(element => element.id);
    expect(descriptionIds).toEqual([
      'einv-lineDescription-l11',
      'einv-lineDescription-l14',
      'einv-lineDescription-l13',
      'einv-lineDescription-l12',
      'einv-lineDescription-l15',
    ]);
    expect(before).toHaveLength(1);
  });

  it('splits an unusually large tax breakdown across safe totals pages', () => {
    const rates = Array.from({ length: 80 }, (_, index) => index + 0.125);
    const pages = project(invoiceWithLines(rates.length, rates));
    const taxRows = pages.flatMap(page => page.elements
      .filter(element => element.id.startsWith('einv-taxAmount-g')));

    expect(taxRows).toHaveLength(rates.length);
    expect(new Set(taxRows.map(element => element.id)).size).toBe(rates.length);
    expect(pages.filter(page => page.elements
      .some(element => element.id === 'einv-payableAmount-t'))).toHaveLength(1);
    expect(pages.at(-1)!.elements.some(element => element.id === 'einv-payableAmount-t')).toBe(true);
    for (const page of pages) {
      for (const element of page.elements) {
        expect(element.y + element.height).toBeLessThanOrEqual(definition.pageHeight);
      }
    }
  });

  it('follows the canonical order after a reorder', () => {
    const invoice = invoiceWithLines(6);
    const [moved] = invoice.lines.splice(4, 1);
    invoice.lines.splice(1, 0, moved);

    expect(projectedDescriptions(project(invoice)))
      .toEqual(['Line 1', 'Line 5', 'Line 2', 'Line 3', 'Line 4', 'Line 6']);
  });

  it('plans the same pages for the same invoice, every time', () => {
    const first = project(invoiceWithLines(90));
    const second = project(invoiceWithLines(90));
    expect(JSON.stringify(second)).toBe(JSON.stringify(first));
  });

  it('never splits a row across a page boundary', () => {
    const plan = planInvoicePages({
      layout: definition.layout,
      wrappingWidth: 208,
      wrappingFontSize: 9,
      rowTexts: Array.from({ length: 150 }, (_, index) => `Line ${index + 1}`),
      taxGroupCount: 2,
    });

    const seen = new Set<number>();
    for (const page of plan.pages) {
      for (const row of page.rows) {
        expect(seen.has(row.lineIndex)).toBe(false);
        seen.add(row.lineIndex);
        expect(row.top + row.height).toBeLessThanOrEqual(definition.layout.rowLimit);
      }
    }
    expect(seen.size).toBe(150);
  });
});

describe('electronic invoice pagination in the editor store', () => {
  const baseTemplate = (): Template => ({
    id: 'template-einv',
    name: 'Invoice',
    category: 'invoice',
    description: '',
    data: {},
    sharedElements: [],
    pages: [{ id: 'page-1', elements: [] }],
  });

  beforeEach(() => {
    useEditorStore.setState({
      currentTemplate: baseTemplate(),
      pageSettings: { ...DEFAULT_PAGE_SETTINGS },
      undoStack: [],
      redoStack: [],
      currentPageIndex: 0,
      selectedElementId: null,
    });
  });

  /** What `useEInvoiceDocument.commit` does, without mounting the workspace. */
  function commit(invoice: EInvoiceDocument): void {
    const document = { ...createEInvoiceDesignerDocument(), invoice };
    useEditorStore.getState().commitEInvoice({
      eInvoice: writeEInvoiceDocument(document),
      pages: projectInvoicePages(definition, invoice, translate),
      pageSettings: { width: definition.pageWidth, height: definition.pageHeight },
    });
  }

  it('stores every generated page, so autosave persists the whole document', () => {
    const invoice = invoiceWithLines(100);
    commit(invoice);

    const stored = useEditorStore.getState().currentTemplate!;
    expect(stored.pages).toHaveLength(project(invoice).length);
    expect(stored.pages.length).toBeGreaterThan(1);

    // Each stored page has its own id and its own elements; none is an empty placeholder.
    expect(new Set(stored.pages.map(page => page.id)).size).toBe(stored.pages.length);
    for (const page of stored.pages) expect(page.elements.length).toBeGreaterThan(0);

    // Every canonical line reached the stored pages exactly once.
    const descriptions = stored.pages.flatMap(page => page.elements
      .filter(element => element.id.startsWith('einv-lineDescription-'))
      .map(element => String(element.content ?? '')));
    expect(descriptions).toEqual(invoice.lines.map(line => line.description));
  });

  it('records one undoable transaction per edit, however many pages it regenerates', () => {
    commit(invoiceWithLines(3));
    const afterFirst = useEditorStore.getState().undoStack.length;

    commit(invoiceWithLines(100));
    expect(useEditorStore.getState().undoStack.length).toBe(afterFirst + 1);
    expect(useEditorStore.getState().currentTemplate!.pages.length).toBeGreaterThan(1);

    useEditorStore.getState().undo();
    // One undo returns the whole multi-page regeneration, not one page of it.
    expect(useEditorStore.getState().currentTemplate!.pages).toHaveLength(1);
  });

  it('uses the A4 page box the layout was measured against', () => {
    commit(invoiceWithLines(3));
    const settings = useEditorStore.getState().pageSettings;

    expect(settings.width).toBe(definition.pageWidth);
    expect(settings.height).toBe(definition.pageHeight);
    // A4 in PDF points.
    expect([settings.width, settings.height]).toEqual([595, 842]);
  });

  it('round-trips the canonical invoice, which stays the source for XML and PDF', () => {
    const invoice = invoiceWithLines(100, [7, 19]);
    commit(invoice);

    const stored = useEditorStore.getState().currentTemplate!.eInvoice;
    const read = readEInvoiceDocument(stored)!;

    // The canonical invoice survives untouched, regardless of how it was paginated.
    expect(read.invoice.lines).toEqual(invoice.lines);
    expect(read.invoice.invoiceNumber).toBe(invoice.invoiceNumber);
    expect(read.invoice.seller.name).toBe(invoice.seller.name);
    expect(read.lineIds).toHaveLength(invoice.lines.length);
    expect(new Set(read.lineIds).size).toBe(invoice.lines.length);
    // Pages are a projection: the stored invoice holds no page or element identity.
    expect(JSON.stringify(read.invoice)).not.toContain('page-einv-');
    expect(JSON.stringify(read.invoice)).not.toContain('einv-lineDescription');
  });
});
