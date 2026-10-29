/** @jest-environment jsdom */
import { exchangeCallback, DesignerAuthError } from '../auth/designerAuth';

const canonical = 'pvad.designer.auth-transaction';
const legacy = 'pxa.designer.auth-transaction';
const state = 'S'.repeat(43);
const transaction = () => ({ verifier: 'V'.repeat(86), state, returnPath: '/pdf/create', createdAt: Date.now() });
const previousFetch = global.fetch;

beforeEach(() => {
  sessionStorage.clear();
  history.replaceState({}, '', `/auth/callback?code=one-time-code&state=${state}`);
  global.fetch = jest.fn()
    .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ token: 'csrf' }) })
    .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ returnPath: '/pdf/create' }) });
});
afterEach(() => { global.fetch = previousFetch; jest.restoreAllMocks(); });

test.each([canonical, legacy])('completes and consumes the exact PKCE transaction from %s', async key => {
  sessionStorage.setItem(key, JSON.stringify(transaction()));
  await expect(exchangeCallback()).resolves.toBe('/pdf/create');
  expect(sessionStorage.getItem(canonical)).toBeNull();
  expect(sessionStorage.getItem(legacy)).toBeNull();
  expect(location.search).toBe('');
  const body = JSON.parse((global.fetch as jest.Mock).mock.calls[1][1].body);
  expect(body.codeVerifier).toBe('V'.repeat(86));
  await expect(exchangeCallback()).rejects.toBeInstanceOf(DesignerAuthError);
  expect(global.fetch).toHaveBeenCalledTimes(2);
});

test('a new transaction supersedes a matching old one instead of falling back', async () => {
  sessionStorage.setItem(canonical, JSON.stringify({ ...transaction(), state: 'N'.repeat(43) }));
  sessionStorage.setItem(legacy, JSON.stringify(transaction()));
  await expect(exchangeCallback()).rejects.toThrow('invalid or expired');
  expect(global.fetch).not.toHaveBeenCalled();
  expect(sessionStorage.getItem(legacy)).toBeNull();
});

test.each([
  'broken-json', 'null', '{}',
  JSON.stringify({ ...transaction(), verifier: 'short' }),
  JSON.stringify({ ...transaction(), createdAt: Date.now() - 11 * 60_000 }),
  JSON.stringify({ ...transaction(), createdAt: Date.now() + 60_000 }),
])('rejects invalid PKCE data before any API call', async raw => {
  sessionStorage.setItem(legacy, raw);
  await expect(exchangeCallback()).rejects.toBeInstanceOf(DesignerAuthError);
  expect(global.fetch).not.toHaveBeenCalled();
  expect(sessionStorage.getItem(legacy)).toBeNull();
});

test('does not exchange a code if stored state cannot be consumed', async () => {
  sessionStorage.setItem(canonical, JSON.stringify(transaction()));
  jest.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => { throw new Error('blocked'); });
  await expect(exchangeCallback()).rejects.toThrow();
  expect(global.fetch).not.toHaveBeenCalled();
});

test('rejects a return path that the browser would normalize to another origin', async () => {
  sessionStorage.setItem(canonical, JSON.stringify(transaction()));
  (global.fetch as jest.Mock).mockReset()
    .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ token: 'csrf' }) })
    .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ returnPath: '/\\elsewhere.example/path' }) });
  await expect(exchangeCallback()).rejects.toThrow('return destination is invalid');
});
