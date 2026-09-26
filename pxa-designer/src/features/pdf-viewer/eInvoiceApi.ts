export interface EInvoiceDiagnostic {
  code: string;
  severity: string;
  message: string;
}

export interface EInvoiceSemanticData {
  invoiceNumber?: string | null;
  issueDate?: string | null;
  currency?: string | null;
  seller: { name?: string | null; identifier?: string | null };
  buyer: { name?: string | null; identifier?: string | null };
  lineNetTotal?: number | null;
  taxExclusiveTotal?: number | null;
  taxTotal?: number | null;
  taxInclusiveTotal?: number | null;
  payableAmount?: number | null;
  lines: Array<{
    identifier?: string | null;
    description?: string | null;
    quantity?: number | null;
    unitCode?: string | null;
    netAmount?: number | null;
  }>;
}

export interface EInvoicePdfConsistencyReport {
  status: 'consistent' | 'reviewRequired' | 'notEvaluated';
  findings: Array<{
    field: string;
    expectedValue?: string | null;
    status: 'confirmed' | 'notFound' | 'notCheckable';
    message: string;
  }>;
}

export interface EInvoiceAttachmentSummary {
  fileName: string;
  mimeType?: string | null;
  relationship: string;
  description?: string | null;
  sizeBytes: number;
  sizeMatches: boolean;
  checksumMatches: boolean;
  isXml: boolean;
  standard?: string | null;
  syntax?: string | null;
  guidelineIdentifier?: string | null;
  profile?: string | null;
  isStructurallyValid: boolean;
  conformanceStatus: 'valid' | 'invalid' | 'unavailable' | 'notEvaluated';
  validatorArtifactSetId?: string | null;
  validatorArtifactSetSha256?: string | null;
  diagnostics: EInvoiceDiagnostic[];
  semanticData?: EInvoiceSemanticData | null;
  pdfConsistency: EInvoicePdfConsistencyReport;
}

async function readProblem(response: Response, fallback: string): Promise<never> {
  const problem = await response.json().catch(() => ({}));
  throw new Error(problem.detail || problem.title || `${fallback} (${response.status}).`);
}

export async function inspectEInvoices(
  file: File,
  signal?: AbortSignal,
): Promise<EInvoiceAttachmentSummary[]> {
  const form = new FormData();
  form.append('file', file);
  const response = await fetch('/api/pxa/v1/pdf/e-invoices/inspect', {
    method: 'POST',
    body: form,
    signal,
  });
  if (!response.ok) return readProblem(response, 'Electronic-invoice inspection failed');
  return response.json();
}

export async function extractEInvoice(
  file: File,
  attachmentName: string,
): Promise<Blob> {
  const form = new FormData();
  form.append('file', file);
  form.append('attachmentName', attachmentName);
  const response = await fetch('/api/pxa/v1/pdf/e-invoices/extract', {
    method: 'POST',
    body: form,
  });
  if (!response.ok) return readProblem(response, 'Electronic-invoice extraction failed');
  return response.blob();
}
