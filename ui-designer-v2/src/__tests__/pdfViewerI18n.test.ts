import { pdfViewerLabels, resolvePdfViewerLocale } from '../features/pdf-viewer/i18n';

describe('pdf viewer i18n', () => {
  test('resolves German browser language to de', () => {
    expect(resolvePdfViewerLocale('de-DE')).toBe('de');
    expect(resolvePdfViewerLocale('de')).toBe('de');
  });

  test('falls back to English for non-German languages', () => {
    expect(resolvePdfViewerLocale('en-US')).toBe('en');
    expect(resolvePdfViewerLocale('fr-FR')).toBe('en');
  });

  test('contains core labels in English and German', () => {
    expect(pdfViewerLabels.en.openPdf).toBe('Open PDF');
    expect(pdfViewerLabels.de.openPdf).toBe('PDF öffnen');
    expect(pdfViewerLabels.en.redact).toBe('Redact');
    expect(pdfViewerLabels.de.redact).toBe('Schwärzen');
  });
});
