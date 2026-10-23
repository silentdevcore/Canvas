/**
 * @jest-environment jsdom
 */
import {
  convertPdfA2b,
  inspectPdfTrust,
  PdfTrustOperationError,
  preflightPdfA2b,
  signPdf,
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

  test('uploads and resolves a tenant-scoped PDF Trust job', async () => {
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        status: 202,
        json: async () => ({
          jobId: 'trust-job-1',
          status: 'Pending',
          statusUrl: '/api/pvad/v1/jobs/trust-job-1',
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          id: 'trust-job-1',
          status: 'Completed',
          downloadUrl: '/api/pvad/v1/jobs/trust-job-1/result',
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => report,
      } as Response);
    const file = new File(['%PDF-1.7'], 'trust.pdf', { type: 'application/pdf' });

    await expect(inspectPdfTrust(file)).resolves.toEqual(report);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/pvad/v1/pdf/trust/inspect/jobs');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBeInstanceOf(FormData);
    expect((init?.body as FormData).get('file')).toBe(file);
    expect(fetchMock.mock.calls[1][0]).toBe('/api/pvad/v1/jobs/trust-job-1');
    expect(fetchMock.mock.calls[2][0]).toBe('/api/pvad/v1/jobs/trust-job-1/result');
  });

  test('surfaces Problem Details without claiming an inspection result', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({ detail: 'PDF inspection is not permitted.' }),
    } as Response);

    const operation = inspectPdfTrust(new File(['%PDF-1.7'], 'trust.pdf'));
    await expect(operation).rejects.toThrow('PDF inspection is not permitted.');
    await expect(operation).rejects.toMatchObject({
      kind: 'policyDenied',
      status: 403,
    } satisfies Partial<PdfTrustOperationError>);
  });

  test('distinguishes a user cancellation from a generic request failure', async () => {
    fetchMock.mockImplementation((_url, init: RequestInit) => new Promise((_resolve, reject) => {
      init.signal?.addEventListener('abort', () => reject(new DOMException('Cancelled', 'AbortError')));
    }));
    const controller = new AbortController();
    const operation = inspectPdfTrust(new File(['%PDF-1.7'], 'trust.pdf'), controller.signal);

    controller.abort();

    await expect(operation).rejects.toMatchObject({ kind: 'cancelled' });
  });

  test('does not start an inspection when the caller already cancelled it', async () => {
    fetchMock.mockImplementation((_url, init: RequestInit) => {
      if (init.signal?.aborted) {
        return Promise.reject(new DOMException('Cancelled', 'AbortError'));
      }
      return Promise.reject(new Error('Expected an aborted signal.'));
    });
    const controller = new AbortController();
    controller.abort();

    await expect(inspectPdfTrust(
      new File(['%PDF-1.7'], 'trust.pdf'),
      controller.signal,
    )).rejects.toMatchObject({ kind: 'cancelled' });
  });

  test('cancels the queued trust job when the caller aborts during polling', async () => {
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        status: 202,
        json: async () => ({
          jobId: 'trust-job-cancel',
          status: 'Pending',
          statusUrl: '/api/pvad/v1/jobs/trust-job-cancel',
        }),
      } as Response)
      .mockImplementationOnce((_url, init: RequestInit) => new Promise((_resolve, reject) => {
        init.signal?.addEventListener('abort', () => reject(new DOMException('Cancelled', 'AbortError')));
      }))
      .mockResolvedValueOnce({ ok: true, status: 200 } as Response);
    const controller = new AbortController();
    const operation = inspectPdfTrust(
      new File(['%PDF-1.7'], 'trust.pdf', { type: 'application/pdf' }),
      controller.signal,
    );
    await Promise.resolve();
    await Promise.resolve();

    controller.abort();

    await expect(operation).rejects.toMatchObject({ kind: 'cancelled' });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/pvad/v1/jobs/trust-job-cancel/cancel',
      { method: 'POST' },
    );
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
        findings: [{
          code: 'PXAPDFACONVERT004', canonicalCode: 'PVADPDFACONVERT004',
          severity: 1, message: 'Synthetic finding', localizationKey: 'diagnostics.PXAPDFACONVERT004',
        }],
      }),
    } as Response);
    const file = new File(['%PDF-1.7'], 'source.pdf', { type: 'application/pdf' });

    const result = await preflightPdfA2b(file);

    expect(result.capability).toBe(0);
    expect(result.originalPreserved).toBe(true);
    expect(result.findings[0]).toMatchObject({
      code: 'PXAPDFACONVERT004', canonicalCode: 'PVADPDFACONVERT004',
      localizationKey: 'diagnostics.PXAPDFACONVERT004',
    });
    expect(fetchMock.mock.calls[0][0]).toBe('/api/pvad/v1/pdf/compliance/preflight');
  });

  test('passes explicit signature invalidation approval to conversion', async () => {
    const converted = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      blob: async () => converted,
    } as Response);
    const file = new File(['%PDF-1.7'], 'source.pdf', { type: 'application/pdf' });

    await expect(convertPdfA2b(file, true, 'PDF/A-3b')).resolves.toBe(converted);

    const [, init] = fetchMock.mock.calls[0];
    expect((init?.body as FormData).get('allowSignatureInvalidation')).toBe('true');
    expect((init?.body as FormData).get('profile')).toBe('PDF/A-3b');
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

  test('sends signing material only to the privileged signing endpoint', async () => {
    const signed = new Blob(['%PDF-1.7 signed'], { type: 'application/pdf' });
    fetchMock.mockResolvedValue({ ok: true, status: 200, blob: async () => signed } as Response);
    const pdf = new File(['%PDF-1.7'], 'source.pdf', { type: 'application/pdf' });
    const certificate = new File(['pkcs12'], 'signer.p12', { type: 'application/x-pkcs12' });

    await expect(signPdf({
      pdf,
      certificate,
      certificatePassword: 'secret',
      profile: 'BaselineT',
      fieldName: 'Approval',
      visible: true,
      pageNumber: 2,
    })).resolves.toBe(signed);

    const [url, init] = fetchMock.mock.calls[0];
    const form = init.body as FormData;
    expect(url).toBe('/api/pvad/v1/pdf/signatures/sign');
    expect(form.get('pdf')).toBe(pdf);
    expect(form.get('certificate')).toBe(certificate);
    expect(form.get('profile')).toBe('BaselineT');
    expect(form.get('pageNumber')).toBe('2');
  });
});
