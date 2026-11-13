/** @jest-environment jsdom */
import { render, waitFor } from '@testing-library/react';
import '@testing-library/jest-dom';
import { PdfDropdownRenderer, PdfRadioRenderer, PdfCheckboxRenderer } from '@/components/Rendering/PdfControlRenderer';
import { PdfTableOfContentsRenderer, PdfWatermarkRenderer } from '@/components/Rendering/PdfDocumentRenderer';
import { PdfSingleLineText, PdfTextBlockRenderer, pdfFontFamily } from '@/components/Rendering/PdfTextRenderer';
import BackendSymbolRenderer from '@/components/Rendering/BackendSymbolRenderer';
import type { SimpleElement } from '@/types';
import FormFieldRenderer from '@/components/Rendering/FormFieldRenderer';
import { PdfFootnoteRenderer, PdfNoteRenderer } from '@/components/Rendering/PdfStructureRenderer';
import { PdfShapeRenderer, PdfCircleRenderer } from '@/components/Rendering/PdfGraphicsRenderer';
import LineRenderer from '@/components/Rendering/LineRenderer';
import { canRenderPdfTable, layoutPdfTable, pdfTableEdges, PdfTableRenderer } from '@/components/Rendering/PdfTableRenderer';
import { resizeTable } from '@/utils/tableDimensions';

jest.mock('jsbarcode', () => jest.fn());

const element = (overrides: Partial<SimpleElement>): SimpleElement => ({
  id: 'visual', type: 'text', x: 0, y: 0, width: 250, height: 100, ...overrides,
});

