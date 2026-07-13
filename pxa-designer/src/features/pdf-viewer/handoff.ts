import type { PdfSource } from './PdfViewer';

const PDF_VIEWER_HANDOFF_KEY = 'pdf_viewer_handoff';

interface PdfViewerHandoff {
  dataUrl?: string;
  url?: string;
  name?: string;
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

export const resolvePdfViewerInitialSource = (search = window.location.search): PdfSource | null => {
  const params = new URLSearchParams(search);
  const handoff = params.get('handoff') === 'session' ? readHandoff() : null;
  if (handoff?.dataUrl || handoff?.url) {
    const file = handoff.dataUrl || handoff.url || '';
    return {
      file,
      kind: 'url',
      name: handoff.name || sourceNameFromUrl(file),
      url: file,
    };
  }

  const src = params.get('src');
  if (!src) {
    return null;
  }

  return {
    file: src,
    kind: 'url',
    name: params.get('name') || sourceNameFromUrl(src),
    url: src,
  };
};
