import assert from 'node:assert/strict';
import test from 'node:test';
import { accountLocales, setAccountLocale, tr } from '../src/authI18n.ts';

const storage = new Map<string, string>();
Object.defineProperty(globalThis, 'localStorage', {
  value: {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => storage.set(key, value),
  },
});
Object.defineProperty(globalThis, 'navigator', {
  value: { language: 'en' },
  configurable: true,
});
const documentElement = { lang: '', dir: '' };
Object.defineProperty(globalThis, 'document', {
  value: { documentElement },
  configurable: true,
});

test('all six Account locales provide translated authentication content', () => {
  const headings = new Set<string>();
  for (const locale of accountLocales) {
    setAccountLocale(locale);
    headings.add(tr('registerHeading'));
    assert.equal(documentElement.lang, locale);
    assert.ok(tr('verificationDescription').length > 20);
    assert.ok(tr('recoveryDescription').length > 20);
  }
  assert.equal(headings.size, accountLocales.length);
});

test('Arabic Account content enables right-to-left document direction', () => {
  setAccountLocale('ar');
  tr('loginHeading');
  assert.equal(documentElement.dir, 'rtl');
  setAccountLocale('de');
  tr('loginHeading');
  assert.equal(documentElement.dir, 'ltr');
});
