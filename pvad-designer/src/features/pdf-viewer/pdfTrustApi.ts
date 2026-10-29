export type PdfTrustStatus = 0 | 1 | 2 | 3 | 4;

export type PdfTrustFailureKind =
  | 'offline'
  | 'timeout'
  | 'cancelled'
  | 'policyDenied'
  | 'unsupported'
  | 'failed';

export class PdfTrustOperationError extends Error {
  constructor(
    public readonly kind: PdfTrustFailureKind,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = 'PdfTrustOperationError';
  }
}

export interface PdfRevocationEvidenceInspection {
  kind: 0 | 1;
  status: PdfTrustStatus;
  source: string;
  thisUpdate: string;
  nextUpdate?: string | null;
}

export interface PdfTrustDiagnostic {
  code: string;
  canonicalCode?: string | null;
  severity: 0 | 1 | 2;
  message: string;
  objectNumber?: number | null;
  generation?: number | null;
  pageNumber?: number | null;
  elementId?: string | null;
  localizationKey?: string | null;
  localizationParameters?: Record<string, string> | null;
}

export interface PdfTrustReport {
  status: PdfTrustStatus;
  structureStatus: PdfTrustStatus;
  sha256: string;
  fileSizeBytes: number;
  pdfVersion?: string | null;
  revisionCount: number;
  objectCount: number;
  encryption: {
    isEncrypted: boolean;
    status: PdfTrustStatus;
    filter?: string | null;
    algorithm?: string | null;
    version?: number | null;
    revision?: number | null;
    keyLengthBits?: number | null;
  };
  signatures: Array<{
    objectNumber: number;
    generation: number;
    status: PdfTrustStatus;
    subFilter?: string | null;
    byteRange: number[];
    contentsLengthBytes?: number | null;
    integrityStatus: PdfTrustStatus;
    coverageStatus: PdfTrustStatus;
    certificateStatus: PdfTrustStatus;
    revocationStatus: PdfTrustStatus;
    revocationEvidence?: PdfRevocationEvidenceInspection[] | null;
    padesProfile?: string | null;
    digestAlgorithm?: string | null;
    signerSubject?: string | null;
    signerThumbprint?: string | null;
    signingTime?: string | null;
    timestampStatus: PdfTrustStatus;
    timestampRevocationStatus: PdfTrustStatus;
    timestampRevocationEvidence?: PdfRevocationEvidenceInspection[] | null;
    timestampTime?: string | null;
    timestampAuthoritySubject?: string | null;
    coversWholeDocument: boolean;
    signedRevisionEndOffset?: number | null;
    embeddedValidationMaterial?: {
      vriMatched: boolean;
      certificateCount: number;
      crlCount: number;
      ocspResponseCount: number;
      status: PdfTrustStatus;
    } | null;
    historicalRevocationStatus: PdfTrustStatus;
    timestampHistoricalRevocationStatus: PdfTrustStatus;
    isDocumentTimestamp: boolean;
    archiveTimestampStatus: PdfTrustStatus;
    archiveTimestampTime?: string | null;
    archiveTimestampAuthoritySubject?: string | null;
    isCertification?: boolean;
    certificationStatus?: PdfTrustStatus;
    certification?: {
      level: number;
      status: PdfTrustStatus;
      fieldLockStatus: PdfTrustStatus;
      disallowedChangeCount: number;
      disallowedChanges: string[];
      lockedFields: string[];
    } | null;
    fieldName?: string | null;
    fieldPageNumber?: number | null;
    fieldRectangle?: number[] | null;
  }>;
  pdfA: {
    standard: string;
    profile?: string | null;
    isClaimed: boolean;
    status: PdfTrustStatus;
    evidence: string;
    validator?: string | null;
    validatedAtUtc?: string | null;
  };
  pdfUa: {
    standard: string;
    profile?: string | null;
    isClaimed: boolean;
    status: PdfTrustStatus;
    evidence: string;
  };
  revisions: Array<{
    sequenceNumber: number;
    kind: string;
    crossReferenceOffset: number;
    endOffset: number;
    sha256: string;
    entryCount: number;
  }>;
  embeddedValidationMaterial?: {
    certificateCount: number;
    crlCount: number;
    ocspResponseCount: number;
    vriEntryCount: number;
    totalBytes: number;
    status: PdfTrustStatus;
  } | null;
  risk?: {
    javaScriptActionCount: number;
    launchActionCount: number;
    embeddedFileCount: number;
    fileAttachmentCount: number;
    richMediaCount: number;
    xfaFormCount: number;
    externalLinkCount: number;
    status: PdfTrustStatus;
    hasRiskyContent: boolean;
  } | null;
  diagnostics: PdfTrustDiagnostic[];
}

