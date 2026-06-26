import { createAnnotationSidecar, type PdfAnnotation } from '../features/pdf-viewer/annotations';
import { deleteSavedAnnotations, flattenAnnotations, loadAnnotations, saveAnnotations } from '../features/pdf-viewer/annotationApi';

const sampleAnnotation = (): PdfAnnotation => ({
  id: 'annotation-1',
  type: 'note',
  pageNumber: 1,
  xPct: 10,
  yPct: 15,
  widthPct: 20,
  heightPct: 10,
  text: 'Review note',
  author: 'Reviewer',
  createdAt: '2026-06-25T10:00:00.000Z',
  color: '#facc15',
  locked: false,
});

const jsonResponse = (status: number, body: unknown): Response => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => body,
} as Response);

const blobResponse = (status: number, body: Blob): Response => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => ({}),
  blob: async () => body,
} as Response);

describe('pdf viewer annotation API client', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    global.fetch = fetchMock;
  });

  test('saveAnnotations posts the versioned sidecar payload', async () => {
    const annotation = sampleAnnotation();
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {
      documentId: 'review-pdf',
      ...sidecar,
      savedAt: '2026-06-25T10:05:00.000Z',
      annotationCount: 1,
    }));

    const response = await saveAnnotations('review-pdf', sidecar);

    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        documentId: 'review-pdf',
        version: 1,
        sourceName: 'review.pdf',
        exportedAt: sidecar.exportedAt,
        annotations: [annotation],
      }),
    });
    expect(response.annotationCount).toBe(1);
  });

  test('loadAnnotations returns null for 404', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(404, { error: 'not found' }));

    await expect(loadAnnotations('missing.pdf')).resolves.toBeNull();
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/missing.pdf');
  });

  test('loadAnnotations URL-encodes document ids', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {
      documentId: 'remote pdf',
      version: 1,
      sourceName: 'remote.pdf',
      exportedAt: '2026-06-25T10:00:00.000Z',
      savedAt: '2026-06-25T10:05:00.000Z',
      annotations: [],
      annotationCount: 0,
    }));

    await loadAnnotations('remote pdf');

    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/remote%20pdf');
  });

  test('deleteSavedAnnotations sends DELETE', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(204, {}));

    await deleteSavedAnnotations('review-pdf');

    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/review-pdf', { method: 'DELETE' });
  });

  test('flattenAnnotations posts pdf file and sidecar form data', async () => {
    const annotation = sampleAnnotation();
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    const flattened = new Blob(['%PDF reviewed'], { type: 'application/pdf' });
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(blobResponse(200, flattened));

    const response = await flattenAnnotations(file, sidecar);

    expect(response).toBe(flattened);
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/flatten', {
      method: 'POST',
      body: expect.any(FormData),
    });

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('sidecar')).toBe(JSON.stringify(sidecar));
  });

  test('throws API error message when request fails', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { error: 'annotations must be an array.' }));

    await expect(loadAnnotations('bad-payload')).rejects.toThrow('annotations must be an array.');
  });
});
