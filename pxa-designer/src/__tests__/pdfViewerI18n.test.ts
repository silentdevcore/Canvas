import i18n from '../i18n';

describe('pdf viewer i18n', () => {
  beforeEach(async () => {
    await i18n.changeLanguage('en');
  });

  test('contains core labels in English and German', () => {
    expect(i18n.getFixedT('en', 'pdfViewer')('openPdf')).toBe('Open PDF');
    expect(i18n.getFixedT('de', 'pdfViewer')('openPdf')).toBe('PDF öffnen');
    expect(i18n.getFixedT('en', 'pdfViewer')('redact')).toBe('Redact');
    expect(i18n.getFixedT('de', 'pdfViewer')('redact')).toBe('Schwärzen');
  });

  test('switching i18n language changes t() output for the pdfViewer namespace', async () => {
    expect(i18n.t('pdfViewer:openPdf')).toBe('Open PDF');
    await i18n.changeLanguage('de');
    expect(i18n.t('pdfViewer:openPdf')).toBe('PDF öffnen');
  });
});
