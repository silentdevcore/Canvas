/**
 * @jest-environment jsdom
 */
import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import PdfViewer from '../features/pdf-viewer/PdfViewer';
import i18n from '../i18n';

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
    jest.restoreAllMocks();
    delete (global as unknown as { fetch?: typeof fetch }).fetch;
  });

  test('renders toolbar, switches language, opens review panel, and focuses search with keyboard', async () => {
    await act(async () => {
      root.render(
        <PdfViewer
          canInspectTrust={false}
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

  test('renders annotations supplied by a migrated Viewer package', async () => {
    await i18n.changeLanguage('en');
    await act(async () => {
      root.render(
        <PdfViewer
          canInspectTrust={false}
          initialSource={{ file: '/migration-preview.pdf', kind: 'url', name: 'migration-preview.pdf', url: '/migration-preview.pdf' }}
          initialAnnotations={[{
            id: 'migrated-note',
            type: 'note',
            pageNumber: 1,
            xPct: 10,
            yPct: 15,
            widthPct: 8,
            heightPct: 8,
            text: 'Migrated review note',
            author: 'Nutrient migration',
            createdAt: '2026-09-22T00:00:00Z',
            color: '#2563eb',
          }]}
        />,
      );
      await new Promise(resolve => window.setTimeout(resolve, 0));
    });

    expect(container.querySelector('.pdfv-annotation-note')).not.toBeNull();
    expect(container.textContent).toContain('Migrated review note');
  });

  test('shows accessible trust states, signature details, signing controls, and mutation protection', async () => {
    await i18n.changeLanguage('en');
    global.fetch = jest.fn()
      .mockResolvedValueOnce({
        ok: true,
        status: 202,
        json: async () => ({
          jobId: 'synthetic-inspect',
          status: 'accepted',
          statusUrl: '/api/synthetic-inspect',
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          id: 'synthetic-inspect',
          status: 'completed',
          downloadUrl: '/api/synthetic-inspect-result',
        }),
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          status: 0,
          structureStatus: 0,
          sha256: 'a'.repeat(64),
          fileSizeBytes: 256,
          pdfVersion: '1.7',
          revisionCount: 2,
          objectCount: 12,
          encryption: { isEncrypted: false, status: 4 },
          signatures: [{
            objectNumber: 8,
            generation: 0,
            status: 0,
            byteRange: [0, 100, 200, 100],
            integrityStatus: 0,
            coverageStatus: 2,
            certificateStatus: 0,
            revocationStatus: 2,
            padesProfile: 'B-T',
            digestAlgorithm: 'SHA-256',
            signerSubject: 'CN=Synthetic Signer',
            signerThumbprint: 'ABC123',
            signingTime: '2026-09-11T10:00:00Z',
            timestampStatus: 0,
            timestampRevocationStatus: 2,
            historicalRevocationStatus: 2,
            timestampHistoricalRevocationStatus: 2,
            coversWholeDocument: false,
            signedRevisionEndOffset: 300,
            isDocumentTimestamp: false,
            archiveTimestampStatus: 4,
          }],
          pdfA: { standard: 'PDF/A', isClaimed: false, status: 4, evidence: 'none' },
          pdfUa: { standard: 'PDF/UA', isClaimed: false, status: 4, evidence: 'none' },
          revisions: [
            { sequenceNumber: 1, kind: 'Original', crossReferenceOffset: 10, endOffset: 300, sha256: 'b'.repeat(64), entryCount: 8 },
            { sequenceNumber: 2, kind: 'Incremental', crossReferenceOffset: 320, endOffset: 400, sha256: 'c'.repeat(64), entryCount: 4 },
          ],
          diagnostics: [{
            code: 'PXAPDFTRUST001',
            canonicalCode: 'PVADPDFTRUST001',
            severity: 1,
            message: 'Reviewed diagnostic.',
          }, {
            code: 'PXATEST999',
            severity: 1,
            message: 'Synthetic technical detail.',
            objectNumber: 9,
            generation: 0,
            pageNumber: 2,
            localizationKey: 'diagnostics.PXATEST999',
            localizationParameters: { code: 'PXATEST999', pageNumber: '2', objectNumber: '9' },
          }],
        }),
      } as Response);

    const sourceFile = new File(['%PDF-1.7'], 'signed.pdf');
    Object.defineProperty(sourceFile, 'arrayBuffer', {
      value: async () => new Uint8Array([0x25, 0x50, 0x44, 0x46, 0x2d, 0x31, 0x2e, 0x37]).buffer,
    });
    await act(async () => {
      root.render(<PdfViewer initialSource={{ file: sourceFile, kind: 'file', name: 'signed.pdf' }} canSign />);
    });
    const trustButton = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'PDF Trust');
    act(() => trustButton?.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(container.textContent).toContain('PKCS#12 certificate');

    const inspectButton = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Inspect trust');
    await act(async () => {
      inspectButton?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      await new Promise(resolve => window.setTimeout(resolve, 20));
    });

    expect(container.textContent).toContain('CN=Synthetic Signer');
    expect(container.querySelector('[aria-label^="Valid."]')).not.toBeNull();
    expect(container.textContent).toContain('ABC123');
    expect(container.textContent).toContain('Later revisions exist');
    expect(container.textContent).toContain('Check PXATEST999 requires review.');
    expect(container.querySelector('code[title="PXAPDFTRUST001"]')?.textContent)
      .toBe('PVADPDFTRUST001');
    expect(container.querySelector('code[title="PXATEST999"]')?.textContent)
      .toBe('PXATEST999');

    const objectNavigation = jest.fn();
    window.addEventListener('pdf-viewer:navigate-object', objectNavigation, { once: true });
    const objectTarget = Array.from(container.querySelectorAll('button'))
      .find(button => button.textContent?.startsWith('Object #9'));
    expect(objectTarget).toBeTruthy();
    act(() => objectTarget!.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(container.querySelector('[data-testid="mock-pdf-page-2"]')).not.toBeNull();
    expect(objectNavigation).toHaveBeenCalledTimes(1);

    const reviewButton = Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Review');
    act(() => reviewButton?.dispatchEvent(new MouseEvent('click', { bubbles: true })));
    expect(container.textContent).toContain('This document has trust properties');

  });
});
