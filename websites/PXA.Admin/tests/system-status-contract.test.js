import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [source, apiSource] = await Promise.all([
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/api.js', import.meta.url), 'utf8'),
]);

test('System status is visible and routable only for System Administrators', () => {
  assert.match(source, /path: '\/system-status'.*systemOnly: true/);
  assert.match(source, /!item\.systemOnly \|\| isSystemAdministrator\(\)/);
  assert.match(source, /if \(location\.pathname === '\/system-status'\)/);
  assert.match(source, /if \(!isSystemAdministrator\(\)\)/);
});

test('System status uses only the protected coarse health endpoint', () => {
  assert.match(apiSource, /request\('\/api\/pxa\/v1\/admin\/system\/health'\)/);
  assert.match(source, /Raw logs, traces, identifiers, and configuration secrets are never returned here/);
  assert.match(source, /siteLinks\.operator}operator\/grafana\//);
  assert.doesNotMatch(source, /connection string|password=/i);
});

test('System status includes explicit loading, failure, stale, and refresh states', () => {
  assert.match(source, /Checking protected system status/);
  assert.match(source, /System status unavailable/);
  assert.match(source, /Showing the last successful result/);
  assert.match(source, /id="system-health-refresh"/);
});
