import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { getAccountNativeLicenses, getAccountNativeLicense, accountNativeLicenseDownloadUrl } from '../src/api.ts';

test('native license queries are same-origin, credentialed and never accept a client tenant ID', async () => {
  const fetchBefore = globalThis.fetch;
  const windowBefore = globalThis.window;
  const requests: { path: string; credentials: RequestCredentials | undefined }[] = [];
  globalThis.window = { dispatchEvent: () => true } as unknown as Window & typeof globalThis;
  globalThis.fetch = async (path, options) => {
    requests.push({ path: String(path), credentials: options?.credentials });
    return new Response(JSON.stringify({ items: [], total: 0, page: 2, pageSize: 25 }), { headers: { 'Content-Type': 'application/json' } });
  };
  try {
    await getAccountNativeLicenses(2); await getAccountNativeLicense('encoded/id');
    assert.deepEqual(requests, [
      { path: '/api/pvad/v1/account/native-licenses?page=2&pageSize=25', credentials: 'include' },
      { path: '/api/pvad/v1/account/native-licenses/encoded%2Fid', credentials: 'include' },
    ]);
    assert.equal(accountNativeLicenseDownloadUrl('encoded/id'), '/api/pvad/v1/account/native-licenses/encoded%2Fid/download');
  } finally { globalThis.fetch = fetchBefore; globalThis.window = windowBefore; }
});

test('native and server license sections stay separate and late responses are invalidated on context reset', async () => {
  const source = await readFile(new URL('../src/pages/licenses.ts', import.meta.url), 'utf8');
  assert.match(source, /Server offline licenses/);
  assert.match(source, /Native library licenses/);
  assert.match(source, /generation\+\+/);
  assert.match(source, /if \(current !== generation\) return/);
  assert.match(source, /if \(current === generation\) state\.nativeDetail/);
  assert.doesNotMatch(source, /\/admin\/native-licenses|issueNativeLicense|renewNativeLicense/);
});

test('Account does not intercept API or file downloads as SPA pages', async () => {
  const source = await readFile(new URL('../src/main.ts', import.meta.url), 'utf8');
  assert.match(source, /link\.hasAttribute\('download'\)/);
  assert.match(source, /link\.hasAttribute\('data-native'\)/);
  assert.match(source, /pathname\.startsWith\('\/api\/'\)\) return/);
});
