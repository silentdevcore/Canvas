import type { Page, PageSettings, SimpleElement, ProjectCodeWorkspace } from '@/types';
export type { ProjectCodeWorkspace } from '@/types';
import type { Workbook } from '@/spreadsheet/types';
import { deleteDesignerAsset, uploadDesignerImage } from '@/services/designerAssetApi';
import { designerMutationHeaders } from '@/auth/designerAuth';

const base = '/api/pvad/v1/designer/projects';

export interface PdfProjectDocument {
  id: string;
  name: string;
  category?: string;
  description?: string;
  pages: Page[];
  sharedElements?: SimpleElement[];
  pageSettings?: PageSettings;
  extensionData?: Record<string, unknown>;
}

export interface ProjectDiagnostic {
  code: string;
  severity: 'info' | 'warning' | 'error';
  message: string;
  source?: string;
}

export interface ProjectCompatibility {
  requiresReview: boolean;
  missingFeatures: string[];
  diagnostics: ProjectDiagnostic[];
}

export interface ProjectUpgradeReport {
  sourceVersion: string;
  targetVersion: string;
  originalPackageSha256: string;
  isLossy: boolean;
  steps: { fromVersion: string; toVersion: string; isLossy: boolean }[];
  diagnostics: ProjectDiagnostic[];
}

export type ProjectTransferPhase = 'uploading' | 'validating';

export interface ProjectTransferProgress {
  phase: ProjectTransferPhase;
  percent: number;
  loaded: number;
  total: number;
}

export interface ProjectOpenOptions {
  signal?: AbortSignal;
  onProgress?: (progress: ProjectTransferProgress) => void;
}

export interface PreparedProjectAssets<T> {
  document: T;
  commit: () => void;
  rollback: () => Promise<void>;
}

export interface ProjectOpenResponse<T> {
  manifest: {
    title: string;
    formatId: string;
    documentSchemaVersion: string;
    requiredFeatures?: string[];
    diagnostics?: ProjectDiagnostic[];
  };
  document: T;
  packageSha256: string;
  compatibility?: ProjectCompatibility;
  upgradeReport?: ProjectUpgradeReport;
  idRemaps?: Record<string, string>;
  codeWorkspace?: ProjectCodeWorkspace;
}

export class DesignerProjectApiError extends Error {
  constructor(message: string, readonly code?: string, readonly status?: number) {
    super(message);
  }
}

export const isProjectOperationCancelled = (error: unknown): boolean =>
  (error instanceof DesignerProjectApiError && error.code === 'PXAPROJECT_CANCELLED') ||
  (error instanceof DOMException && error.name === 'AbortError');

const safeFileStem = (name: string, fallback: string): string => {
  const value = name.trim()
    .replace(/[<>:"/\\|?*\u0000-\u001f\s]+/g, '-')
    .replace(/^[.-]+|[.-]+$/g, '')
    .slice(0, 180);
  return value || fallback;
};

const responseFileName = (response: Response, fallback: string): string => {
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  const plain = disposition.match(/filename="?([^";]+)"?/i);
  return encoded ? decodeURIComponent(encoded[1]) : plain?.[1] ?? fallback;
};

const download = (blob: Blob, fileName: string): void => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
};

const throwResponse = async (response: Response): Promise<never> => {
  const problem = await response.json().catch(() => null);
  throw new DesignerProjectApiError(
    problem?.detail || problem?.title || `Project operation failed (${response.status}).`,
    problem?.code,
    response.status,
  );
};

const parseProjectResponse = <T>(text: string, status: number): ProjectOpenResponse<T> => {
  let body: unknown;
  try {
    body = JSON.parse(text);
  } catch {
    throw new DesignerProjectApiError(
      status >= 200 && status < 300
        ? 'The server returned an invalid project response.'
        : `Project operation failed (${status}).`,
      status >= 200 && status < 300 ? 'PXAPROJECT009' : undefined,
      status,
    );
  }
  if (status < 200 || status >= 300) {
    const problem = body as { detail?: string; title?: string; code?: string } | null;
    throw new DesignerProjectApiError(
      problem?.detail || problem?.title || `Project operation failed (${status}).`,
      problem?.code,
      status,
    );
  }
  return body as ProjectOpenResponse<T>;
};

