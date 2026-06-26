import type { PdfAnnotation, PdfAnnotationSidecar } from './annotations';

interface SaveAnnotationsRequest {
  documentId: string;
  version: 1;
  sourceName: string | null;
  exportedAt: string;
  annotations: PdfAnnotation[];
}

interface AnnotationsApiResponse extends SaveAnnotationsRequest {
  savedAt: string;
  annotationCount: number;
}

const API_BASE = '/api/pdf-viewer/annotations';

const assertOk = async (response: Response): Promise<void> => {
  if (response.ok) {
    return;
  }

  const body = await response.json().catch(() => ({}));
  throw new Error(body.error || `HTTP ${response.status}`);
};

export const saveAnnotations = async (
  documentId: string,
  sidecar: PdfAnnotationSidecar,
): Promise<AnnotationsApiResponse> => {
  const request: SaveAnnotationsRequest = {
    documentId,
    version: sidecar.version,
    sourceName: sidecar.sourceName,
    exportedAt: sidecar.exportedAt,
    annotations: sidecar.annotations,
  };

  const response = await fetch(API_BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  await assertOk(response);
  return response.json() as Promise<AnnotationsApiResponse>;
};

export const loadAnnotations = async (documentId: string): Promise<AnnotationsApiResponse | null> => {
  const response = await fetch(`${API_BASE}/${encodeURIComponent(documentId)}`);
  if (response.status === 404) {
    return null;
  }

  await assertOk(response);
  return response.json() as Promise<AnnotationsApiResponse>;
};

export const deleteSavedAnnotations = async (documentId: string): Promise<void> => {
  const response = await fetch(`${API_BASE}/${encodeURIComponent(documentId)}`, { method: 'DELETE' });
  await assertOk(response);
};

export const flattenAnnotations = async (
  pdfFile: File,
  sidecar: PdfAnnotationSidecar,
): Promise<Blob> => {
  const form = new FormData();
  form.append('file', pdfFile);
  form.append('sidecar', JSON.stringify(sidecar));

  const response = await fetch(`${API_BASE}/flatten`, {
    method: 'POST',
    body: form,
  });
  await assertOk(response);
  return response.blob();
};
