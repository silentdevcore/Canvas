/** @jest-environment jsdom */

import { SpreadsheetService } from '@/services/SpreadsheetService';
import type { Workbook } from '@/spreadsheet/types';

const workbook: Workbook = {
  id: 'workbook-1',
  name: 'Quarterly Sales',
  sheets: [{
    id: 'sheet-1',
    name: 'Sheet1',
    rowCount: 1,
    colCount: 1,
    columns: [],
    rows: [],
    cells: [],
    merges: [],
    frozenRows: 0,
    frozenCols: 0,
  }],
  definedNames: [],
};

const xlsxMime = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const response = (bytes: number[], contentType: string, fileName = 'Server Workbook.xlsx') => ({
  ok: true,
  status: 200,
  headers: new Headers({
    'Content-Type': contentType,
    'Content-Disposition': `attachment; filename="${fileName}"`,
  }),
  blob: async () => new Blob([new Uint8Array(bytes)], { type: contentType }),
}) as Response;

describe('Spreadsheet export reliability', () => {
  const originalFetch = global.fetch;
  const originalCreateObjectUrl = URL.createObjectURL;
  const originalRevokeObjectUrl = URL.revokeObjectURL;
  const originalStructuredClone = global.structuredClone;

  beforeEach(() => {
    URL.createObjectURL = jest.fn(() => 'blob:spreadsheet');
    URL.revokeObjectURL = jest.fn();
    global.structuredClone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    URL.createObjectURL = originalCreateObjectUrl;
    URL.revokeObjectURL = originalRevokeObjectUrl;
    global.structuredClone = originalStructuredClone;
    jest.restoreAllMocks();
  });

  test('downloads one validated XLSX using the server filename', async () => {
    global.fetch = jest.fn(async () => response([0x50, 0x4b, 0x03, 0x04], xlsxMime));
    const names: string[] = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      names.push(this.download);
    });

    await SpreadsheetService.exportXlsx(workbook);

    expect(names).toEqual(['Server-Workbook.xlsx']);
  });

  test('rejects JSON or invalid bytes instead of saving them as XLSX', async () => {
    const click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    global.fetch = jest.fn(async () => response([0x7b, 0x7d], 'application/json', 'error.json'));
    await expect(SpreadsheetService.exportXlsx(workbook)).rejects.toThrow(/instead of an Excel workbook/i);

    global.fetch = jest.fn(async () => response([0x00, 0x01, 0x02], xlsxMime));
    await expect(SpreadsheetService.exportXlsx(workbook)).rejects.toThrow(/invalid Excel workbook data/i);
    expect(click).not.toHaveBeenCalled();
  });

  test('uses consistent safe filenames for CSV and JSON', () => {
    const names: string[] = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      names.push(this.download);
    });

    SpreadsheetService.exportText('a,b', ' Q3: Europe / Sales? ', 'csv');
    SpreadsheetService.exportText('{"sheets":[]}', ' Q3: Europe / Sales? ', 'json');

    expect(names).toEqual(['Q3-Europe-Sales.csv', 'Q3-Europe-Sales.json']);
  });
});