const openWithProgress = async <T>(
  url: string,
  form: FormData,
  signal: AbortSignal | undefined,
  onProgress: (progress: ProjectTransferProgress) => void,
): Promise<ProjectOpenResponse<T>> => {
  const cancelled = () => new DesignerProjectApiError('The project operation was cancelled.', 'PXAPROJECT_CANCELLED', 499);
  if (signal?.aborted) throw cancelled();
  const headers = await designerMutationHeaders();
  if (signal?.aborted) throw cancelled();
  return new Promise((resolve, reject) => {
  const request = new XMLHttpRequest();
  const abort = () => request.abort();
  const cleanup = () => signal?.removeEventListener('abort', abort);
  const fail = (error: unknown) => {
    cleanup();
    reject(error);
  };

  request.open('POST', url);
  request.withCredentials = true;
  request.setRequestHeader('Accept', 'application/json');
  for (const [name, value] of Object.entries(headers)) request.setRequestHeader(name, value);
  request.upload.addEventListener('progress', event => {
    const total = event.lengthComputable ? event.total : form.get('file') instanceof File
      ? (form.get('file') as File).size
      : 0;
    const loaded = event.loaded;
    const ratio = total > 0 ? Math.min(loaded / total, 1) : 0;
    onProgress({ phase: 'uploading', percent: Math.round(ratio * 85), loaded, total });
  });
  request.upload.addEventListener('load', () => {
    const file = form.get('file');
    const total = file instanceof File ? file.size : 0;
    onProgress({ phase: 'validating', percent: 90, loaded: total, total });
  });
  request.addEventListener('load', () => {
    try {
      const result = parseProjectResponse<T>(request.responseText, request.status);
      onProgress({ phase: 'validating', percent: 100, loaded: 1, total: 1 });
      cleanup();
      resolve(result);
    } catch (error) {
      fail(error);
    }
  });
  request.addEventListener('error', () => fail(new DesignerProjectApiError(
    'The project service could not be reached.', 'PXAPROJECT_NETWORK', 0,
  )));
  request.addEventListener('abort', () => fail(new DesignerProjectApiError(
    'The project operation was cancelled.', 'PXAPROJECT_CANCELLED', 499,
  )));
  if (signal?.aborted) {
    fail(new DesignerProjectApiError(
      'The project operation was cancelled.', 'PXAPROJECT_CANCELLED', 499,
    ));
    return;
  }
  signal?.addEventListener('abort', abort, { once: true });
  onProgress({ phase: 'uploading', percent: 0, loaded: 0, total: 0 });
  request.send(form);
});
};

