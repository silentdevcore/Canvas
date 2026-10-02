/** @jest-environment jsdom */
import {
  DesignerProjectApiError,
  openPdfProject,
  openSpreadsheetProject,
  preparePdfProjectAssets,
  projectCompatibilitySummary,
  storePdfProjectAssets,
} from '@/services/designerProjectApi';
import { deleteDesignerAsset, uploadDesignerImage } from '@/services/designerAssetApi';

jest.mock('@/services/designerAssetApi', () => ({
  deleteDesignerAsset: jest.fn(),
  uploadDesignerImage: jest.fn(),
}));

describe('Designer project API', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    jest.mocked(deleteDesignerAsset).mockReset();
    jest.mocked(uploadDesignerImage).mockReset();
    global.fetch = fetchMock;
    global.structuredClone = value => JSON.parse(JSON.stringify(value));
  });

  test('opens a PXAP through the canonical PDF project endpoint', async () => {
    const file = new File(['project'], 'invoice.pxap', {
      type: 'application/vnd.powerdox.pxap+zip',
    });
    const response = {
      manifest: { title: 'Invoice', formatId: 'pxap', documentSchemaVersion: '1.0' },
      document: { id: 'invoice', name: 'Invoice', pages: [{ id: 'page-1', elements: [] }] },
      packageSha256: 'a'.repeat(64),
    };
    fetchMock.mockResolvedValue({ ok: true, json: async () => response });

    await expect(openPdfProject(file)).resolves.toEqual(response);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/pxa/v1/designer/projects/pdf/open');
    expect(init).toEqual(expect.objectContaining({ method: 'POST', credentials: 'include' }));
    expect((init.body as FormData).get('file')).toBe(file);
  });

  test('sends selected-target IDs for deterministic collision handling', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        manifest: { title: 'Invoice', formatId: 'pxap', documentSchemaVersion: '1.0' },
        document: { id: 'document-import-2', name: 'Invoice', pages: [] },
        packageSha256: 'a'.repeat(64),
        idRemaps: { document: 'document-import-2' },
      }),
    });

    await openPdfProject(new File(['project'], 'invoice.pxap'), ['document', 'page-1']);

    const init = fetchMock.mock.calls[0][1];
    expect(init.body).toBeInstanceOf(FormData);
    expect((init.body as FormData).get('reservedIds')).toBe('["document","page-1"]');
  });

  test('keeps PDF and Spreadsheet project endpoints separate', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        manifest: { title: 'Budget', formatId: 'pxas', documentSchemaVersion: '1.0' },
        document: { id: 'budget', name: 'Budget', sheets: [] },
        packageSha256: 'b'.repeat(64),
      }),
    });

    await openSpreadsheetProject(new File(['project'], 'budget.pxas'));

    expect(fetchMock.mock.calls[0][0]).toBe('/api/pxa/v1/designer/projects/spreadsheet/open');
  });

  test('captures unknown PDF document, page, and settings properties for re-export', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        manifest: { title: 'Future', formatId: 'pxap', documentSchemaVersion: '1.0' },
        document: {
          id: 'future',
          name: 'Future',
          futureDocumentOption: { enabled: true },
          pages: [{ id: 'page-1', elements: [], futurePageOption: 42 }],
          pageSettings: { width: 595, height: 842, orientation: 'portrait', futureSettingsOption: 'kept' },
        },
        packageSha256: 'c'.repeat(64),
      }),
    });

    const result = await openPdfProject(new File(['project'], 'future.pxap'));

    expect(result.document.extensionData).toEqual({ futureDocumentOption: { enabled: true } });
    expect(result.document.pages[0].extensionData).toEqual({ futurePageOption: 42 });
    expect(result.document.pageSettings?.extensionData).toEqual({ futureSettingsOption: 'kept' });
  });

  test('surfaces stable project diagnostics from Problem Details', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ code: 'PXAPROJECT007', detail: 'The package was tampered with.' }),
    });

    const operation = openPdfProject(new File(['broken'], 'broken.pxap'));

    await expect(operation).rejects.toThrow('The package was tampered with.');
    await expect(operation).rejects.toMatchObject({
      code: 'PXAPROJECT007',
      status: 400,
    } satisfies Partial<DesignerProjectApiError>);
  });

  test('reports real upload progress and server validation for large projects', async () => {
    const progress: Array<{ phase: string; percent: number }> = [];
    const response = {
      manifest: { title: 'Large', formatId: 'pxap', documentSchemaVersion: '1.0' },
      document: { id: 'large', name: 'Large', pages: [{ id: 'page-1', elements: [] }] },
      packageSha256: 'd'.repeat(64),
    };
    const requestListeners = new Map<string, () => void>();
    const uploadListeners = new Map<string, (event: ProgressEvent) => void>();
    class ProjectRequest {
      upload = { addEventListener: (name: string, listener: (event: ProgressEvent) => void) => uploadListeners.set(name, listener) };
      status = 200;
      responseText = JSON.stringify(response);
      open = jest.fn();
      setRequestHeader = jest.fn();
      addEventListener = (name: string, listener: () => void) => requestListeners.set(name, listener);
      send = () => {
        uploadListeners.get('progress')?.({ lengthComputable: true, loaded: 50, total: 100 } as ProgressEvent);
        uploadListeners.get('load')?.({} as ProgressEvent);
        requestListeners.get('load')?.();
      };
      abort = jest.fn();
    }
    const original = global.XMLHttpRequest;
    global.XMLHttpRequest = ProjectRequest as unknown as typeof XMLHttpRequest;
    try {
      await openPdfProject(new File([new Uint8Array(100)], 'large.pxap'), [], {
        onProgress: event => progress.push({ phase: event.phase, percent: event.percent }),
      });
    } finally {
      global.XMLHttpRequest = original;
    }

    expect(progress).toEqual([
      { phase: 'uploading', percent: 0 },
      { phase: 'uploading', percent: 43 },
      { phase: 'validating', percent: 90 },
      { phase: 'validating', percent: 100 },
    ]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  test('cancels a project upload through AbortSignal with a stable diagnostic', async () => {
    const requestListeners = new Map<string, () => void>();
    class ProjectRequest {
      upload = { addEventListener: jest.fn() };
      open = jest.fn();
      setRequestHeader = jest.fn();
      addEventListener = (name: string, listener: () => void) => requestListeners.set(name, listener);
      send = jest.fn();
      abort = () => requestListeners.get('abort')?.();
      status = 0;
      responseText = '';
    }
    const original = global.XMLHttpRequest;
    global.XMLHttpRequest = ProjectRequest as unknown as typeof XMLHttpRequest;
    const controller = new AbortController();
    try {
      const operation = openPdfProject(new File(['project'], 'cancel.pxap'), [], {
        signal: controller.signal,
        onProgress: jest.fn(),
      });
      controller.abort();
      await expect(operation).rejects.toMatchObject({
        code: 'PXAPROJECT_CANCELLED',
        status: 499,
      });
    } finally {
      global.XMLHttpRequest = original;
    }
  });

  test('summarizes missing capabilities and review diagnostics before project apply', () => {
    expect(projectCompatibilitySummary({
      requiresReview: true,
      missingFeatures: ['pdf.future-feature'],
      diagnostics: [
        { code: 'PXAPROJECT_EXTERNAL_LINK', severity: 'warning', message: 'External link.', source: 'page-1' },
        { code: 'PXAPROJECT_INFO', severity: 'info', message: 'Informational.' },
      ],
    })).toBe('Feature: pdf.future-feature | PXAPROJECT_EXTERNAL_LINK: External link.');
    expect(projectCompatibilitySummary({
      requiresReview: false,
      missingFeatures: [],
      diagnostics: [],
    })).toBeUndefined();
  });

  test('requires review whenever the server reports an in-memory schema upgrade', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        manifest: { title: 'Legacy', formatId: 'pxap', documentSchemaVersion: '1.0' },
        document: { id: 'legacy', name: 'Legacy', pages: [] },
        packageSha256: 'original-hash',
        compatibility: { requiresReview: false, missingFeatures: [], diagnostics: [] },
        upgradeReport: {
          sourceVersion: '0.9', targetVersion: '1.0', originalPackageSha256: 'original-hash',
          isLossy: false, steps: [{ fromVersion: '0.9', toVersion: '1.0', isLossy: false }],
          diagnostics: [{
            code: 'PXAPROJECT_UPGRADED', severity: 'warning',
            message: 'The project was upgraded in memory.', source: 'document',
          }],
        },
        idRemaps: {},
      }),
    });

    const result = await openPdfProject(new File(['project'], 'legacy.pxap'));

    expect(result.compatibility).toMatchObject({ requiresReview: true });
    expect(result.compatibility?.diagnostics).toContainEqual(expect.objectContaining({
      code: 'PXAPROJECT_UPGRADED',
    }));
  });

  test('rejects project image types unsupported by the tenant Asset Store', async () => {
    fetchMock.mockResolvedValue({
      blob: async () => new Blob(['<svg/>'], { type: 'image/svg+xml' }),
    });

    const operation = storePdfProjectAssets({
      id: 'project',
      name: 'Project',
      pages: [{
        id: 'page-1',
        elements: [{
          id: 'image-1',
          type: 'image',
          content: 'data:image/svg+xml;base64,PHN2Zy8+',
          x: 0,
          y: 0,
          width: 100,
          height: 100,
        }],
      }],
    });

    await expect(operation).rejects.toMatchObject({ code: 'PXAPROJECT010' });
  });

  test('removes assets already uploaded when a later project image fails', async () => {
    fetchMock.mockResolvedValue({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    });
    jest.mocked(uploadDesignerImage)
      .mockResolvedValueOnce({ id: 'asset-1' } as never)
      .mockRejectedValueOnce(new Error('upload failed'));
    jest.mocked(deleteDesignerAsset).mockResolvedValue();

    const operation = storePdfProjectAssets({
      id: 'project',
      name: 'Project',
      pages: [{
        id: 'page-1',
        elements: ['image-1', 'image-2'].map(id => ({
          id,
          type: 'image' as const,
          content: 'data:image/png;base64,cG5n',
          x: 0,
          y: 0,
          width: 100,
          height: 100,
        })),
      }],
    });

    await expect(operation).rejects.toThrow('upload failed');
    expect(deleteDesignerAsset).toHaveBeenCalledWith('asset-1');
  });

  test('rolls back prepared PDF assets when applying the project fails', async () => {
    fetchMock.mockResolvedValue({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    });
    jest.mocked(uploadDesignerImage).mockResolvedValue({ id: 'asset-rollback' } as never);
    jest.mocked(deleteDesignerAsset).mockResolvedValue();
    const source = {
      id: 'project', name: 'Project',
      pages: [{ id: 'page-1', elements: [{
        id: 'image-1', type: 'image' as const, content: 'data:image/png;base64,cG5n',
        x: 0, y: 0, width: 100, height: 100,
      }] }],
    };

    const prepared = await preparePdfProjectAssets(source);
    expect(prepared.document.pages[0].elements[0]).toMatchObject({
      assetId: 'asset-rollback', content: undefined,
    });
    expect(source.pages[0].elements[0].content).toBe('data:image/png;base64,cG5n');

    await prepared.rollback();
    await prepared.rollback();
    expect(deleteDesignerAsset).toHaveBeenCalledTimes(1);
    expect(deleteDesignerAsset).toHaveBeenCalledWith('asset-rollback');
  });

  test('does not remove prepared PDF assets after commit', async () => {
    fetchMock.mockResolvedValue({
      blob: async () => new Blob(['png'], { type: 'image/png' }),
    });
    jest.mocked(uploadDesignerImage).mockResolvedValue({ id: 'asset-committed' } as never);
    jest.mocked(deleteDesignerAsset).mockResolvedValue();

    const prepared = await preparePdfProjectAssets({
      id: 'project', name: 'Project',
      pages: [{ id: 'page-1', elements: [{
        id: 'image-1', type: 'image', content: 'data:image/png;base64,cG5n',
        x: 0, y: 0, width: 100, height: 100,
      }] }],
    });
    prepared.commit();
    await prepared.rollback();

    expect(deleteDesignerAsset).not.toHaveBeenCalled();
  });
});
