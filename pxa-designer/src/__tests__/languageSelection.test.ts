import { updateLanguageSelection } from '@/utils/languageSelection';

describe('language multi-select', () => {
  test('adds languages in selection order without duplicates', () => {
    expect(updateLanguageSelection(['en'], 'de', true)).toEqual(['en', 'de']);
    expect(updateLanguageSelection(['en', 'de'], 'de', true)).toEqual(['en', 'de']);
  });

  test('removes only the selected language', () => {
    expect(updateLanguageSelection(['en', 'de', 'ar'], 'de', false)).toEqual(['en', 'ar']);
  });

  test('keeps the list stable when an absent language is removed', () => {
    expect(updateLanguageSelection(['en', 'de'], 'fr', false)).toEqual(['en', 'de']);
  });
});
