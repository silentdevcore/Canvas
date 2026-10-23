/** @jest-environment jsdom */

import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import i18n from '@/i18n';
import PdfViewer from '@/features/pdf-viewer/PdfViewer';
import { TextEncoder } from 'util';

const reactTestEnvironment = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
reactTestEnvironment.IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('react-pdf', () => ({
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
  Document: ({ children, onLoadSuccess }: { children: React.ReactNode; onLoadSuccess?: (document: unknown) => void }) => {
    const loaded = React.useRef(false);
    React.useEffect(() => {
      if (loaded.current) return;
      loaded.current = true;
      onLoadSuccess?.({
        numPages: 2,
        getPage: async () => ({
          getViewport: () => ({ width: 612, height: 792 }),
          getTextContent: async () => ({ items: [] }),
        }),
      });
    }, [onLoadSuccess]);
    return <div>{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => <div>Page {pageNumber}</div>,
}));

const pdfBytes = new TextEncoder().encode('%PDF-1.7\n%%EOF');
const pdfResponse = () => ({
  ok: true,
  status: 200,
  arrayBuffer: async () => pdfBytes.buffer.slice(0),
}) as Response;

describe('PDF Viewer download and print reliability', () => {
  let container: HTMLDivElement;
  let root: Root;
  const originalFetch = global.fetch;
  const originalCreateObjectUrl = URL.createObjectURL;
  const originalRevokeObjectUrl = URL.revokeObjectURL;

  beforeEach(async () => {
    await i18n.changeLanguage('en');
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    global.fetch = jest.fn(async () => pdfResponse());
    URL.createObjectURL = jest.fn(() => 'blob:validated-pdf');
    URL.revokeObjectURL = jest.fn();
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    global.fetch = originalFetch;
    URL.createObjectURL = originalCreateObjectUrl;
    URL.revokeObjectURL = originalRevokeObjectUrl;
    jest.restoreAllMocks();
  });

  const renderViewer = async () => {
    await act(async () => {
      root.render(<PdfViewer initialSource={{ file: '/remote.pdf', kind: 'url', name: 'Remote Report', url: '/remote.pdf' }} />);
      await Promise.resolve();
    });
  };

  test('downloads remote PDF bytes instead of navigating to the source URL', async () => {
    const downloads: Array<{ name: string; href: string }> = [];
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function click(this: HTMLAnchorElement) {
      downloads.push({ name: this.download, href: this.href });
    });
    await renderViewer();

    const download = container.querySelector('button[title="Download"]') as HTMLButtonElement;
    await act(async () => {
      download.click();
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(downloads).toEqual([{ name: 'Remote-Report.pdf', href: 'blob:validated-pdf' }]);
    expect(global.fetch).toHaveBeenCalledWith('/remote.pdf');
    expect(container.textContent).toContain('download:completed');
  });

  test('opens a dedicated validated PDF target before invoking print', async () => {
    const replace = jest.fn();
    const print = jest.fn();
    const printWindow = {
      opener: window,
      document: { title: '', body: { textContent: '' } },
      location: { replace },
      print,
      close: jest.fn(),
    } as unknown as Window;
    jest.spyOn(window, 'open').mockReturnValue(printWindow);
    jest.spyOn(window, 'setTimeout').mockImplementation((handler) => {
      handler();
      return 1 as unknown as ReturnType<typeof setTimeout>;
    });
    await renderViewer();

    act(() => (container.querySelector('button[title="Print"]') as HTMLButtonElement).click());
    const confirm = container.querySelector('.pdfv-print-panel .pdfv-button-primary') as HTMLButtonElement;
    await act(async () => {
      confirm.click();
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(window.open).toHaveBeenCalledWith('', '_blank');
    expect(replace).toHaveBeenCalledWith('blob:validated-pdf');
    expect(print).toHaveBeenCalledTimes(1);
  });

  test('keeps the print panel open and reports a blocked popup', async () => {
    jest.spyOn(window, 'open').mockReturnValue(null);
    await renderViewer();

    act(() => (container.querySelector('button[title="Print"]') as HTMLButtonElement).click());
    const confirm = container.querySelector('.pdfv-print-panel .pdfv-button-primary') as HTMLButtonElement;
    await act(async () => confirm.click());

    expect(container.textContent).toContain('The PDF print window was blocked by the browser.');
    expect(container.querySelector('.pdfv-print-panel')).toBeTruthy();
  });
});
