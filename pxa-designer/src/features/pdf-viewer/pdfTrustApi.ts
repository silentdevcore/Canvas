export type PdfTrustStatus = 0 | 1 | 2 | 3 | 4;

export interface PdfRevocationEvidenceInspection {
  kind: 0 | 1;
  status: PdfTrustStatus;
  source: string;
  thisUpdate: string;
  nextUpdate?: string | null;
}

export interface PdfTrustDiagnostic {
  code: string;
  severity: 0 | 1 | 2;
  message: string;
  objectNumber?: number | null;
  generation?: number | null;
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
    severity: 0 | 1 | 2;
    message: string;
    objectNumber?: number | null;
    generation?: number | null;
  }>;
}

export async function inspectPdfTrust(file: File): Promise<PdfTrustReport> {
  const form = new FormData();
  form.append('file', file);
  const response = await fetch('/api/pxa/v1/pdf/trust/inspect', {
    method: 'POST',
    body: form,
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.title || `PDF Trust inspection failed (${response.status}).`);
  }

  return response.json() as Promise<PdfTrustReport>;
}

export type PdfArchiveProfile = 'PDF/A-2b' | 'PDF/A-3b';

export async function preflightPdfA2b(
  file: File,
  profile: PdfArchiveProfile = 'PDF/A-2b',
): Promise<PdfA2bConversionPreflightReport> {
  const form = new FormData();
  form.append('file', file);
  form.append('profile', profile);
  const response = await fetch('/api/pxa/v1/pdf/compliance/preflight', { method: 'POST', body: form });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.title || `PDF/A preflight failed (${response.status}).`);
  }
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
  const response = await fetch('/api/pxa/v1/pdf/compliance/convert', { method: 'POST', body: form });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.title || `PDF/A conversion failed (${response.status}).`);
  }
  const blob = await response.blob();
  if (blob.type && !blob.type.includes('pdf')) {
    throw new Error('PDF/A conversion returned an unexpected file type.');
  }
  return blob;
}
