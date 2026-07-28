const templateBase = '/api/pxa/v1/designer/templates';

export interface PersistedDesignDocument {
  template: Record<string, unknown>;
  pageSettings: PageSettings;
  jsonData: Record<string, unknown>;
  documentMode: 'pdf' | 'word';
  currentPageIndex: number;
}

export interface DesignerTemplateDocument {
  id: string;
  name: string;
  description: string | null;
  tags: string[];
  status: string;
  revision: number;
  designDocument: PersistedDesignDocument;
  checksum: string;
  schemaVersion: string;
  designerVersion: string;
  publishedVersionId: string | null;
  updatedAt: string;
}

export interface DesignerTemplateSummary {
  id: string;
  name: string;
  description: string | null;
  tags: string[];
  status: string;
  revision: number;
  publishedVersionId: string | null;
  updatedAt: string;
}

export interface DesignerTemplatePage {
  items: DesignerTemplateSummary[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DesignerTemplateVersion {
  id: string;
  versionNumber: number;
  label: string | null;
  checksum: string;
  schemaVersion: string;
  designerVersion: string;
  createdByUserId: string;
  createdAt: string;
}

export interface CreateDesignerTemplateVersionResult {
  created: boolean;
  version: DesignerTemplateVersion;
}

export class DesignerTemplateApiError extends Error {
  status = 0;
  code?: string;
  currentRevision?: number;
  updatedBy?: string;
  updatedAt?: string;
  offline = false;
  cause?: unknown;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${templateBase}${path}`, {
      credentials: 'include',
      ...init,
      headers: { Accept: 'application/json', ...init.headers },
    });
  } catch (cause) {
    const error = new DesignerTemplateApiError('The template service is offline.');
    error.offline = true;
    error.cause = cause;
    throw error;
  }
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const error = new DesignerTemplateApiError(
      body?.detail || body?.title || 'The template operation failed.',
    );
    error.status = response.status;
    error.code = body?.code;
    error.currentRevision = body?.currentRevision;
    error.updatedBy = body?.updatedBy;
    error.updatedAt = body?.updatedAt;
    throw error;
  }
  return body as T;
}

export const listDesignerTemplates = (
  search = '',
  archived = false,
  signal?: AbortSignal,
): Promise<DesignerTemplatePage> => {
  const query = new URLSearchParams({ page: '1', pageSize: '100', archived: String(archived) });
  if (search.trim()) query.set('search', search.trim());
  return request(`?${query}`, { signal });
};

export const getDesignerTemplate = (
  id: string,
  signal?: AbortSignal,
): Promise<DesignerTemplateDocument> =>
  request<DesignerTemplateDocument>(`/${encodeURIComponent(id)}`, { signal });

export const createDesignerTemplate = (
  name: string,
  description: string,
  designDocument: PersistedDesignDocument,
): Promise<DesignerTemplateDocument> =>
  request<DesignerTemplateDocument>('', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name,
      description,
      tags: [],
      designDocument,
      schemaVersion: '1.0',
      designerVersion,
    }),
  });

export const updateDesignerTemplateDraft = (
  id: string,
  revision: number,
  designDocument: PersistedDesignDocument,
): Promise<DesignerTemplateDocument> =>
  request<DesignerTemplateDocument>(`/${encodeURIComponent(id)}/draft`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', 'If-Match': `"${revision}"` },
    body: JSON.stringify({
      revision,
      designDocument,
      schemaVersion: '1.0',
      designerVersion,
    }),
  });

export const createDesignerTemplateVersion = (
  id: string,
  revision: number,
  label?: string,
): Promise<CreateDesignerTemplateVersionResult> =>
  request<CreateDesignerTemplateVersionResult>(`/${encodeURIComponent(id)}/versions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ revision, label: label || null }),
  });

export const publishDesignerTemplate = (
  id: string,
  revision: number,
): Promise<DesignerTemplateDocument> =>
  request<DesignerTemplateDocument>(`/${encodeURIComponent(id)}/publish`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ revision }),
  });

export const archiveDesignerTemplate = (
  id: string,
  revision: number,
): Promise<DesignerTemplateDocument> =>
  request<DesignerTemplateDocument>(`/${encodeURIComponent(id)}/archive`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ revision }),
  });
import type { PageSettings } from '@/types';
import { designerVersion } from '@/product/productMetadata';
