import { isDocumentRtlLanguage } from '@/utils/documentDirection';

describe('document RTL language policy', () => {
  test.each(['ar', 'ar-SA', 'AR'])('treats %s as RTL', language => {
    expect(isDocumentRtlLanguage(language)).toBe(true);
  });

  test.each(['en', 'de', 'he', 'fa', 'ur', '', null, undefined])(
    'does not treat %s as RTL',
    language => {
      expect(isDocumentRtlLanguage(language)).toBe(false);
    },
  );
});