describe('visual rendering regressions', () => {
  test('dropdown defaults to the first option and clips the normal appearance to its inner box', () => {
    const { container } = render(<PdfDropdownRenderer element={element({
      type: 'dropdown', options: ['Sales', 'Finance'], fieldLabel: 'Department', style: { fontSize: 3 },
    })} />);
    expect(container.querySelector('text')).toHaveTextContent('Sales');
    expect(container.querySelector('text')).toHaveAttribute('font-size', '6');
    expect(container.querySelector('text')).toHaveAttribute('clip-path');
  });
  test('dropdown without options matches the backend static label and arrow', () => {
    const { container } = render(<PdfDropdownRenderer element={element({
      type: 'dropdown', fieldLabel: 'Department', style: { fontSize: 12, borderColor: '#ff0000' },
    })} />);
    expect([...container.querySelectorAll('text')].map(node => node.textContent)).toEqual(['Department', 'v']);
    expect(container.querySelector('text')).toHaveAttribute('x', '6');
    expect(container.querySelector('svg > rect')).toHaveAttribute('stroke', '#ff0000');
  });
  test('links use PDF underline offset, thickness and measured unkerned advance', () => {
    const prototype = SVGElement.prototype as SVGElement & { getComputedTextLength?: () => number };
    const original = prototype.getComputedTextLength;
    prototype.getComputedTextLength = () => 143.25;
    try {
      const { container } = render(<PdfSingleLineText kind="link" text="Documentation"
        element={element({ type: 'link', style: { fontSize: 12, color: '#1d4ed8' } })} />);
      const underline = container.querySelector('[data-pdf-underline]');
      expect(underline).toHaveAttribute('x2', '143.25');
      expect(Number(underline?.getAttribute('y1'))).toBeCloseTo(10.08);
      expect(Number(underline?.getAttribute('stroke-width'))).toBeCloseTo(0.72);
      expect(underline).toHaveAttribute('stroke', '#1d4ed8');
      expect(container.querySelector('text')).not.toHaveAttribute('text-decoration');
    } finally {
      if (original) prototype.getComputedTextLength = original;
      else delete prototype.getComputedTextLength;
    }
  });

  test.each(['qrcode', 'barcode'] as const)('backend %s images retain source bytes and appropriate resampling', async type => {
    const original = global.fetch;
    global.fetch = jest.fn().mockResolvedValue({ ok: true,
      headers: { get: () => 'image/png' }, arrayBuffer: async () => new Uint8Array([137, 80, 78, 71]).buffer });
    try {
      const { container } = render(<BackendSymbolRenderer type={type} element={element({ type, qrValue: 'data', barcodeValue: '123' })} />);
      await waitFor(() => expect(container.querySelector('[data-symbol-status]')).toHaveAttribute('data-symbol-status', 'ready'));
      expect(container.querySelector('img')).toHaveAttribute('src', 'data:image/png;base64,iVBORw==');
      expect(container.querySelector('img')).toHaveStyle({ imageRendering: type === 'qrcode' ? 'auto' : 'pixelated' });
    } finally { global.fetch = original; }
  });

  test('styled tables use the shared renderer while incomplete tables retain the legacy fallback', () => {
    const table = element({ type: 'table', cellData: [['A', 'B']] });
    expect(canRenderPdfTable(table)).toBe(true);
    expect(canRenderPdfTable({ ...table, columnWidths: [80, 0] })).toBe(false);
    expect(canRenderPdfTable({ ...table, cellData: [[]] })).toBe(false);
    expect(canRenderPdfTable({ ...table, cellStyles: [{ row: 0, col: 0, bold: true }] })).toBe(true);
  });
  test('individual cell typography, padding and fills override header defaults including explicit false', () => {
    const table = element({ type: 'table', width: 200, height: 30, headerRow: true,
      cellData: [['A long cell that wraps across many lines', 'B']],
      cellStyles: [{ row: 0, col: 0, fontSize: 18, padding: 12, bold: false, italic: true, color: '#ff0000', backgroundColor: '#00ff00' }] });
    const layout = layoutPdfTable(table)!;
    expect(layout.heights[0]).toBeGreaterThan(60);
    const { container } = render(<PdfTableRenderer element={table} />);
    const cell = container.querySelector('[data-table-cell="0:0"]')!;
    expect(cell.querySelector('rect')).toHaveAttribute('fill', '#00ff00');
    expect(cell.querySelector('text')).toHaveAttribute('font-size', '18');
    expect(cell.querySelector('text')).toHaveAttribute('font-weight', 'normal');
    expect(cell.querySelector('text')).toHaveAttribute('font-style', 'italic');
    expect(cell.querySelector('text')).toHaveAttribute('fill', '#ff0000');
  });
  test('explicit zero suppresses a shared border and each edge is painted once', () => {
    const table = element({ type: 'table', width: 200, height: 40, cellData: [['A', 'B']],
      cellStyles: [{ row: 0, col: 0, borderRight: { width: 0 } }] });
    const edges = pdfTableEdges(table, layoutPdfTable(table)!);
    expect(edges).toHaveLength(3);
    expect(edges.some(edge => edge.x1 === 100 && edge.x2 === 100)).toBe(false);
  });
  test.each([2, 3])('shared borders use explicit width precedence for a %s point left border', leftWidth => {
    const table = element({ type: 'table', width: 200, height: 40, cellData: [['A', 'B']],
      cellStyles: [{ row: 0, col: 0, borderRight: { width: leftWidth, color: '#ff0000' } },
        { row: 0, col: 1, borderLeft: { width: 2, color: '#0000ff' } }] });
    const edges = pdfTableEdges(table, layoutPdfTable(table)!);
    expect(edges).toEqual([{ x1: 100, x2: 100, y1: 0, y2: 40, width: leftWidth,
      color: leftWidth > 2 ? '#ff0000' : '#0000ff' }]);
  });
  test('matrix headers do not shift sparse styles onto the wrong data row', () => {
    const table = element({ type: 'table', cellData: [['A']],
      style: { rdlTablixColumnHierarchy: [{ headerText: 'Header' }] },
      cellStyles: [{ row: 0, col: 0, padding: 0 }, { row: 0, col: 0, padding: 99 }, { row: 8, col: 0, padding: 80 }] });
    const layout = layoutPdfTable(table)!;
    expect(layout.cells[0][0].padding).toBe(4);
    expect(layout.cells[1][0].padding).toBe(0);
    expect(layout.overrides.size).toBe(1);
  });
  test('table resizing preserves cells and column settings while changing the canonical matrix', () => {
    const table = element({ type: 'table', cellData: [['A', 'B'], ['C', 'D']],
      columnWidths: [1, 2], columnAlignments: ['left', 'right'],
      cellStyles: [{ row: 0, col: 0, bold: true }, { row: 1, col: 1, italic: true }],
      style: { rows: 2, columns: 2, selectedCellRow: 1, selectedCellCol: 1 } });
    const grown = resizeTable(table, 3, 3)!;
    expect(grown.cellData).toEqual([['A', 'B', ''], ['C', 'D', ''], ['', '', '']]);
    expect(grown.columnWidths).toEqual([1, 2, 1]);
    expect(grown.columnAlignments).toEqual(['left', 'right', 'left']);
    expect(layoutPdfTable({ ...table, ...grown })?.rows).toHaveLength(3);
    const shrunk = resizeTable(table, 1, 1)!;
    expect(shrunk.cellData).toEqual([['A']]);
    expect(shrunk.cellStyles).toEqual([{ row: 0, col: 0, bold: true }]);
    expect(shrunk.style).toMatchObject({ rows: 1, columns: 1, selectedCellRow: 0, selectedCellCol: 0 });
    expect(table.cellData).toEqual([['A', 'B'], ['C', 'D']]);
    for (const invalid of [0, -1, 1.5, NaN, Infinity]) expect(resizeTable(table, invalid, 2)).toBeNull();
  });

  test('blank footnotes do not introduce footer text missing from the PDF', () => {
    const { container } = render(<PdfFootnoteRenderer pageHeight={842}
      element={element({ type: 'footnote', footnoteText: '   ' })} />);
    expect(container.querySelector('[data-footnote-body]')).toBeNull();
  });
  test('Arabic uses the bundled face and anchors RTL at the right edge', () => {
    expect(pdfFontFamily('Noto Sans Arabic')).toContain('PVADE Arabic');
    const { container } = render(<PdfTextBlockRenderer element={element({
      language: 'ar', textDirection: 'rtl', style: { textAlign: 'right', paddingRight: 8 },
    })} text={'\u0645\u0631\u062d\u0628\u0627'} />);
    expect(container.querySelector('text')).toHaveAttribute('text-anchor', 'start');
    expect(container.querySelector('text')).toHaveAttribute('x', '242');
  });

  test('radio respects a selected non-first value', () => {
    const { container } = render(<PdfRadioRenderer element={element({
      type: 'radio', options: ['Email', 'Postal'], selectedValue: 'Postal',
    })} />);
    expect([...container.querySelectorAll('[data-radio-selected]')].map(node => node.getAttribute('data-radio-selected')))
      .toEqual(['false', 'true']);
    expect(container.querySelectorAll('input')).toHaveLength(0);
  });

  test('checkbox tick goes down then up in CSS coordinates', () => {
    const { container } = render(<PdfCheckboxRenderer element={element({ type: 'checkbox', height: 14, checkState: 'checked' })} />);
    expect(container.querySelector('path')).toHaveAttribute('d', 'M 2.5 6.5 L 5.5 10 L 11.5 3');
  });

  test('TOC uses its configured title, levels and page-number settings', () => {
    const { container } = render(<PdfTableOfContentsRenderer element={element({
      type: 'toc', tocTitle: 'Contents', tocMinLevel: 2, tocShowPageNumbers: false,
      tocEntries: [{ text: 'Excluded', level: 1, page: 1 }, { text: 'Included', level: 2, page: 2 }],
    })} />);
    expect(container).toHaveTextContent('Contents');
    expect(container).toHaveTextContent('Included');
    expect(container).not.toHaveTextContent('Excluded');
    expect(container.querySelectorAll('text')).toHaveLength(2);
  });

  test('watermark preserves case and applies no second rotation', () => {
    const { container } = render(<PdfWatermarkRenderer element={element({
      type: 'watermark', content: 'Draft', style: { rotation: -35, opacity: 0.35, fontWeight: 'bold' },
    })} />);
    expect(container.querySelector('svg')).toHaveStyle({ opacity: '0.35' });
    expect(container.querySelector('svg')?.style.transform).toBe('');
    expect(container.querySelector('text')).toHaveTextContent('Draft');
    expect(container.querySelector('text')).toHaveAttribute('font-weight', 'bold');
  });

  test('form label and required marker use PDF baseline and width rules', () => {
    const { container } = render(<FormFieldRenderer element={element({
      type: 'field', fieldLabel: 'Reference', required: true, height: 58, style: { fontSize: 11 },
    })} />);
    const texts = container.querySelectorAll('text');
    expect(Number(texts[0].getAttribute('y'))).toBeCloseTo(9.92);
    expect(Number(texts[1].getAttribute('x'))).toBeCloseTo(9 * 11 * 0.52);
    expect(container.querySelector('rect')).toHaveAttribute('y', '16.5');
    expect(container.querySelector('rect')).toHaveAttribute('height', '23');
  });

  test('notes wrap body text with the backend paragraph estimator', () => {
    const { container } = render(<PdfNoteRenderer element={element({
      type: 'note', noteTitle: 'Review note', noteBody: 'Verify values before publishing this document.',
    })} />);
    const lines = [...container.querySelectorAll('text')];
    expect(lines.map(node => node.textContent)).toEqual(['Review note', 'Verify values before publishing this', 'document.']);
    expect(Number(lines[2].getAttribute('y')) - Number(lines[1].getAttribute('y'))).toBeCloseTo(12);
  });

  test.each([420, 595, 842])('footnote body uses the actual page height %s', pageHeight => {
    const { container } = render(<PdfFootnoteRenderer pageHeight={pageHeight} element={element({
      type: 'footnote', y: 125, footnoteRef: 'A', footnoteText: 'A note', style: { fontSize: 9, footnoteBottomOffset: 5 },
    })} />);
    expect(container.querySelector('text')).toHaveTextContent('A');
    expect(container.querySelector('[data-footnote-body]')).toHaveTextContent('A A note');
    expect(Number(container.querySelector('[data-footnote-body]')?.getAttribute('y'))).toBe(pageHeight - 125 - 32 - 5);
    expect(container.querySelector('svg')).toHaveStyle({ overflow: 'visible' });
  });

  test('shape and circle borders are centered on canonical bounds, not inset', () => {
    const shape = render(<PdfShapeRenderer element={element({ type: 'rect', style: { borderWidth: 4 } })} />);
    expect(shape.container.querySelector('rect')).toHaveAttribute('x', '0');
    expect(shape.container.querySelector('rect')).toHaveAttribute('width', '250');
    const circle = render(<PdfCircleRenderer element={element({ type: 'circle', style: { borderWidth: 4 } })} />);
    expect(circle.container.querySelector('ellipse')).toHaveAttribute('rx', '125');
  });

  test('dashed lines use fixed backend dash lengths independent of thickness', () => {
    const { container } = render(<LineRenderer element={element({
      type: 'line', height: 4, style: { strokeWidth: 3, dashStyle: 'dashed' },
    })} />);
    expect(container.querySelector('line')).toHaveAttribute('stroke-dasharray', '8 4');
    expect(container.querySelector('line')).toHaveAttribute('y1', '2');
  });

  test('tables use data rows, relative widths and one grid, not HTML auto-layout', () => {
    const table = element({ type: 'table', width: 300, height: 120, cellData: [['Name', 'Amount'], ['Item', '5']],
      columnWidths: [2, 1], columnAlignments: ['left', 'right'], headerRow: true,
      style: { rows: 8, cellFontSize: 10, cellPadding: 4 } });
    const layout = layoutPdfTable(table)!;
    expect(layout.widths).toEqual([200, 100]);
    expect(layout.heights).toEqual([60, 60]);
    const { container } = render(<PdfTableRenderer element={table} />);
    expect(container.querySelector('table')).toBeNull();
    expect(container.querySelectorAll('[data-table-cell]')).toHaveLength(4);
    expect(Number(container.querySelector('[data-table-cell="1:1"] text')?.getAttribute('x'))).toBeCloseTo(300 - 4 - 5.2);
  });

  test('tables grow for wrapped rows and keep empty cells empty', () => {
    const table = element({ type: 'table', width: 120, height: 24, cellData: [['A long description that wraps', '']],
      style: { cellFontSize: 10, cellPadding: 4 } });
    const layout = layoutPdfTable(table)!;
    expect(layout.heights[0]).toBeGreaterThan(24);
    const { container } = render(<PdfTableRenderer element={table} />);
    expect(container.querySelector('[data-table-cell="0:1"]')?.textContent).toBe('');
    expect(layoutPdfTable(element({ type: 'table', cellData: [] }))).toBeNull();
    expect(layoutPdfTable({ ...table, columnWidths: [0, 0] })).toBeNull();
  });
});
