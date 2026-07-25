/**
 * @jest-environment jsdom
 */
import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import PdfViewer from '../features/pdf-viewer/PdfViewer';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

interface MockPdfDocument {
  numPages: number;
  getPage: (pageNumber: number) => Promise<{
    getViewport: () => { width: number; height: number };
  }>;
}

jest.mock('react-pdf', () => ({
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
  Document: ({ children, onLoadSuccess }: { children: React.ReactNode; onLoadSuccess?: (document: MockPdfDocument) => void }) => {
    const loadedRef = React.useRef(false);

    React.useEffect(() => {
      if (!loadedRef.current) {
        loadedRef.current = true;
        onLoadSuccess?.({
          numPages: 2,
          getPage: async () => ({
            getViewport: () => ({ width: 612, height: 792 }),
          }),
        });
      }
    }, [onLoadSuccess]);

    return <div data-testid="mock-pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid={`mock-pdf-page-${pageNumber}`}>Page {pageNumber}</div>
  ),
}));

describe('pdf viewer smoke test', () => {
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => {
      root.unmount();
    });
    container.remove();
  });

  test('renders toolbar, switches language, opens review panel, and focuses search with keyboard', async () => {
    await act(async () => {
      root.render(
        <PdfViewer
          initialSource={{
            file: '/sample.pdf',
            kind: 'url',
            name: 'sample.pdf',
            url: '/sample.pdf',
          }}
        />,
      );
    });

    expect(container.textContent).toContain('Open PDF');
    await act(async () => {
      await new Promise(resolve => window.setTimeout(resolve, 0));
    });
    expect(container.textContent).toContain('/ 2');

    const labels = Array.from(container.querySelectorAll('label')) as HTMLLabelElement[];
    const languageSelect = labels
      .find(label => label.textContent?.includes('Language'))
      ?.querySelector('select');
    expect(languageSelect).toBeTruthy();
    // Language now switches via the global i18next instance (i18n.changeLanguage),
    // which is async — flush pending microtasks so the re-render lands before asserting.
    await act(async () => {
      languageSelect!.value = 'de';
      languageSelect!.dispatchEvent(new Event('change', { bubbles: true }));
      await new Promise(resolve => window.setTimeout(resolve, 0));
    });
    expect(container.textContent).toContain('PDF öffnen');

    const buttons = Array.from(container.querySelectorAll('button')) as HTMLButtonElement[];
    const reviewButton = buttons
      .find(button => button.textContent === 'Überprüfen');
    expect(reviewButton).toBeTruthy();
    act(() => {
      reviewButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });
    expect(container.textContent).toContain('Notiz');
    expect(container.textContent).toContain('Schwärzen');

    act(() => {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }));
    });
    await act(async () => {
      await new Promise(resolve => window.setTimeout(resolve, 0));
    });
    expect(document.activeElement).toBe(container.querySelector('input[placeholder="Dokumenttext suchen"]'));
  });
});
