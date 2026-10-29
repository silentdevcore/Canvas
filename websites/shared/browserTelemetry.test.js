import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import {
  classifyBrowserApiOutcome,
  consumeStatusRecovery,
  createBrowserEventDeduplicator,
  createStatusLoopDetector,
  normalizeBrowserRoute,
  rememberStatusRecovery,
  recordBrowserRecoveryAction,
  recordBrowserStatusPage,
  recordBrowserUnknownDiagnosticMapping,
  initializeBrowserTelemetry,
} from './browserTelemetry.js';

test('canonical telemetry wins and repeated setup reuses a legacy collector', () => {
  const previousCanonical = globalThis.__pvadBrowserTelemetry;
  const previousLegacy = globalThis.__pxaBrowserTelemetry;
  const events = [];
  const bridge = { record: (...args) => events.push(args) };
  try {
    globalThis.__pvadBrowserTelemetry = bridge;
    globalThis.__pxaBrowserTelemetry = { record: () => { throw new Error('duplicate collector'); } };
    recordBrowserStatusPage('offline');
    assert.equal(events.length, 1);
    globalThis.__pvadBrowserTelemetry = undefined;
    globalThis.__pxaBrowserTelemetry = bridge;
    initializeBrowserTelemetry({ application: 'company' });
    initializeBrowserTelemetry({ application: 'company' });
    assert.equal(globalThis.__pvadBrowserTelemetry, bridge);
    recordBrowserUnknownDiagnosticMapping();
    assert.equal(events.length, 2);
  } finally {
    globalThis.__pvadBrowserTelemetry = previousCanonical;
    globalThis.__pxaBrowserTelemetry = previousLegacy;
  }
});

test('legacy status caches migrate and recovery is consumed once', () => {
  const values = new Map([
    ['pxa-status-loop-v1', JSON.stringify([{ statusKey: 'offline', route: 'home', at: 1000 }])],
    ['pxa-status-recovery-v1', JSON.stringify({ action: 'retry', at: 1000 })],
  ]);
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  };
  const detect = createStatusLoopDetector({ threshold: 2, storage });
  assert.equal(detect('offline', 'home', 1100), true);
  assert.equal(values.has('pxa-status-loop-v1'), false);
  assert.ok(values.has('pvad-status-loop-v1'));
  assert.deepEqual(consumeStatusRecovery(storage, 1100), { action: 'retry', correlationId: undefined });
  assert.equal(consumeStatusRecovery(storage, 1100), undefined);
  assert.equal(values.has('pxa-status-recovery-v1'), false);
});

test('Account and Admin event producers match their canonical listeners', () => {
  for (const [app, extension] of [['Account', 'ts'], ['Admin', 'js']]) {
    const api = readFileSync(new URL(`../PVAD.${app}/src/api.${extension}`, import.meta.url), 'utf8');
    const main = readFileSync(new URL(`../PVAD.${app}/src/main.${extension}`, import.meta.url), 'utf8');
    for (const name of ['session-expired', 'access-denied']) {
      assert.ok(api.includes(`'pvad:${name}'`));
      assert.ok(main.includes(`'pvad:${name}'`));
      assert.ok(!api.includes(`'pxa:${name}'`));
      assert.ok(!main.includes(`'pxa:${name}'`));
    }
  }
  const admin = readFileSync(new URL('../PVAD.Admin/src/main.js', import.meta.url), 'utf8');
  const adminApi = readFileSync(new URL('../PVAD.Admin/src/api.js', import.meta.url), 'utf8');
  assert.ok(admin.includes("'pvad:api-online'"));
  assert.ok(adminApi.includes("'pvad:api-online'"));
  const account = readFileSync(new URL('../PVAD.Account/src/main.ts', import.meta.url), 'utf8');
  assert.ok(account.includes("addEventListener('pvad:rerender'"));
  for (const page of ['dashboard', 'profile', 'security', 'usage', 'organization', 'subscription', 'licenses', 'closure', 'developerAccess']) {
    const source = readFileSync(new URL(`../PVAD.Account/src/pages/${page}.ts`, import.meta.url), 'utf8');
    assert.ok(source.includes("'pvad:rerender'"));
    assert.ok(!source.includes("'pxa:rerender'"));
  }
});

test('normalizes routes to bounded application groups without query or identifiers', () => {
  assert.equal(normalizeBrowserRoute('company', '/products/generator?campaign=secret'), 'products');
  assert.equal(normalizeBrowserRoute('account', '/profile#security'), 'profile');
  assert.equal(normalizeBrowserRoute('admin', '/users/8f350da9-15a0-4d90-b3ba-1ee44234f402'), 'users');
  assert.equal(normalizeBrowserRoute('designer', '/create/document-123'), 'designer');
  assert.equal(normalizeBrowserRoute('company', '/customer/private-document'), 'other');
});

