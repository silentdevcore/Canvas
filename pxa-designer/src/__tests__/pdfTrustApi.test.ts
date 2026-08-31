/**
 * @jest-environment jsdom
 */
import {
  convertPdfA2b,
  inspectPdfTrust,
  preflightPdfA2b,
  type PdfTrustReport,
} from '@/features/pdf-viewer/pdfTrustApi';

const report: PdfTrustReport = {
  status: 2,
  structureStatus: 0,
  sha256: 'a'.repeat(64),
  fileSizeBytes: 128,
  pdfVersion: '1.7',
  revisionCount: 1,
  objectCount: 5,
  encryption: { isEncrypted: false, status: 4 },
  signatures: [],
  pdfA: { standard: 'PDF/A', isClaimed: false, status: 4, evidence: 'none' },
  pdfUa: { standard: 'PDF/UA', isClaimed: false, status: 4, evidence: 'none' },
  revisions: [],
  diagnostics: [],
};

describe('PDF Trust API', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    global.fetch = fetchMock;
  });

  test('uploads the PDF through the canonical trust endpoint', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => report,
    } as Response);
    const file = new File(['%PDF-1.7'], 'trust.pdf', { type: 'application/pdf' });

    await expect(inspectPdfTrust(file)).resolves.toEqual(report);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/pxa/v1/pdf/trust/inspect');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBeInstanceOf(FormData);
    expect((init?.body as FormData).get('file')).toBe(file);
  });

  test('surfaces Problem Details without claiming an inspection result', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({ detail: 'PDF inspection is not permitted.' }),
    } as Response);

    await expect(inspectPdfTrust(new File(['%PDF-1.7'], 'trust.pdf')))
      .rejects.toThrow('PDF inspection is not permitted.');
  });

  test('returns the machine-readable PDF/A preflight report', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        targetProfile: 'PDF/A-2b',
        capability: 0,
        sourceSha256: 'b'.repeat(64),
        fileSizeBytes: 128,
        objectCount: 5,
        signatureCount: 0,
        invalidatesSignatures: false,
        originalPreserved: true,
        findings: [],
      }),
    } as Response);
    const file = new File(['%PDF-1.7'], 'source.pdf', { type: 'application/pdf' });

    const result = await preflightPdfA2b(file);

    expect(result.capability).toBe(0);
    expect(result.originalPreserved).toBe(true);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/pxa/v1/pdf/compliance/preflight');
  });

  test('passes explicit signature invalidation approval to conversion', async () => {
    const converted = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      blob: async () => converted,
    } as Response);
    const file = new File(['%PDF-1.7'], 'source.pdf', { type: 'application/pdf' });

    await expect(convertPdfA2b(file, true)).resolves.toBe(converted);

    const [, init] = fetchMock.mock.calls[0];
    expect((init?.body as FormData).get('allowSignatureInvalidation')).toBe('true');
  });

  test('surfaces the server reason when PDF/A conversion is blocked', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ detail: 'An embedded font is missing.' }),
    } as Response);

    await expect(convertPdfA2b(new File(['%PDF-1.7'], 'source.pdf'), false))
      .rejects.toThrow('An embedded font is missing.');
  });
});
