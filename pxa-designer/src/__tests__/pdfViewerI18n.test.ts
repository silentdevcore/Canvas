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

  test.each(['en', 'de', 'fr', 'es', 'it', 'ar'])('provides localized trust UX in %s', language => {
    const translate = i18n.getFixedT(language, 'pdfViewer');
    const keys = [
      'trustCancel',
      'trustRetry',
      'trustFailure.offline',
      'trustFailure.timeout',
      'trustFailure.cancelled',
      'trustFailure.policyDenied',
      'trustFailure.unsupported',
      'trustStatusExplanation.valid',
      'trustStatusExplanation.invalid',
      'trustStatusExplanation.indeterminate',
      'trustStatusExplanation.unsupported',
      'trustStatusExplanation.notApplicable',
      'redactionCompare',
      'mutationConfirmation',
      'trustInspectionRequired',
      'signPdf',
      'signingPrivacy',
      'riskyContent',
      'embeddedFiles',
      'affectedObject',
      'affectedElement',
      'technicalDetails',
      'diagnosticSummary.information',
      'diagnosticSummary.warning',
      'diagnosticSummary.error',
    ];

    for (const key of keys) {
      expect(translate(key)).not.toBe(key);
    }
  });
});
