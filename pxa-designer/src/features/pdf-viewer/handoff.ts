import type { PdfSource } from './PdfViewer';
import type { PdfAnnotation } from './annotations';
import type { PdfFormFieldValue } from './pdfForms';

const PDF_VIEWER_HANDOFF_KEY = 'pdf_viewer_handoff';

export type PdfViewerInitialFitMode = 'page' | 'width';

export interface PdfViewerHandoff {
  dataUrl?: string;
  url?: string;
  name?: string;
  initialPageNumber?: number;
  initialFitMode?: PdfViewerInitialFitMode;
  annotations?: PdfAnnotation[];
  formValues?: Array<{ name: string; value: PdfFormFieldValue }>;
}

export interface PdfViewerLaunchConfig {
  source: PdfSource | null;
  initialPageNumber: number;
  initialFitMode: PdfViewerInitialFitMode;
  annotations: PdfAnnotation[];
  formValues: Array<{ name: string; value: PdfFormFieldValue }>;
}

export const blobToDataUrl = (blob: Blob): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(String(reader.result));
  reader.onerror = () => reject(reader.error ?? new Error('Could not read PDF preview.'));
  reader.readAsDataURL(blob);
});

export const writePdfViewerHandoff = (handoff: PdfViewerHandoff): void => {
  sessionStorage.setItem(PDF_VIEWER_HANDOFF_KEY, JSON.stringify(handoff));
};

const sourceNameFromUrl = (url: string): string => {
  try {
    const parsed = new URL(url);
    const lastSegment = parsed.pathname.split('/').filter(Boolean).pop();
    return lastSegment || parsed.hostname || 'Remote PDF';
  } catch {
    return 'Remote PDF';
  }
};

const readHandoff = (): PdfViewerHandoff | null => {
  const raw = sessionStorage.getItem(PDF_VIEWER_HANDOFF_KEY);
  if (!raw) {
    return null;
  }

  sessionStorage.removeItem(PDF_VIEWER_HANDOFF_KEY);
  try {
    return JSON.parse(raw) as PdfViewerHandoff;
  } catch {
    return null;
  }
};

export const resolvePdfViewerLaunchConfig = (search = window.location.search): PdfViewerLaunchConfig => {
  const params = new URLSearchParams(search);
  const handoff = params.get('handoff') === 'session' ? readHandoff() : null;
  if (handoff?.dataUrl || handoff?.url) {
    const file = handoff.dataUrl || handoff.url || '';
    return {
      source: {
        file,
        kind: 'url',
        name: handoff.name || sourceNameFromUrl(file),
        url: file,
      },
      initialPageNumber: Math.max(1, Math.trunc(handoff.initialPageNumber ?? 1)),
      initialFitMode: handoff.initialFitMode === 'width' ? 'width' : 'page',
      annotations: Array.isArray(handoff.annotations) ? handoff.annotations : [],
      formValues: Array.isArray(handoff.formValues) ? handoff.formValues : [],
    };
  }

  const src = params.get('src');
  if (!src) {
    return { source: null, initialPageNumber: 1, initialFitMode: 'page', annotations: [], formValues: [] };
  }

  return {
    source: {
      file: src,
      kind: 'url',
      name: params.get('name') || sourceNameFromUrl(src),
      url: src,
    },
    initialPageNumber: Math.max(1, Number.parseInt(params.get('page') ?? '1', 10) || 1),
    initialFitMode: params.get('fit') === 'width' ? 'width' : 'page',
    annotations: [],
    formValues: [],
  };
};

export const resolvePdfViewerInitialSource = (search = window.location.search): PdfSource | null => (
  resolvePdfViewerLaunchConfig(search).source
);
