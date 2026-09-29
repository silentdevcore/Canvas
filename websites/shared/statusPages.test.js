import assert from 'node:assert/strict';
import test from 'node:test';

globalThis.window = {
  location: {
    href: 'https://company.example.test/missing',
    origin: 'https://company.example.test',
  },
};

const {
  createStatusPageModel,
  getStatusCatalog,
  getStatusDefinition,
  renderStatusPage,
} = await import('./statusPages.ts');

test('status catalog contains unique keys and valid HTTP statuses', () => {
  const catalog = getStatusCatalog();
  assert.equal(new Set(catalog.map((entry) => entry.statusKey)).size, catalog.length);
  assert.ok(catalog.some((entry) => entry.statusKey === 'notFound' && entry.httpStatus === 404));
  assert.ok(catalog.some((entry) => entry.statusKey === 'trialExpired' && entry.httpStatus === 403));
  assert.ok(catalog.every((entry) => entry.httpStatus === null || entry.httpStatus >= 400));
});

test('unknown status keys fail closed', () => {
  assert.throws(() => getStatusDefinition('madeUp'), /Unknown PXA status key/);
});

test('status model exposes bounded recovery and diagnostic fields', () => {
  const model = createStatusPageModel('upstreamUnavailable', {
    diagnosticCode: 'PXAAPI018',
    correlationId: 'trace-42',
    secondaryAction: 'support',
    supportHref: '/support',
  });

  assert.equal(model.retryPolicy, 'manual');
  assert.equal(model.primaryAction, 'retry');
  assert.equal(model.secondaryAction, 'support');
  assert.equal(model.diagnosticCode, 'PXAAPI018');
  assert.equal(model.correlationId, 'trace-42');
  assert.equal(model.supportHref, '/support');
});

test('renderer escapes customer-controlled diagnostic values', () => {
  const html = renderStatusPage('unexpected', {
    diagnosticCode: '<script>alert(1)</script>',
    correlationId: 'trace-42',
  });
  assert.doesNotMatch(html, /<script>/);
  assert.match(html, /&lt;script&gt;/);
  assert.match(html, /trace-42/);
});

test('renderer replaces unsafe action protocols', () => {
  const html = renderStatusPage('notFound', { homeHref: 'javascript:alert(1)' });
  assert.doesNotMatch(html, /javascript:/);
  assert.match(html, /href="\/"/);
});
