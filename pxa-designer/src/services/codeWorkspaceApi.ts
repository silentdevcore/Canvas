export type CodeLanguage = 'json' | 'csharpModel' | 'csharpPdf' | 'csharpBase64';
export type DraftStatus = 'Saved' | 'Modified' | 'Generated' | 'Outdated' | 'Invalid' | 'Conflict';

export interface CodeDiagnostic {
  code: string;
  severity: 'info' | 'warning' | 'error';
  message: string;
  line?: number;
  column?: number;
  elementId?: string;
}

export interface SourceMapEntry {
  elementId: string;
  language: CodeLanguage;
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
}

export interface CodeConversionResult {
  sourceLanguage: CodeLanguage;
  targetLanguage: CodeLanguage;
  fidelity: 'exact' | 'compatible' | 'reviewRequired' | 'unsupported';
  documentFidelity: 'exact' | 'compatible' | 'reviewRequired' | 'unsupported';
  sourcePreservation: 'preserved' | 'regenerated' | 'structureLost';
  generatedSource: string;
  canonicalDesign: any | null;
  diagnostics: CodeDiagnostic[];
  sourceMap: SourceMapEntry[];
  sourceChecksum: string;
  resultChecksum: string;
  canonicalChecksum: string;
}

export interface CodeWorkspace {
  id: string;
  templateId: string;
  revision: number;
  baseTemplateRevision: number;
  persisted: boolean;
  json: { source: string; checksum: string };
  cSharpModel: { source: string; checksum: string };
  cSharpPdf: { source: string; checksum: string };
  cSharpBase64: { source: string; checksum: string };
  canonicalDesign: any;
  sourceMap: SourceMapEntry[];
  canonicalChecksum: string;
  updatedAt: string;
}

const base = (templateId: string) => `/api/pxa/v1/designer/templates/${encodeURIComponent(templateId)}/code-workspace`;

async function request<T>(templateId: string, path = '', init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${base(templateId)}${path}`, {
    credentials: 'include',
    ...init,
    headers: { Accept: 'application/json', 'X-PXA-Application': 'designer', ...init.headers },
  });
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    const error = new Error(body?.detail || body?.title || `Code workspace request failed (${response.status}).`) as Error & { status?: number; body?: any };
    error.status = response.status;
    error.body = body;
    throw error;
  }
  return body as T;
}

export const getCodeWorkspace = (templateId: string, signal?: AbortSignal) =>
  request<CodeWorkspace>(templateId, '', { signal });

export const saveCodeDraft = (templateId: string, revision: number, language: CodeLanguage, source: string) =>
  request<CodeWorkspace>(templateId, '', {
    method: 'PUT', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ revision, language, source }),
  });

export const validateCodeDraft = (templateId: string, language: CodeLanguage, source: string) =>
  request<any>(templateId, '/validate', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ language, source }),
  });

export const convertCodeDraft = (templateId: string, sourceLanguage: CodeLanguage, targetLanguage: CodeLanguage, source: string) =>
  request<CodeConversionResult>(templateId, '/convert', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ sourceLanguage, targetLanguage, source }),
  });

export interface ExecuteResult {
  success: boolean;
  canonicalDesign: any | null;
  pdfBytes?: string;
  diagnostics: CodeDiagnostic[];
  sourceMap: SourceMapEntry[];
  fidelity: CodeConversionResult['fidelity'];
}

export const executeCodeDraft = (templateId: string, language: CodeLanguage, source: string) =>
  request<ExecuteResult>(templateId, '/execute', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ language, source }),
  });

export const applyCodeDraft = (templateId: string, workspaceRevision: number, templateRevision: number, language: CodeLanguage, source: string) =>
  request<{ templateRevision: number; workspaceRevision: number; conversion: CodeConversionResult }>(templateId, '/apply', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ workspaceRevision, templateRevision, language, source }),
  });
