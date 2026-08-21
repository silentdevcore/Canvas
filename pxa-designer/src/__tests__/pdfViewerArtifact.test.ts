/** @jest-environment jsdom */

import { assertPdfBytes, downloadPdfBytes, safePdfFileName } from '@/features/pdf-viewer/pdfArtifact';
import { TextEncoder } from 'util';

describe('PDF Viewer artifact handling', () => {
  const originalCreateObjectUrl = URL.createObjectURL;
  const originalRevokeObjectUrl = URL.revokeObjectURL;

  beforeEach(() => {
    URL.createObjectURL = jest.fn(() => 'blob:pdf');
    URL.revokeObjectURL = jest.fn();
  });

  afterEach(() => {
    URL.createObjectURL = originalCreateObjectUrl;
    URL.revokeObjectURL = originalRevokeObjectUrl;
    jest.restoreAllMocks();
  });

  test('accepts a PDF header and rejects non-PDF bytes', () => {
    expect(assertPdfBytes(new TextEncoder().encode('%PDF-1.7\n%%EOF'))).toBeTruthy();
    expect(() => assertPdfBytes(new TextEncoder().encode('{"error":true}'))).toThrow(/valid PDF/i);
  });

  test('downloads validated bytes with a safe PDF filename', () => {
    const downloads: string[] = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloads.push(this.download);
    });

    downloadPdfBytes(new TextEncoder().encode('%PDF-1.7\n%%EOF'), ' Q3: Europe / Report? ');

    expect(downloads).toEqual(['Q3-Europe-Report.pdf']);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:pdf');
  });

  test('normalizes empty and duplicate PDF extensions', () => {
    expect(safePdfFileName('invoice.PDF')).toBe('invoice.pdf');
    expect(safePdfFileName('   ')).toBe('document.pdf');
  });
});
