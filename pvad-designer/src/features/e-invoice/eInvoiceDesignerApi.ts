import { readPvadDocumentHeader } from '../../utils/pvadDocumentHeaders';

/**
 * Client for the electronic-invoice API.
 *
 * It sends the Designer's typed invoice unchanged, so the XML, the PDF and a later Viewer
 * inspection all derive from the same values the Designer showed. Nothing here re-computes an
 * invoice amount.
 */

import {
  attachmentFileName,
  toGenerationRequest,
  type EInvoiceDocument,
  type EInvoiceSettings,
} from './invoiceModel';

const BASE = '/api/pvad/v1/pdf/e-invoices';

export interface EInvoiceValidationReport {
  isStructurallyValid: boolean;
  isConformanceValidated: boolean;
  syntax?: string | null;
  guidelineIdentifier?: string | null;
  diagnostics: Array<{ code: string; canonicalCode?: string | null; severity: string; message: string }>;
  /**
   * The backend's own vocabulary: `valid` only ever comes from a pinned official artifact set,
   * `unavailable` means an artifact set was selected but could not be verified, and
   * `notEvaluated` means none was configured. Structural recognition never produces any of them.
   */
  conformanceStatus: 'valid' | 'invalid' | 'unavailable' | 'notEvaluated';
  validatorArtifactSetId?: string | null;
  validatorArtifactSetSha256?: string | null;
}

export interface EInvoiceValidationEvidence {
  id: string;
  operation: string;
  documentSha256: string;
  containerSha256?: string | null;
  standard: string;
  standardVersion: string;
  profile: string;
  isStructurallyValid: boolean;
  isConformanceValidated: boolean;
  conformanceStatus: string;
  validatorArtifactSetId?: string | null;
  validatorArtifactSetSha256?: string | null;
  revalidatesRecordId?: string | null;
  createdAt: string;
  retainUntil: string;
}

/** A blocked or rejected operation, carrying the backend's stable Problem Details code. */
export class EInvoiceApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code?: string,
    readonly problem?: Record<string, unknown>,
  ) {
    super(message);
    this.name = 'EInvoiceApiError';
  }
}

async function fail(response: Response, fallback: string): Promise<never> {
  const problem = (await response.json().catch(() => ({}))) as Record<string, unknown>;
  throw new EInvoiceApiError(
    (problem.detail as string) || (problem.title as string) || `${fallback} (${response.status}).`,
    response.status,
    problem.code as string | undefined,
    problem,
  );
}

/** Generates the invoice XML for the current Designer state. */
export async function generateInvoiceXml(
  invoice: EInvoiceDocument,
  settings: EInvoiceSettings,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${BASE}/generate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(toGenerationRequest(invoice, settings)),
    signal,
  });
  if (!response.ok) return fail(response, 'Invoice XML generation failed');
  return response.blob();
}