export type PdfComplianceConversionCapability = 0 | 1 | 2;

export interface PdfA2bConversionPreflightReport {
  targetProfile: string;
  capability: PdfComplianceConversionCapability;
  sourceSha256: string;
  fileSizeBytes: number;
  objectCount: number;
  signatureCount: number;
  invalidatesSignatures: boolean;
  originalPreserved: boolean;
  findings: Array<{
    code: string;
    canonicalCode?: string | null;
    severity: 0 | 1 | 2;
    message: string;
    objectNumber?: number | null;
    generation?: number | null;
    pageNumber?: number | null;
    elementId?: string | null;
    localizationKey?: string | null;
    localizationParameters?: Record<string, string> | null;
  }>;
}

interface PvadJobAcceptedResponse {
  jobId: string;
  status: string;
  statusUrl: string;
}

interface PvadJobResponse {
  id: string;
  status: string;
  downloadUrl?: string | null;
  failureReason?: string | null;
}

async function readProblem(response: Response, fallback: string): Promise<never> {
  const problem = await response.json().catch(() => ({}));
  const message = problem.detail || problem.title || `${fallback} (${response.status}).`;
  const kind: PdfTrustFailureKind = response.status === 401 || response.status === 403
    ? 'policyDenied'
    : response.status === 408 || response.status === 504
      ? 'timeout'
      : response.status === 415 || response.status === 422 || response.status === 501
        ? 'unsupported'
        : response.status === 499
          ? 'cancelled'
          : 'failed';
  throw new PdfTrustOperationError(kind, message, response.status);
}

function normalizeFetchError(error: unknown, timedOut: boolean): never {
  if (error instanceof PdfTrustOperationError) {
    throw error;
  }
  if (error instanceof DOMException && error.name === 'AbortError') {
    throw new PdfTrustOperationError(timedOut ? 'timeout' : 'cancelled', error.message);
  }
  const offline = typeof navigator !== 'undefined' && navigator.onLine === false;
  throw new PdfTrustOperationError(
    offline ? 'offline' : 'failed',
    error instanceof Error ? error.message : 'The PDF Trust request failed.',
  );
}

async function trustFetch(
  input: RequestInfo | URL,
  init: RequestInit,
  fallback: string,
  signal?: AbortSignal,
): Promise<Response> {
  const controller = new AbortController();
  let timedOut = false;
  const timeout = window.setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, 30_000);
  const abort = () => controller.abort();
  if (signal?.aborted) {
    controller.abort();
  } else {
    signal?.addEventListener('abort', abort, { once: true });
  }
  try {
    const response = await fetch(input, { ...init, signal: controller.signal });
    if (!response.ok) {
      return await readProblem(response, fallback);
    }
    return response;
  } catch (error) {
    return normalizeFetchError(error, timedOut);
  } finally {
    window.clearTimeout(timeout);
    signal?.removeEventListener('abort', abort);
  }
}

