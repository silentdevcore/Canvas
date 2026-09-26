import { extractEInvoice, inspectEInvoices } from '../features/pdf-viewer/eInvoiceApi';

const jsonResponse = (status: number, body: unknown): Response => ({
  ok: status >= 200 && status < 300,
  status,
  json: async () => body,
} as Response);

describe('PDF Viewer electronic-invoice API client', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    global.fetch = fetchMock;
  });

  test('inspects the currently opened PDF', async () => {
    const file = new File(['%PDF'], 'invoice.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [{
      fileName: 'factur-x.xml',
      relationship: 'Alternative',
      sizeBytes: 100,
      sizeMatches: true,
      checksumMatches: true,
      isXml: true,
      standard: 'FacturX',
      syntax: 'CII',
      profile: 'BASIC',
      isStructurallyValid: true,
      conformanceStatus: 'notEvaluated',
      diagnostics: [],
      semanticData: {
        invoiceNumber: 'INV-42',
        currency: 'EUR',
        seller: { name: 'Seller GmbH' },
        buyer: { name: 'Buyer AG' },
        payableAmount: 119,
        lines: [],
      },
      pdfConsistency: { status: 'consistent', findings: [] },
    }]));

    const result = await inspectEInvoices(file);

    expect(result[0].standard).toBe('FacturX');
    expect(result[0].semanticData?.invoiceNumber).toBe('INV-42');
    expect(result[0].pdfConsistency.status).toBe('consistent');
    expect(fetchMock).toHaveBeenCalledWith('/api/pxa/v1/pdf/e-invoices/inspect', {
      method: 'POST',
      body: expect.any(FormData),
      signal: undefined,
    });
    expect((fetchMock.mock.calls[0][1].body as FormData).get('file')).toBe(file);
  });

  test('extracts one exact invoice attachment', async () => {
    const file = new File(['%PDF'], 'invoice.pdf', { type: 'application/pdf' });
    const xml = new Blob(['<Invoice />'], { type: 'application/xml' });
    fetchMock.mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({}),
      blob: async () => xml,
    } as Response);

    await expect(extractEInvoice(file, 'factur-x.xml')).resolves.toBe(xml);

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('attachmentName')).toBe('factur-x.xml');
  });

  test('surfaces Problem Details from the API', async () => {
    const file = new File(['%PDF'], 'invoice.pdf', { type: 'application/pdf' });
    fetchMock.mockResolvedValueOnce(jsonResponse(403, { detail: 'Invoice access is not permitted.' }));

    await expect(inspectEInvoices(file)).rejects.toThrow('Invoice access is not permitted.');
  });
});
