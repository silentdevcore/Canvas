/** @jest-environment jsdom */
import { readDesignerRuntimeConfig, RuntimeConfigurationError, type RuntimeConfigGlobals } from '../config/runtimeConfig';
import { accountBaseUrl, accountPageUrl } from '../auth/designerAuth';

const globals = globalThis as RuntimeConfigGlobals;
const previous = globals.__PVAD_CONFIG__;
afterEach(() => { globals.__PVAD_CONFIG__ = previous; });

test('reads and normalizes the runtime configuration, including an on-premise base path', () => {
  expect(readDesignerRuntimeConfig({ __PVAD_CONFIG__: { accountUrl: 'https://accounts.example/customer' } }))
    .toEqual({ accountUrl: 'https://accounts.example/customer/' });
});

test('former runtime configuration globals are ignored', () => {
  expect(readDesignerRuntimeConfig({ __PXA_CONFIG__: { accountUrl: 'https://accounts.example/' } } as RuntimeConfigGlobals))
    .toEqual({ accountUrl: undefined });
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
  globals.__PVAD_CONFIG__ = undefined;
  expect(readDesignerRuntimeConfig({})).toEqual({ accountUrl: undefined });
  expect(accountBaseUrl()).toBe('http://localhost:5178/');
  globals.__PVAD_CONFIG__ = { accountUrl: '' };
  expect(accountBaseUrl()).toBe('http://localhost:5178/');
});

test('the account URL consumer uses the canonical configuration', () => {
  globals.__PVAD_CONFIG__ = { accountUrl: 'https://accounts.example/customer/' };
  expect(accountPageUrl('/designer-authorize')).toBe('https://accounts.example/customer/designer-authorize');
});
