/** @jest-environment jsdom */

import { SpreadsheetService } from '@/services/SpreadsheetService';
import type { Workbook } from '@/spreadsheet/types';
import { deleteDesignerAsset, uploadDesignerImage } from '@/services/designerAssetApi';

jest.mock('@/services/designerAssetApi', () => ({
  deleteDesignerAsset: jest.fn(),
  designerAssetContentUrl: jest.fn((assetId: string) => `/assets/${assetId}`),
  uploadDesignerImage: jest.fn(),
}));

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
    jest.mocked(deleteDesignerAsset).mockReset();
    jest.mocked(uploadDesignerImage).mockReset();
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

  test('removes uploaded images when a later native-project image fails', async () => {
    global.fetch = jest.fn(async () => ({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    } as Response));
    jest.mocked(uploadDesignerImage)
      .mockResolvedValueOnce({ id: 'asset-1', contentUrl: '/assets/asset-1' } as never)
      .mockRejectedValueOnce(new Error('upload failed'));
    jest.mocked(deleteDesignerAsset).mockResolvedValue();
    const project: Workbook = {
      ...workbook,
      sheets: [{
        ...workbook.sheets[0],
        images: ['image-1', 'image-2'].map(id => ({
          id,
          data: 'data:image/png;base64,cG5n',
          row: 0,
          col: 0,
          width: 100,
          height: 100,
        })),
      }],
    };

    await expect(SpreadsheetService.storeEmbeddedImages(project)).rejects.toThrow('upload failed');
    expect(deleteDesignerAsset).toHaveBeenCalledWith('asset-1');
  });

  test('keeps source data immutable and can roll back prepared project images', async () => {
    global.fetch = jest.fn(async () => ({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    } as Response));
    jest.mocked(uploadDesignerImage).mockResolvedValue({
      id: 'asset-prepared', contentUrl: '/assets/asset-prepared', contentType: 'image/png',
    } as never);
    jest.mocked(deleteDesignerAsset).mockResolvedValue();
    const source: Workbook = {
      ...workbook,
      sheets: [{ ...workbook.sheets[0], images: [{
        id: 'image-1', data: 'data:image/png;base64,cG5n',
        row: 0, col: 0, width: 100, height: 100,
      }] }],
    };

    const prepared = await SpreadsheetService.prepareEmbeddedImages(source);
    expect(source.sheets[0].images?.[0].data).toBe('data:image/png;base64,cG5n');
    expect(prepared.workbook.sheets[0].images?.[0]).toMatchObject({ assetId: 'asset-prepared' });
    expect(prepared.workbook.sheets[0].images?.[0]).not.toHaveProperty('data');

    await prepared.rollback();
    await prepared.rollback();
    expect(deleteDesignerAsset).toHaveBeenCalledTimes(1);
    expect(deleteDesignerAsset).toHaveBeenCalledWith('asset-prepared');
  });

  test('retains prepared project images after commit', async () => {
    global.fetch = jest.fn(async () => ({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    } as Response));
    jest.mocked(uploadDesignerImage).mockResolvedValue({
      id: 'asset-committed', contentUrl: '/assets/asset-committed', contentType: 'image/png',
    } as never);
    jest.mocked(deleteDesignerAsset).mockResolvedValue();

    const prepared = await SpreadsheetService.prepareEmbeddedImages({
      ...workbook,
      sheets: [{ ...workbook.sheets[0], images: [{
        id: 'image-1', data: 'data:image/png;base64,cG5n',
        row: 0, col: 0, width: 100, height: 100,
      }] }],
    });
    prepared.commit();
    await prepared.rollback();

    expect(deleteDesignerAsset).not.toHaveBeenCalled();
  });
});