test('detects one bounded status-page loop without retaining URLs or user data', () => {
  const values = new Map();
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
  const isLoop = createStatusLoopDetector({ threshold: 3, windowMs: 1000, storage });

  assert.equal(isLoop('unavailable', 'designer', 1000), false);
  assert.equal(isLoop('unavailable', 'designer', 1200), false);
  assert.equal(isLoop('unavailable', 'designer', 1400), true);
  assert.equal(isLoop('unavailable', 'designer', 1500), false);
  assert.equal(isLoop('customer-private-status', 'designer', 1600), false);
  assert.doesNotMatch([...values.values()].join(''), /customer-private-status|https?:|@/);
});

test('classifies API statuses without exposing endpoint or response details', () => {
  assert.equal(classifyBrowserApiOutcome(401), 'unauthorized');
  assert.equal(classifyBrowserApiOutcome(403), 'forbidden');
  assert.equal(classifyBrowserApiOutcome(429), 'rate_limited');
  assert.equal(classifyBrowserApiOutcome(500), 'server_error');
  assert.equal(classifyBrowserApiOutcome(422), 'client_error');
  assert.equal(classifyBrowserApiOutcome(204), 'completed');
});

test('records only allowlisted status pages and recovery actions', () => {
  const recorded = [];
  const previousTelemetry = globalThis.__pxaBrowserTelemetry;
  globalThis.__pxaBrowserTelemetry = {
    record: (...args) => recorded.push(args),
  };

  try {
    recordBrowserStatusPage('notFound', 'trace-safe_123');
    recordBrowserStatusPage('customer-private-status');
    recordBrowserRecoveryAction('home', 'trace-safe_123');
    recordBrowserRecoveryAction('customer-private-action');
    recordBrowserUnknownDiagnosticMapping();
  } finally {
    globalThis.__pxaBrowserTelemetry = previousTelemetry;
  }

  assert.deepEqual(recorded, [
    ['status_page', 'shown', { name: 'notFound', correlationId: 'trace-safe_123' }],
    ['status_recovery', 'selected', { name: 'home', correlationId: 'trace-safe_123' }],
    ['diagnostic_mapping', 'unknown'],
  ]);
});

test('drops free-form correlation values before recording status telemetry', () => {
  const recorded = [];
  const previousTelemetry = globalThis.__pxaBrowserTelemetry;
  globalThis.__pxaBrowserTelemetry = { record: (...args) => recorded.push(args) };
  try {
    recordBrowserStatusPage('unexpected', 'customer@example.test/path?secret=yes');
  } finally {
    globalThis.__pxaBrowserTelemetry = previousTelemetry;
  }
  assert.deepEqual(recorded, [
    ['status_page', 'shown', { name: 'unexpected', correlationId: undefined }],
  ]);
});

test('deduplicates repeated failures without suppressing later recovery attempts', () => {
  const shouldRecord = createBrowserEventDeduplicator(2000);
  const failure = { type: 'api_failure', outcome: 'server_error', route: 'designer' };

  assert.equal(shouldRecord(failure, 1000), true);
  assert.equal(shouldRecord(failure, 1500), false);
  assert.equal(shouldRecord(failure, 3000), true);
  assert.equal(shouldRecord({ type: 'navigation', outcome: 'completed', route: 'designer' }, 3001), true);
});

test('persists and consumes only bounded recovery outcomes across navigation', () => {
  const values = new Map();
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  };

  assert.equal(rememberStatusRecovery('retry', 'trace-safe_123', storage, 1000), true);
  assert.deepEqual(consumeStatusRecovery(storage, 1200), {
    action: 'retry',
    correlationId: 'trace-safe_123',
  });
  assert.equal(consumeStatusRecovery(storage, 1300), undefined);

  assert.equal(rememberStatusRecovery('not-allowed', 'trace-safe_123', storage, 1400), false);
  assert.equal(rememberStatusRecovery('home', 'customer@example.test', storage, 1500), true);
  assert.deepEqual(consumeStatusRecovery(storage, 1600), {
    action: 'home',
    correlationId: undefined,
  });

  assert.equal(rememberStatusRecovery('reload', undefined, storage, 2000), true);
  assert.equal(consumeStatusRecovery(storage, 200_001), undefined);
});
