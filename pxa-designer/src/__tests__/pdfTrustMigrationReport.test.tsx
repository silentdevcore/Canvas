/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import PdfTrustMigrationReport, {
  resolveMigrationDocumentationHref,
  type PdfTrustMigrationResult,
} from '@/components/Migrations/PdfTrustMigrationReport';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe('PDF Trust migration report', () => {
  test('resolves only HTTP documentation links', () => {
    expect(resolveMigrationDocumentationHref(
      'https://docs.example.test',
      '/docs/guides/PXA.Pdf-Trust-Generator-Examples.html#pdfua-1',
    )).toBe('https://docs.example.test/docs/guides/PXA.Pdf-Trust-Generator-Examples.html#pdfua-1');
    expect(resolveMigrationDocumentationHref(
      'https://docs.example.test',
      'javascript:alert(1)',
    )).toBe('https://docs.example.test');
  });

  test('renders every manual action with its workflow link', async () => {
    const report: PdfTrustMigrationResult = {
      providerKey: 'itext7',
      migrationKind: 'code',
      overallDisposition: 'ReviewRequired',
      manualActions: [
        {
          actionId: 'CANMIGPDFTRUST100',
          capability: 'DigitalSignature',
          disposition: 'ReviewRequired',
          sourceOperation: 'PdfSigner',
          targetOperation: 'PdfTrustPostProcessingPipeline.Signing',
          documentationPath: '/docs/guides/PXA.Pdf-Trust-Generator-Examples.html#pades-b-t-lt-and-lta',
        },
      ],
    };
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PdfTrustMigrationReport report={report} documentationBaseUrl="https://docs.example.test" />);
    });

    expect(container.textContent).toContain('trust.capabilities.digitalSignature');
    expect(container.textContent).toContain('PdfSigner');
    const link = container.querySelector('a');
    expect(link?.href).toContain('#pades-b-t-lt-and-lta');
    expect(link?.target).toBe('_blank');
    expect(link?.rel).toBe('noreferrer');

    await act(async () => root.unmount());
    container.remove();
  });

  test('normalizes legacy numeric enum values without crashing', async () => {
    const report: PdfTrustMigrationResult = {
      providerKey: 'nutrient',
      migrationKind: 'code',
      overallDisposition: 'ReviewRequired',
      manualActions: [{
        actionId: 'CANMIGNUTNET021',
        capability: 0,
        disposition: 1,
        sourceOperation: 'SignPDF',
        documentationPath: '/docs/guides/PXA.Pdf-Trust-Generator-Examples.html',
      }],
    };
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(<PdfTrustMigrationReport report={report} documentationBaseUrl="https://docs.example.test" />);
    });

    expect(container.textContent).toContain('trust.capabilities.digitalSignature');
    expect(container.textContent).toContain('trust.dispositions.ReviewRequired');

    await act(async () => root.unmount());
    container.remove();
  });
});
