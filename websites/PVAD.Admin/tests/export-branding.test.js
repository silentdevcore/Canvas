import assert from 'node:assert/strict';
import test from 'node:test';
import { exportAdminAudit, exportAdminLegalAcceptance } from '../src/api.js';

test('Admin exports use current fallbacks and preserve server-provided customer filenames', async (context) => {
  const previousWindow = globalThis.window;
  globalThis.window = new EventTarget();
  context.after(() => {
    if (previousWindow === undefined) delete globalThis.window;
    else globalThis.window = previousWindow;
  });
  let disposition;
  const requests = [];
  context.mock.method(globalThis, 'fetch', async (url, options) => {
    requests.push({ url, options });
    if (url.endsWith('/csrf')) {
      return new Response(JSON.stringify({ token: 'synthetic-csrf' }), {
        headers: { 'Content-Type': 'application/json' },
      });
    }
    return new Response('synthetic export', {
      headers: disposition ? { 'Content-Disposition': disposition } : {},
    });
  });

  const audit = await exportAdminAudit('json', {});
  assert.equal(audit.filename, 'pvad-audit.json');
  const legal = await exportAdminLegalAcceptance('synthetic-version', 'csv');
  assert.equal(legal.filename, 'pvad-legal-acceptance.csv');

  disposition = 'attachment; filename="Customer-PXA-archive.json"';
  assert.equal((await exportAdminAudit('json', {})).filename, 'Customer-PXA-archive.json');
  assert.equal((await exportAdminLegalAcceptance('synthetic-version', 'json')).filename,
    'Customer-PXA-archive.json');
  for (const request of requests) {
    assert.match(request.url, /^\/api\/pvad\/v1\//);
    assert.equal(request.options.credentials, 'include');
    if (!request.url.endsWith('/csrf'))
      assert.equal(request.options.headers['X-PVAD-CSRF'], 'synthetic-csrf');
  }
});
