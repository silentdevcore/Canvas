import { getTemplateElements, getTemplatePages } from '../data/templateContent';
import { getTemplateElementsLocalized, getTemplatePagesLocalized } from '../data/templateContent.i18n';
import { OVERRIDES_BY_LOCALE } from '../data/templateOverrides';

describe('template content localization', () => {
  beforeEach(() => {
    // `getTemplateElements` bakes `Date.now()` into generated element ids; fix the
    // clock so two separate calls in the same test produce identical ids/output.
    jest.spyOn(Date, 'now').mockReturnValue(1700000000000);
  });

  afterEach(() => {
    jest.restoreAllMocks();
    // Reset any override the test injected so tests stay isolated from each other.
    delete OVERRIDES_BY_LOCALE.de;
  });

  test('falls back to the English content when no override exists for the locale', () => {
    const englishResult = getTemplateElements('invoice-freelancer');
    expect(getTemplateElementsLocalized('invoice-freelancer', 'xx-not-a-real-locale')).toEqual(englishResult);
  });

  test('falls back to English when the locale exists but has no override for this template', () => {
    const englishResult = getTemplateElements('invoice-freelancer');
    expect(getTemplateElementsLocalized('invoice-freelancer', 'de')).toEqual(englishResult);
  });

  test('returns the override once one exists for the template id + locale', () => {
    const overrideElements = [{ id: 'de-1', type: 'text' as const, x: 0, y: 0, width: 10, height: 10, content: 'Rechnung' }];
    OVERRIDES_BY_LOCALE.de = { elements: { 'invoice-freelancer': () => overrideElements } };

    const result = getTemplateElementsLocalized('invoice-freelancer', 'de');
    expect(result).toEqual(overrideElements);
    expect(result).not.toEqual(getTemplateElements('invoice-freelancer'));
  });

  test('getTemplatePagesLocalized falls back to English multi-page content when no override exists', () => {
    expect(getTemplatePagesLocalized('book-10page', 'xx-not-a-real-locale')).toEqual(
      getTemplatePages('book-10page'),
    );
  });
});
