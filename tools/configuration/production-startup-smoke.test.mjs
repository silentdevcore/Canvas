import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';
import { requiresRetentionRejection, verifyRejectedStartup } from './production-startup-smoke.mjs';

const state = { Status: 'exited', ExitCode: 78, OOMKilled: false, Error: '' };
const diagnostic = 'PVADSTART001: Production startup is blocked.';

test('only an approved inventory with every retention approval avoids the rejection test', () => {
  const inventory = { productionApproved: true, activities: [{ retention: { approvalStatus: 'approved' } }] };
  assert.equal(requiresRetentionRejection(inventory), false);
  assert.equal(requiresRetentionRejection({ ...inventory, productionApproved: false }), true);
  assert.equal(requiresRetentionRejection({ ...inventory, activities: [{ retention: { approvalStatus: 'pending' } }] }), true);
  assert.throws(() => requiresRetentionRejection({ productionApproved: true, activities: [] }));
  assert.throws(() => requiresRetentionRejection({ productionApproved: true, activities: [{}] }));
});

test('a bounded independent configuration exit with a safe diagnostic passes', () => {
  verifyRejectedStartup(state, diagnostic, 250);
});

for (const [name, changed] of [
  ['running process', { Status: 'running' }], ['successful exit', { ExitCode: 0 }],
  ['unhandled abort', { ExitCode: 134 }], ['killed process', { ExitCode: 137 }],
  ['OOM kill', { OOMKilled: true }], ['runtime failure', { Error: 'Synthetic runtime error' }],
]) test(`does not count ${name} as a controlled rejection`, () => {
  assert.throws(() => verifyRejectedStartup({ ...state, ...changed }, diagnostic, 250));
});

test('timeout, missing diagnostics, a listener or an unhandled exception fail verification', () => {
  assert.throws(() => verifyRejectedStartup(state, diagnostic, 15_001));
  for (const logs of ['', `${diagnostic}\nNow listening on: http://127.0.0.1:8080`, `${diagnostic}\nUnhandled exception.`])
    assert.throws(() => verifyRejectedStartup(state, logs, 250));
});

test('CI exercises the production rejection against its actual built API image', async () => {
  const workflow = await readFile(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  assert.match(workflow, /node tools\/configuration\/production-startup-smoke\.mjs --image pvad-webapi:sbom/);
});