export async function inspectPdfTrust(file: File, signal?: AbortSignal): Promise<PdfTrustReport> {
  const form = new FormData();
  form.append('file', file);
  const response = await trustFetch('/api/pvad/v1/pdf/trust/inspect/jobs', {
    method: 'POST',
    body: form,
  }, 'PDF Trust inspection failed', signal);
  const accepted = await response.json() as PvadJobAcceptedResponse;
  const deadline = Date.now() + 60_000;

  try {
    while (true) {
      if (signal?.aborted) {
        throw new PdfTrustOperationError('cancelled', 'The PDF Trust inspection was cancelled.');
      }
      if (Date.now() >= deadline) {
        throw new PdfTrustOperationError('timeout', 'The PDF Trust inspection timed out.');
      }
      const statusResponse = await trustFetch(accepted.statusUrl, {
        method: 'GET',
      }, 'PDF Trust inspection status failed', signal);
      const job = await statusResponse.json() as PvadJobResponse;
      const status = job.status.toLowerCase();
      if (status === 'completed' && job.downloadUrl) {
        const result = await trustFetch(job.downloadUrl, {
          method: 'GET',
        }, 'PDF Trust inspection result failed', signal);
        return result.json() as Promise<PdfTrustReport>;
      }
      if (status === 'completed') {
        throw new PdfTrustOperationError('failed', 'The PDF Trust inspection result is unavailable.');
      }
      if (status === 'failed' || status === 'deadletter' || status === 'expired') {
        throw new PdfTrustOperationError(
          'failed',
          job.failureReason || 'The PDF Trust inspection did not complete.',
        );
      }
      if (status === 'cancelled') {
        throw new PdfTrustOperationError('cancelled', 'The PDF Trust inspection was cancelled.');
      }
      await new Promise<void>((resolve, reject) => {
        const onAbort = () => {
          window.clearTimeout(timeout);
          reject(new PdfTrustOperationError('cancelled', 'The PDF Trust inspection was cancelled.'));
        };
        const timeout = window.setTimeout(() => {
          signal?.removeEventListener('abort', onAbort);
          resolve();
        }, 300);
        signal?.addEventListener('abort', onAbort, { once: true });
      });
    }
  } catch (error) {
    if (signal?.aborted || error instanceof PdfTrustOperationError &&
      (error.kind === 'cancelled' || error.kind === 'timeout')) {
      void fetch(`/api/pvad/v1/jobs/${accepted.jobId}/cancel`, { method: 'POST' }).catch(() => undefined);
    }
    throw error;
  }
}

export type PdfArchiveProfile = 'PDF/A-2b' | 'PDF/A-3b';
export type PdfPadesProfile = 'BaselineB' | 'BaselineT' | 'BaselineLT' | 'BaselineLTA';

export interface PdfSignRequest {
  pdf: File;
  certificate: File;
  certificatePassword: string;
  profile: PdfPadesProfile;
  fieldName?: string;
  reason?: string;
  visible?: boolean;
  pageNumber?: number;
}

export async function signPdf(request: PdfSignRequest): Promise<Blob> {
  const form = new FormData();
  form.append('pdf', request.pdf);
  form.append('certificate', request.certificate);
  form.append('certificatePassword', request.certificatePassword);
  form.append('profile', request.profile);
  form.append('fieldName', request.fieldName || 'Signature1');
  if (request.reason) form.append('reason', request.reason);
  form.append('visible', String(Boolean(request.visible)));
  if (request.visible) form.append('pageNumber', String(request.pageNumber || 1));

  const response = await trustFetch(
    '/api/pvad/v1/pdf/signatures/sign',
    { method: 'POST', body: form },
    'PDF signing failed',
  );
  const blob = await response.blob();
  if (blob.type && !blob.type.includes('pdf')) {
    throw new PdfTrustOperationError('failed', 'PDF signing returned an unexpected file type.');
  }
  return blob;
}

export async function preflightPdfA2b(
  file: File,
  profile: PdfArchiveProfile = 'PDF/A-2b',
): Promise<PdfA2bConversionPreflightReport> {
  const form = new FormData();
  form.append('file', file);
  form.append('profile', profile);
  const response = await trustFetch(
    '/api/pvad/v1/pdf/compliance/preflight',
    { method: 'POST', body: form },
    'PDF/A preflight failed',
  );
  return response.json() as Promise<PdfA2bConversionPreflightReport>;
}

export async function convertPdfA2b(
  file: File,
  allowSignatureInvalidation: boolean,
  profile: PdfArchiveProfile = 'PDF/A-2b',
): Promise<Blob> {
  const form = new FormData();
  form.append('file', file);
  form.append('allowSignatureInvalidation', String(allowSignatureInvalidation));
  form.append('profile', profile);
  const response = await trustFetch(
    '/api/pvad/v1/pdf/compliance/convert',
    { method: 'POST', body: form },
    'PDF/A conversion failed',
  );
  const blob = await response.blob();
  if (blob.type && !blob.type.includes('pdf')) {
    throw new Error('PDF/A conversion returned an unexpected file type.');
  }
  return blob;
}
