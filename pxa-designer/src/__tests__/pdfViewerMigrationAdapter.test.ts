/**
 * @jest-environment jsdom
 */
import { resolvePdfViewerLaunchConfig, writePdfViewerHandoff } from '@/features/pdf-viewer/handoff';
import { openMigratedPdfViewer } from '@/features/pdf-viewer/migrationAdapter';

describe('PDF Viewer migration handoff', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('preserves the migrated source, page and fit mode exactly once', () => {
    writePdfViewerHandoff({
      url: '/documents/report.pdf',
      name: 'Report.pdf',
      initialPageNumber: 4,
      initialFitMode: 'width',
      annotations: [],
      formValues: [],
    });

    expect(resolvePdfViewerLaunchConfig('?handoff=session')).toEqual({
      source: {
        file: '/documents/report.pdf',
        kind: 'url',
        name: 'Report.pdf',
        url: '/documents/report.pdf',
      },
      initialPageNumber: 4,
      initialFitMode: 'width',
      annotations: [],
      formValues: [],
    });
    expect(resolvePdfViewerLaunchConfig('?handoff=session')).toEqual({
      source: null,
      initialPageNumber: 1,
      initialFitMode: 'page',
      annotations: [],
      formValues: [],
    });
  });

  it('transfers migrated annotations and form values into the PXA Viewer once', () => {
    const annotation = {
      id: 'note-1',
      type: 'note' as const,
      pageNumber: 1,
      xPct: 10,
      yPct: 20,
      widthPct: 5,
      heightPct: 5,
      text: 'Review',
      author: 'Migration',
      createdAt: '2026-09-22T00:00:00Z',
      color: '#2563eb',
    };
    writePdfViewerHandoff({
      dataUrl: 'data:application/pdf;base64,JVBERi0=',
      annotations: [annotation],
      formValues: [{ name: 'Approved', value: true }],
    });

    const launch = resolvePdfViewerLaunchConfig('?handoff=session');

    expect(launch.annotations).toEqual([annotation]);
    expect(launch.formValues).toEqual([{ name: 'Approved', value: true }]);
    expect(resolvePdfViewerLaunchConfig('?handoff=session').annotations).toEqual([]);
  });

  it('normalizes invalid launch preferences', () => {
    writePdfViewerHandoff({
      url: '/documents/report.pdf',
      initialPageNumber: -10,
      initialFitMode: 'page',
    });

    const result = resolvePdfViewerLaunchConfig('?handoff=session');

    expect(result.initialPageNumber).toBe(1);
    expect(result.initialFitMode).toBe('page');
  });

  it('rejects externally hosted PDFs at the generated integration boundary', () => {
    expect(() => openMigratedPdfViewer({
      schemaVersion: 1,
      providerKey: 'nutrient-viewer',
      documentSource: 'https://customer.example/private.pdf',
    })).toThrow('application-owned PDF source');
  });

  it.each(['//evil.example/private.pdf', '../private.pdf', 'data:application/pdf;base64,secret'])(
    'rejects unsafe migrated source %s',
    (documentSource) => {
      expect(() => openMigratedPdfViewer({
        schemaVersion: 1,
        providerKey: 'nutrient-viewer',
        documentSource,
      })).toThrow('application-owned PDF source');
    },
  );
});
