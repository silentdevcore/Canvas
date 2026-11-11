import assert from 'node:assert/strict';
import test from 'node:test';
import { currentUser, exportAdminAudit, exportAdminLegalAcceptance } from '../src/api.js';

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

  disposition = 'attachment; filename="Customer-PVAD-archive.json"';
  assert.equal((await exportAdminAudit('json', {})).filename, 'Customer-PVAD-archive.json');
  assert.equal((await exportAdminLegalAcceptance('synthetic-version', 'json')).filename,
    'Customer-PVAD-archive.json');
  for (const request of requests) {
    assert.match(request.url, /^\/api\/pvad\/v1\//);
    assert.equal(request.options.credentials, 'include');
    if (!request.url.endsWith('/csrf'))
      assert.equal(request.options.headers['X-PVAD-CSRF'], 'synthetic-csrf');
  }
});

for (const contentType of ['application/json', 'application/problem+json; charset=utf-8', 'APPLICATION/PROBLEM+JSON']) {
  test(`Admin preserves raw Problem Details from ${contentType}`, async context => {
    const previousWindow = globalThis.window;
    globalThis.window = new EventTarget();
    context.after(() => {
      if (previousWindow === undefined) delete globalThis.window;
      else globalThis.window = previousWindow;
    });
    const problem = { code: 'PVADAPI018', canonicalCode: 'PVADAPI018', detail: 'PVADAPI018: Service unavailable.', traceId: 'synthetic-trace' };
    context.mock.method(globalThis, 'fetch', async () => new Response(JSON.stringify(problem), {
      status: 503, headers: { 'Content-Type': contentType },
    }));
    await assert.rejects(currentUser(), error => {
      assert.equal(error.message, problem.detail);
      assert.equal(error.code, problem.code);
      assert.equal(error.traceId, problem.traceId);
      assert.deepEqual(error.body, problem);
      assert.equal(error.status, 503);
      return true;
    });
  });
}
