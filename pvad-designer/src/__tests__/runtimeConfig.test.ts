/** @jest-environment jsdom */
import { readDesignerRuntimeConfig, RuntimeConfigurationError, type RuntimeConfigGlobals } from '../config/runtimeConfig';
import { accountBaseUrl, accountPageUrl } from '../auth/designerAuth';

const globals = globalThis as RuntimeConfigGlobals;
const previous = { current: globals.__PVAD_CONFIG__, legacy: globals.__PXA_CONFIG__ };
afterEach(() => { globals.__PVAD_CONFIG__ = previous.current; globals.__PXA_CONFIG__ = previous.legacy; });

test.each(['__PVAD_CONFIG__', '__PXA_CONFIG__'] as const)('reads and normalizes %s, including an on-premise base path', key => {
  expect(readDesignerRuntimeConfig({ [key]: { accountUrl: 'https://accounts.example/customer' } }))
    .toEqual({ accountUrl: 'https://accounts.example/customer/' });
});

test('equivalent aliases are accepted; unrelated future settings do not change known fields', () => {
  const config = readDesignerRuntimeConfig({
    __PVAD_CONFIG__: { accountUrl: 'https://accounts.example', future: true },
    __PXA_CONFIG__: { accountUrl: 'https://accounts.example/' },
  });
  expect(config.accountUrl).toBe('https://accounts.example/');
  expect(Object.isFrozen(config)).toBe(true);
});

test('conflicting aliases and explicit empty conflicts expose keys, never values', () => {
  for (const value of ['https://canonical-private.example/', '']) {
    try {
      readDesignerRuntimeConfig({ __PVAD_CONFIG__: { accountUrl: value }, __PXA_CONFIG__: { accountUrl: 'https://legacy-private.example/' } });
      throw new Error('Expected a conflict');
    } catch (error) {
      expect(error).toBeInstanceOf(RuntimeConfigurationError);
      expect((error as Error).message).toContain('__PVAD_CONFIG__.accountUrl');
      expect((error as Error).message).not.toMatch(/private|https:/);
    }
  }
});

test.each([null, [], 'not-an-object', { accountUrl: false }, { accountUrl: '/relative' },
  { accountUrl: 'javascript:alert(1)' }, { accountUrl: 'file:///private' },
  { accountUrl: 'https://user:secret@example.test/' }, { accountUrl: 'https://example.test/?secret=token' },
  { accountUrl: 'https://example.test/#token' }, { accountUrl: 'https://example.test\\other' },
  { accountUrl: 'https://exam\nple.test/' }, { accountUrl: 'https://example.test/base path' }])(
  'rejects malformed or unsafe configuration without leaking it', value => {
    expect(() => readDesignerRuntimeConfig({ __PVAD_CONFIG__: value })).toThrow(RuntimeConfigurationError);
    try { readDesignerRuntimeConfig({ __PVAD_CONFIG__: value }); }
    catch (error) { expect((error as Error).message).not.toMatch(/secret|token|private|javascript/); }
  },
);

test('missing and blank values preserve the default behavior', () => {
  globals.__PVAD_CONFIG__ = undefined; globals.__PXA_CONFIG__ = undefined;
  expect(readDesignerRuntimeConfig({})).toEqual({ accountUrl: undefined });
  expect(accountBaseUrl()).toBe('http://localhost:5178/');
  globals.__PVAD_CONFIG__ = { accountUrl: '' };
  expect(accountBaseUrl()).toBe('http://localhost:5178/');
});

test('the account URL consumer uses canonical configuration and the legacy-only fallback', () => {
  globals.__PVAD_CONFIG__ = { accountUrl: 'https://accounts.example/customer/' }; globals.__PXA_CONFIG__ = undefined;
  expect(accountPageUrl('/designer-authorize')).toBe('https://accounts.example/customer/designer-authorize');
  globals.__PVAD_CONFIG__ = undefined; globals.__PXA_CONFIG__ = { accountUrl: 'http://localhost:5190' };
  expect(accountBaseUrl()).toBe('http://localhost:5190/');
});
