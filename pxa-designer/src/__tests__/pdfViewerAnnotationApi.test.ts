import { createAnnotationSidecar, type PdfAnnotation } from '../features/pdf-viewer/annotations';
import { applyRedactions, deleteSavedAnnotations, embedAnnotations, extractNativeAnnotations, flattenAnnotations, loadAnnotations, preflightRedactions, saveAnnotations } from '../features/pdf-viewer/annotationApi';
import { RedactionBlockedError, isApplyAllowed } from '../features/pdf-viewer/redaction';

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

const blobResponse = (status: number, body: Blob, headers: Record<string, string> = {}): Response => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => ({}),
  blob: async () => body,
  headers: { get: (name: string) => headers[name] ?? null },
} as unknown as Response);

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

  test('embedAnnotations posts pdf file and sidecar form data', async () => {
    const annotation = sampleAnnotation();
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    const embedded = new Blob(['%PDF annotated'], { type: 'application/pdf' });
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(blobResponse(200, embedded));

    const response = await embedAnnotations(file, sidecar);

    expect(response).toBe(embedded);
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/embed', {
      method: 'POST',
      body: expect.any(FormData),
    });

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('sidecar')).toBe(JSON.stringify(sidecar));
  });

  test('extractNativeAnnotations posts pdf file and returns sidecar', async () => {
    const annotation = sampleAnnotation();
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(jsonResponse(200, sidecar));

    const response = await extractNativeAnnotations(file);

    expect(response.annotations).toEqual([annotation]);
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/extract', {
      method: 'POST',
      body: expect.any(FormData),
    });

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
  });

  test('applyRedactions posts pdf file and sidecar form data', async () => {
    const annotation = { ...sampleAnnotation(), type: 'redaction' as const };
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    const redacted = new Blob(['%PDF redacted'], { type: 'application/pdf' });
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(blobResponse(200, redacted));

    const response = await applyRedactions(file, sidecar);

    expect(response.blob).toBe(redacted);
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/redact', {
      method: 'POST',
      body: expect.any(FormData),
    });

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('sidecar')).toBe(JSON.stringify(sidecar));
  });

  test('applyRedactions surfaces the audit headers alongside the file', async () => {
    const annotation = { ...sampleAnnotation(), type: 'redaction' as const };
    const sidecar = createAnnotationSidecar('review.pdf', [annotation]);
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(blobResponse(200, new Blob(['%PDF']), {
      'PXA-Redaction-Result-Sha256': 'a'.repeat(64),
      'PXA-Redaction-Audit': 'b'.repeat(64),
      'PXA-Redaction-Audit-Signed': 'true',
    }));

    const response = await applyRedactions(file, sidecar, 'Privacy request');

    expect(response.resultSha256).toBe('a'.repeat(64));
    expect(response.auditRecordSha256).toBe('b'.repeat(64));
    expect(response.auditSigned).toBe(true);
    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('reason')).toBe('Privacy request');
  });

  test('applyRedactions reports an unsigned audit record as unsigned', async () => {
    const sidecar = createAnnotationSidecar('review.pdf', [{ ...sampleAnnotation(), type: 'redaction' as const }]);
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(blobResponse(200, new Blob(['%PDF']), {
      'PXA-Redaction-Audit-Signed': 'false',
    }));

    const response = await applyRedactions(file, sidecar);

    expect(response.auditSigned).toBe(false);
  });

  test('preflightRedactions posts to the preflight route and returns the report', async () => {
    const sidecar = createAnnotationSidecar('review.pdf', [{ ...sampleAnnotation(), type: 'redaction' as const }]);
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    const report = {
      capability: 'Removable',
      sha256: 'c'.repeat(64),
      fileSizeBytes: 10,
      pageCount: 1,
      revisionCount: 1,
      regions: [],
      documentFindings: [],
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, report));

    const response = await preflightRedactions(file, sidecar);

    expect(response).toEqual(report);
    expect(fetchMock).toHaveBeenCalledWith('/api/pdf-viewer/annotations/redact/preflight', {
      method: 'POST',
      body: expect.any(FormData),
    });
    expect(isApplyAllowed(response)).toBe(true);
  });

  test('applyRedactions raises a blocked error carrying the preflight report', async () => {
    const sidecar = createAnnotationSidecar('review.pdf', [{ ...sampleAnnotation(), type: 'redaction' as const }]);
    const file = new File(['%PDF input'], 'review.pdf', { type: 'application/pdf' });
    const preflight = {
      capability: 'ReviewRequired',
      sha256: 'd'.repeat(64),
      fileSizeBytes: 10,
      pageCount: 1,
      revisionCount: 1,
      regions: [],
      documentFindings: [{ code: 'PXAPDFREDACT201', capability: 'ReviewRequired', message: 'Partial run.' }],
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(409, {
      error: 'The redaction was blocked by preflight.',
      code: 'PXAPDFREDACTVIEWER001',
      preflight,
    }));

    // A refusal must stay structured; flattening it to a string would hide why.
    const error = await applyRedactions(file, sidecar).catch((thrown: unknown) => thrown);

    expect(error).toBeInstanceOf(RedactionBlockedError);
    const blocked = error as RedactionBlockedError;
    expect(blocked.code).toBe('PXAPDFREDACTVIEWER001');
    expect(blocked.preflight?.documentFindings[0].code).toBe('PXAPDFREDACT201');
    expect(isApplyAllowed(blocked.preflight)).toBe(false);
  });

  test('throws API error message when request fails', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { error: 'annotations must be an array.' }));

    await expect(loadAnnotations('bad-payload')).rejects.toThrow('annotations must be an array.');
  });
});