const save = async (
  kind: 'pdf' | 'spreadsheet',
  document: PdfProjectDocument | Workbook,
  name: string,
  codeWorkspace?: ProjectCodeWorkspace,
): Promise<void> => {
  const includeWorkspace = kind === 'pdf' && codeWorkspace !== undefined;
  const response = await fetch(`${base}/${kind}/${includeWorkspace ? 'export-with-workspace' : 'export'}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', Accept: kind === 'pdf' ? 'application/vnd.pvad.pvadp+zip' : 'application/vnd.pvad.pvads+zip' },
    body: JSON.stringify(includeWorkspace ? { document, codeWorkspace } : document),
  });
  if (!response.ok) return throwResponse(response);
  const blob = await response.blob();
  const expected = kind === 'pdf' ? 'application/vnd.pvad.pvadp+zip' : 'application/vnd.pvad.pvads+zip';
  if ((response.headers.get('Content-Type') ?? blob.type).split(';')[0] !== expected)
    throw new DesignerProjectApiError('The server returned an unexpected project format.', 'PXAPROJECT002');
  const extension = kind === 'pdf' ? '.pvadp' : '.pvads';
  download(blob, responseFileName(response, `${safeFileStem(name, kind === 'pdf' ? 'document' : 'workbook')}${extension}`));
};

const open = async <T>(
  kind: 'pdf' | 'spreadsheet',
  file: File,
  reservedIds: readonly string[] = [],
  options: ProjectOpenOptions = {},
): Promise<ProjectOpenResponse<T>> => {
  const form = new FormData();
  form.append('file', file);
  if (reservedIds.length) form.append('reservedIds', JSON.stringify(reservedIds));
  const url = `${base}/${kind}/open`;
  if (options.onProgress)
    return openWithProgress<T>(url, form, options.signal, options.onProgress);
  const response = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json' },
    body: form,
    signal: options.signal,
  });
  if (!response.ok) return throwResponse(response);
  return response.json() as Promise<ProjectOpenResponse<T>>;
};

const extensionData = (
  value: Record<string, unknown>,
  knownProperties: ReadonlySet<string>,
): Record<string, unknown> | undefined => {
  const entries = Object.entries(value).filter(([key]) => !knownProperties.has(key));
  return entries.length ? Object.fromEntries(entries) : undefined;
};

const PDF_DOCUMENT_PROPERTIES = new Set([
  'id', 'name', 'category', 'description', 'pages', 'sharedElements', 'pageSettings',
]);
const PDF_PAGE_PROPERTIES = new Set(['id', 'elements', 'layout']);
const PDF_PAGE_SETTINGS_PROPERTIES = new Set([
  'width', 'height', 'orientation', 'unit', 'backgroundColor', 'backgroundImage',
  'backgroundImageFit', 'margins', 'bleedSize', 'pageNumbering', 'globalWatermark',
  'metadata', 'namedStyles', 'protection', 'encryption', 'complianceProfile',
  'accessibilityProfile', 'customProperties', 'trackChanges', 'systemLanguage',
  'activeLanguages', 'localizedProperties', 'targetLanguage',
]);

const capturePdfExtensionData = (document: PdfProjectDocument): PdfProjectDocument => {
  const documentExtensions = extensionData(
    document as unknown as Record<string, unknown>,
    PDF_DOCUMENT_PROPERTIES,
  );
  return {
    ...document,
    ...(documentExtensions ? { extensionData: documentExtensions } : {}),
    pages: document.pages.map(page => {
      const pageExtensions = extensionData(
        page as unknown as Record<string, unknown>,
        PDF_PAGE_PROPERTIES,
      );
      return { ...page, ...(pageExtensions ? { extensionData: pageExtensions } : {}) };
    }),
    ...(document.pageSettings
      ? { pageSettings: (() => {
          const settingsExtensions = extensionData(
            document.pageSettings as unknown as Record<string, unknown>,
            PDF_PAGE_SETTINGS_PROPERTIES,
          );
          return {
            ...document.pageSettings,
            ...(settingsExtensions ? { extensionData: settingsExtensions } : {}),
          };
        })() }
      : {}),
  };
};

const requireUpgradeReview = <T>(result: ProjectOpenResponse<T>): ProjectOpenResponse<T> => {
  if (!result.upgradeReport) return result;
  const existing = result.compatibility ?? { requiresReview: false, missingFeatures: [], diagnostics: [] };
  const diagnostics = [...existing.diagnostics];
  for (const diagnostic of result.upgradeReport.diagnostics) {
    if (!diagnostics.some(item => item.code === diagnostic.code && item.source === diagnostic.source))
      diagnostics.push(diagnostic);
  }
  return {
    ...result,
    compatibility: { ...existing, requiresReview: true, diagnostics },
  };
};

export const savePdfProject = (document: PdfProjectDocument, codeWorkspace?: ProjectCodeWorkspace) =>
  save('pdf', document, document.name, codeWorkspace);
export const openPdfProject = async (
  file: File,
  reservedIds: readonly string[] = [],
  options: ProjectOpenOptions = {},
) => {
  const result = await open<PdfProjectDocument>('pdf', file, reservedIds, options);
  return requireUpgradeReview({ ...result, document: capturePdfExtensionData(result.document) });
};
export const saveSpreadsheetProject = (workbook: Workbook) => save('spreadsheet', workbook, workbook.name);
export const openSpreadsheetProject = (
  file: File,
  reservedIds: readonly string[] = [],
  options: ProjectOpenOptions = {},
) => open<Workbook>('spreadsheet', file, reservedIds, options).then(requireUpgradeReview);

export const projectCompatibilitySummary = (compatibility?: ProjectCompatibility): string | undefined => {
  if (!compatibility?.requiresReview) return undefined;
  const parts = [
    ...compatibility.missingFeatures.map(feature => `Feature: ${feature}`),
    ...compatibility.diagnostics
      .filter(diagnostic => diagnostic.severity !== 'info')
      .map(diagnostic => `${diagnostic.code}: ${diagnostic.message}`),
  ];
  return parts.join(' | ') || undefined;
};

export const preparePdfProjectAssets = async (
  document: PdfProjectDocument,
): Promise<PreparedProjectAssets<PdfProjectDocument>> => {
  const copy = structuredClone(document);
  const uploadedAssetIds: string[] = [];
  let settled = false;
  const elements = [
    ...copy.pages.flatMap(page => page.elements),
    ...(copy.sharedElements ?? []),
  ];
  try {
    for (const element of elements) {
      if (element.type !== 'image' || !element.content?.startsWith('data:')) continue;
      const blob = await (await fetch(element.content)).blob();
      if (blob.type !== 'image/png' && blob.type !== 'image/jpeg') {
        throw new DesignerProjectApiError(
          `Project image '${element.id}' uses unsupported media type '${blob.type || 'unknown'}'.`,
          'PXAPROJECT010',
        );
      }
      const extension = blob.type === 'image/jpeg' ? 'jpg' : 'png';
      const asset = await uploadDesignerImage(new File(
        [blob],
        `project-image-${element.id}.${extension}`,
        { type: blob.type },
      ));
      uploadedAssetIds.push(asset.id);
      element.assetId = asset.id;
      element.content = undefined;
    }
    return {
      document: copy,
      commit: () => { settled = true; },
      rollback: async () => {
        if (settled) return;
        settled = true;
        await Promise.allSettled(uploadedAssetIds.map(assetId => deleteDesignerAsset(assetId)));
      },
    };
  } catch (error) {
    await Promise.allSettled(uploadedAssetIds.map(assetId => deleteDesignerAsset(assetId)));
    throw error;
  }
};

export const storePdfProjectAssets = async (document: PdfProjectDocument): Promise<PdfProjectDocument> => {
  const prepared = await preparePdfProjectAssets(document);
  prepared.commit();
  return prepared.document;
};
