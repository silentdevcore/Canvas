import { sheetToCsv, parseCsv, csvToSheet, workbookToJson, jsonToWorkbook } from '../spreadsheet/io';
import { emptySheet, cellKey, workbookToWire, workbookFromWire, type Workbook } from '../spreadsheet/types';
import { formatCellValue } from '../spreadsheet/numberFormat';

describe('number-format display', () => {
  test('number / currency / percent / date / passthrough', () => {
    expect(formatCellValue(1234.5, '#,##0.00')).toBe('1,234.50');
    expect(formatCellValue(1234.5, '"€"#,##0.00')).toBe('€1,234.50');
    expect(formatCellValue(0.1234, '0.00%')).toBe('12.34%');
    expect(formatCellValue('2026-06-27', 'dd.MM.yyyy')).toMatch(/^\d{2}\.\d{2}\.2026$/);
    expect(formatCellValue('hello', '#,##0.00')).toBe('hello'); // non-numeric passthrough
    expect(formatCellValue(5, undefined)).toBe('5');             // no format → raw
  });
});

describe('spreadsheet CSV io', () => {
  test('sheetToCsv emits computed values with RFC-4180 quoting', () => {
    const sheet = emptySheet('S');
    sheet.cells[cellKey(0, 0)] = { row: 0, col: 0, type: 'text', value: 'a,b' };       // needs quoting
    sheet.cells[cellKey(0, 1)] = { row: 0, col: 1, type: 'text', value: 'he said "hi"' }; // quote escaping
    sheet.cells[cellKey(1, 0)] = { row: 1, col: 0, type: 'number', value: 42 };
    sheet.cells[cellKey(1, 1)] = { row: 1, col: 1, type: 'formula', formula: '=1+1' };

    // computed: formula at (1,1) resolves to 2; others echo their literal value.
    const computed = (r: number, c: number) => {
      if (r === 1 && c === 1) return 2;
      const cell = sheet.cells[cellKey(r, c)];
      return cell ? (cell.value as string | number) : null;
    };

    const csv = sheetToCsv(sheet, computed);
    expect(csv).toBe('"a,b","he said ""hi"""\r\n42,2');
  });

  test('parseCsv handles quotes, embedded commas, and newlines', () => {
    const rows = parseCsv('"a,b","c\nd"\r\n1,2');
    expect(rows).toEqual([['a,b', 'c\nd'], ['1', '2']]);
  });

  test('csvToSheet detects numbers vs text', () => {
    const sheet = csvToSheet('Name,Qty\nCoffee,2\nTea,5', 'Data');
    expect(sheet.cells[cellKey(0, 0)]).toMatchObject({ type: 'text', value: 'Name' });
    expect(sheet.cells[cellKey(1, 1)]).toMatchObject({ type: 'number', value: 2 });
    expect(sheet.cells[cellKey(2, 0)]).toMatchObject({ type: 'text', value: 'Tea' });
  });
});

describe('spreadsheet JSON io', () => {
  test('workbook JSON round-trips losslessly (formulas + styles)', () => {
    const sheet = emptySheet('S');
    sheet.cells[cellKey(0, 0)] = { row: 0, col: 0, type: 'text', value: 'Hi', style: { bold: true } };
    sheet.cells[cellKey(1, 0)] = { row: 1, col: 0, type: 'formula', formula: '=SUM(A1:A1)' };
    const wb: Workbook = workbookToWire('Book', [sheet]);

    const restored = jsonToWorkbook(workbookToJson(wb));
    expect(restored.sheets[0].cells.find((c) => c.row === 0 && c.col === 0)?.style?.bold).toBe(true);
    expect(restored.sheets[0].cells.find((c) => c.row === 1 && c.col === 0)?.formula).toBe('=SUM(A1:A1)');
  });

  test('jsonToWorkbook rejects non-workbook JSON', () => {
    expect(() => jsonToWorkbook('{"foo":1}')).toThrow();
  });

  test('advanced Phase-2 fields + version survive a load → save round-trip', () => {
    const wire: Workbook = {
      schemaVersion: '1.0',
      id: 'w',
      name: 'Book',
      definedNames: [{ name: 'Sales', refersTo: 'Sheet1!$A$1' }],
      sheets: [{
        id: 's', name: 'Sheet1', rowCount: 100, colCount: 26,
        columns: [{ index: 0, width: 20, outlineLevel: 1 }],
        rows: [{ index: 0, height: 18, hidden: true }],
        cells: [{ row: 0, col: 0, type: 'text', value: 'X', comment: 'note', hyperlink: 'https://x.com' }],
        merges: ['A1:B1'], frozenRows: 1, frozenCols: 0,
        autoFilterRange: 'A1:B2',
        pageSetup: { orientation: 'landscape', header: 'Report' },
        protection: { protected: true },
        conditionalFormats: [{ range: 'A1', type: 'cellIs', operator: 'greaterThan', value: '3', color: '#f00' }],
        dataValidations: [{ range: 'A2', type: 'list', listSource: 'a,b,c' }],
      }],
    };

    const { sheets, definedNames, schemaVersion } = workbookFromWire(wire);
    const out = workbookToWire('Book', sheets, definedNames, schemaVersion);
    const s = out.sheets[0];

    expect(out.schemaVersion).toBe('1.0');
    expect(out.definedNames[0]).toEqual({ name: 'Sales', refersTo: 'Sheet1!$A$1' });
    expect(s.columns.find((c) => c.index === 0)).toMatchObject({ width: 20, outlineLevel: 1 });
    expect(s.rows[0]).toMatchObject({ index: 0, height: 18, hidden: true });
    expect(s.autoFilterRange).toBe('A1:B2');
    expect(s.pageSetup).toMatchObject({ orientation: 'landscape', header: 'Report' });
    expect(s.protection).toMatchObject({ protected: true });
    expect(s.conditionalFormats?.[0]).toMatchObject({ range: 'A1', value: '3' });
    expect(s.dataValidations?.[0]).toMatchObject({ range: 'A2', listSource: 'a,b,c' });
    const cell = s.cells.find((c) => c.row === 0 && c.col === 0);
    expect(cell).toMatchObject({ comment: 'note', hyperlink: 'https://x.com' });
  });
});
