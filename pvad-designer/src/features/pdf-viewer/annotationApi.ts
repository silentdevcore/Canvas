import type { PdfAnnotation, PdfAnnotationSidecar } from './annotations';
import { readPvadDocumentHeader } from '../../utils/pvadDocumentHeaders';
import {
  RedactionBlockedError,
  type RedactionApplyOutcome,
  type RedactionPreflightReport,
} from './redaction';

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

/** Reports what would be removed. Never changes the uploaded document. */
export const preflightRedactions = async (
  pdfFile: File,
  sidecar: PdfAnnotationSidecar,
): Promise<RedactionPreflightReport> => {
  const form = new FormData();
  form.append('file', pdfFile);
  form.append('sidecar', JSON.stringify(sidecar));

  const response = await fetch(`${API_BASE}/redact/preflight`, {
    method: 'POST',
    body: form,
  });
  await assertOk(response);
  return response.json() as Promise<RedactionPreflightReport>;
};

/**
 * Applies the redaction. A 409 means the backend refused rather than returning a file it
 * cannot stand behind, so the structured report is surfaced instead of being flattened into
 * a message string.
 */
export const applyRedactions = async (
  pdfFile: File,
  sidecar: PdfAnnotationSidecar,
  reason?: string,
): Promise<RedactionApplyOutcome> => {
  const form = new FormData();
  form.append('file', pdfFile);
  form.append('sidecar', JSON.stringify(sidecar));
  if (reason) {
    form.append('reason', reason);
  }

  const response = await fetch(`${API_BASE}/redact`, {
    method: 'POST',
    body: form,
  });

  if (response.status === 409) {
    const body = await response.json().catch(() => ({}));
    throw new RedactionBlockedError(
      body.error || 'The redaction was refused.',
      body.code ?? null,
      body.preflight ?? null,
      body.verification ?? null,
      body.canonicalCode ?? null,
    );
  }

  await assertOk(response);
  return {
    blob: await response.blob(),
    resultSha256: readPvadDocumentHeader(response.headers, 'Redaction-Result-Sha256'),
    auditRecordSha256: readPvadDocumentHeader(response.headers, 'Redaction-Audit'),
    auditSigned: readPvadDocumentHeader(response.headers, 'Redaction-Audit-Signed') === 'true',
  };
};

export const embedAnnotations = async (
  pdfFile: File,
  sidecar: PdfAnnotationSidecar,
): Promise<Blob> => {
  const form = new FormData();
  form.append('file', pdfFile);
  form.append('sidecar', JSON.stringify(sidecar));

  const response = await fetch(`${API_BASE}/embed`, {
    method: 'POST',
    body: form,
  });
  await assertOk(response);
  return response.blob();
};

export const extractNativeAnnotations = async (
  pdfFile: File,
): Promise<PdfAnnotationSidecar> => {
  const form = new FormData();
  form.append('file', pdfFile);

  const response = await fetch(`${API_BASE}/extract`, {
    method: 'POST',
    body: form,
  });
  await assertOk(response);
  return response.json() as Promise<PdfAnnotationSidecar>;
};
