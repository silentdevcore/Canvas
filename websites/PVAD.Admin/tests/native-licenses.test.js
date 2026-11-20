import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';
import { createNativeLicensePage } from '../src/nativeLicenses.js';
const require = createRequire(new URL('../../../pvad-designer/package.json', import.meta.url));
const { JSDOM } = require('jsdom');
const e = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
const flush = () => new Promise(resolve => setImmediate(resolve));

function fixture(overrides = {}) {
  const dom = new JSDOM('<main></main>', { url: 'https://admin.example.test/native-licenses' });
  const previous = { FormData: globalThis.FormData };
  globalThis.FormData = dom.window.FormData;
  const api = {
    getNativeLicenseCapabilities: async () => ({ enabled: true, products: [{ id: 'pdf', providers: [] }, { id: 'migration.pdf.code', providers: ['aspose-pdf'] }] }),
    getAdminOrganizations: async () => ({ items: [{ id: 'organization-a', name: 'Synthetic organization' }] }),
    getNativeLicenses: async () => ({ items: [], total: 0 }),
    getNativeLicenseDetails: async id => ({ license: { id, model: 'Subscription', artifactSha256: 'synthetic-hash' }, grants: [], customerReference: 'synthetic', redistributionAllowed: false }),
    nativeLicenseDownloadUrl: id => `/api/pvad/v1/admin/native-licenses/${id}/download`,
    ...overrides,
  };
  let page;
  const root = dom.window.document.querySelector('main');
  const render = () => { root.innerHTML = page.html(); page.bind(root); };
  page = createNativeLicensePage(api, e, render);
  page.setContext('operator-a'); render();
  return { page, root, dom, render, close() { globalThis.FormData = previous.FormData; dom.window.close(); } };
}

test('disabled customer deployment does not load manufacturer records or render an issuance form', async () => {
  const f = fixture({ getNativeLicenseCapabilities: async () => ({ enabled: false, products: [] }),
    getNativeLicenses: async () => { assert.fail('Customer mode must not request issuer records'); } });
  try { await f.page.ensureLoaded(); assert.match(f.root.textContent, /Manufacturer issuance disabled/); assert.equal(f.root.querySelector('form'), null); }
  finally { f.close(); }
});

test('native issue uses selected exact provider keys, UTC terms, CSRF adapter and stable retries', async () => {
  const calls = [];
  const f = fixture({ issueNativeLicense: async payload => { calls.push(payload); throw new Error('Synthetic network failure'); } });
  try {
    await f.page.ensureLoaded();
    let form = f.root.querySelector('#native-license-issue');
    form.elements.organizationId.value = 'organization-a'; form.elements.customerReference.value = 'synthetic';
    form.querySelector('[name="product:pdf"]').checked = true;
    const migration = form.querySelector('[name="product:migration.pdf.code"]');
    migration.checked = true; migration.dispatchEvent(new f.dom.window.Event('change', { bubbles: true }));
    form = f.root.querySelector('#native-license-issue');
    form.querySelector('[name="provider:migration.pdf.code"]').value = 'aspose-pdf';
    form.dispatchEvent(new f.dom.window.Event('submit', { bubbles: true, cancelable: true })); await flush();
    assert.equal(calls.length, 1); assert.deepEqual(calls[0].grants, [{ product: 'pdf', providers: [] }, { product: 'migration.pdf.code', providers: ['aspose-pdf'] }]);
    assert.ok(calls[0].validFrom.endsWith('Z')); assert.equal(calls[0].updatesUntil, null);
    form = f.root.querySelector('#native-license-issue');
    assert.equal(form.elements.customerReference.value, 'synthetic');
    form.dispatchEvent(new f.dom.window.Event('submit', { bubbles: true, cancelable: true })); await flush();
    assert.equal(calls.length, 2); assert.equal(calls[0].requestId, calls[1].requestId);
    assert.match(f.root.textContent, /Synthetic network failure/);
  } finally { f.close(); }
});

test('Trial and perpetual forms generate separate native terms without hidden subscription or redistribution grants', async () => {
  for (const model of ['Trial', 'Perpetual']) {
    const calls = [];
    const f = fixture({ issueNativeLicense: async payload => { calls.push(payload); throw new Error('Synthetic stop after capture'); } });
    try {
      await f.page.ensureLoaded();
      let form = f.root.querySelector('#native-license-issue');
      form.elements.organizationId.value = 'organization-a'; form.elements.customerReference.value = 'synthetic';
      form.querySelector('[name="product:pdf"]').checked = true;
      form.elements.model.value = model;
      form.elements.model.dispatchEvent(new f.dom.window.Event('change', { bubbles: true }));
      form = f.root.querySelector('#native-license-issue');
      form.dispatchEvent(new f.dom.window.Event('submit', { bubbles: true, cancelable: true })); await flush();
      assert.equal(calls.length, 1);
      if (model === 'Trial') {
        assert.equal(Date.parse(calls[0].validUntil) - Date.parse(calls[0].validFrom), 30 * 86400000);
        assert.equal(calls[0].updatesUntil, null); assert.equal(calls[0].redistributionAllowed, false);
      } else { assert.equal(calls[0].validUntil, null); assert.ok(calls[0].updatesUntil.endsWith('Z')); }
    } finally { f.close(); }
  }
});

test('organization and operator changes discard late results rather than revealing prior license data', async () => {
  let resolve;
  const f = fixture({ getNativeLicenses: () => new Promise(done => { resolve = done; }) });
  try {
    const pending = f.page.ensureLoaded(); await flush();
    f.page.clear(); f.page.setContext('operator-b');
    resolve({ items: [{ id: 'prior-private-license', model: 'Subscription' }], total: 1 });
    await pending; f.render(); assert.doesNotMatch(f.root.innerHTML, /prior-private-license/);
  } finally { f.close(); }
});

test('manufacturer access failure is actionable and does not restart an infinite fetch loop', async () => {
  let count = 0;
  const f = fixture({ getNativeLicenseCapabilities: async () => { count++; throw Object.assign(new Error('Denied'), { status: 403 }); } });
  try {
    await f.page.ensureLoaded(); await f.page.ensureLoaded();
    assert.equal(count, 1); assert.match(f.root.textContent, /recent sign-in/);
    assert.equal(f.root.querySelector('#native-license-reauth').textContent, 'Sign out and sign in');
    assert.doesNotMatch(f.root.textContent, /Checking manufacturer access/);
  } finally { f.close(); }
});

test('license metadata is escaped and unknown provider options never appear', async () => {
  const f = fixture({ getAdminOrganizations: async () => ({ items: [{ id: 'org', name: '<img src=x onerror=alert(1)>' }] }) });
  try {
    await f.page.ensureLoaded();
    assert.equal(f.root.querySelector('img'), null);
    assert.deepEqual([...f.root.querySelectorAll('[name="provider:migration.pdf.code"] option')].map(option => option.value), ['aspose-pdf']);
  } finally { f.close(); }
});
