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

test('System status uses protected health and retention-governance endpoints', () => {
  assert.match(apiSource, /request\('\/api\/pxa\/v1\/admin\/system\/health'\)/);
  assert.match(apiSource, /request\('\/api\/pxa\/v1\/admin\/system\/dependency-compliance'\)/);
  assert.match(apiSource, /adminRetentionBase = '\/api\/pxa\/v1\/admin\/system\/retention'/);
  assert.match(apiSource, /adminRetentionBase}\/dry-run/);
  assert.match(apiSource, /adminRetentionBase}\/legal-holds/);
  assert.match(source, /Raw logs, traces, identifiers, and configuration secrets are never returned here/);
  assert.match(source, /siteLinks\.operator}operator\/grafana\//);
  assert.match(source, /Run safe dry run/);
  assert.match(source, /This workspace never exposes a direct cleanup action/);
  assert.match(source, /Create legal hold/);
  assert.doesNotMatch(apiSource, /retention\/cleanup|retention\/execute/);
  assert.doesNotMatch(source, /connection string|password=/i);
});

test('System status includes explicit loading, failure, stale, and refresh states', () => {
  assert.match(source, /Checking protected system status/);
  assert.match(source, /System status unavailable/);
  assert.match(source, /Showing the last successful result/);
  assert.match(source, /id="system-health-refresh"/);
  assert.match(source, /Loading protected retention policy status/);
  assert.match(source, /id="retention-retry"/);
});

test('Dependency compliance has a separate protected Governance workspace', () => {
  assert.match(source, /path: '\/dependency-compliance'.*group: 'Governance'.*systemOnly: true/);
  assert.match(source, /if \(location\.pathname === '\/dependency-compliance'\)/);
  assert.match(source, /<h1>Dependency compliance<\/h1>/);
  assert.match(source, /href="\/legal">Open Legal documents/);
  assert.match(source, /separate APIs, records, permissions, and audit events/);
  assert.match(source, /Generated SBOMs provide the complete inventory/);
  assert.match(source, /Production blocker/);
  assert.match(source, /id="dependency-compliance-retry"/);
});