/** Creates the visible PDF/A-3 hybrid invoice. */
export async function createHybridInvoicePdf(
  invoice: EInvoiceDocument,
  settings: EInvoiceSettings,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${BASE}/create`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(toGenerationRequest(invoice, settings)),
    signal,
  });
  if (!response.ok) return fail(response, 'Hybrid invoice creation failed');
  return response.blob();
}

export async function validateInvoiceXml(
  xml: Blob,
  settings: EInvoiceSettings,
  signal?: AbortSignal,
): Promise<EInvoiceValidationReport> {
  const form = new FormData();
  form.append('file', xml, attachmentFileName(settings.standard));
  form.append('standard', settings.standard);
  form.append('version', settings.version);
  form.append('profile', settings.profile);
  const response = await fetch(`${BASE}/validate`, { method: 'POST', body: form, signal });
  if (!response.ok) return fail(response, 'Invoice validation failed');
  return response.json();
}

export interface AttachApprovals {
  /** Approves rewriting a source whose archival identification forbids embedded files. */
  allowArchiveConversion?: boolean;
  allowSignatureInvalidation?: boolean;
  replaceExistingInvoice?: boolean;
  /** Approves an invoice whose values were not found in the visible page. */
  allowInconsistentPdf?: boolean;
}

export interface AttachmentPreflight {
  attachment: {
    targetProfile: string;
    capability: string;
    strategy: string;
    pageCount: number;
    signatureCount: number;
    sourceArchiveProfile?: string | null;
    requiresArchiveConversion: boolean;
    requiresApproval: boolean;
    invalidatesSignatures: boolean;
    originalPreserved: boolean;
    conflictingAttachmentNames: string[];
    estimatedOutputSizeBytes: number;
    findings: Array<{ code: string; severity: string; message: string }>;
  };
  invoice: EInvoiceValidationReport;
  consistency: { status: string; findings: Array<{ field: string; status: string; message: string }> };
  mode: string;
  attachmentFileName: string;
  isEditable: boolean;
}

export async function preflightInvoiceAttachment(
  pdf: File,
  xml: Blob,
  settings: EInvoiceSettings,
  approvals: AttachApprovals = {},
  signal?: AbortSignal,
): Promise<AttachmentPreflight> {
  const response = await fetch(`${BASE}/attach/preflight`, {
    method: 'POST',
    body: attachmentForm(pdf, xml, settings, approvals),
    signal,
  });
  if (!response.ok) return fail(response, 'Attachment preflight failed');
  return response.json();
}

export interface AttachmentResult {
  pdf: Blob;
  /** Evidence read back from the response headers. */
  sourceSha256?: string;
  resultSha256?: string;
  invoiceSha256?: string;
  strategy?: string;
  pagesUnchanged: boolean;
  complianceProfile?: string;
  consistency?: string;
  conformance?: string;
  validationRecordId?: string;
}

export async function attachInvoiceToPdf(
  pdf: File,
  xml: Blob,
  settings: EInvoiceSettings,
  approvals: AttachApprovals = {},
  signal?: AbortSignal,
): Promise<AttachmentResult> {
  const response = await fetch(`${BASE}/attach`, {
    method: 'POST',
    body: attachmentForm(pdf, xml, settings, approvals),
    signal,
  });
  if (!response.ok) return fail(response, 'Invoice attachment failed');
  return {
    pdf: await response.blob(),
    sourceSha256: header(response, 'Source-SHA256'),
    resultSha256: header(response, 'Result-SHA256'),
    invoiceSha256: header(response, 'Invoice-SHA256'),
    strategy: header(response, 'Attachment-Strategy'),
    pagesUnchanged: header(response, 'Pages-Unchanged') === 'true',
    complianceProfile: header(response, 'Compliance-Profile'),
    consistency: header(response, 'Invoice-Consistency'),
    conformance: header(response, 'Invoice-Conformance'),
    validationRecordId: header(response, 'Validation-Record'),
  };
}

/** Retained validation verdicts for this tenant, newest first. */
export async function listValidationEvidence(
  documentSha256?: string,
  limit?: number,
  signal?: AbortSignal,
): Promise<EInvoiceValidationEvidence[]> {
  const query = new URLSearchParams();
  if (documentSha256) query.set('documentSha256', documentSha256);
  if (limit) query.set('limit', String(limit));
  const suffix = query.size > 0 ? `?${query}` : '';
  const response = await fetch(`${BASE}/validations${suffix}`, { signal });
  if (!response.ok) return fail(response, 'Reading validation history failed');
  return response.json();
}

function attachmentForm(
  pdf: File,
  xml: Blob,
  settings: EInvoiceSettings,
  approvals: AttachApprovals,
): FormData {
  const form = new FormData();
  form.append('file', pdf);
  form.append('invoice', xml, attachmentFileName(settings.standard));
  form.append('standard', settings.standard);
  form.append('version', settings.version);
  form.append('profile', settings.profile);
  form.append('allowArchiveConversion', String(approvals.allowArchiveConversion ?? false));
  form.append('allowSignatureInvalidation', String(approvals.allowSignatureInvalidation ?? false));
  form.append('replaceExistingInvoice', String(approvals.replaceExistingInvoice ?? false));
  form.append('allowInconsistentPdf', String(approvals.allowInconsistentPdf ?? false));
  return form;
}

function header(response: Response, name: string): string | undefined {
  return readPvadDocumentHeader(response.headers, name) ?? undefined;
}
