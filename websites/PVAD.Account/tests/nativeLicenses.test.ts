import assert from 'node:assert/strict';
import test from 'node:test';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import type { UserInfo } from '../src/api.ts';
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


test('orgless license page loads personal artifacts without calling organization licensing APIs', async () => {
  const require = createRequire(new URL('../../../pvad-designer/package.json', import.meta.url));
  const { build } = await import(require.resolve('vite'));
  const compiled = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false,
    lib: { entry: fileURLToPath(new URL('../src/pages/licenses.ts', import.meta.url)), formats: ['es'], fileName: 'licenses' } } });
  const result = Array.isArray(compiled) ? compiled[0] : compiled;
  const { licensesPage } = await import('data:text/javascript;base64,' + Buffer.from(result.output[0].code).toString('base64')) as { licensesPage: (user: UserInfo) => string };
  const previousFetch = globalThis.fetch;
  const previousWindow = globalThis.window;
  const paths: string[] = [];
  globalThis.window = { dispatchEvent: () => true } as unknown as Window & typeof globalThis;
  globalThis.fetch = async path => {
    paths.push(String(path));
    assert.match(String(path), /account\/native-licenses/);
    return new Response(JSON.stringify({ items: [{ id: 'personal-artifact', organizationId: null, ownerUserId: 'owner', model: 'Perpetual' }], total: 1, automaticIssuancePending: true }),
      { headers: { 'Content-Type': 'application/json' } });
  };
  try {
    const owner: UserInfo = { id: 'owner', username: 'owner', email: '', displayName: 'Owner', roles: [], permissions: [], organizations: [],
      activeOrganizationId: null, lastLoginAt: null, canReadPersonalNativeLicenses: true };
    licensesPage(owner);
    await new Promise(resolve => setTimeout(resolve, 0));
    const html = licensesPage(owner);
    assert.equal(paths.length, 1);
    assert.match(html, /personal-artifact/);
    assert.match(html, /Your registration is recorded for license issuance/);
    assert.doesNotMatch(html, /Server offline licenses/);
  } finally { globalThis.fetch = previousFetch; globalThis.window = previousWindow; }
});


test('orgless account landing page opens personal licensing without organization requests', async () => {
  const require = createRequire(new URL('../../../pvad-designer/package.json', import.meta.url));
  const { build } = await import(require.resolve('vite'));
  const compiled = await build({ configFile: false, logLevel: 'silent', build: { write: false, minify: false,
    lib: { entry: fileURLToPath(new URL('../src/pages/dashboard.ts', import.meta.url)), formats: ['es'], fileName: 'dashboard' } } });
  const result = Array.isArray(compiled) ? compiled[0] : compiled;
  const { dashboardPage } = await import('data:text/javascript;base64,' + Buffer.from(result.output[0].code).toString('base64')) as { dashboardPage: (user: UserInfo) => string };
  const previousFetch = globalThis.fetch;
  let requests = 0;
  globalThis.fetch = async () => { requests++; throw new Error('An orgless account must not query organization status.'); };
  try {
    const html = dashboardPage({ id: 'owner', username: '', email: '', displayName: 'Owner', roles: [], permissions: [], organizations: [],
      activeOrganizationId: null, lastLoginAt: null, canReadPersonalNativeLicenses: true });
    await new Promise(resolve => setTimeout(resolve, 0));
    assert.equal(requests, 0);
    assert.match(html, /href="\/licenses"/);
    assert.match(html, /Personal account/);
    assert.doesNotMatch(html, /Trial workspace/);
  } finally { globalThis.fetch = previousFetch; }
});
