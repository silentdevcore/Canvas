import { DEFAULT_PAGE_SETTINGS, normalizePageSettings } from '@/store';

test.each([null, undefined, {}])('missing optional page settings use document defaults (%p)', settings => {
  const normalized = normalizePageSettings(settings);
  expect(normalized).toEqual(DEFAULT_PAGE_SETTINGS);
  expect(normalized).not.toBe(DEFAULT_PAGE_SETTINGS);
  expect(normalized.margins).not.toBe(DEFAULT_PAGE_SETTINGS.margins);
});

test('partial page settings preserve explicit geometry and fill nested defaults', () => {
  const settings = normalizePageSettings({ width: 842, height: 595, orientation: 'landscape',
    metadata: { ...DEFAULT_PAGE_SETTINGS.metadata, title: 'Synthetic project' } });
  expect(settings).toMatchObject({ width: 842, height: 595, orientation: 'landscape',
    metadata: { ...DEFAULT_PAGE_SETTINGS.metadata, title: 'Synthetic project' }, margins: DEFAULT_PAGE_SETTINGS.margins });
});
