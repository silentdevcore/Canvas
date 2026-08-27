export type PdfTrustStatus = 0 | 1 | 2 | 3 | 4;

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
  }>;
  pdfA: {
    standard: string;
    profile?: string | null;
    isClaimed: boolean;
    status: PdfTrustStatus;
    evidence: string;
  };
  pdfUa: {
    standard: string;
    profile?: string | null;
    isClaimed: boolean;
    status: PdfTrustStatus;
    evidence: string;
  };
  diagnostics: PdfTrustDiagnostic[];
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
