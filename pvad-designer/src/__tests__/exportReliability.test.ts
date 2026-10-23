/** @jest-environment jsdom */

import { ExportService } from '@/services/ExportService';
import { DEFAULT_PAGE_SETTINGS } from '@/store';
import type { Page, Template } from '@/types';

const template: Template = {
  id: 'template-1',
  name: 'Quarterly Report',
  category: 'test',
  description: '',
};
const pages: Page[] = [{ id: 'page-1', elements: [] }];

const response = (bytes: number[], contentType: string, fileName: string) => ({
  ok: true,
  status: 200,
  statusText: 'OK',
  headers: new Headers({
    'Content-Type': contentType,
    'Content-Disposition': `attachment; filename="${fileName}"`,
  }),
  blob: async () => new Blob([new Uint8Array(bytes)], { type: contentType }),
  json: async () => ({}),
}) as Response;

describe('Designer export reliability', () => {
  const originalFetch = global.fetch;
  const originalCreateObjectUrl = URL.createObjectURL;
  const originalRevokeObjectUrl = URL.revokeObjectURL;

  beforeEach(() => {
    URL.createObjectURL = jest.fn(() => 'blob:export');
    URL.revokeObjectURL = jest.fn();
  });

  afterEach(() => {
    global.fetch = originalFetch;
    URL.createObjectURL = originalCreateObjectUrl;
    URL.revokeObjectURL = originalRevokeObjectUrl;
    jest.useRealTimers();
    jest.restoreAllMocks();
  });

  test('PDF export downloads exactly one validated PDF with the server filename', async () => {
    global.fetch = jest.fn().mockResolvedValue(
      response([0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37], 'application/pdf', 'Quarterly-Report.pdf'),
    );
    const downloaded: string[] = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloaded.push(this.download);
    });

    await ExportService.exportToPDF(template, pages, [], DEFAULT_PAGE_SETTINGS);

    expect(downloaded).toEqual(['Quarterly-Report.pdf']);
    expect(downloaded.some(name => name.endsWith('.json'))).toBe(false);
  });

  test('PDF export rejects a JSON response without starting a download', async () => {
    global.fetch = jest.fn().mockResolvedValue(
      response([0x7b, 0x7d], 'application/json', 'error.json'),
    );
    const click = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    await expect(ExportService.exportToPDF(template, pages, [], DEFAULT_PAGE_SETTINGS))
      .rejects.toThrow(/instead of application\/pdf/i);
    expect(click).not.toHaveBeenCalled();
  });

  test('multi-page PNG export downloads the ZIP declared by the server', async () => {
    global.fetch = jest.fn().mockResolvedValue(
      response([0x50, 0x4b, 0x03, 0x04], 'application/zip', 'Quarterly-Report-png-pages.zip'),
    );
    const downloaded: string[] = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloaded.push(this.download);
    });

    await ExportService.exportViaBackend(
      'png', template, [...pages, { id: 'page-2', elements: [] }], [], DEFAULT_PAGE_SETTINGS,
    );

    expect(downloaded).toEqual(['Quarterly-Report-png-pages.zip']);
  });

  test('print renders a PDF and prints the dedicated PDF window, not the Designer page', async () => {
    jest.useFakeTimers();
    global.fetch = jest.fn().mockResolvedValue(
      response([0x25, 0x50, 0x44, 0x46, 0x2d], 'application/pdf', 'Quarterly-Report.pdf'),
    );
    const print = jest.fn();
    const replace = jest.fn();
    const close = jest.fn();
    const printWindow = {
      opener: window,
      document: { title: '', body: { textContent: '' } },
      location: { replace },
      print,
      close,
    } as unknown as Window;
    jest.spyOn(window, 'open').mockReturnValue(printWindow);
    const designerPrint = jest.spyOn(window, 'print').mockImplementation(() => undefined);

    await ExportService.printToPDF(template, pages, [], DEFAULT_PAGE_SETTINGS);
    jest.advanceTimersByTime(900);

    expect(replace).toHaveBeenCalledWith('blob:export');
    expect(print).toHaveBeenCalledTimes(1);
    expect(designerPrint).not.toHaveBeenCalled();
    expect(close).not.toHaveBeenCalled();
  });

  test('safe filenames are non-empty and remove reserved characters', () => {
    expect(ExportService.safeFileStem('  Q3: Europe / Sales?  ')).toBe('Q3-Europe-Sales');
    expect(ExportService.safeFileStem('   ')).toBe('document');
  });
});
