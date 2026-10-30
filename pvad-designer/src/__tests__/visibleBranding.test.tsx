/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import i18n from '@/i18n';
import InvoiceDiagnosticsPanel from '@/features/e-invoice/InvoiceDiagnosticsPanel';
import { displayDiagnosticCode, displaySystemMessage, presentRelease } from '../../../websites/shared/diagnosticPresentation';
import releaseManifest from '../../../product-metadata/pvad-releases.json';

test('display aliases preserve technical codes, suffixes and unrelated customer strings', () => {
  for (const code of ['PXAAPI010', 'PXACODE042', 'PXA-PDF-CHART-102', 'PXAPROJECT007']) {
    expect(displayDiagnosticCode(code)).toBe(`PVAD${code.slice(3)}`);
    expect(displayDiagnosticCode(displayDiagnosticCode(code))).toBe(displayDiagnosticCode(code));
  }
  expect(displayDiagnosticCode('CANMIG100')).toBe('CANMIG100');
  expect(displayDiagnosticCode('customer-PXA-document')).toBe('customer-PXA-document');
  expect(displaySystemMessage('Review PXAPDFTRUST203.')).toBe('Review PVADPDFTRUST203.');
  expect(displaySystemMessage('Customer PXA Ltd.')).toBe('Customer PXA Ltd.');
});

test('release presentation does not rewrite the historical manifest', () => {
  const original = JSON.stringify(releaseManifest);
  for (const release of releaseManifest.releases) {
    const shown = presentRelease(release);
    expect(JSON.stringify([shown.title, shown.summary, shown.changes])).not.toMatch(/PXA|Power Dox Automation/);
    expect(shown.version).toBe(release.version);
    expect(shown.documentationPath).toBe(release.documentationPath);
  }
  expect(JSON.stringify(releaseManifest)).toBe(original);
});

test.each(['en', 'de', 'fr', 'es', 'it', 'ar'])('invoice findings show display aliases in %s while keeping raw selectors', async language => {
  (globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  await i18n.changeLanguage(language);
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  const finding = { code: 'PXAEINV100', severity: 'error' as const, field: 'invoiceNumber', messageKey: 'validation.invoiceNumberRequired' };
  try {
    await act(async () => root.render(<InvoiceDiagnosticsPanel findings={[finding]} report={null} onFocusField={() => {}} />));
    expect(container.textContent).toContain('PVADEINV100');
    expect(container.textContent).not.toContain('PXA');
    expect(container.querySelector('[data-code="PXAEINV100"]')).not.toBeNull();
    expect(finding.code).toBe('PXAEINV100');
    expect(i18n.t('common:aboutPvaDe')).toContain('PVADE');
  } finally {
    await act(async () => root.unmount());
    container.remove();
    await i18n.changeLanguage('en');
  }
});
