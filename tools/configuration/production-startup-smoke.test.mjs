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
  for (const logs of ['', `${diagnostic}\nNow listening on: http://127.0.0.1:8080`, `${diagnostic}\nUnhandled exception.`,
    `${diagnostic}\n${JSON.stringify({ eventName: 'ListeningOnAddress', attributes: { address: 'http://127.0.0.1:8080' } })}`])
    assert.throws(() => verifyRejectedStartup(state, logs, 250));
  for (const elapsed of [-1, NaN, Infinity]) assert.throws(() => verifyRejectedStartup(state, diagnostic, elapsed));
});

test('CI exercises the production rejection against its actual built API image', async () => {
  const workflow = await readFile(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  assert.match(workflow, /node tools\/configuration\/production-startup-smoke\.mjs --image pvad-webapi:sbom/);
});

test('retention refusal precedes activation database reads and both identity bootstrappers', async () => {
  const source = await readFile(new URL('../../PVAD.WebApi/Program.cs', import.meta.url), 'utf8');
  const start = source.indexOf('Environment.ExitCode = await PvadApplicationLifetime.RunAsync(app, initialize:');
  assert.ok(start >= 0, 'Initialization must run inside the controlled host lifetime.');
  const initialization = source.slice(start);
  const gate = initialization.indexOf('.EnsureProductionApproval()');
  const activation = initialization.indexOf('EnsureActivationSafeAsync(app.Services, cancellationToken)');
  const oauth = initialization.indexOf('PvadOAuthBootstrapper.InitializeAsync(app)');
  const identity = initialization.indexOf('PvadDevelopmentIdentityBootstrapper.InitializeAsync(app)');
  assert.ok(gate >= 0 && gate < activation && activation < oauth && oauth < identity);
  assert.doesNotMatch(source.slice(0, start), /EnsureActivationSafeAsync\(app.Services|Bootstrapper.InitializeAsync\(app\)/);
  assert.match(source, /AddSingleton<PVAD\.WebApi\.Application\.Retention\.PvadRetentionStartupGate>/);
});

test('CI preserves a bounded startup result on failure without publishing private startup logs', async () => {
  const source = await readFile(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const start = source.indexOf('- name: Preserve production startup verification result');
  const end = source.indexOf('- name: Generate WebApi and Designer SPDX SBOMs', start);
  assert.ok(start >= 0 && end > start);
  const artifact = source.slice(start, end);
  assert.match(artifact, /always\(\)/);
  assert.match(artifact, /steps.startup_rejection.outcome == 'failure'/);
  assert.match(artifact, /path: artifacts\/sbom\/webapi-startup\/report.json/);
  assert.match(artifact, /retention-days: 7/);
  assert.doesNotMatch(artifact, /startup\.log|private\.pem|public\.pem/);
});
